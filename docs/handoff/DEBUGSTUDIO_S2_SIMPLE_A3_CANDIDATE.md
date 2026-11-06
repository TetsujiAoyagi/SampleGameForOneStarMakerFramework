# DebugStudio S2 — 読取り用途に絞ったA3 r3

## 0. 状態と入力

- type: slice / status: 最新develop `cea28ee4` を取り込み済み。implementation head `33ec902f` の再判定Cは未完了。C′は独立監査済み。旧headの判定結果を流用しない。
- risk: high（App寿命の待受け・終了経路を変更するため）。A0独立代替検討とA2構造レビューを実施。
- owner: 主担当。最終判断は依頼者。
- created: 2026-10-21 / expires: Phase D完了または置換revision。
- updated: 2026-11-06。developをrebaseで取り込み、旧headの判定を無効として新headの再検証へ移行。過去の候補時点の記述は経緯であり、現在の状態は本節を正とする。
- branch: codex/pr146-review-fixes（ローカル状態記録）。公開PR branch: codex/debugstudio-s2-verification。
- implementation base: `cea28ee42070026fa2d9bbec3c154c7b9d24283b` / current implementation head: `33ec902fd6b9f129e9a6643380058159178f2ac3`（develop取り込み後、判定C/C′未完了）。
- 入力: ClaudeのDEBUGSTUDIO_S2_CUT_PLAN.mdと、依頼者の「Debug用途のToolで厳密なトランザクションを要求していない」という今回の指示。
- CUTが参照するDECISION_PACKは指定worktreeでは見つからなかった。D番号を独立した承認根拠にせず、CUTの具体的な用途と今回の指示から候補を作る。
- 承認: 依頼者「凍結してよい、PhaseC/C’までサブエージェント使いながら君の裁量で完遂して」。r3の採否案、保証緩和、受入境界、Unity製品変更を行わない場合の全EditMode回帰除外を承認として記録。実装・必要なcommit・Editor操作・.NET検証・証拠保存を含む。Phase Dのmerge/task終了は今回の出口に含めない。
- harvest: 実装後のCLI/WPF起動方法と制約をtools/DebugStudioのREADMEへ統合。計画は保存引渡し後に置換・整理。

旧A3や再編案、固定packetは変更しない。それらの強い保証を本スライスの必須条件へ持ち込まない。書込み時の世代識別（旧D9）はS3で改めて判断し、S2で導入も撤回もしない。

### 引継ぎ票

- 次のPhase/役割: AC5のRelease実Editor記録とA3期限の扱いを解決して判定Cを完了し、更新したblind bundleで別担当がC′を再監査する。両結果の別session Evidence取得確認後、Phase Dの突合・最終判断へ渡す。
- 凍結revision: A3 r3、凍結commit `e4910a392be1a1cb8cd1f617c127d93bf511aa18`。現implementation base `cea28ee42070026fa2d9bbec3c154c7b9d24283b` / head `33ec902fd6b9f129e9a6643380058159178f2ac3`。記録commitはimplementation headに含めない。旧head `e7ea37ab` と `6e1f82c3` のC/C′記録は後段に分けて保持し、新headの判定へ流用しない。
- Phase A snapshot: ローカル固定済み。取得案内は主担当のprivate artifacts内。B別sessionが独立した期待hashでfetchし、manifestと必須2文書の内容hash・閲覧を確認済み。
- 旧headのPhase B resultと発見C入力: Artifact Storageでローカル固定済み、別sessionによるfetchと必須内容の閲覧を確認済み。旧headの判定C evidence / C′ blind入力は同じbase/head・生証拠から237fileの同一snapshotとして判定前に生成し、ローカル固定済み。private取得案内FINAL_C_FETCH_GUIDE.jsonとBLIND_CPRIME_FETCH_GUIDE.jsonにより、それぞれ別sessionで全file集合/hash・必須74pathの閲覧を確認済み。追加実機は行わず、この固定証拠と決定記録に基づきC/C′を判定した。
- 新headのPhase B result: ローカル固定済み。private取得案内 PR146_B_FETCH_GUIDE.json により別sessionでfetch・全2 fileの内容hashと原記録の閲覧を確認済み。同一PC・同一ユーザーでの確認。
- 動作確認: 下記の新head判定Cに含めて実施済み。
- 新headの発見C: GPT-6-astraが構造・契約・失敗経路を確認し、阻害指摘0、後続指摘0。限定App 19件・CLI 14件成功、TRX実行名を確認しcontract-auditはerrors/warnings 0。52 fileの非blind入力・結果をローカル固定し、別sessionで全hashと必須12 pathの閲覧を確認済み。取得案内 `PR146_DISCOVERY_FETCH_GUIDE.json`。
- 必要な権限: ローカル.NET実行、対象を限定したEditor操作、WPF起動/終了、Artifact Storage stage/fetch。実行環境の拒否は迂回しない。
- 停止規則: 凍結済みI1〜I4/E1〜E3を満たすまでBと発見Cを最大4往復、C′差戻しを最大3往復。新しい契約/所有者/寿命が必要ならAへ戻して分類する。現在の問いを阻害しない追加保証はS3以後へ送る。
- 決定記録: AC5のHierarchy再読取りについて、依頼者は読取りが成功していればGOでよいと裁定した。§6の15秒は再読取りの総時間と読み、fixture終了時点を計時起点に追加しない。判断経緯と生時刻は非blind判定記録に保存する。
- 実行環境: 実機証拠取得時は対象project・Unity version・検証用checkoutのclean状態と固定headを確認した。2026-10-28はClaudeも同時作業中で、依頼者はUnity Editor起動前の事前連絡を指定している。今回の判定に追加実機は不要で、他者のEditorや接続先は操作していない。

## 1. 今回閉じる問い

同じWPFプロセスを `--listen` で起動したまま、AIがEditorでPlayを始め、CLIでHierarchyとInspectorを読み、Play停止・再開後にもGUIのConnect操作なしでまた読めるか。

1本のsliceで扱う。今回の中心は「常駐WPFの待受け直し」と「実用になるbest-effortの読取り」。専用の世代/transaction/相関基盤は作らない。

### 減らす保証と残す保証

- 採用候補: inspectは要求送信前から購読し、最初のTargetId一致の応答を返す。自分が送ったrequestの応答であることや最新値であることは保証しない。同じNodeIdへGUIが出した応答や、直前queryの少し古い応答を採り得る。
- 採用候補: CLIはGUIの選択を変更しない。表示中と同じ対象の詳細が更新されることは許す。CLI専用の表示storeやprefix振分けは作らない。
- 採用候補: NodeIdは現在のPlayでHierarchyから取り直して使う。過去出力・AIの記憶のIDを再利用した場合、別対象を読める可能性をhelpへ書く。TargetNameだけを識別保証にはしない。
- 残す: 使わない限り従来GUI起動のまま、明示停止/終了で自動再待受しない、待受けを重複させない、CLI要求と終了処理は有限、事実でない成功や最新性を出さない。
- 範囲外: 書込みと配送分類、service/connection世代、requestId伝播、pending辞書、受信調停、固定lease、headless、Player、遠隔、GUI全般の不具合修正。読取りの制約を将来の書込みへ自動適用しない。

## 2. 現行コードで確認できたこと

基点とCUT記載のコード基点は一致。前回のcbcdb22cから対象DebugStudio/DebugSocketコードへの差分はない。以下は静的事実であり、接続失敗を実測した報告ではない。

- App.OnStartupはWindowを作って表示するだけ。待受け開始は現状SessionWindowViewModel.ConnectCommandからSessionService.ConnectAsyncへ進む。
- server transportの自然切断はReceiveLoop終了→FinalizeReceiveLoopAsync→ShutdownSessionAsync(server.Stop)の後にDisconnected/Faultedを通知する。自動待受け直しの所有者はいない。
- ConnectAsyncは接続accept後に返る。Connect/cleanupが未完了のまま再帰的に呼ぶと重複やalready-activeになる。1本のloopで待つ必要がある。
- SessionResetPolicyは次のConnect開始でHierarchy/InspectorをClearする。一方、切断時はHierarchy/Inspectorを消さない。「古いIDはStudioに一切残らない」は正確ではない。
- HierarchyStore.GetRetainedSnapshotはnodes/revision/captured時刻を同じlockで複製できる。接続状態とのtransactionは提供しない。
- InspectorQueryServiceはBeginQueryで表示対象を変えるためCLIから呼ばない。SessionService.SendProtocolMessageAsyncで直接queryを送れる。InspectorDetailReceivedはTargetId/取得時刻/sectionsを持つ。
- InspectorStoreは表示中でないTargetIdのdetailを無視する。同じ対象のGUI更新は起こり得る。
- MainWindow.OnClosedは同期dispose、Session VMは背景イベントからDispatcher.Invokeを使う。新loopの停止をそのままUIの同期待ちへ追加しない。
- AcceptTimeoutSecondsの既定は60秒。無接続のaccept期限でもTimeoutExceptionとなり、現行server transportはFaulted通知とcleanupを行う。handshake期限もTimeoutExceptionであり、例外型だけで全timeoutを正常待受けとみなさない。
- Runtime connect modeには再試行loopが既にある。SampleGameの現在configもlocalhost:5011へのconnect。S2でRuntime接続機能を新設する必要はない。

根拠: App.xaml.cs、AppCompositionRoot、SessionService/ResetPolicy、DebugStudioServerSessionTransport、InspectorQueryService/InspectorStore、HierarchyStore、Runtime DebugSocketTransportHost（いずれも現行source）。

## 3. A1設計候補

### 3.1 起動と待受け直し

- WPFへ `--listen` を追加。引数なしは現行どおり。起動先はlocalhost:5011/debugsocket/、CLI controlは既存localhost:5012/cli-control/。公開範囲を変更しない。
- Coreの小さな `AutoListenService`（仮名）がApp寿命の継続意図、loop Task、取消、retry待ちだけを所有する。AppCompositionRootで作り、ViewModelを経由せずStartする。
- loopは切断待ちの購読を先に用意→SessionService.ConnectAsyncをawait→自然切断/故障のterminalをawait→次Connect。例外時もConnect Taskの片付け完了後に再試行する。イベントhandlerからConnectを再帰呼出ししない。
- bind等の故障では診断を残し、取消可能な1秒、2秒、5秒（上限5秒）の間隔で再試行する。正常接続後に初期間隔へ戻す。通常の切断後はcleanup完了を待って再待受。無接続accept期限は正常な待受け継続として扱い、故障のFaulted表示・backoffへ流さない。既定60秒期限と現行transportのFaulted通知を局所対応し、accept継続時も未完了Taskやlistenerを重複させない。接続要求後のhandshake失敗やbind失敗を無接続期限へ一括分類しない。opt-inなしの既存動作は維持する。
- opt-in中の既存GUI Connect/Disconnectも同じ小serviceへStart/Stop意図を伝える。Stopは継続意図を先に閉じ、accept/retry待ちをcancelし、transport Disconnectを完了してloopを止める。GUIと自動loopから別々のConnectを起動しない。opt-inなしの既存動作は維持。
- opt-in時のCanConnect/CanDisconnectも継続意図・停止中状態へ従わせる。accept/接続/retry中はStop可能とし、Faultedでもretryを止められる。稼働中・停止処理中のStartで別loopを起動しない。service状態変更で既存ボタンのCanExecute変更を通知する。
- 明示Stop後は新しい明示Startまで再待受しない。終了では再Start不可。自然切断のcleanup完了という既存経路を使い、全イベントの世代調停へ広げない。
- --listen時のWindow終了はOnClosingで最初のCloseを一度延期し、終了中の追加Closeも取消して同じ終了Taskへ集約する。UIをpumpしたままAutoListen停止→control要求取消・完了→persist/VMを含む残りのWindow所有lifetime解放を一回だけawaitし、完了後の再Closeだけを許す。AppCompositionRootの既存OrderedAsyncDisposableに沿って順序を配線し、同じcontrol/VMを手動経路と複合lifetimeの両方から破棄しない。最後に既存VMがsessionをDisposeし、AutoListenはsessionのDispose所有者にならない。この経路ではOnClosedの旧同期Disposeを再実行しない。各解放の例外は診断して残りの解放を試み、finallyから再Closeへ進め、失敗したDisposeを再実行しない。OnClosingの非同期処理から例外を未処理で流さず、既定起動の終了回帰を維持する。汎用lifetime managerは作らない。

### 3.2 hierarchy

`debugstudio-cli hierarchy [--control-uri URI] [--timeout-seconds N]`。WPFの既存HierarchyStore.GetRetainedSnapshotを返す。選択状態は入力にしない。

出力は取得したnodes、revision、capturedAtUnixTimeMilliseconds、hasSnapshot、接続状態と交渉状態。NodeId/ParentId等のlong識別子はJSON文字列。hasSnapshotはnodes件数から判定せず、既存Runtimeが1から採番するretained revision > 0を基準にする。初期状態/Clear後はrevisionとcaptured時刻が0でfalse、正のrevisionを持つ受信済snapshotは空でもtrue。未受信はhasSnapshot=falseと理由を返す。新しいstoreや世代状態を作らない。

snapshotは保持値で、接続状態/交渉状態は応答作成時の観測。全項目が厳密に同時の状態であるとは主張しない。切断中の保持値も状態と時刻付きで読める。接続中でもRuntimeの今この瞬間の完全な木とは限らない。新しいCLI専用store/世代/source stateは作らない。

### 3.3 inspect

`debugstudio-cli inspect <NodeId> [--control-uri URI] [--timeout-seconds N]`。正のInt64を受ける。

Coreの小さな読取りserviceが要求ごとにTCSと購読を作る。購読→既存InspectorQueryの送信→最初のTargetId一致の完了を待つ。GUI用InspectorQueryService/BeginQueryは呼ばない。queryFlagsは既存のmetadata/components/properties/raw値を利用する。

Readyなら対象情報、State、captured時刻、section/propertyの型・RawValue等を返す。NotFound/Faulted/Unsupportedも応答として返し失敗exitにする。別TargetIdは無視する。同TargetIdの応答の出所・取得の前後は区別しない。型付きRawValueの欠落を表示文字列から捏造しない。

未接続/必要capability未交渉は送信せず失敗。要求中にRuntime切断を観測したら失敗。接続を跨いで要求を持ち越さず、再接続後はCLIを呼び直す。高度な世代識別や自動再送は追加しない。

期限はqueryの送信待ちにも渡す。購読前後の接続状態を確認し、TCSはRunContinuationsAsynchronouslyを使用する。期限/取消/切断/正常・失敗応答の全経路をfinallyで購読解除する。CLIプロセス終了をWPFが即時検出する保証は置かず、切断が観測できた時または要求deadlineまでに解除する。専用監視Taskやpending辞書を追加しない。

### 3.4 controlとCLIの最小契約

既存control listenerを利用し、read用request/responseをDebugStudio側のYAML正本へ追加する。operation=hierarchy/inspect、NodeId、request timeout、応答data/errorを持つ。control側requestIdはCLI応答との既存の対応に使ってよいが、Runtime Inspectorの外側requestIdを保存・伝播する改修はしない。Unity surface/wireを変更しない。

新しいreadコマンドだけJSONをstdoutへ1objectで返す。例は `status:ok|error`、`reason`、`data`。status/reasonはreadの完了・未接続・未受信・NotFound・Faulted・timeout等の事実を表す。成功exit0、read失敗1、usage2、read timeout3。送信の適用有無を分類するunknown-outcomeは導入しない。

既存sendのtext/stdout/stderr/exit（timeout2を含む）とoffline compareは変更しない。共通transaction resultや全コマンドのJSON移行はしない。S3が書込みの意味を設計する。

readの既定timeout15秒、指定は0超〜60秒。control要求の全frame受信15秒（fragmentで延長しない）、応答送信3秒、server/client close3秒。期限超過はsocketをAbort/Disposeする。sendの既存の明示request timeoutは維持し、S2のread上限を勝手に適用しない。既存CLI clientのfinally CloseAsync(None)だけは有限closeへ直す。確定結果はclose失敗で覆さない。

control listenerは1接続ずつでよい。新しいreadが他のCLI要求を最大timeout分待たせることは受容する。無要求/不完全frame/close待ちが無期限に次接続を塞ぐことは許容しない。

## 4. 責務・規模・変更対象

いずれも候補。新しい依存packageやasmdefは不要。

- App.xaml.cs（16行、+10〜20）: --listen解析と起動指示。起動policyをViewModelへ置かない。
- AppCompositionRoot（283行、+20〜40）: AutoListen/読取りserviceの生成と寿命配線。中核logicを足さない。
- 新規Core/Services/AutoListenService（100〜180行）: 1本の待受けloopと明示Start/Stop、retry。App所有。既存session facadeと狭いretry待機delegateへ依存。fake session/シグナルで検査。
- 新規Core/Services/CliReadService（80〜140行）: 既存snapshot取得と要求寿命のInspector購読。request所有。WPF型・Unity型に依存せず、store/query facadeを使う。
- SessionWindowViewModel（332行、+15〜35）: opt-in時のStart/Stop委譲とCanExecute/変更通知。MainWindow.xaml.cs（51行、+30〜60）: 一回だけの非同期解放、終了中のClose取消と完了後の再Close。50%以上の増加を見込むが、Window終了という単一責務とUI Dispatcherの境界なので同fileで限定修正し、汎用managerへ分割しない。XAMLの画面変更なし。
- DebugStudioCliControlService（338行、+40〜80）: read routingとsocket deadline。readの中核はCliReadServiceへ置く。deadlineを独立Managerへ広げない。
- Program（68行、+15〜30）/parser（165行、+30〜50）、Client read control helper（新規80〜140行）: read専用入口とJSON出力。既存send/compareを作り直さない。
- Contracts/DebugStudio surfaceのread DTOとcodegen。追加only、既存field/enum値を変えない。Unity側の生成出力に差分を出さない。
- server transport（527行）/SessionService（223行）: 原則既存の自然切断terminal/Connect経路を使う。必要な修正があってもこの用途のcleanup・待受け直しに限定。世代・全受信routing再設計が必要ならAへ返す。

少数の新規classは異なる寿命（App loop／要求購読）とテスト境界を分けるため。CliReadService自体はAppから再利用し、TCS/購読/期限だけ要求寿命とする。既存Core.Services名前空間に置き、新しい総合session抽象や製品公開APIを作らない。規模警報に達するfileへ独立のpolicyを押し込まない。Scene/Prefab/Runtimeへ依存を逆流させない。

## 5. 最低条件・受入候補

### 不変条件

- I1: --listenなしの起動と既存send/compareの契約を変えない。
- I2: 待受けloopは1本、明示Stop/終了後に再待受しない。起動/切断/失敗処理でUIを塞がない。
- I3: inspectはGUI選択を変えず、購読は要求の終了までに解除する。別TargetIdは採用しない。
- I4: CLI/controlの受信・応答・終了が期限なく固着しない。保持値/同TargetId best-effortを厳密な最新・自己応答と称さない。

### 成立条件

- E1: --listenでConnectを押さず実Runtimeへ接続し、hierarchy/inspectで読める。
- E2: WPFを再起動せずPlayを停止・再開し、新HierarchyのNodeIdでまた読める。
- E3: 未受信/未接続/未知NodeId/Runtime失敗/timeoutが結果として返り、正常値と区別できる。

受入の対応:

- AC1(I1): 引数なしの従来起動と正常終了、send成功/失敗/timeout出力、compare、usage2の既存回帰。read DTO fixtureで初期/Clear後のhasSnapshot=falseと、空でも正のrevisionの受信済snapshotのtrueを区別する。
- AC2(I2,E2): fake sessionで自然切断後の2回目Connect、bind失敗のretry待機、明示Stop・終了中の取消、同時Startでもloop1本を検査。Faultedでretry待機中もGUIと同じStop入口のCanExecuteがtrueで、Stop後に待機を進めてもConnectが増えないことを含む。追加のfake 1ケースで無接続accept期限を注入し、故障backoffなしに待受けを継続し、明示Stop後は期限を進めても次acceptを始めないことを確認する。現行transportのFaulted通知を見落としてfakeだけで成功にしない。実Editorで60秒待つ検証は要求しない。イベントhandler内の再帰Connectを使わない。
- AC3(I3,E3): inspect Ready/NotFound/Faulted/Unsupported、別TargetId無視、timeout/切断/取消で解除、BeginQuery非呼出し。同TargetIdのGUI由来応答を採用し同対象のGUI値更新を許すこともfixtureで明示する。
- AC4(I4): 不完全frameの継続・応答送信停滞・client/server close無応答を期限注入で確認。read要求/送信/終了後に次controlが処理できる。再接続sourceや全failure組合せを網羅する試験へ拡張しない。
- AC5(I2,I4,E1,E2,E3): 実Editorの§6を通す。2回目の読取りが成功することを初回の接続成功で代替しない。未知NodeIdが失敗を返すことも確認。進行中inspectとPlay切断が重なる実WPF正常終了を1ケース確認し、UI固着・再待受け・二重解放がないことを観測する。

GOはI1〜I4/E1〜E3を同じ候補headで満たすこと。best-effortの許容事項を欠陥として再び必須相関基盤を導入しない。停止/期限/正常な2回目接続の欠陥は修正する。今回実行していないのでGO判定はない。

## 6. 検証経路・Phase分担

判定Cで実WPFプロセスを `dotnet run --project tools/DebugStudio/src/DebugStudio.App/DebugStudio.App.csproj -c Release -- --listen` で起動する。専用STA/WPF test hostは作らない。現在のSampleGame connect設定を確認し、必要な一時設定を退避・復元する。

Editorの正しい版/projectを確認し、既存Pipelineのstatus/discoveryと必要ならevalでPlay開始・停止、テスト用root/active/inactive対象の生成、値の独立読取りを行う。Scene YAML編集や本番asset保存はしない。Play1でhierarchy→その出力のNodeIdへinspect→現在のHierarchyにない正のInt64による未知IDの失敗、Play停止→再開後にfixture再生成→Hierarchyを取り直してinspect。WPFプロセスは同じまま、GUIボタンを押さない。対象照合は一意なtest root/親子pathとEditor側の実値で行う。

fixture作成だけでは自動Hierarchy更新にならないため、次の経路を固定する。各wrapper呼出しは別commandとして実行する。以下はC実行用の手順であり、今回実行した証拠ではない。

1. repo rootで `tools/unity-editor.cmd status`、次に `tools/unity-editor.cmd command`。公開名を確認してPlayを操作する。名前付き操作が無ければevalで `UnityEditor.EditorApplication.isPlaying = true;` / `false;` を使い、状態を別evalで確認する。
2. WPFの接続交渉完了・初回Hierarchyを待つ。その後、次のC#をUTF-8 Base64にして `tools/unity-editor.cmd eval <Base64>` に一引数で渡す。PowerShellでは `$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($code))` で作る。Play2でも新しいtokenで繰り返す。

```csharp
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Play required");
var flags = System.Reflection.BindingFlags.NonPublic;
var initializer = typeof(SampleGame.DependOnAll.AppInitializer)
    .GetField("s_instance", flags | System.Reflection.BindingFlags.Static).GetValue(null);
var service = (OneStarMaker.Runtime.DebugSocketServices.DebugSocketService)
    typeof(OneStarMaker.Runtime.AbstractApplicationInitializer)
    .GetField("_debugSocketService", flags | System.Reflection.BindingFlags.Instance).GetValue(initializer);
if (service == null) throw new System.InvalidOperationException("DebugSocket service missing");
var token = "S2ReadProbe_" + System.Guid.NewGuid().ToString("N");
var root = new UnityEngine.GameObject(token);
var active = new UnityEngine.GameObject("Active");
active.transform.SetParent(root.transform, false);
var inactive = new UnityEngine.GameObject("Inactive");
inactive.transform.SetParent(root.transform, false);
inactive.SetActive(false);
service.NotifyHierarchyChanged();
UnityEngine.Debug.Log(token + "/Active activeSelf=" + active.activeSelf
    + "; " + token + "/Inactive activeSelf=" + inactive.activeSelf);
```

3. 既存service取得のreflectionは試験用eval内だけに置き、製品getterを追加しない。現在のprivate field名を静的照合済みだが、evalコンパイル・取得・publish疎通は未確認。Hierarchyからtoken配下の2対象が現れるまで総計15秒以内で再読取りし、各NodeIdを取り直してinspectのGameObject.ActiveSelf（BooleanのRawValue）をEditor側の独立値true/falseと照合する。NodeIdだけの一致や表示文字列の一致で値照合を代替しない。NodeIdとUnity InstanceIDが同じとの仮定は置かない。fixtureはPlay終了で消え、本番assetへ保存しない。
4. 初回疎通担当はC実行担当。wrapper/eval/private field取得が不成立なら検証補助手順を修正し、製品変更が必要ならAへ戻す。厳密相関の追加で置き換えない。

各段のコマンド、stdout/stderr/exit、対象head、WPF PID、Editorの独立値、二度のPlay境界を保存する。取得時刻は相関保証ではなく診断情報。同じPIDだけで成功とせず二度目の実値を確認する。最後にinspect開始・要求受付を既存診断で確認してから、応答完了待ちを挟まずPlay停止とWindow.Closeを操作する。要求が切断/取消/期限のいずれかで有限に終わり、WPFが正常終了することを記録する。競合前にreadが完了した観測を「進行中」と記録しない。これはCの1ケースであり、多数の競合組合せや専用WPF test hostを増やさない。終了時は今回のWPFを正常終了、設定を復元する。人間所有Editorを強制終了しない。

.NETは新規loop/read/deadlineの少数fixtureと既存関連回帰。差戻しfilterはAutoListen/CliRead/ControlLifetime/既存CliControlPlane/CommandCorrelation、CLI projectのsend/compare回帰、Contractsの追加DTO roundtrip。判定必須は同集合の全件、codegen --check、contract-audit/docs-audit、実接続AC5。テストのTask.Delay/Thread.Sleepは禁止し、retry/期限をシグナルまたは注入待機で進める。

Unity source/Unity surface/Sceneの製品変更は行わないため、最終全EditMode回帰の除外を提案する。代替証拠は関連.NET回帰と実EditorのPlay2回。Unity側製品変更が必要になったら除外を引き継がずPhase Aへ返す。Bは.NET確認とEditor compileまで、Unityテスト/BuildはCの標準経路に限定する。

未知経路: --listen自体は未実装、実Editorの2回Playは未確認。担当はC実行担当、確認地点はAC5。B出口の動作確認は実WPFの起動/明示終了を軽く観測、Unity実行部分は未実施を明記してCへ引き渡す。Cで不成立なら待受け/終了/CLI読取りのどこが原因かを分けてBへ戻す。受入れを黙って削減しない。

高コスト証拠: なし（Player/別hostなし）。C照合scriptはofflineで生CLI JSON/exitとEditor独立値を読み、fixture（Ready0、NotFound1、usage2、read timeout3、Play2回正常）を先に通す方式。BはC照合scriptを作らない。C/C'のbase/head固定・blind分離・証拠保存は共通workflowに従う。

## 7. A3候補の採否・未決

主担当案: Claude CUTの1本化、--listen opt-inと待受け直し、既存HierarchyStore、TargetId best-effort、同対象GUI値更新許容、有限終了、既存send/compare維持を採用候補とする。

補正する表現: 「古いIDはStudioには残らない」「古いloopのイベントは起こらない」は一般保証にしない。切断保持値と通常の自然切断再待受けを扱う。CLI終了の即時検出も要求しない。添付内の『S2でowner確認不要』は本候補の承認に代用しない。

人間が凍結前に判断するのは、上記の保証緩和（同TargetIdで古い別query応答を採用可・GUI同対象更新可・Play毎のID取り直し）と、この1本の受入境界。今回のDebug用途という方針に沿った推奨候補として提示し、承認済みとは記録しない。

A0: 初稿前の独立検討で、自然切断terminal後の1loop、要求寿命購読、期限、実WPFで2回Playの構成を支持。正式A2の代用にはしない。
A1: r1を固定し、同一SHAの候補とCUTを独立A2へ渡した。
A2: GPT-6-astra（構造・寿命）とGPT-6.1-sol（用途・受入れ）で独立に静的照合。双方が単一sliceとbest-effortを支持した。原記録は別保存。
A3候補での採否: retry中のGUI停止条件（AR1/F1）、fixture作成後の既存NotifyHierarchyChangedとservice取得経路（F2）を採用。所有解除順・Window lifetimeまでの非同期終了を具体化。いずれも既存の停止/検証条件の局所補正で、新しいtransaction保証は追加しない。不採用の必須修正なし。書込み世代/自己応答保証はS3以後に保留する。

r2への追加レビュー（依頼者が2026-10-24に提示したDEBUGSTUDIO_S2_SIMPLE_A3_REVIEW.md、担当モデルは文書に記載なし）への主担当の採否案:

- R1: 採用。無接続の正常待受けを故障扱いしないため、§3.1とAC2へ局所補正。handshake失敗と混同しない。新しい世代/相関基盤は追加しない。
- R2: 採用。Window所有の解放を一回にする経路、追加Closeと例外時の終了先を§3.1へ明記。AC1の既定終了回帰、AC5の実WPF終了1ケースへ反映。
- M1: 採用（既存DTO確認の明確化）。revision基準と未受信/空snapshotのfixtureを§3.2/AC1へ反映。
- M2: 採用（既存実値照合の明確化）。§6でBoolean RawValueとEditor独立値を照合する。
- M3: 確認メモとして採用。既存SessionCapabilityCoordinatorはhello送信例外をNegotiationFaultedへ吸収するため、ConnectAsyncの例外だけをhandshake失敗の根拠にしない。実Editorで再接続停止を観測した場合に限り局所対処を判断し、今回watchdogを新設しない。

追加レビューは静的確認・条件付き承認の推奨であり、人間の凍結/B開始指示ではない。r3は主担当による根拠照合と局所反映までで、r3の独立再レビュー・保存/取得確認は未実施。r2のレビュー/取得確認をr3実施済みへ読み替えない。
人間との採否確定と凍結は未実施。本書は引き続き**A3候補、凍結しない。** 実装や実機成立の判定ではない。

次session用:
> DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.mdを入口に、Debug用途に絞った単一S2案のA3候補を検討する。--listenで同じWPFからPlay停止再開後もCLIで読み、TargetId best-effortと同対象GUI値更新を許容する。新世代/requestId/transaction基盤や旧4分割を必須へ戻さず、指摘の実害と最小修正を示す。依頼者の明示判断前に凍結・実装・commit・push・Editor操作・Unityテスト・Buildを行わない。


## PRレビュー後のPhase B差戻し（2026-10-28）

切断イベントが解除後に呼ばれる場合に破棄済み取消へ触れないよう修正し、停滞する送信も切断で終了する。凍結条件 I2 / E2 と AC3 の切断・再待受けの欠陥に対する B 適応。非対応hostのcommand失敗応答をread失敗として扱い、不正read payloadにはread失敗を返す。E3の失敗とtimeoutの区別に対する B 適応。send専用の `--uri` をREADMEで明確化した。契約や責務配置は変更しない。

client/serverのtimeout起点とclient離脱後の占有は、既存の期限と即時検出を保証しない凍結条件の変更を伴うため、S3以後の検討に送る。新headでは旧C/C′の判定を使わない。

限定確認: Release AppのCliReadServiceTests / CliControlLifetimeTests 19件、CLIのReadCommandTests 14件成功。contract-audit / docs-auditはerrors=0、warnings=0。最初の並列buildは共有DLLの競合で失敗し、逐次実行で確認した。Unity Editor compile・実機動作確認・判定必須検証は未実施。新しいB resultはローカル固定し別sessionで取得・閲覧を確認済み。

## Phase B 実装結果

担当: GPT-6.1-sol（統合担当と同系列の実装helper 2件）。凍結r3の範囲で、単一AutoListen、明示Stop、idle acceptの継続、WPFの一回の非同期解放、既存retained hierarchyと要求寿命inspect、DebugStudio-only DTO/CLI、control/clientの有限期限を実装した。既存send/compare契約を維持する。CLI/WPFの使い方とbest-effort制約は既存の`tools/DebugStudio/src/DebugStudio.Cli/README.md`へ統合した。

B適応: 同じpending acceptをidle期限後も継続し、control listenerでも未完了acceptの取り残しを避ける。期限テストは既存責務の内部delegateまたはTimeProviderで進める。Window所有のOrderedAsyncDisposableとVM解放は各失敗を診断して残りを試み、一回の解放に集約した。AutoListenはsessionをDisposeしない。新しい世代・requestId伝播・pending辞書・Unity製品変更はない。Unity生成出力の内容差分は0。

.NET Release確認: 関連App/loop/read/control/transport/相関とshell回帰84件、CLI全112件、Contracts全38件が成功（各failed=0/skipped=0）。codegen --checkは56生成物一致。contract-auditとdocs-auditはerrors=0/warnings=0。既存テスト由来のnullable/MessagePack analyzer警告が残る。CLI/Contractsの最初のRelease --no-buildはその構成が未生成のため実行不可で、通常ビルド付き再実行が上記成功結果。生ログ・TRXと正確なコマンドはprivate B resultに保存する。

B出口の軽いWPF観測: 引数なしと--listenの両方でWindow handle取得後にCloseMainWindowを送り、15秒以内にexit 0を観測した。Unityの接続・Play2回・進行中inspectと切断が重なる終了は未実施で、C実行担当へ引き渡す。BからEditorを起動せず、Unity compile・Unityテスト・Build・判定C・C′は未実行。B観測をCの判定証拠へ転用しない。

構造補足: MainWindowはWindow終了という単一責務のまま非同期化した。OrderedAsyncDisposableの一回の例外継続cleanupとMainWindowViewModelの残り解放は、凍結済み所有解除契約を実現する局所変更である。control期限は既存control service内、読取りの中核はCliReadService、App loopはAutoListenServiceへ分離した。

## 旧implementation head `e7ea37ab` のPhase C / C′記録

発見C（GPT-6-astra）の初回差戻し1件は、既定accept期限が無視するHTTP要求で更新される問題だった。Bが凍結条件内で修正し、C（GPT-6-sol）が確認した。未公開実装履歴を整理してimplementation headをe7ea37abに固定した。

固定headのC実行では、Release .NET App 85/85、CLI 112/112、Contracts 38/38、failed/skipped 0。実行名集合をTRXと照合した。codegen56生成物一致、contract/docs auditはerrors0/warnings0。C作成の隔離checkerは合成17fixtureと、Editor独立値を読み取った実観測3件が期待どおりだった。

実Editor 6000.6.0f1と同一WPF PIDで、二度のPlayそれぞれに新しいfixtureを生成し、現在のHierarchyから取得したNodeIdのBoolean RawValueをEditor独立値true/falseと照合した。未知の正Int64 IDはNotFound、GUIのInspector選択はNo selectionだった。軽量debuggerでWPFのinspect要求受付位置を確認し、その後Play停止と通常Window.Closeを重ねた。readは有限にdisconnected、WPFはexit0。Debug構成と軽量debuggerの採用は依頼者が提示したメモリ制約に対応した検証経路の適応で、Visual Studioを起動せず、製品sourceや受入条件は変更していない。Editorのprocess限定content modeは元のnullに戻し、Scene cleanと設定file hash不変を記録した。

C′（GPT-6-luna）はCの所見を含まない同じ固定入力237fileを別sessionでfetchし、全file集合・bytes/hash、必須74pathの内容、3 TRXのテスト名集合を独立に確認した。固定source38fileはheadのblobと一致した。patchのgit再生成時の違いはhunk headerの関数名表示だけで、hunk範囲と本文は一致した。最初のblind報告は15秒の起点をfixture終了と読み証拠不足とした。主担当が§6原文に起点の明記があるかと初回読取りの生時刻を示して再確認を依頼し、C′はC所見を読まず、依頼者の後続判断より前に起点の追加は過剰解釈として自ら撤回した。

実際にはPlay1のfixture完了から最初のHierarchy読取り開始まで24.862秒あった。読取り自体は初回1回で0.472秒、Play2は0.481秒で両対象を取得した。§6の15秒は再読取りの総時間と読み、fixture完了を起点とする条件を追加しない。依頼者は読取りが成功していればGOでよいと判断し、Cも原文と一次記録を再検討してGOに訂正した。決定記録はprivate OWNER_15S_DECISION.md。C/C′の最終判定はともにGO、blockerなし。fixture完了から読取り開始まで15秒以内だったとは主張しない。

残る範囲: InspectorはTargetId一致のbest-effortであり、古い応答や同対象GUI応答を採り得る。NodeIdはPlayごとに取り直す。実機の終了競合はdebuggerで受付位置を停止した制御観測で、全競合順序の網羅ではない。Release .NETの回帰は実行済みだが、実機WPFはDebug構成。Cの引数なしWPFは正常Closeとプロセス消滅を確認したが終了codeは未取得であり、0と推定しない。Unity製品・wire差分がないため全EditMode回帰はA3で承認された除外を適用した。


## 修正後implementation head `6e1f82c3` のPhase C / C′結果

判定C: GPT-6-astra。GO、凍結条件または常時契約に反する未解決blocker 0。Release .NET App 87/87、CLI 113/113、Contracts 38/38、failed/skipped 0。codegen 56生成物一致、contract/docs audit errors/warnings 0。Editor 6000.6.0f1で同じWPF PIDによる2回のPlayを確認し、Hierarchy由来NodeIdでInspectorのActiveSelf Boolean RawValueが独立に読んだEditor値true/falseと一致した。未知の正Int64 IDはNotFound/exit 1。要求受理後にPlay停止とWPF終了を重ね、CLIは切断でexit 1、WPFはexit 0。通常のWPF終了もexit 0。

終了時の限定事項: --listen WPFは通常終了を要求したが、UI captureがtimeoutしプロセスが残ったため、対象PID・project・開始時刻を照合してから限定終了した。`WaitForExit=true`、exit -1をraw記録し、正常終了とは扱わない。既知のUnity native終了stall停止規則に従い追調査していない。GUI選択状態は新しい画面証拠を取得せず、fixture/source regressionに依拠する。Unity Editorの無関係なURP Global Settingsと5件のvendor `.pdb.meta`変化は開始状態との一致を確認して復元し、作業treeはclean、設定hash不変を確認した。

独立blind C′: GPT-6.1-terra。Cの所見・HANDOFFを含まないblind guideから同一固定入力を別sessionで取得。archive 201 entries、共通payload 200 filesのfile集合・bytes/hashを確認し、必須33 pathとsource 37 filesを閲覧した。凍結A3、コード、失敗経路、各テスト結果を監査し、GOを妨げる指摘0。追加の同一実機試験や変更は行っていない。

監査できなかった範囲と残存リスク: WPFの正常終了競合を全順序で網羅せず、今回の終了競合は要求受付位置での制御観測。別host、Player、すべての失敗組合せ、GUI選択の新規目視証拠、全source行の独立照合は対象外。Inspectorは凍結どおりTargetId一致のbest-effortで、古い応答または同対象GUI応答を採り得る。実Editorの--listen異常終了は環境リスクとして残すが、C/C′は受入条件・常時契約のblockerとはしなかった。

Evidence取得確認: 判定C/C′のblind共通入力案内 `PR146_FINAL_SHARED_FETCH_GUIDE.json`、C判定結果案内 `PR146_FINAL_C_RESULT_FETCH_GUIDE.json`、C′結果案内 `PR146_CPRIME_RESULT_FETCH_GUIDE.json` をそれぞれ別sessionで照合した。共通入力は全archive/hashと必須内容を確認、C結果はarchiveと13 file、C′結果はarchiveと1 fileのhash・内容を確認済み。対応する確認記録は `PR146_FINAL_SHARED_FETCH_CONFIRMATION.json`、`PR146_FINAL_C_RESULT_FETCH_CONFIRMATION.json`、`PR146_CPRIME_RESULT_FETCH_CONFIRMATION.json`。これらの案内は主担当のprivate artifactsにあり、公開文書・PR本文にはreference、hash、ローカルpathを記載しない。

Phase C/C′まで完了。Phase D、PRへのコードpush、merge、task終了、HARVEST/HANDOFF削除は依頼範囲外であり、別指示を待つ。

## develop更新後の再判定

2026-11-06に `origin/develop` (`cea28ee42070026fa2d9bbec3c154c7b9d24283b`) を `codex/pr146-review-fixes` へrebaseで取り込んだ。競合なし。GitHub PR branchへは未push。新implementation headは `33ec902fd6b9f129e9a6643380058159178f2ac3`。凍結baseとimplementation headが変わったため、上記の旧C/C′所見はこのheadの判定として流用しない。

統合差分の確認: 新headと旧判定head `6e1f82c3a00430b46e0e50f885b6681e52425a15` の間で `tools/DebugStudio/` と `protocol/debugsocket/` のfile内容差分は0。Unity側のFoundation DebugSocket、Runtime DebugSocketServices、Foundation DebugSocket testsにも差分0。新developのDebugStudio関連で必要な追加検証を判定Cで照合する。

新baseでの再検証: Release App test project全262、CLI全113、Contracts全38がすべて成功、failed/skipped 0。今回はApp test project全件を実行したため、旧判定の関連App集合87件とは範囲が異なる。AppのTelemetryStoreCloneTestsでCS8602、ContractsのCorrelationProtocolRoundtripTestsでMsgPack015のbuild warningが出た。`pwsh tools/protocol-codegen/generate.ps1 --check` は56生成物一致。`pwsh tools/contract-audit.ps1` errors/warnings 0（Unity C#差分0）、`pwsh tools/docs-audit.ps1` errors/warnings 0。raw console logとTRXは別Evidence bundleへ収録する。

AC5の実Editor観測は旧headで得た同一DebugStudio/DebugSocket source内容のraw記録を再利用し、最新source archiveとのblob一致を新bundleで検証する。別host、Player、Unity Editor全体の終了成功、GUI選択の新規画面記録は主張しない。Editor終了の異常は前節のとおり残存リスクとして扱う。

新base/headのC′は別sessionがblind bundleを監査し、凍結条件または常時契約に結び付く実装blockerなしとした。判定Cは未完了。A3凍結snapshotはexpiresを2026-11-05または置換revisionと定めており、現headで置換revisionはない。またAC5の実Editor記録はA3指定のRelease構成ではなくDebug構成で、Releaseの引数なし起動記録はAC5の代替にならない。コード差分に重大な指摘がない場合は現headを維持するという依頼者判断を受け、develop取り込み済みheadを維持してPRへ公開する。これは凍結期限の延長、AC5充足、GOまたはPhase D判断を意味しない。判定Cは未完了のまま後続確認へ渡す。

Phase D、PR merge、task終了は未実施。
