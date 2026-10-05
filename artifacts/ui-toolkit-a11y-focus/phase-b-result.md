# Phase B result — UI Toolkit の初期フォーカスと名前・ヒント

- snapshot id: `ui-toolkit-a11y-focus-phase-b`
- generated at: 2026-10-05T14:17:15Z
- implementation base: `f0b4b1d022a213603d8ce52d036f31795a9eb2cd`
- implementation head: `53785e6c9f5d45f1869f7409c4dc75aa16987865`
- 発見 C / 判定 C の指摘は載せない。

## 実装

- `UIAccessibilityText` が名前とヒントの側表を持つ。`Set` と `Bind` は `IDisposable` を返し、外した代入だけをスタックから除く。`AssistiveSupport` と世界レジストリは呼んでいない。
- `UIToolkitInitialFocus` がタブ順を選び、パネルがあるときだけ `Focus` する。Modal / Dialog 以外は `ShouldApply` が false。
- `UIToolkitView.InitialFocusElementName` と `ApplyInitialFocus` を追加した。
- `UICommon.AddUIToolkitView` は ViewIn 成功後に `ApplyInitialFocus` を呼ぶ。失敗時の cleanup はその前に抜ける。
- `CreateToolkitBlocker` は `focusable = false` と `tabIndex = -1`。
- ConfirmDialog の UXML はメッセージをタブ順の外、OK を 1、Cancel を 2 にした。View はメッセージ名を表示文へバインドし、OK / Cancel に名前とヒントを付け、初期フォーカス名を `ok-button` にした。
- EditMode テストを `UIAccessibilityTextTests`、`UIToolkitInitialFocusTests`、`ConfirmDialogAccessibilityTests` に追加し、Blocker の既存テストへタブ順の確認を足した。

## A1 との差（契約は変えていない）

- Unity 6000.6 の VisualElement に名前・ヒントのプロパティが無い。helper は要素ローカルの側表にした。読み上げ階層には接続していない。
- タブ順の実装は `VisualElementFocusRing` の呼び出しではなく、同じ比較（正の tabIndex は昇順、次に 0、負は除外）を自前で行う。リングは `HierarchyDisplayed` が立つまで空になるため、ViewIn 直後の選択に使えない。

## 未実行

- Unity Editor のコンパイル確認。この VM に 6000.6.0f1 の Editor は無い。
- EditMode。`tools/run-tests.ps1` は `D:\UnityEditor` を要求し、プロセス起動前に失敗した。`results.xml` は生成されていない。
- 全 EditMode 回帰。

## 担当

- Phase B は A1 と同じクラウドセッション。モデルは Grok 4.7。ベンダーは Cursor。
- A2 と A3 は未実施のまま実装した。人間の凍結は無い。
