# DebugStudio 進捗ログ 2026-04-29

## 前回セッション（2026-04-28）の成果

### 完了した大型タスク

#### 1. NX1 Layout Persistence（layout 保存復元）

**概要：** DebugStudio が起動時に AvalonDock レイアウトを復元し、終了時に現在の pane 配置・サイズを保存するようにした。

**実装内容：**
- `ShellLayoutPersistenceService` : `%LocalAppData%\DebugStudio\shell-layout.xml` へ XML serialization
- `ShellLayoutSerializerService` : default layout 生成、XML 検証、serialize/deserialize の責務分離
- `ShellLayoutCoordinator` : MainWindow から restore/save の順序制御だけを呼ぶ
- `ToolWindowDefinition` 拡張：dock kind / group / order / close-hide policy を静的定義として保持
- `MainWindow.xaml` の inline layout をやめ、**コード生成した default layout を唯一の正本に変更**
- `MainWindow.OnClosing` で保存、`OnClosed` は `async void` をやめて同期 wait に変更
- 日本語コメント厚めに追加
- テスト 16 件追加（layout 保存先・互換性検証・default fallback）

**テスト結果：** 127 tests passed

---

#### 3. Live Validation Support（Unity/DebugStudio 実接続の支援実装）

**概要：**  
`DEBUGSTUDIO-Unity_plannning.md` を見直し、live validation を人手で最後まで回しやすくする支援実装を追加した。  
主眼は「未確認の追加実装を積む」ことではなく、**実接続時に capability / log / telemetry / hierarchy / inspector / command を観測ベースで切り分けやすくすること**。

**実装内容：**

*Unity side*
- `DebugSocketService` に `RuntimeDiagnosticsSnapshot` と `GetRuntimeDiagnosticsSnapshot()` を追加
  - active session の有無
  - session id
  - pending queue 長
  - `MaxQueueLength`
  - 未接続時 drop 累積
  - queue overflow drop 累積
- 再接続直後に `runtime-diagnostics` service status を返すようにし、
  **「落ちていたが見えていなかった」** 状況を接続直後に拾えるようにした
- `DebugSocketService` に built-in debug command を追加
  - `debugsocket.runtime-diagnostics` / `runtime-diagnostics`
  - `debugsocket.ping` / `ping`
- built-in command は app 側 dispatcher override の有無に依存せず常に使えるため、
  command correlation の最小成功系をどのアプリでも同じ条件で検証できる

*DebugStudio side*
- `CommandWindowViewModel`
  - 既定 command を `debugsocket.runtime-diagnostics` に変更
  - built-in command guide を panel 上に表示
  - latest result summary と **latest payload JSON** を分離表示
- `CommandPanel.xaml`
  - built-in command guide の表示
  - `Latest Payload JSON` read-only pane を追加
- `CommandCorrelationTests`
  - command panel の既定値
  - latest payload JSON 表示
  を検証する 2 テストを追加

*Plan / Progress sync*
- `DEBUGSTUDIO-Unity_plannning.md`
  - 妥当性判定
  - Phase 0（準備物先行整備）
  - coverage audit
  - 実行 runbook
  - 結果記録テンプレート
  を追加し、live validation の正本へ更新

**ファイル更新：**
- `Assets/OneStarMaker/Runtime/DebugSocketServices/DebugSocketService.cs`
- `DebugStudio/src/DebugStudio.App/Features/Commands/CommandWindowViewModel.cs`
- `DebugStudio/src/DebugStudio.App/Features/Commands/CommandPanel.xaml`
- `DebugStudio/tests/DebugStudio.App.Tests/CommandCorrelationTests.cs`
- `DEBUGSTUDIO-Unity_plannning.md`

**テスト結果：**
- `DebugStudio.App.Tests` : **174 tests passed**
- `Assembly-CSharp.csproj` build : succeeded

---

#### 2. NX2 Command Correlation（command pending/result 相関）

**概要：** requestId 単位で command request と result を相関させ、pending 監視、timeout、disconnect 終端化を実装した。

**実装内容：**

*Core Models*
- `CommandDispatchState` enum : Pending/Succeeded/Failed/DispatchFailed/TimedOut/Disconnected/Orphaned
- `CommandDispatchRecord` : 1 件の command request/result 相関結果を畳んだ app model

*Store Expansion*
- `CommandStore` : 従来は「最新 result だけ」だったが、pending 追跡・history retention に拡張
  - `TrackPending()` : 送信直前に pending entry として登録、requestId 固定
  - `AppendResult()` : requestId 単位で result と相関、orphan result も記録
  - `ExpirePending()` : 一定時間を超えた pending を timeout 化（timer は外側から与える）
  - `MarkDisconnected()` : 切断時に全 pending を終端化（stale command の carry-over 防止）
  - retention = 128（超過時は completed entry から old ones を削除、pending は保護）

*CommandService Upgrade*
- `SendAsync(string commandType, string payloadJson)` : requestId を中央生成して返す
- `SendAsync(DebugCommandEnvelopeV1 command)` : 既存 API も互換維持、requestId 検証
- `SweepTimedOutCommands(TimeSpan timeout)` : pending のタイムアウト判定（service は timer 不所持、外側から呼ばせる）
- dispatch failure も store へ `MarkDispatchFailed()` で終端化（UI が「消えた command」を見ないようにする）

*SessionResetPolicy / SessionMessageRouter*
- disconnect 時に `_commandStore.MarkDisconnected()` を呼んで stale pending をクリア
- result routing に capability gate 追加 : CommandResult 非対応時は frame を捨てる（stray frame 対策）

*CommandWindowViewModel → User-Facing Authoring UI*
- 最小限の raw command authoring を実装
  - command type input （デフォルト "ping"）
  - payload JSON textarea （デフォルト "{}"）
  - Send button（`AsyncRelayCommand` で UI thread 安全）
- dispatcher timer で 1 秒ごとに `SweepTimedOutCommands()` を呼ぶ（pending timeout = 15 秒）
- `ObservableCollection<CommandHistoryItemViewModel>` で recent commands を ListView に表示
  - State / RequestId / CommandType / Summary / Timing の 5 列
  - 新しい順に並ぶ
- dispatch count / result count / pending count を 3 列で表示
- latest result を整形して表示

*Test Safety Rail*
- `CommandCorrelationTests` : 7 件のテスト
  - TrackPending / AppendResult correlation / orphan detection
  - timeout / disconnect state transition
  - capability gate が無いときの send rejection
  - dispatch failure store 記録

**ファイル追加：**
- `Core/Models/CommandDispatchState.cs`
- `Core/Models/CommandDispatchRecord.cs`
- `Features/Commands/CommandHistoryItemViewModel.cs`
- `Tests/CommandCorrelationTests.cs`

**ファイル更新：**
- `Core/Stores/CommandStore.cs` : 大幅拡張
- `Core/Stores/CommandStoreSnapshot.cs` : 構造拡張
- `Core/Services/CommandService.cs` : requestId 生成・dispatch failure 処理追加
- `Core/Services/SessionResetPolicy.cs` : disconnect 時 mark handling
- `Core/Services/SessionMessageRouter.cs` : CommandResult capability gate
- `Core/Formatting/DebugStudioTextFormatter.cs` : 新 formatter 2 件追加（state / timing 表示）
- `Features/Commands/CommandWindowViewModel.cs` : 新規置換（placeholder → honest authoring UI）
- `Features/Commands/CommandPanel.xaml` : レイアウト大幅拡張（input rows + recent history ListView）
- `Core/Composition/AppCompositionRoot.cs` : CommandService constructor update
- `Tests/ShellCompositionTests.cs` : composition harness update

**テスト結果：** 134 tests passed（+ 7 new correlation tests）

---

## 実装スケール

- **Code Changes**
  - 新規 .cs files : 4 件
  - 更新 .cs files : 9 件
  - 新規 .xaml : 0 件（CommandPanel.xaml は更新）
  - 更新 .xaml files : 2 件

- **テスト増加**
  - NX1 : + 16 tests (ShellLayoutPersistenceTests)
  - NX2 : + 7 tests (CommandCorrelationTests)
  - 累計 : 117 → 127 → 134 tests

---

## 設計上の重要判断

### 1. RequestId 生成の中央化

**判断：** service 側で `commandType + timestamp + GUID` で生成し、呼び出し側が忘れないようにする

**理由：** 
- 呼び出し側が ID を自分で作ると、重複・無視・空文字列のリスクがある
- service へ requestId 生成責務を集約することで、唯一の正本にできる

**トレードオフ：** 呼び出し側が詳細な ID を作りたい場合、既に作ったものを持ち込める API も用意

### 2. Pending を List で保持、retention = 128

**判断：** pending だけを特別扱いし、completed/failed/timed out は容量超過時に古いものから削除

**理由：**
- pending は「今 Unity が答えるまで待っている」なので絶対に落とすと結果ロストになる
- completed/failed は「見たことある」という履歴なので、容量制限でも問題ない

**トレードオフ：** OOM を恐れて pending も削除すると、UI が「送ったのに返答が来ない」状態を正確に見られなくなる

### 3. Timeout を UI timer が sweep で実行（service はロジックだけ）

**判断：** service は `ExpirePending(now, timeout_ms)` の pure function、UI が `DispatcherTimer` で 1 秒ごと呼ぶ

**理由：**
- service が timer を持つと、shutdown/dispose 時の cleanup が複雑になる
- UI component の lifetime に timer を結び付けることで、自動的に cleanup できる

**トレードオフ：** 外側から sweep を呼ばないとタイムアウトが動かないが、test でも explicit に呼べるメリット

### 4. Result capability gate を router に追加

**判断：** `CommandResult` frame は CommandResult capability が negotiated になるまで受理しない

**理由：** stray/stale frame で command UI 状態が巻き戻るのを避ける（前のセッションの遅延 result など）

**トレードオフ：** 正当な result が capability 交渉前に着いた場合、一時的に見えなくなるが、これは仕様通り

---

## 既知制限

### これからの作業

- **next-runtime-validation** : Editor / player build で live validation runbook を実行
- **next-hierarchy-delta** : live validation で delta 失敗が観測された場合にだけ追加実装へ進む
- **next-inspector-detail** : live validation で detail 応答不備が観測された場合にだけ追加実装へ進む
- **next-structured-log** : structured log viewer (filtering / search / export)

### 今回実装しなかったこと

- Command の意味付け（どんな command type が存在するか）
  → Unity側 dispatcher の責務。今回の built-in command は live validation 用の最小共通面だけ
- Pending UI で待機中の indication（spinning indicator など）
  → result 列に「pending count」で示しているが、animated indicator は次の UI pass で
- Command result の詳細 parse /構造化表示
  → 現在は latest payload JSON をそのまま読ませる。意味付けは Unity side command authoring の領分

---

## テスト規律

### CommandCorrelationTests 覆カバレッジ

1. **Pending Track：** TrackPending() が entry を作り、PendingCount を increment
2. **Correlation Success：** matching result ID で Pending → Succeeded 状態遷移
3. **Orphan Detection：** 未知 request ID の result は Orphaned entry として保存
4. **Timeout Path：** ExpirePending() で Pending → TimedOut 状態遷移
5. **Disconnect End：** MarkDisconnected() で全 pending を Disconnected 状態へ
6. **Capability Gate at Send：** 未交渉時の SendAsync() は InvalidOperationException を throw
7. **Dispatch Failure：** transport 送信失敗後も DispatchFailed entry が store に残る

### テスト規律の意図

- State machine 遷移の健全性を守る（double-send、stale state、orphan 等）
- Service 境界での gating を検証（capability gate、dispatch rejection）
- UI が「何が起こったか」を honest に見られるようにする

---

## 次セッション推奨事項

1. `DEBUGSTUDIO-Unity_plannning.md` の runbook どおりに live validation を実行
2. 結果を A(Unity 未送信) / B(protocol-decode-capability) / C(store-service-lifecycle) / D(UI-viewmodel) で分類
3. その分類結果に従って、Unity / DebugStudio / 文書のどこへ次修正を置くか決める

---

## ビルド・テスト状況（セッション最終）

```
Build: ✅ succeeded
Tests: ✅ DebugStudio.App.Tests 174/174 passed
  - CommandCorrelationTests に live validation UX の 2 テストを追加
Build: ✅ Assembly-CSharp.csproj succeeded
```

---

## コード品質の観察

- **日本語コメント** : CommandStore / CommandService / CommandWindowViewModel に厚めに追加済み
- **責務分離** : service は logic (timeout, dispatch gate), store は state (pending tracking), UI は dispatch + view refresh
- **Feature flag 的な gate** : capability negotiation による CommandResult frame filtering
- **安全な cleanup** : disconnect時に stale pending を確実にクリア、reconnect時に fresh start

---

**作成日時：** 2026-04-29 06:11:54 JST  
**前回セッション：** 2026-04-28 04:59 - 05:45 JST
