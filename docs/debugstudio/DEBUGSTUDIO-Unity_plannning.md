## 検証結果と採用判断

この plan は **概ね妥当** です。特に、いまの段階で「Unity 側へ未確認の追加実装を積む」のではなく、
**live validation で vertical slice を通し、欠陥を観測ベースで分類する** という順序は正しいです。

妥当と判断した根拠は次の通りです。

- `Assets\OneStarMaker\Runtime\DebugSocketServices\DebugSocketService.cs`
  - capability hello/welcome
  - hierarchy snapshot / delta
  - inspector query/detail
  - single-client / bounded queue
  がすでに実装されている
- `DebugStudio\src\DebugStudio.App\Core\Services\SessionService.cs`
  - connect 前 reset
  - connect 後 capability hello
  - disconnect/fault 時の reset
  が整理されている
- `DebugStudio\src\DebugStudio.App\Core\Services\SessionMessageRouter.cs`
  - log / telemetry / service status / hierarchy / inspector / command result の routing が分離されている
- `DebugStudio\src\DebugStudio.App\Core\Stores\HierarchyStore.cs`
  - snapshot / delta の適用と base revision mismatch 時の安全側動作がある
- `DebugStudio\src\DebugStudio.App\Core\Stores\InspectorStore.cs`
  - 遅延 detail / 古い revision を捨てる保護がある
- `DebugStudio\src\DebugStudio.App\Core\Stores\CommandStore.cs`
  - requestId 相関、timeout、disconnect 終端化がある
- `DebugStudio\src\DebugStudio.App\Features\Commands\CommandWindowViewModel.cs`
  - raw command authoring と pending/result 可視化の最小 UI がある

したがって、「実装不足を仮定して先に増築する」のではなく、
**まず live で slice を通し、どこが壊れているかを観測する** 方針を採用してよいです。

ただし、この plan には 3 つ改善が必要です。

1. **Phase 0 を追加する**  
   live validation の前に、こちらで機械的に用意できる診断面・確認観点・結果記録テンプレートを先に揃える。
   人手検証そのものと、検証を回しやすくする準備を混ぜない。
2. **各 slice の exit criteria を明文化する**  
   「見えた/見えない」ではなく、
   - transport
   - decode
   - store
   - UI
   のどこまで成立したら pass とするかを固定する。
3. **観測結果の残し方を先に固定する**  
   live validation 後にその場のメモで散らさず、
   slice ごとに「症状 / 再現手順 / 期待 / 実観測 / 分類 / 次アクション」で記録する。

この修正版 plan を正本として進める。

## 改善した進め方

### Phase 0: 準備物の先行整備

- capability / log / telemetry / hierarchy / inspector / command の各 slice について、
  観測ポイントと pass 条件を先に固定する
- Unity / DebugStudio のどこを見れば分類できるかを一覧化する
- live validation 結果を書き残すテンプレートを先に作る
- 可能な範囲で runtime diagnostics を先に露出する

現時点では Unity 側に `DebugSocketService.GetRuntimeDiagnosticsSnapshot()` を追加済みで、
少なくとも次は事前観測できる。

- active session の有無
- session id
- pending queue 長
- `MaxQueueLength`
- 未接続時 drop 累積
- queue overflow drop 累積

### Phase 1 以降の原則

- **先に接続、その後に機能 slice**
- transport が不安定ならその先へ進まない
- fail した slice は必ず
  - Unity 未送信
  - protocol/decode/capability
  - store/service/lifecycle
  - UI/viewmodel
  のいずれかへ分類する
- 追加実装は分類結果に基づくものだけ行う

### この plan で先にこちらが進めるもの

1. live validation runbook を整える
2. 既存コード上の coverage を audit する
3. live validation を回しやすくする最小 diagnostics を足す
4. その後に人手で Editor / player build の接続確認を行う

## Coverage audit

- **Capability / Session**
  - 実装証跡あり
  - `DebugSocketService` の welcome 生成
  - `SessionService` の connect 前 reset / connect 後 hello
- **Log / Telemetry**
  - 実装証跡あり
  - Unity 側の realtime stream / telemetry sink
  - DebugStudio 側の `LogStore` / `TelemetryStore`
- **Hierarchy / Inspector**
  - 実装証跡あり
  - Unity 側の snapshot / delta / inspector detail 応答
  - DebugStudio 側の `HierarchyStore` / `InspectorStore`
- **Command correlation**
  - 実装証跡あり
  - `CommandStore` の pending / timeout / disconnect 終端化
  - `CommandWindowViewModel` の raw command UI

現時点の判断として、4 slice すべてに **live で確認する価値があるだけの実装基盤** はある。
したがって、次の主作業は追加実装ではなく **実接続での観測** である。

## このPlanの前提の議論

- 以下引用

正確には「Unity 側をもう1段実装してから」より、「まず live validation で実際に通して、足りない所だけ Unity 側を追加実装する」が正しい順序です。理由は、進捗メモでは未実装扱いの項目でも、実コード上はかなり入っているからです。たとえば DebugSocketService.cs:418 には capability welcome、DebugSocketService.cs:784 には hierarchy delta 生成、DebugSocketService.cs:911 には inspector detail 応答が見えます。一方で DEBUGSTUDIO_PROGRESS_2026-04-29.md:155 はまだ pending 扱いなので、今は「実装不足」より「文書と実装のズレ」が起きています。

アーキテクトとしての推奨は、vertical slice で動かしてから要望ベース改善に入る進め方です。ただし「言われたものを足す」ではなく、「観測された欠陥に対して改善する」運びにした方がいいです。最低限、次の slice が通るかを見れば十分です。1. capability hello/welcome が成立する。2. log と telemetry が見える。3. hierarchy snapshot と delta が反映される。4. inspector query から detail が返る。5. command を 1 つ送って result correlation が見える。この5本が通れば、次の改善はかなり具体化します。

今の段階で危ないのは、live validation を飛ばして Unity 側へ追加実装を積むことです。そうすると本当は transport や capability gating や session reset の問題なのに、「未実装だと思って足したコード」が増えて設計が濁ります。逆に、先に DebugStudio を動かせば、改善要求はかなり良い粒度で出ます。たとえば「hierarchy delta が来ていない」のか、「来ているが store 適用がおかしい」のか、「UI が更新していない」のかを切り分けられます。加えて、SessionMessageRouter.cs:77 の capability gate や、SessionService.cs:102 の reset policy が実環境で妥当かも見えます。

次の一手としてはこれが最短です。

Unity 実機側と DebugStudio を接続して、上の5本の vertical slice を順番に検証する。
その結果を「未送信」「送信済みだが decode 失敗」「store 反映不備」「UI 表示不備」に分類する。
分類結果をもとに、Unity 側追加実装、DebugStudio 側修正、仕様明文化の3系統に分けて改善する。
必要なら次に、live validation 用の具体的な検証シナリオと観測項目を architect 観点でそのまま実行できる形に落とします。

- 引用ここまで

上記をまとめたものが以下に続く

## Plan: DebugStudio Live Validation Handoff

Unity 側と DebugStudio 側をこれ以上先読み実装で進めず、まず end-to-end で接続して動作を観測する。目的は「未実装を足すこと」ではなく、「実装済みの経路が本当に通るか」「文書と実装の差分は何か」「次の改善を Unity 側 / DebugStudio 側 / 仕様明文化のどこへ置くべきか」を切り分けること。単一クライアント前提、WPF 継続、Unity 側 zero-allocation 優先、MessagePack/WebSocket 継続を固定条件とする。

**Steps**
1. Phase 1: 実行前の基準合わせ  
   Unity 側と DebugStudio 側の正本を確認する。特に capability handshake、log/telemetry 送信、hierarchy snapshot/delta、inspector query/detail、command/result correlation の 5 系統が「実装済み想定」なのか「文書上 pending」なのかを整理する。ここで目的は実装追加ではなく、live validation の期待値を固定すること。
2. Phase 2: 最小接続 slice の検証  
   Unity ランタイムを起動し、DebugStudio から接続する。最初に見るのは capability hello/welcome、接続状態遷移、disconnect/reconnect 時の reset のみ。ここで失敗したら他機能へ進まず、transport/session lifecycle に戻って原因を切り分ける。  
   以降の全 step の前提
3. Phase 3: 観測 slice 1 - Log / Telemetry  
   Log と Telemetry が Unity から DebugStudio まで到達し、store に保持され、UI に表示されることを確認する。未表示の場合は「未送信」「送信済みだが decode 失敗」「store 反映不備」「UI 表示不備」に分類する。JsonFileTelemetrySink と realtime stream formatter の実経路が通るかをここで確定する。  
   depends on 2
4. Phase 4: 観測 slice 2 - Hierarchy / Inspector  
   hierarchy snapshot が見えるか、変更時に delta が反映されるか、選択操作から inspector query/detail が往復するかを確認する。進捗メモでは pending とされているが実コード上は sender/query 経路が見えるため、まず live で通るかを観測し、文書と実装の差分を潰す。  
   depends on 2  
   parallel with 3 if別担当で可能
5. Phase 5: 観測 slice 3 - Commands  
   DebugStudio から raw command を 1 件送り、requestId 相関、pending、result、timeout、disconnect 終端化を確認する。ここで重要なのは command の意味そのものではなく、transport と state machine が成立しているかを確認すること。  
   depends on 2  
   parallel with 3 if別担当で可能
6. Phase 6: 失敗分類と次アクション化  
   各 slice の結果を 4 区分で整理する。A: Unity 側未送信または送信条件未成立、B: protocol/decode/capability gating 問題、C: store/service/lifecycle 問題、D: UI/viewmodel 問題。この分類に従って、次の修正を Unity 側、DebugStudio 側、仕様文書のいずれに置くか決める。  
   depends on 3,4,5
7. Phase 7: 要望ベース改善の入口を固定  
   live validation 後の要望は自由記述で受けず、必ず「誰が見ている症状か」「再現手順」「どの slice で壊れたか」「期待挙動」「観測ログ」の 5 点セットで起票する。これにより UI 要望と protocol 欠陥を混同しない。  
   depends on 6
8. Phase 8: 文書同期  
   DEBUGSTUDIO_PROGRESS_2026-04-29.md と実装の差分を更新する。特に hierarchy delta / inspector detail が本当に pending なのか、実装済みで live validation 未了なのかを明確に書き換える。  
   depends on 6

**Relevant files**
- Assets/OneStarMaker/Runtime/DebugSocketServices/DebugSocketService.cs — Unity 側の capability hello/welcome、hierarchy delta、inspector query/detail、single-client queue/drop policy の正本
- Assets/OneStarMaker/Foundation/Telemetry/JsonFileTelemetrySink.cs — telemetry が realtime stream へ流れる入口
- DebugStudio/src/DebugStudio.Client/DebugStudioSession.cs — client transport lifecycle、connect/disconnect/fault の正本
- DebugStudio/src/DebugStudio.App/Core/Services/SessionService.cs — reset と inbound routing orchestration
- DebugStudio/src/DebugStudio.App/Core/Services/SessionMessageRouter.cs — capability gate と store routing
- DebugStudio/src/DebugStudio.App/Core/Stores/HierarchyStore.cs — snapshot/delta 適用結果の確認点
- DebugStudio/src/DebugStudio.App/Core/Stores/InspectorStore.cs — inspector detail 適用結果の確認点
- DebugStudio/src/DebugStudio.App/Core/Stores/CommandStore.cs — requestId 相関、pending、timeout、disconnect 終端化の確認点
- DebugStudio/src/DebugStudio.App/Features/Commands/CommandWindowViewModel.cs — command UI の pending sweep と表示挙動
- DEBUGSTUDIO_MESSAGEPACK_FLOW_2026-04-29.md — envelope/payload/data flow の正本
- DEBUGSTUDIO_PROGRESS_2026-04-29.md — 現状認識と live validation 後に同期すべき進捗文書
- Assets/Docs/Architecture/12-telemetry.md — receiver app の schema/version handling 未整理点

**Verification**
1. DebugStudio 起動後、接続直後に capability negotiation が成立し、接続状態が `Connecting -> Connected` へ遷移することを確認する。
2. Unity 側から log と telemetry を最低 1 件ずつ発生させ、DebugStudio 側で受信・保持・表示の 3 段階が成立することを確認する。
3. hierarchy snapshot 表示、変更後 delta 反映、inspector query/detail 応答の 3 点を確認する。どれかが欠けた場合は sender / decoder / store / UI に分類する。
4. raw command を 1 件送信し、`pending -> succeeded/failed` の遷移、もしくは timeout/disconnect 終端化が観測できることを確認する。
5. 接続を切断して再接続し、stale state が持ち越されないこと、必要な state だけが reset されることを確認する。
6. live validation 結果を「通った経路」「未実装」「実装済みだが失敗」「文書誤り」に分けて記録し、次の修正担当を Unity / DebugStudio / 文書に割り当てる。

**Decisions**
- 次の優先順位は「Unity 側をもう1段実装」ではなく「live validation で通る経路を確定する」に置く。
- 単一クライアント固定は正式な前提とする。multi-client 余地は今回の計画対象外。
- command は意味付けの完成を待たず、まず transport と requestId correlation の成立だけを見る。
- hierarchy delta / inspector detail は進捗メモ上 pending でも、実コード上の実装痕跡を優先して live で確認する。
- 改善要求は観測ベースで起票し、雰囲気ベースの要望収集にしない。

**Further Considerations**
1. Unity Editor 上での再現に加えて、実機または player build でも少なくとも 1 回は接続確認する。Editor と build で threading / scene lifecycle がずれる可能性がある。
2. live validation 時は DebugStudio 側だけでなく Unity 側の送信条件も観測できるよう、必要なら一時的な diagnostics snapshot を使って queue overflow / drop を記録する。
3. live validation の結果、hierarchy delta / inspector detail が本当に動いているなら、最優先の次作業は進捗文書修正と手動検証手順の固定であって、追加実装ではない。

## 実行 runbook

### Slice 1: Capability / Session

- 操作:
  - Unity を起動する
  - DebugStudio から接続する
  - 1 回 disconnect して再接続する
- 観測点:
  - `SessionService`
  - `SessionMessageRouter`
  - `CapabilityStateStore`
  - service status 表示
- Pass 条件:
  - `Connecting -> Connected` が成立する
  - capability welcome が 1 回だけ適用される
  - 再接続後に stale hierarchy / inspector / command pending が残らない

### Slice 2: Log / Telemetry

- 操作:
  - Unity 側で最低 1 件の log を出す
  - telemetry を最低 1 件発火させる
- 観測点:
  - Unity 側: `AppLoggerFactory`, `JsonFileTelemetrySink`, realtime stream
  - DebugStudio 側: `LogStore`, `TelemetryStore`, 対応 ViewModel
- Pass 条件:
  - 受信できる
  - store に保持される
  - UI に表示される
  - 切断/再接続後に新旧データの混線がない

### Slice 3: Hierarchy / Inspector

- 操作:
  - 接続直後の hierarchy snapshot を確認する
  - GameObject の生成/破棄/有効無効などで delta を発生させる
  - 1 ノード選択して inspector detail を要求する
- 観測点:
  - Unity 側: `DebugSocketService`
  - DebugStudio 側: `HierarchyStore`, `InspectorStore`, 対応 ViewModel
- Pass 条件:
  - snapshot が見える
  - delta で revision が進む
  - inspector detail が selected target にだけ適用される

### Slice 4: Command correlation

- 操作:
  - Command panel から `debugsocket.runtime-diagnostics` を 1 件送る
  - 必要なら `ping` または `debugsocket.ping` も送る
  - 成功応答、失敗応答、または timeout/disconnect のいずれかを確認する
- 観測点:
  - `CommandService`
  - `CommandStore`
  - `CommandWindowViewModel`
- Pass 条件:
  - requestId 付き pending が積まれる
  - result で同一行が終端化される
  - disconnect/time out 時に stale pending が残らない

## 結果記録テンプレート

各 slice の結果は次の形式で残す。

- slice:
- 症状:
- 再現手順:
- 期待挙動:
- 実観測:
- 分類: A(Unity 未送信) / B(protocol-decode-capability) / C(store-service-lifecycle) / D(UI-viewmodel)
- 次アクション:

この内容を正本として進める。
