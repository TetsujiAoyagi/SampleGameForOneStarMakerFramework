# UI Toolkit の初期フォーカスと名前・ヒント

- type: slice
- status: C
- branch: `cursor/uitk-a11y-focus-a33f`
- implementation base commit: `f0b4b1d022a213603d8ce52d036f31795a9eb2cd`
- implementation head commit: `53785e6c9f5d45f1869f7409c4dc75aa16987865`
- risk: high
- owner: 未指定（A3 の人間オーナーはこのセッションで決まっていない）
- created: 2026-10-05
- expires: 2026-11-05
- harvest to: `unity/Assets/Docs/Architecture/06-ui.md` と `unity/Assets/Docs/Architecture/32-accessibility-input-dof.md`
- Phase A snapshot path / id: `artifacts/ui-toolkit-a11y-focus/phase-a-snapshot.md` / `ui-toolkit-a11y-focus-phase-a`
- Phase A snapshot generated at: 2026-10-05T14:17:15Z
- Phase A snapshot hash: `47da27388f6d99c29f92da4c0a24356a65fba79f995dd47d772ef1dbe975bed8`
- Phase B result snapshot path / id: `artifacts/ui-toolkit-a11y-focus/phase-b-result.md` / `ui-toolkit-a11y-focus-phase-b`
- Phase B result snapshot generated at: 2026-10-05T14:17:15Z
- Phase B result snapshot hash: `3223cb32c8a39e2f76f32ab9194f1c2947edb648b1f53eb9661a50742f27c697`
- evidence bundle path / id: `artifacts/ui-toolkit-a11y-focus/` / `ui-toolkit-a11y-focus-blind-53785e6c`
- evidence bundle generated at: 2026-10-05T14:17:15Z
- evidence bundle hash: `ac280e88646f7e197270a9e0edfd092ee91eecd2f7a432ca0f5658003ae5c795`（manifest.json の SHA-256。判定用 XML は含まない）
- C' blind bundle path / id: 同上。判定 XML が無いので C' の開始は許可しない
- C' blind bundle generated at: 2026-10-05T14:17:15Z
- C' blind bundle hash: `ac280e88646f7e197270a9e0edfd092ee91eecd2f7a432ca0f5658003ae5c795`

判定 C と C' は未了。GO ではない。status の C は、同一セッションが発見の材料を揃えたところまでを指す。独立した Phase C ではない。

## 1. 目的と対象外

- 目的: UI Toolkit の Dialog / Modal に、UISystem だけで初期フォーカス、タブ順、要素ローカルの名前とヒントを持たせ、ConfirmDialog でその契約を見せる。
- 対象外: ViewOut 時のフォーカス復帰。`AssistiveSupport` への投影と読み上げ。§31 の出力予算。Input のリマップと片手プロファイル。§30 の世界レジストリと `BindAccessible`。§29 のエフェクト合成。HpGauge、ScriptSystem、InputSystem、SoundSystem。
- 現況: 着手時、UISystem にフォーカス helper と名前 helper は無く、ConfirmDialog の UXML に `tabindex` も `focusable` も無かった。Unity 6000.6 の `VisualElement` に accessibility name / hint プロパティは無い。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: UI Toolkit の Dialog / Modal は、世界レジストリを使わず、初期フォーカスとタブ順、および要素に紐づく名前とヒントだけで、ConfirmDialog を次の操作対象まで用意できるか。
- 進める最低条件:
  1. 名前とヒントを VisualElement に付け、`IDisposable` を外すとその代入だけが消える。`UIToolkitView.Track` に渡せる。
  2. Dialog と Modal は ViewIn 成功後に、名前付き既定またはタブ順の先頭へ `Focus` を試みる。他レイヤーは対象外。パネルが無ければ例外にしない。
  3. ConfirmDialog はメッセージをタブ順の外に置き、OK を `tabIndex` 1、Cancel を `tabIndex` 2 とする。メッセージ名は表示文に追従する。OK / Cancel は名前とヒントを持つ。初期フォーカス名は `ok-button`。
  4. Toolkit Blocker は `focusable = false` かつ `tabIndex = -1`。
  5. 上記を EditMode テストが固定する。
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  - `Set` の名前とヒントが読め、Dispose で消える。先の代入を外しても後の代入は残る。
  - `Bind` は Observable に追従し、Dispose 後は更新しない。
  - Track した代入は View 破棄で外れる。
  - タブ順は正の `tabIndex` の昇順、その後 0 の深さ優先。負、非 focusable、`display: none`、無効な枝は入らない。
  - 名前付き既定がタブ順に無ければ先頭へ戻る。
  - `ShouldApply` は Modal と Dialog だけ true。Loading は false。
  - ConfirmDialog の UXML と初期化後の文言が一致し、破棄後に文言が残らない。
  - Blocker はタブ順に入らない。
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
  - ViewOut のフォーカス復帰。HANDOFF は未作成。
  - 側表から `AccessibilityNode` への投影。§31 の予算とは分け、HANDOFF は未作成。
  - Input リマップ、片手プロファイル、世界の巡回。§32 の Input 側。このスライスのファイルには入れない。
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: GO は最低条件を満たし、空 filter の全 EditMode が 1 件以上かつ失敗 0 で、常時契約とこの境界に反する欠陥が残っていないこと。NO-GO は最低条件未達か、その反証が残ること。A3 の人間採否が記録されるまで GO にしない。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし。A3 は未実施。
- 本文へ転記した実装制約:
  - 依存は Game から Framework。asmdef 参照は足さない。
  - 要素に触れる `IDisposable` は `Track` へ集める。
  - 編集する Unity 側 `.cs` は `#nullable enable`。`record` は使わない。
  - 破棄されうる `UnityEngine.Object` に `?.` / `??` / `is null` を使わない。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。
  - helper 名は `BindAccessible` にしない。StableId、Flags、Registry へ登録しない。
  - `AssistiveSupport.activeHierarchy` は触らない。
  - 初期フォーカスは ViewIn 成功後。ViewOut では戻さない。
  - HpGauge、ScriptSystem、InputSystem、SoundSystem は編集しない。
  - 正の `tabIndex` は昇順で 0 より前。負はタブ順から外す。`VisualElementFocusRing` は `HierarchyDisplayed` 待ちで ViewIn 直後に空になるため、選択は同じ比較を UISystem 側で行う。
- 未決事項: なし。A2/A3 が無いため、側表という保持場所は人間が凍結していない。

## 3. 責務マップ

- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UIAccessibilityText.cs`: 要素ローカルの名前とヒント。新規約 145 行。所有者は代入の `IDisposable`。R3 と UIElements のみ。公開は `Set` / `Bind` / `TryGet`。EditMode でパネルなしに検証する。
- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UIToolkitInitialFocus.cs`: タブ順選択と `Focus` 試行。新規約 202 行。状態は持たない。公開は `ShouldApply` / `Select` / `TryApply`。
- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UIToolkitView.cs`: 初期フォーカス名の宣言と Modal/Dialog のときだけ適用。194 行から 214 行。50% 未満。
- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UICommon.cs`: ViewIn 成功後の呼び出しと Blocker の 2 プロパティ。571 行から 577 行。元から 500 行警報の上にある。増分は Add 手順の 1 呼び出しであり、算法は別ファイルへ出した。Add を割ると手順の所有者が裂けるので非分割。
- `unity/Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.uxml`: 静的なタブ順。
- `unity/Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialogView.cs`: 文言の Track と `ok-button`。105 行から 116 行。SampleGame から Framework への一方向。
- テストは `OneStarMaker.Tests` と `SampleGame.Tests.Editor`。asmdef 参照は増やしていない。

## 4. 実装計画

- 変更対象: 上記の UISystem、ConfirmDialog、テスト。
- 順序: 側表、タブ順、View の適用、UICommon、ConfirmDialog、テスト。
- Phase B から Phase A へ差し戻す条件: 新しい asmdef、新しい所有者、AssistiveSupport、または中核がテスト不能になること。今回は該当せず、側表と自前のタブ順比較は A1 の helper の実装詳細として B で記録した。
- 対象外を維持する方法: 変更パスを UISystem と ConfirmDialog とテストに限る。

## 5. テストとレビュー計画

- 単体テスト: `UIAccessibilityTextTests`、`UIToolkitInitialFocusTests`、`ConfirmDialogAccessibilityTests`、`CreateToolkitBlocker_SetsNamePickingModeAndFullscreenStyle`。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `UIAccessibilityTextTests|UIToolkitInitialFocusTests|ConfirmDialogAccessibilityTests|CreateToolkitBlocker_SetsNamePickingModeAndFullscreenStyle`
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 空 filter の全 EditMode。`pwsh tools/run-tests.ps1`。
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。
- 統合・Unity テスト: 上記 EditMode。PlayMode は最低条件に含めていない。
- 操作・実行時・目視条件の検証経路（条件ごとの担当、環境・初期状態、操作、観測と合否、対象版・保存証拠・C / C' への受け渡し。既存テストは参照で可）: 合否は `tabIndex`、`focusable`、側表の文字列、パネル参加時の `focusedElement`。見た目判定は不要。証拠は NUnit XML。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点（未知経路が無い場合だけ `なし`。Phase C へ委譲する場合は「未確認。初回確認は Phase C」と理由・担当・確認地点、不成立時の対応）: 未確認。初回確認は Unity 6000.6.0f1 がある環境の判定 C。この VM には Editor が無く、`D:\UnityEditor` も無い。不成立がテスト失敗なら実装を直す。Editor 不在は環境不足であり、成功に読み替えない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方（観察記録の受理 / 画像の独立再評価。無ければ `なし`）: なし。
- 機械検査: `pwsh tools/contract-audit.ps1`。実装 head の作業ツリーで errors=0 warnings=0。
- A0/A1 主担当・モデル・ベンダー: このクラウドセッション。Grok 4.7。Cursor。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 実施していない。
- A3 統合担当・モデル・採否: 実施していない。採否は無い。凍結していない。
- C' 用に予約した担当・モデル・ベンダー: なし。
- 独立性の強化条件を満たせない場合の理由: 同一セッションが A1 と実装を行い、A2 を起動していない。AI の C' は、実装と Phase C の両方と異なるモデルの新規セッションが要る。このセッションでは C' を実行しない。

## 6. Phase B 実装結果

- 実装: `UIAccessibilityText`、`UIToolkitInitialFocus`、`UIToolkitView.ApplyInitialFocus`、`UICommon` の ViewIn 後呼び出しと Blocker の非フォーカス、ConfirmDialog のタブ順と文言、EditMode テスト。詳細は Phase B snapshot。
- HANDOFF との差: エンジンに名前プロパティが無いため側表にした。`VisualElementFocusRing` は使わず、同じ tabIndex 比較を UISystem に置いた。どちらも A1 に書いた helper の範囲で、公開の所有者や asmdef は増やしていない。
- 未実行: Unity コンパイル。EditMode。全 EditMode。
- implementation head commit: `53785e6c9f5d45f1869f7409c4dc75aa16987865`
- Phase B 担当・モデル・ベンダー: このクラウドセッション。Grok 4.7。Cursor。A2/A3 が無い状態で実装した。

## 7. Phase C

- 種別: 発見 / 判定: 発見の準備。独立した Phase C ではなく、B と同じセッションの構造照合。判定は未着手。GO ではない。
- evidence bundle id / hash: `ui-toolkit-a11y-focus-blind-53785e6c` / `ac280e88646f7e197270a9e0edfd092ee91eecd2f7a432ca0f5658003ae5c795`
- 構造適合: 責務マップのファイル以外は変更していない。asmdef 参照は増えていない。SampleGame は Runtime の helper を呼ぶだけ。側表は要素と Disposable の寿命に閉じ、Registry と `AssistiveSupport` は呼ばない。初期フォーカスは ViewIn 成功後だけ。UICommon の 500 行超過は既存で、今回は +6 行。非分割理由は §3。
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: コード差分について、この同一セッションの読みでは常時契約への違反は見つけていない。A3 が無いので凍結済み条件は存在しない。GO を止める未達は次のとおり。A1 に書いた「A3 の人間採否が記録されるまで GO にしない」と、判定必須の全 EditMode が未実行であること。
- 後続スライスへ移送する findings: ViewOut のフォーカス復帰。側表のスクリーンリーダー投影。`TryApply_FocusesNamedButtonWhenDocumentPanelExists` は未実行で、Editor 上の `UIDocument` がパネルを作らない場合はテストの準備を直す。設計の所有者は変えない見込み。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter UIAccessibilityTextTests|UIToolkitInitialFocusTests|ConfirmDialogAccessibilityTests`。起点 filter の ConfirmDialog と helper を先に見るため。ランナーは Unity を起動する前に失敗した。
- テスト結果（XML 上の実行テスト名と件数）: XML は無い。`results.xml` は生成されていない。step は `artifacts/ui-toolkit-a11y-focus/unity-editmode-attempt-step.json`。status=failed、exitCode=1、executablePath は空、cases は空、件数は null。失敗文に `Cannot find drive. A drive with the name 'D' does not exist.` と `results.xml がありません` がある。これをテスト成功にもテスト失敗件数にも数えない。
- 判定必須のうち未実行: 空 filter の全 EditMode。起点 filter の EditMode も未実行。
- 重い検証を発見段階で限定実行した場合の理由と範囲: 限定実行は開始できなかった。
- 未確認事項: コンパイル、パネル参加時の `Focus`、全 EditMode。`pwsh` は VM に無く、このセッションでは PowerShell 7.5.4 のポータブル版で contract-audit と run-tests を起動した。
- 担当・モデル: 同一セッション。Grok 4.7。Cursor。B と分離していない。

## 8. Phase C'

- 担当方式: 未実施
- blind audit bundle id / hash: 未実施
- 確認範囲・方法（全件機械検査 / 代表箇所の目視・操作等）: 未実施
- 判定（人間担当は本人の明示回答まで未実施）: 未実施
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: 未実施
- 後続スライスへ移送する findings: 未実施
- 残存リスク: 未実施
- 監査できなかった範囲: 未実施
- 独立性: 未実施
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: 未実施
- 担当・モデル: 未実施

## 9. Phase D

- C / C' の突合: 未実施
- マージ判断: 未実施
- harvest: 未実施。公開文書の §32 は、初期フォーカス配線を「今はしない」と書いたままである。
- 削除確認: 未実施
