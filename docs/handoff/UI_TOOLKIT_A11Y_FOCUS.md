# UI Toolkit の初期フォーカスと名前・ヒント — PR95 修正 HANDOFF

- type: slice
- status: B（今回の修正。判定 C / C' は未着手）
- branch: `codex/repair-pr95-20261010`
- original repair A3 develop base: `8953a908d1caf19c1957c128fc9690f3ac2f5569`
- final upstream integration base: `f5c67596a264baca48e49771acf54b34076d9c22`（#110 の Artifacts 4 パスのみ）
- repair implementation base: `d54014881dee2c8f83810ab171a6702358ea6c77`
- repair implementation commit: `4eb334886dc242bba6c8d18ee5854fdafbd968b4`（変更なし）
- final candidate head: 本 HANDOFF の metadata を含む merge commit。Git HEAD で固定し、Phase C に渡す
- original published PR95 head: `3d04523640d96afc0b71d0261b74877b96bf7d89`
- risk: high
- owner: 統合親担当。Phase D の GO / NO-GO と merge は利用者の承認済み順次計画に従い親担当が判断する。
- repair A3 freeze: 2026-10-10
- expires: 2026-11-05
- harvest to: `unity/Assets/Docs/Architecture/06-ui.md` と `unity/Assets/Docs/Architecture/32-accessibility-input-dof.md`
- frozen repair Phase A snapshot: `artifacts/integration-review-20261010/pr95-repair/phase-a-repair-snapshot.md`（ローカル、SHA-256 `00d624678ed10f3c38bb40cf0fc5cd6bb950d238201c9dab4465bf8f8f733236`）
- neutral repair Phase B result: `artifacts/integration-review-20261010/pr95-repair/phase-b-repair-result.md`（ローカル、SHA-256 `4f13aea41467ed125a5550ae440a851615ca2aae9b19bd387799c450ba55de2c`）
- review evidence / C' blind bundle: 未生成。最終 repair head の Phase C が作成する。
- original evidence retrieval: 公開済み commit `3d04523640d96afc0b71d0261b74877b96bf7d89` の `artifacts/ui-toolkit-a11y-focus/` に旧 9 ファイルを保持。SHA-256 検証済みのローカル複製は `artifacts/integration-review-20261010/pr95-repair/original-published-evidence/`。旧結果は今回の head の合格証拠に流用しない。

元の公開 PR95 作業に A2 / A3 はなく、独立した判定 C / C' は完了していない。今回の A2 / A3 は 2026-10-10 の修正境界だけを凍結し、元の履歴を変更しない。

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
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: GO は最低条件を満たし、空 filter の全 EditMode が 1 件以上かつ失敗 0 で、常時契約とこの境界に反する欠陥が残っていないこと。NO-GO は最低条件未達か、その反証が残ること。今回の修正 A3 は 2026-10-10 に凍結済みだが、判定 C と C' が完了するまで GO にしない。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。
- A3 後の例外承認: なし。元の公開作業に A2/A3 は無かった。今回の修正 A3 は下記 §5 とローカル Phase A snapshot に 2026-10-10 の採用事項を固定したものであり、過去の欠落を遡及して埋めない。
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
- 未決事項: 元の公開作業では A2/A3 が無く、側表という保持場所は当時凍結していなかった。今回の修正は既存配置を維持する。

## 3. 責務マップ

- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UIAccessibilityText.cs`: 要素ローカルの名前とヒント。新規約 145 行。所有者は代入の `IDisposable`。R3 と UIElements のみ。公開は `Set` / `Bind` / `TryGet`。EditMode でパネルなしに検証する。
- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UIToolkitInitialFocus.cs`: タブ順選択と `Focus` 試行。新規約 202 行。状態は持たない。公開は `ShouldApply` / `Select` / `TryApply`。
- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UIToolkitView.cs`: 初期フォーカス名の宣言と Modal/Dialog のときだけ適用。194 行から 214 行。50% 未満。
- `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/UICommon.cs`: ViewIn 成功後の呼び出しと Blocker の 2 プロパティ。571 行から 577 行。元から 500 行警報の上にある。増分は Add 手順の 1 呼び出しであり、算法は別ファイルへ出した。Add を割ると手順の所有者が裂けるので非分割。
- `unity/Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.uxml`: 静的なタブ順。
- `unity/Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialogView.cs`: 文言の Track と `ok-button`。105 行から 116 行。SampleGame から Framework への一方向。
- テストは `OneStarMaker.Tests` と `SampleGame.Tests.Editor`。asmdef 参照は増やしていない。

## 4. 実装計画

- 変更対象: 元スライスは上記の UISystem、ConfirmDialog、テスト。今回の修正は `UIToolkitInitialFocus.Select`、既存テストと TestSupport、HANDOFF、証拠台帳だけ。
- 順序: 元実装の順序は側表、タブ順、View の適用、UICommon、ConfirmDialog、テスト。今回の修正は候補リスト選択、回帰テスト、曖昧な `Object` の修飾、機械検査。
- Phase B から Phase A へ差し戻す条件: 新しい asmdef、所有者、寿命、公開 API、AssistiveSupport、または実 `UICommon.AddUIView` 経路を既存 TestSupport でテストできない場合。
- 対象外を維持する方法: 新しい runtime API と asmdef は加えず、フォーカス復帰・投影・Input・その他機能を編集しない。

## 5. テストとレビュー計画

- 単体テスト: 既存の `UIAccessibilityTextTests`、`UIToolkitInitialFocusTests`、`ConfirmDialogAccessibilityTests`、Blocker。今回、隠れた/無効な祖先と root 名のフォールバック、および `UICommon.AddUIView` の ViewIn 成功・失敗・取消経路を追加する。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可）: `UIAccessibilityTextTests|UIToolkitInitialFocusTests|UICommonUIToolkitTests|ConfirmDialogAccessibilityTests`。今回の実 ViewIn 経路を含める。
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 空 filter の全 EditMode。`pwsh tools/run-tests.ps1`。
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。
- 統合・Unity テスト: 空 filter の EditMode 全件が判定必須。ConfirmDialog の実破棄確認は EditMode `[UnityTest]` の一件で `EnterPlayMode` / `ExitPlayMode` を使う。独立した PlayMode suite は判定必須に含めない。
- 操作・実行時・目視条件の検証経路（条件ごとの担当、環境・初期状態、操作、観測と合否、対象版・保存証拠・C / C' への受け渡し。既存テストは参照で可）: 合否は `tabIndex`、`focusable`、側表の文字列、パネル参加時の `focusedElement`。見た目判定は不要。証拠は NUnit XML。
- 未知の操作経路の疎通結果と確認地点: 今回の修正環境では Unity 6000.6.0f1 が `D:\UnityEditor\6000.6.0f1\Editor\Unity.exe` にある。実 `UICommon.AddUIView` の EditMode 経路と全 EditMode の初回実行・判定は Phase C が担当する。失敗を成功へ読み替えない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方（観察記録の受理 / 画像の独立再評価。無ければ `なし`）: なし。
- 機械検査: Phase B は `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` を実行し、結果を中立的な B result に記録する。
- A0/A1 主担当・モデル・ベンダー: 元の公開 PR95 作業は Grok 4.7 / Cursor。同一セッションの A1 と実装であり、今回の修正 B 担当とは異なる。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 元の公開作業では未実施。今回の修正では親担当が独立の architecture（gpt-6.1-sol）と contract（gpt-6-sol）レビューを実施したと報告した。実行時 ID は B 担当が独自には確認していない。
- A3 統合担当・モデル・採否: 元の公開作業では未実施。今回の修正は、利用者が順次統合計画を承認し、親担当が 2026-10-10 に A2 の指摘を採否して凍結した。候補リストからの選択、`UnityEngine.Object` 修飾、実 ViewIn テスト、証拠整理を採用。public helper 縮小と Bind 購読例外対応は後続候補。
- C' 用に予約した担当・モデル・ベンダー: 親担当が今回の最終 head で独立した監査を調整する。未着手。
- 独立性: 元の公開作業は A1 と B が同一セッションで A2 が無かった。今回の Phase B は独立 C/C' を実施しない。最終 head の独立性と結果は親担当が別途記録する。

## 6. Phase B 実装結果（今回の修正）

- 実装: `Select` は `ListTabStops(root)` の候補だけで優先名を探し、候補がなければ先頭へ戻す。既存の名前/ヒントや View の公開面は変更しない。
- 回帰テスト: 隠れた祖先、無効な祖先、root 名、実 `UICommon.AddUIView` の ViewIn 完了・失敗・取消。共有 `TestToolkitView` は UniTask に依存させず、既存の `OneStarMaker.Tests`（UniTask 参照済み）内のテスト専用派生だけが完了シグナルを持つ。取消アサーションは派生した取消例外を受け入れる。ConfirmDialog の名前・ヒントと実破棄後の解除は、EditMode `[UnityTest]` が Process 限定の `content:runtimeMode=addressables` で Play Mode に入って確認する。teardown は元の環境変数値と、読み込み済みの元シーン構成（元が空なら空シーン）を戻す。`UIAccessibilityTextTests` と ConfirmDialog Play fixture の `UnityEngine.Object` 曖昧性を解消。
- 証拠整理: 旧 9 ファイルは元の公開 head と SHA-256 照合済みローカル複製に保存。最終ブランチの正味差分から除く。
- HANDOFF との差: なし。元スライス §1–5 の製品境界内の修正。
- 未実行: Unity コンパイル、限定 EditMode、空 filter 全 EditMode、Build。Phase C 担当へ引き渡す。
- repair implementation commit: `4eb334886dc242bba6c8d18ee5854fdafbd968b4`。最終判定対象は最新 `develop` との merge 後の Git HEAD。
- Phase B 担当: PR95 修正担当 Codex（現在セッション）。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手。親担当が C / C' を突合して GO / NO-GO と merge を判断する。
