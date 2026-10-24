# DebugStudio S2 — 読取り用途に絞ったA3 r3

## 0. 状態と入力

- type: slice / status: A3 r3凍結済み・Phase B実装完了。次は動作確認担当から発見Cへ進む。
- risk: high（App寿命の待受け・終了経路を変更するため）。A0独立代替検討とA2構造レビューを実施。
- owner: 主担当。最終判断は依頼者。
- created: 2026-10-21 / expires: 2026-11-05または置換revision。
- updated: 2026-10-25。候補r3を承認済み。Bの未公開実装履歴を整理してGO候補headを固定した。過去の候補時点の記述は経緯であり、現在の許可範囲は本節を正とする。
- branch: codex/debugstudio-s2-final（検証用checkout）。元checkoutはcodex/debugstudio-s2-verification。
- implementation base: 2734a6ea7bc844861feb5acdfd8df213eb3ec3ef / head: e7ea37ab841e2c328fcbab17db6e718e5888ff38。
- 入力: ClaudeのDEBUGSTUDIO_S2_CUT_PLAN.mdと、依頼者の「Debug用途のToolで厳密なトランザクションを要求していない」という今回の指示。
- CUTが参照するDECISION_PACKは指定worktreeでは見つからなかった。D番号を独立した承認根拠にせず、CUTの具体的な用途と今回の指示から候補を作る。
- 承認: 依頼者「凍結してよい、PhaseC/C’までサブエージェント使いながら君の裁量で完遂して」。r3の採否案、保証緩和、受入境界、Unity製品変更を行わない場合の全EditMode回帰除外を承認として記録。実装・必要なcommit・Editor操作・.NET検証・証拠保存を含む。Phase Dのmerge/task終了は今回の出口に含めない。
- harvest: 実装後のCLI/WPF起動方法と制約をtools/DebugStudioのREADMEへ統合。計画は保存引渡し後に置換・整理。

旧A3や再編案、固定packetは変更しない。それらの強い保証を本スライスの必須条件へ持ち込まない。書込み時の世代識別（旧D9）はS3で改めて判断し、S2で導入も撤回もしない。

### 引継ぎ票

- 次のPhase/役割: C担当。修正後の固定入力を再確認し、blocker解消後に同じheadで判定必須検証を行う。主担当は統合・証拠配送を担当し、C′は判定Cの必須証拠が揃ってから別モデル新規担当へ渡す。
- 凍結revision: A3 r3、凍結commit e4910a392be1a1cb8cd1f617c127d93bf511aa18。実装引継ぎcommit e7ea37ab841e2c328fcbab17db6e718e5888ff38。本節だけの状態記録commitをimplementation headへ含めない。Cは検証用checkoutのHEAD・clean状態と照合する。
- Phase A snapshot: ローカル固定済み。取得案内は主担当のprivate artifacts内。B別sessionが独立した期待hashでfetchし、manifestと必須2文書の内容hash・閲覧を確認済み。
- Phase B resultと発見C初回入力: Artifact Storageでローカル固定済み。新規C担当によるfetch、manifest全59項目hashと必須内容の閲覧を確認済み。B修正結果と新headの発見C入力は主担当がprivate取得案内で渡す。判定C evidence / C′ blind bundleは未生成。CとC′の判定入力は同じbase/head、生証拠から作り、C所見はblind入力へ混ぜない。
- 必要な権限: ローカル.NET実行、対象を限定したEditor操作、WPF起動/終了、Artifact Storage stage/fetch。実行環境の拒否は迂回しない。
- 停止規則: 凍結済みI1〜I4/E1〜E3を満たすまでBと発見Cを最大4往復、C′差戻しを最大3往復。新しい契約/所有者/寿命が必要ならAへ戻して分類する。現在の問いを阻害しない追加保証はS3以後へ送る。
- 決定記録: 現時点でなし。条件を変えない裁定のみ別recordへ残す。

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


## Phase B 実装結果

担当: GPT-6.1-sol（統合担当と同系列の実装helper 2件）。凍結r3の範囲で、単一AutoListen、明示Stop、idle acceptの継続、WPFの一回の非同期解放、既存retained hierarchyと要求寿命inspect、DebugStudio-only DTO/CLI、control/clientの有限期限を実装した。既存send/compare契約を維持する。CLI/WPFの使い方とbest-effort制約は既存の`tools/DebugStudio/src/DebugStudio.Cli/README.md`へ統合した。

B適応: 同じpending acceptをidle期限後も継続し、control listenerでも未完了acceptの取り残しを避ける。期限テストは既存責務の内部delegateまたはTimeProviderで進める。Window所有のOrderedAsyncDisposableとVM解放は各失敗を診断して残りを試み、一回の解放に集約した。AutoListenはsessionをDisposeしない。新しい世代・requestId伝播・pending辞書・Unity製品変更はない。Unity生成出力の内容差分は0。

.NET Release確認: 関連App/loop/read/control/transport/相関とshell回帰84件、CLI全112件、Contracts全38件が成功（各failed=0/skipped=0）。codegen --checkは56生成物一致。contract-auditとdocs-auditはerrors=0/warnings=0。既存テスト由来のnullable/MessagePack analyzer警告が残る。CLI/Contractsの最初のRelease --no-buildはその構成が未生成のため実行不可で、通常ビルド付き再実行が上記成功結果。生ログ・TRXと正確なコマンドはprivate B resultに保存する。

B出口の軽いWPF観測: 引数なしと--listenの両方でWindow handle取得後にCloseMainWindowを送り、15秒以内にexit 0を観測した。Unityの接続・Play2回・進行中inspectと切断が重なる終了は未実施で、C実行担当へ引き渡す。BからEditorを起動せず、Unity compile・Unityテスト・Build・判定C・C′は未実行。B観測をCの判定証拠へ転用しない。

構造補足: MainWindowはWindow終了という単一責務のまま非同期化した。OrderedAsyncDisposableの一回の例外継続cleanupとMainWindowViewModelの残り解放は、凍結済み所有解除契約を実現する局所変更である。control期限は既存control service内、読取りの中核はCliReadService、App loopはAutoListenServiceへ分離した。
