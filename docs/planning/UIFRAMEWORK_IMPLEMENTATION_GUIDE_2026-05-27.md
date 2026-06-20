# UIFramework 実装ガイド（明日着手用）

## 0. 前提
- 方針: **UIToolkit メイン + uGUI 差し替え可能**
- アーキテクチャ: **MVVM**
- Reactive 基盤: **R3**
- 重要要件: **DebugLayer は常に最上位**

---

## 1. 実装方針（固定）
1. 依存方向は `View -> ViewModel -> UseCase/Service` のみ。
2. ViewModel は Unity UI 実装（UIToolkit/uGUI）を参照しない。
3. View 実装差し替えは backend adapter で吸収する。
4. R3 は UI の状態通知・入力イベント伝播に限定し、Domain 層へ漏らし過ぎない。
5. Layer 管理で表示順・入力ブロック・show/hide の責務を一元化する。

---

## 2. UI Layer 設計（確定順）
下ほど背面、上ほど前面。

1. `Transient`（toast/tooltip）
2. `Screen`（通常画面）
3. `Modal`（ダイアログ）
4. `GlobalHUD`（常時HUD）
5. `SystemOverlay`（致命系通知）
6. `Debug`（デバッグUI） ← **最上位固定**

### ルール
- `Debug` は他 Layer の input block を受けない（明示的に閉じるまで操作可能）。
- `SystemOverlay` は `Debug` 以外を抑止できる。
- `Modal` は `Screen` 入力を遮断するが `GlobalHUD` の一部表示は許可可能。

---

## 3. 実装パーツ一覧（D0-D7）

## D0: 契約定義
- `IUIView`
- `IViewBinder`
- `IUIViewBackend`
- `IUIWindowHandle`

**完了条件**
- ViewModel 側コードが UIToolkit/uGUI どちらにも依存しない。

## D1: Layer 管理
- `UILayerId`（enum）
- `UILayerOrder`（明示 order。Debug 最大値）
- `UIRoot`
- `UILayerStackService`

**完了条件**
- `Push/Pop/Show/Hide` と input block 判定が Layer 基盤で成立。

## D2: MVVM + R3 基底
- `ViewModelBase`
- `UIViewBase<TViewModel>`
- `BindingScope`（`CompositeDisposable` 管理）

**完了条件**
- bind/unbind が冪等、購読リークなし。

## D3: UIToolkit backend
- `UIToolkitViewBackend`
- `UIToolkitBinder`
- UXML/USS 読み込みヘルパ

**完了条件**
- D0 契約を満たす実 View が 1 つ動く。

## D4: uGUI backend（最小）
- `UGUIViewBackend`
- `UGUIBinder`

**完了条件**
- 同一 ViewModel を uGUI でも最小表示できる。

## D5: 画面遷移
- `UIRouter`
- `UIScreenService`
- `UIModalService`

**完了条件**
- `push/pop/replace`、戻る、modal 多重制御が成立。

## D6: Vertical Slice
- 例: `DebugDashboardViewModel`
- UIToolkit View（必要なら uGUI mirror）

**完了条件**
- 実データ表示、入力、遷移、close まで end-to-end 動作。

## D7: 既存統合
- `AbstractApplicationInitializer`
- `SceneDirector`
- `UICommon`

**完了条件**
- 既存起動フローから新 UIFramework を起動可能。

---

## 4. 明日の実装手順（そのまま実行用）
1. D0 の interface 群を先に追加（契約を固定）。
2. D1 で `UILayerId/Order` と `UILayerStackService` を実装し、**Debug最上位**をテストで固定。
3. D2 の MVVM 基底を追加して R3 購読寿命を統一。
4. D3 で UIToolkit backend を実装し、最小画面を表示。
5. D6 の vertical slice を通してから D4（uGUI 最小）へ進む。
6. 最後に D5/D7 で遷移強化と既存統合。

---

## 5. 先に書くべきテスト
1. Layer order テスト（Debug が常に最上位）。
2. Modal input block テスト。
3. bind/unbind 冪等テスト（再オープン時に購読重複しない）。
4. backend 差し替えテスト（同一 ViewModel で UIToolkit/uGUI が成立）。

---

## 6. 実装時の注意
1. hot path で LINQ / boxing / 文字列連結を避ける。
2. View close 時の破棄順を `View -> Binder -> ViewModel` で固定。
3. ViewModel は Unity API を直接呼ばない。
4. Layer order は enum + 固定値で管理（magic number 禁止）。

