# Observation progress repair — 凍結A3 v1

type: slice。status: B実装待ち。risk: high。owner: OSM maintainer。expires: 2026-10-31。harvest: 既存testing/build documentation。通常tracked HANDOFF方式を採用、external-current-v1/H2c限定B許可は採用しない。

2026-10-04。A0の明示ユーザー承認（観測器の保存処理・対応テスト、失敗時の拒否を維持）を具体化する。slice base 4c3b53cbd03cb34fb2448d006a548bbee3b5d451、branch codex/observation-progress-repair。D/merge/push/PRなし。旧BS3 A3 v1と旧H2c A3/CURRENT/rawを変更しない。

## 問いと最低条件

問い: 同期観測保存を短いWindowsファイル共有競合から有界に回復させ、未回復・曖昧な保存を拒否したまま、BS3修復を含む最終headを独立C/C′へ渡せるか。

- O1: Target Unity6000.6.0f1上の実File.Replaceと削除共有なしreaderで競合を決定的に起こす。実例外type/HResult、attempt数、runtime/BCL MVID、elapsedをraw/XMLに残し、retry境界でreaderを解放して同じ完全なtempが公開されること、旧readerが半端なbytesを見ないこと、temp残骸なしを検証。元障害の主体/HResultは未確定と明記。
- O2: 認識したWindows replacementエラーだけ、attempt5回以下・retry開始から100ms以内で再試行。永続ロック・未分類・永久・partial/曖昧状態は例外となり成功へ変換しない。全試行は同じflush済みtempを使い、delete-then-move/copy・成功推測・temp再生成なし。cleanup失敗は主例外を消さない。
- O3: callbackは同期のまま。Progress/SaveRecoveryだけが既存AtomicWriteを使い、WriteNew/Terminal/CreateRecoveryの追加専用、schema/sequence、bootstrap sticky failure、runner error vetoを変えない。新挙動と失敗分岐を決定的テストで確認。
- O4: 新最終headで全EditMode（空filter、ObserveUnity、全leafPassed/skip0/compile0/observation complete/runner0/前後clean）、関連offline3suite、contract/docs audits、独立C/C′を完了。全EditModeにBS3本fixture・7失敗寿命回帰を含め、旧BS3 A3のM1〜M4も同じheadで再確認。旧936件successは履歴のみ。

これらを満たせば終了。未知の外部ロック主体の全環境同定はbuild/test infrastructure後続、H2c正式scope/base/spec調整はH2c再開A revisionへ送る。成功まで同じfullを反復しない。

## 実装設計

既存TestObservationWriter.AtomicWriteを保つ。GUID所有tempにWriteNew/Flush(true)した後、初回にdestの有無を選択する。destなしのMoveは1回だけ。destありのReplaceのみ再試行し、失敗後にMoveへ切替しない。

HRESULTは全文一致で0x80070020（sharing）、0x80070021（lock）、0x80070497（1175 unable to remove replaced）だけ許可し、Windows以外はretryしない。0x80070498/499（1176/1177 partial）、access denied、disk full、unknown、generic IOExceptionは即時失敗。元日本語messageを1175と断定しない。32は.NET/standaloneMono制御実測、33/1175はOS契約に基づく許可で注入テスト。MicrosoftのReplaceFile契約で1175は両pathnameを維持し、1176/1177には部分変更がある: https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilea 。文言照合はしない。

最大5attempt、各retry前に20ms待ち（最大計80ms）、最初のreplace開始からStopwatchで100msのretry開始deadline。firstattemptは必ず1回。各failure後とwait後にdeadlineを確認し、期限を越えたら追加attemptなし。native IO自身の時間を強制停止する契約ではなく、retryによる追加滞留の上限。productionの短い同期waitだけThread.Sleep(20)を許可（AGENTSの禁止はテスト）。testsは注入wait/elapsedで進め、Task.Delay/Thread.Sleep禁止。

retry前に同じtempとdestが存在することを確認。いずれか欠落・temp消費などは再試行せずfail closed。既存契約はinvocation GUID directoryの単一同期writerで、別writerによる内容改ざん検知を追加契約にしない。OSが両pathnameを維持する既知failureと専有tempにより安全を説明する。毎回旧dest全byteを再読/hashして任意の外部改ざんを検出する構成は採らない（1MB超progressを全callbackで余計に読むコストと新しいread競合を避ける）。削除/消費のpartial faultは注入で検証する。

seamはAtomicWriteのinternal overloadに集約し、replace delegate、wait delegate、elapsed reader、必要ならcleanup delete delegateをローカル引数で渡す。productionは実IO/Stopwatch/wait。global mutable hook、公開API、filesystem framework、独立serviceなし。主処理例外をcatchしてtempcleanup後に再throw、cleanupも失敗ならAggregateExceptionで両例外を保持。成功時cleanupだけが失敗しても失敗。全例外を握り潰さない。

## 責務と規模

- TestObservationWriter.cs（現127行、+70〜120予想）: Editor観測JSON保存/復帰に伴う原子的publication、per-call tempとretry時間を所有。既存のSystem.IO/System/Diagnostics/Threading依存だけ。Editor assembly internal、Runtime/API/asmdef不変。50%増警報はpublicationとretry/cleanupが同じ所有temp・同期寿命・変更理由なので非分割。polling serviceや汎用helperを増やさない。
- ObservationAtomicWriteTests.cs（新250〜450行+meta）: 同フォルダ・同asmdefにatomic publication failure回帰を新設する。既存ObservationIoTests.csのJSON/identity/recovery/terminal試験は変更せず保持。内部writer直接テストなのでAssetDatabaseなし。realWindowsfileとinjectedfailureの両方を使う。50%増/新設警報は一つのpublication責務を検証するため非分割。
- docs/handoff/OBSERVATION_PROGRESS_REPAIR.md と BS3_BURST_REPAIR.md: 小さい指示/結果/証拠参照。raw/完全diff/planning/所見はGit外。

製品Runtime、asmdef、公開API、Unityassets/YAML、bootstrap/callback/runner/schemaを編集しない。#nullable enable、record禁止、Game→Framework維持。新seamは既存friend assembly内internal。

## テスト経路

新tests: realreader release→recover、realpersistentreader→exhaustion、delete-sharingreader oldbytes/newpath、許可3code/拒否code、deadlinebefore/afterwait、temp消費/dest消失、cleanupとpublication両fault、normalMove/Replaceの完全文字列。Progress/SaveRecoveryが同じ保存primitiveを使うことは既存roundtripとfinalfullのreloadで確認。stickyfailureとrunner vetoは既存Bootstrap catchの無変更検査と既存offline負例で検証し、productionのBootstrapにfault injectionを増やさない。

担当Cが起点filter OneStarMaker.Tests.Editor.TestObservation を標準pwsh tools/run-tests.ps1 -Platform EditMode -ObserveUnityで実行。実Unity上のheldreader試験は初回C限定runまで未確認。認識できないHResult/異なるpartial動作なら成功扱いせず原因をC-ownedboundedspikeで調べ、契約変更はAへ戻す。最終は空filter1process全EditMode、除外なし。SAMPLEGAME_CONTENT__RUNTIMEMODE=addressablesをprocess内で設定/復元。初回からsandbox外、既存Editor/Burst確認、live progress/sidecarを開かずunity.logのみ。BはUnitytest/buildなし。

offline必須: pwsh tools/Harness/tests/UnityObservation.Tests.ps1、UnityTestRunner.Tests.ps1、UnityGate.Tests.ps1（全case）、contract-audit.ps1、docs-audit.ps1。H2c CURRENTへadoptしない。H2c用全offline suiteは正式再開時に別途。

## 独立性・固定入力

B gpt-6-sol新規、C gpt-6.1-sol新規、C′gpt-6-astra新規blind。gpt-5.6-solは未使用維持。A2 architecture/failureを同じA1で別model新規へ依頼、A0-only代替も別途。C′に他所見・可変HANDOFF・CURRENT・list_agents結果を渡さない。

全implementation差分は7983e6884a01e01c335e92d9045dd2b34bea4d06..finalheadで固定。slice4c3b53c..finalheadも明示。旧BS3凍結A3 v1と本slice A3（承認済みobserver別slice依存とBS3再確認を明記）、所見なしB結果、完全diff/source、最終raw、判定前機械検査を共通bundleに保存。今回のimplementation commit前にBS3 HANDOFFを所見なし凍結A/Bのcarry-forward文書へ前向きに変更する。旧review記録は4c3b53cとGit外原rawに不変保全。したがって7983e68..finalheadの完全diffは全pathを含んだままblindに渡せる。履歴改変・diff除外・所見のredactionはしない。今回のC/C′結果は判定後のreview-recordにのみ追記し、固定implementation headを更新しない。別worktreeからrawとhashを実際に読ませる。同OpenAI/GPT強化条件未達は記録。

## A2採否と実装への確定事項

A1 v1 SHA256 f46cc8c6c6b1946c3eb8e9aee992479ab65a7652dd8e5544d46564436ac5f999。A2 architecture=gpt-6.1-sol新規、failure=gpt-6-luna新規（同じA1、相互所見なし）。追加A0-only alternative=gpt-6-luna別新規。主担当rootが明示承認済み範囲内で統合する。

採用: retryをReplaceのIOException catchだけに限定。WriteNew/Move/wait/cleanupの例外は同じHRESULTでもretryしない。deadline起点は最初のReplace開始直前、追加attemptはelapsed <100msのみ。単調時計を使う。actualreaderはfirstReplace失敗を記録した後のwait seamで解放。全realhandleはfinallyで終了する。real試験のTestContext出力がXMLに含まれることをCが確認し、BCL MVID/HResult/attempt/elapsedを直接読めるようにする。

採用: publication+cleanupの例外は同一インスタンスまたは識別sentinelを共に検証。tempが残った事実も隠さない。publication成功後のcleanupのみの失敗も例外として保つ。known/unknown/partialコード分類、deadline前後、5回上限、temp消費/dest欠落を個別に決定的検証する。100msは追加attempt開始のdeadlineでありnativeIOの強制停止ではない。

不採用: 任意外部writerによる同path内容改ざんの全bytehash検出はこのsliceのsinglewriter契約を越える。専有temp・文書化されたknownerrorのpathname維持・欠落拒否を使う。原actor同定や累積main-threadstall運用計測はbuild/test infrastructure後続。original1175再現とは主張しない。

BS3 carry-forwardの採用: 旧A3 v1自体は変更せず、そこで対象外だったobserverを今回のユーザー承認済み別sliceで修復する。両者を含む全diffを同一headでレビューしM1〜M4/O1〜O4を判定する。このdependency追加は旧headの合格証拠流用を許さない。最終BS3 HANDOFFへ旧v1と本v1の関係を記載する。

Bは上記2つのC#と新meta以外に実装を広げない。新しい製品状態/API/asmdef/所有契約・拒否緩和が必要なら停止してAへ返す。最終のA3と中立B snapshot/rawは同一host別worktreeでhash照合しC/C′が直接読む。両者の所見はDまで相互に渡さず、Dは今回実施しない。
