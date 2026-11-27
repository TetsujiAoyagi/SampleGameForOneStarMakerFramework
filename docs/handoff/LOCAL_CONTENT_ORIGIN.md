# 検証済み content の取得元（Local）— S4-3a

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 を 4 行に分けた最初の行 S4-3a。分け方は §2.2）
- status: `B`（**C'（1 巡目、2026-11-27）が blocker 1 件（CP-1、§8）。B へ戻す。** 判定 C（判断担当、2 回目）は implementation head `34673237` で GO だったが、C' の blocker の修正で implementation head が変わるので、その判定 C と C' の結果は新しい head に流用しない）
- branch: `claude/trusting-ramanujan-kxphtr`（PR の base は `develop`）
- implementation base commit: `9e7d414833456e2c9dd930c5f96432b24d9ee919`（`develop` の head、2026-11-26、S4-V の Phase D の merge。branch に merge した。§1 の現況と §3 の行数は改訂 3 でこの base で取り直した（改訂 1・2 は `07cc2c97`、初版と初回の A2 の固定 base は `1fd200b5`）
- implementation head commit: `346732372c19ee5cdaa8ad9b8d00495e91e6d7a5`
- A3 凍結記録: 承認者 owner（root）、2026-11-27、revision 1（この記録を含む commit）。根拠は owner の会話での指示（revision 1 の凍結と、A2 の観点の再レビュー・目的レビューのやり直しを行わないことの 2 点を「両方 ok、凍結しよ」で承認）。次の担当は Phase B。前の記録: revision 0（改訂 3）は owner が 2026-11-26 に凍結した（commit `5a906078`。根拠は D の裁定の承認）
- risk: `high`（Framework の公開 API に取得元の差込口を足し、cache への書き込みを呼ぶ調停と、Play の入口の候補の並びに触れる。program の行は `normal` だったが、高リスクの例の「公開 API」に当たるので上げた）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「DIST transport と disk cache の境界」（取得元の差込口、取得元の head、digest の出所、取得の調停と止まる規則、取得が古い install を消しうること）と「まだ保証しないこと」、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「配信用 transport の公開境界」（head の登録）と「宣言からの Play 経路」（取得の操作、選んだ base の記録）、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（別の `Library` の base と上書きの答え）、repo の README の「試す」、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 の行と後続へ送る入力
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と base の code だけ）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: Phase B result snapshot は個別に作らない（§6 の Phase B の実装結果と「動作確認」の節を両方の bundle に収録した）。判定 C の evidence bundle と C' blind bundle（1 巡目）はどちらもローカル固定済み（未昇格）で、取得は送信と別の process で確かめた。取得案内は進行役の worktree の git 管理外に置き、evidence bundle の案内は Phase D の担当へ、C' blind bundle の案内は C' の担当へ進行役が渡す。両方の bundle は判定 C の判断担当が読んだのと同じ原記録から、判定 C の後に作った。C' blind bundle の Phase A は凍結した revision 1 の §1〜§5（§5.1 を除く）を手を加えずに収め、§5 の本文にある §6 の節への参照 1 か所もそのまま残した（参照先の節は収めていない）
- 引継ぎ票:
  - 次の Phase・役割: Phase B（C' の CP-1 の修正）。
  - 次の作業: 開始時に引継ぎ票の照合をする。§8 の CP-1 を直す: `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` を implementation base の内容に戻す（base `9e7d4148` からの差分がこの file に残らないこと）。この file は §3 の責務マップに無い。ほかに Library の無い checkout での Unity 起動で書き換わり、意図せず commit された file が diff に無いかを、base..head の name-status で確かめて §6 に記録する。修正後に新しい implementation head を固定し、発見 C へ渡す。新しい head では判定必須の検証（全 EditMode 回帰と E 系列）と判定 C・C'（2 巡目）をやり直す。
  - 読む範囲: 既定（§0〜§5、差し戻しなので §6〜§8 も）。重点は §8 の CP-1 と §3 の責務マップ。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。
  - 必要な権限: `unity/Assets/Settings/` の asset と `docs/handoff/` の編集、git。Unity の起動と資格情報は要らない（asset は git で base の内容に戻す）。
  - 停止規則: §2.7 の停止規則と §4 の差し戻し条件。変更は CP-1 の asset の復元と、同じ原因で混入した file の復元・§6 の記録に限る。製品 code・test・driver を変える必要が出たら止めて owner に聞く。判定 C の後の差し戻しはこれで 4 巡目で、§0 の上限（4 巡）に達する。
  - 引き渡さないもの: C' 2 巡目には、C' 自身の前巡の指摘（CP-1）への人間の裁定だけを決定記録として渡し、発見 C・判定 C の所見は渡さない。
  - 独立性の注記: Phase A に関与した系列は Anthropic（主担当、初回と改訂 3 の A2、Phase C の立場の事前の読み）と OpenAI（外部計画レビュー 1〜4 件目と修正差分の確認）。Phase B は Anthropic。発見 C と判定 C の判断担当は OpenAI（Phase B と違う系列。Phase A の外部計画レビューと同じ系列なので独立性制約あり）。C' 用に xAI の Grok を残す（§5 の C' の予約）。

### 実測の方法

- base の code と文書を読み、`grep` と `wc -l` で確かめた。Unity は起動していない。
- A1〜A3 の初版は base `1fd200b5` で書き、S4-2 の HANDOFF は branch `claude/player-content-overlay` の head `86a04d1`（Phase B と発見 C の往復中）の版を読んだ。改訂 1 で base を `07cc2c97`（S4-2 の merge の後）に取り直し、`1fd200b5..07cc2c97` の差分のうち本書の §1・§3 が挙げる file に当たる物は `PlayEntryCandidates.cs` と `PlayEntryInterceptor.cs` だけだった（§1 の該当行に反映）。S4-2 の結論は、merge した program §8 の S4-2 の行と「S4-2 から移送した入力」を正とした。
- 改訂 1 で、Windows の開発の PC の `pwsh` で `tools/docs-audit.ps1` と `tools/contract-audit.ps1` を通した（結果は PR の本文）。`tools/workflow-task.ps1 start` は凍結の時に行う。
- 改訂 3（やり直し、2026-11-26）で base を `9e7d4148` に取り直した。`git diff --stat 07cc2c97 9e7d4148 -- unity/Assets` は空で、§1・§3 の製品の file の事実と行数（`git show 9e7d4148:<file> | wc -l`）は改訂 2 のとおりだった。変わったのは S4-V の基盤と、それに移った S4-2 の driver で、[Harness README](../../tools/Harness/README.md)「実機の検証の scenario」、`tools/Harness/Scenario.psm1` の公開関数、基盤の offline test の driver の走査（`tools/Harness/Tests/ScenarioTestSupport.ps1` の `Get-DriverScanFindings`）、`tools/s42-player-overlay.ps1` を読んだ。Unity は起動していない。

## 1. 目的と対象外

- 目的: 自分の PC で publish した検証済みの content（目的 build）を、所在・digest・revision を入力せずに、Editor の 1 つの操作で取得元から取得・検証・install して、Play（経路 Built）の base に使えるようにする。同時に、取得元の種類（LAN、cloud）を後から足す時に、検証・install・cache・選択・記録の code を変えずに済む差込口を、network を使わずに確定する。取得できない時と一致しない時は、別の revision や未検証の bytes に黙って切り替えず、取得元ごとの理由を示す。
- 利用者: (a) 自分の PC で content を build・publish し、それを base に Editor で Play する開発者。(b) 取得元の実装を足す host の開発者（次の行で LAN を足す担当と、後で cloud を足す担当を含む）。
- 目的・利用者の改訂の記録（A1、2026-11-25）: A0 の文言は、目的が「検証済みの content（目的 build）を手元に持たない開発者が、取得元の種類・所在・digest を Play のたびに入力しなくても、設定した順の取得元（手元の publish 済みの directory、LAN。後から足す cloud など）から、期待する identity と digest に一致する revision を取得・検証・install して、Editor の Play の base に使えるようにする。取得元を足す・差し替える時に、検証・install・cache・選択・記録の code を変えずに済むようにする。取得できない時と一致しない時は、別の revision や未検証の bytes に黙って切り替えず、理由を示す。」、利用者が「(a) 編集する開発者 (b) 遊ぶだけの人 (c) 取得元を足す人」だった。変更の理由は 2 つ。owner が 2026-11-25 に S4-3 を 4 行に分けることを承認したので、LAN と「遊ぶだけの人」（content を別の PC から受け取る人）を S4-3b へ移した。取得を始めるきっかけ（Play の入口、先読み、hook）を S4-3c にまとめたので、本書の取得は「1 つの操作」にした（§2.2）。利用者の入力は増やしていない。**文言そのものへの owner の承認は得ていない**（owner の「A3 まで進める」の指示で A2 に進んだ）。A3 で owner が確認し、変える時は目的レビューをやり直す（§2.9 の D-1）。A2 と A3 の統合では、この 2 欄を変えていない。
- 対象外（所有は §2.6）:
  - LAN と cloud の取得元の実装、network の失敗（timeout の値、再試行、進捗、同時の取得の合流）、別の PC から受け取る content（S4-3b）。cloud は program §7 の D5 のとおり、差込口が塞がないことの確認までにする。
  - Play ボタンを押した時に取得すること、先読み、checkout hook（S4-3c）。本書の取得は利用者が始める操作で、Play の判定（`PlayEntryDecision`）と transaction は変えない。
  - 「自分の base」を選ぶ規則と、pin を運ぶ経路（S4-3d）。本書は、取得元が勧める 1 件を取ることと、呼び手が pin を渡せる口までを持つ。
  - 要求の時点の install と登録（S4-4）。13 章の「Runtime は network から起動しない」は変えない。Player の中での取得（program §10 の U10）、Android・iOS（S4-5）。
  - CI が content を publish する仕組み（program §10 の U9）、VCS の adapter、観測 UI と menu の整理、Play の record に digest の出所を足すこと（S5）。
  - Delivery の窓と From published directory の動き、installer・cache・receipt・検証の code（`Runtime/BuildContent/Distribution/` の今ある file）。今のまま残す。
  - cache の掃除の入口（`Evict` と known-good の呼び手）、壊れた install を直す操作。配布の単位を変えること（revision 全体の取得のまま）。署名による検証。
- 現況（base `9e7d4148`。初版の `1fd200b5`、改訂 1・2 の `07cc2c97` から取り直した。`07cc2c97..9e7d4148` に `unity/Assets` の差分は無く、製品の file の行は改訂 2 のまま。変わったのは下の Play の入口の行の後半と、S4-V の基盤の行）:

| 事実 | 場所 | 本書に効く点 |
|---|---|---|
| bytes の取得口 `IContentArtifactSource`（相対 path から stream を開く）は public で、実装は `LocalContentArtifactSource` と `HttpContentArtifactSource` の 2 つ。「file が無い」と「読めない」を区別せず、Local は open の時の IO 例外を全て、HTTP は 404 も `TransportFailure` にする。Local の open の後の read の IO 例外は包まれない | `Runtime/BuildContent/Distribution/ContentArtifactSource.cs`（162 行。`:31-34`、`:72-74`） | bytes の差し替えは既にできる。「無い」は取得口の失敗 code では分からない。読んでいる途中の例外は installer が `IoFailure`（到着点の側と同じ code）に包む |
| `ContentInstaller.InstallAsync(request, source, token)` が、`transport.json` を取得口から読み、staging で全 file を検証してから immutable な `installed/<contentSet>/<revision>` を rename で公開する。manifest の検査（`ContentManifestValidation.Validate`）は、protocol・Unity の版・identity・target を、digest の照合より先に見る。既にある revision は、request の digest での完全な再検証だけで再利用し、receipt を書き直さない。install の全体（取得口からの読みを含む）の間、cache の transaction の lock を持ち、2 つ目の操作は待たずに `Busy` になる | 同 `ContentInstaller.cs`（185 行。`:43-128`）、`ContentManifestValidation.cs:131-145`、`ContentCacheStore.AcquireTransaction` | 検証と install は取得口の種類に依らず 1 つの経路。digest の違う manifest が、先に「互換でない」「identity が違う」で落ちうる。再利用した revision の `installedUtc` は古いまま。`await` は `ConfigureAwait(false)` を付けていない |
| installer は、content の file を読む前に容量を確保し（`EnsureCapacityUnsafe` → `MakeRoom`）、budget を超えるなら今回の対象でも known-good でもない古い install を消す。消した後でも足りなければ `BudgetUnsatisfied` で失敗する。最後の file の copy の後の木の検証・receipt・rename の段では取消を見ない | `ContentInstaller.cs:93`、`ContentCacheStore.cs:186-232` | 取得は、成功だけでなく失敗や取消で終わっても古い install を消しうる。rename が終われば取消は効かない |
| install のたびに `MakeRoom` が走り、印の正しい残りの staging と tombstone を掃除し、budget を超える時は、known-good でも今回の対象でもない lease の無い install を古い順に消す。cache の中の無関係な receipt が 1 件壊れていると、全ての install が `InstallConflict` になる。製品の code に `MarkKnownGood` と `Evict` の呼び手は無い | 同 `ContentCacheStore.cs`（438 行。`:184-226`）、`ContentEvictionPolicy.cs` | 取得は、今まで Play が使っていた古い revision を消しうる（Delivery の窓・検証 Play の install と同じ動き）。取得の途中で失われた task の staging は、次の install が片付ける |
| install の記録（`receipt.json`）は version・digest・contentSet・revision・target・時刻だけで、どこから取得したかの欄が無い | 同 `ContentDeliveryFiles.cs`（155 行）、`InstalledRevisionVerifier.cs`（139 行） | 本書は receipt を変えず、取得の出所は host の側の記録に残す（§3） |
| 取得元が持つ revision と、その期待 digest を得る口が無い。Delivery の窓は digest・contentSet・revision・cache root・budget を手で入れる。標準の経路（From published directory）は batch の結果 file `last-result.json` を file dialog で選び、そこから digest を取る。`last-result.json` は batch の開始で消して成功の時に書き直す 1 個だけで、検証 Play の publish は書かない | `SampleGame/DependOnAll/Editor/Build/ContentDeliveryWindow.cs`（212 行）、`VerifyPlayLauncher.ReceiveAsync`、`SampleGameContentBatch.cs:65-68,119` | 差し替えの本体はここ（§2.3）。過去の publish と検証 Play の publish の digest は、bytes と別の場所のどこにも残っていない |
| publish は `ContentTransportPublisher.Publish(result, publishRoot)` が、呼び出し専用の staging に全 bytes と `transport.json` を揃えてから、同じ volume の rename で `publishRoot`（1 revision の directory）を公開する。既にある公開先は更新しない。build の identity は時刻と GUID を含み、同じ identity の再 publish は起きない。batch と検証 Play の公開先は `artifacts/bs2b/transport/<identity>/` | `Editor/Build/Content/ContentTransportPublisher.cs`（227 行）、`SampleGameContentBatch.cs`（202 行。`:105`）、`VerifyPlayLauncher.cs:453-454` | 親の `transport/` が「取得元の root」になる。publisher は親の directory の file を書かない契約である |
| batch は「成功結果の確定より前に失敗すれば、この実行が公開先を消す」。結果 file の確定（`Commit`）の失敗もこの経路に入る | `SampleGameContentBatch.cs:59-60,71-83,119-127` | publish の後に足す段は、確定の失敗で取り消せる必要がある |
| Play ボタンの入口の経路 Built は installed cache だけを見る。宣言（contentSet は宣言名）と target が合う entry を、receipt の `installedUtc` の新しい順に検証し、最初に通った物を使う。検証に使う digest は receipt が持つ値である。cache に無ければ候補 0 で拒否し、回復操作（`VerifyPlayBuild | SwitchToSource`）を示す。既存の test はこの回復の値を完全一致で見る。取得が cache の lock を持つ間に Play を押すと、`Inspect` が `Busy` になり「cache を読めない」で拒否される | `PlayEntryCandidates.cs`（175 行。`Select` は `:43-65`。S4-2 が足した `ResolvePlayerBase` は、遊べる Player の identity と digest で 1 件だけを選び、この並びを使わない）、`PlayEntryDecision.cs:242-246`、`SampleGame/Tests/Editor/Build/PlayEntryDecisionTests.cs:338,462` | 既に install 済みの revision を取得しても、その時刻は新しくならないので、今の規則のままでは Play の base に選ばれない。Player の base の決め方は本書の並びの変更の影響を受けない |
| Play ボタンの入口は、batch と、開いている Scene の名前が `InitTestScene` で始まる Play を素通りする（rule `BatchMode` / `TestRunnerScene` の `PassThrough` で、候補を選ばず設定を補わない）。`run-tests.ps1` は batch で走るので、そこから走る test の `EnterPlayMode` は入口の判定を通らない。実 Editor の Play を `eval` で操作して record を読む経路は S3-4b の判定と S4-2 の判定 C の追加で使われた。Play の record は `SessionState` にあり、Editor の process をまたがない。Enter Play Mode Options が有効で domain reload を無効にしている。入口の RunPurpose と経路は `EditorUserSettings`（`UserSettings/` の下。git 管理外）に残る | `PlayEntryInterceptor.cs:184-196`、`PlayEntryDecision.cs:197-201`、`PlayEntryRecord.Load`、`unity/ProjectSettings/EditorSettings.asset:29-30`（`m_EnterPlayModeOptions: 1`）、`PlayEntrySettings.cs`（36 行）、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」の 1 行目と観測の注意 | 実 Editor の Play を見る検証（§5 の E 系列）は、S4-2 の `[Explicit]` の test と `run-tests.ps1` の組では組めない。窓のある Editor を case ごとに起動し、`eval` で操作する（次の行の基盤の adapter）。Play への移行は domain reload をまたがない。入口の設定は製品の setter を `eval` から reflection で呼んで変える |
| S4-V（2026-11-26 に merge）が実機の検証の基盤を置いた。driver は `Scenario.psm1` だけを import し、case の列・fixture・照合する値・保存する証拠だけを書く。checkout の用意（既定は `<主の worktree の親>\osm-v\<scenario>-<checkout>` で、run をまたいで `Library` ごと使い回す）と事前確認、固定 patch の適用と復元、process の終了の確認、窓のある Editor の adapter（送る `eval` は送り直さず、読む `eval` は読み直す）、run の root と外部 root（`<drive>\ovr\<scenario>\<RunId>`）、証拠と原記録の置き場は基盤が持つ。管理する file（`Set-ScenarioFile`）は ignore される file に限り、`Reset-ScenarioFiles` で書く前に戻る。`Invoke-ScenarioBatch`（`-batchmode -nographics -quit -executeMethod`）は offline test だけで示した拡張点（§9.1 の (b)）で、実の使い方は使う行が確かめる。基盤の offline test は、列に載せた driver の構文木を走査し、git・process の起動・削除・Editor の接続の code を拒む。S4-2 の driver はこの基盤に移り、固定 patch `PlayerOverlayEdits/ac01-title.patch` は変わらない | [Harness README](../../tools/Harness/README.md)「実機の検証の scenario」、`tools/Harness/Scenario.psm1`（837 行。公開関数は `:834-837`）、`tools/Harness/Tests/ScenarioDriver.Tests.ps1`（313 行。走査の列は `:284`）、`tools/Harness/Tests/ScenarioTestSupport.ps1:182-189`、`tools/s42-player-overlay.ps1`（154 行）、program §9.1 | §5 の E 系列の driver は、この基盤の上の scenario だけを書く。検証用の checkout は run をまたいで使い回されるので、git 管理外の `artifacts/` と `UserSettings/` の file（前の run の publish、head、選んだ base の記録、入口の設定）が残る。patch はそのまま使う |
| 要求時解決の供給元（`IContentProvider`）の契約は「候補の有無を答えるだけで、bytes の取得・install・Build はしない」 | `Runtime/ContentResolution/Core/IContentProvider.cs` | Local / LAN を `IContentProvider` として足す形は、この契約と合わない（§2.1） |
| cache root の既定は `LocalApplicationData` の下の `OSM/ContentCache`、disk budget の既定は 10 GiB（どちらも Delivery の窓の設定 `UserSettings/OSMContentDelivery.json`）。cache は local NTFS が要る。target は `StandaloneWindows64-Player` の固定。受け入れる Unity の版は literal | `VerifyPlayLauncher.ResolveCacheRoot`、`ContentDeliveryWindow.Settings`、`ContentManifestValidation` | 取得の操作は検証 Play と同じ設定を読む |
| 一時的な移動の失敗の再試行 `ContentStagingMove` は、directory を、まだ無い final へ移す時だけ移し直す（`ReadyToMove` が staging の directory と final の不在を見る）。一時的な失敗の判定 `IsTransient` と上限の定数は internal | `Editor/Build/Content/ContentStagingMove.cs`（93 行。`:38-40,77,86-87`） | 既にある file の置き換えには、そのままでは使えない |
| R2 への Content の保存と取得は `tools/artifacts.ps1 content collect / fetch` にある。file 単位の object で、取得には `reference` と、それとは別の期待 manifestSha256 が要る。Unity の側からは自動で起動しない。R2 の側に上書きを防ぐ仕組みは無い | [tools/Artifacts/README.md](../../tools/Artifacts/README.md)「開発Content 1件の保存と取得」 | cloud の経路は既に 1 つあり、Unity の取得口とはつながっていない。Spring 221 object の fetch は 742 秒（program §2.1） |
| S4-2（2026-11-25 に merge）は、別の PC の base、base の選び方、CI からの取得、取得した transport の install を入口が代わりに行うこと、CI の Player を取る時の改行の正規化、digest から登録までの窓と cache の lease を S4-3 へ送った。owner は同日、行き先を決めていなかった入力の (1)（入口が `player-compat.json` の記録の `identity` を選んだ Player の identity と比べていない）と (2)（`PlayerOverlayStartupObserver` が log を読んでから process の終了を確かめる間の競合）も S4-3 の入力にした。別の `Library` の base に上書きを重ねた **Player** の答えは「同じ」 | program §8 の S4-2 の行と「S4-2 から移送した入力」、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「別の `Library` の上書きと script の field の差（Player、6000.6.9f1）」 | 振り分けは §2.6。Editor での同じ問いは未測定（S8） |

- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの（誰がいつ） | 目的からの正当化 |
|---|---|---|---|
| U1 content を publish する（batch と検証 Play。今ある操作） | 宣言名（今のまま。増やさない） | 取得元の head の書き換え（publish の成功の後に host が自動で行う。自動導出）。head の置き場所（公開先の親の directory。既定値） | head を書く操作を利用者に足さない（目的の「入力せずに」） |
| U2 取得する | 「Acquire content」のボタンを押すこと（実行時の操作）。対象の宣言は、Play の入口で既に選んである RunPurpose（今ある永続の選択。新しい入力ではない） | 取得元とその順（host の既定値: repo の `artifacts/bs2b/transport`）。取る revision と期待 digest（取得元の head。自動導出）。target・Unity の版・root schema（固定値）。cache root と budget（Delivery の窓の今ある設定。既定値あり）。Play が使う base の記録（自動導出） | 目的の「1 つの操作」。押すこと以外の入力は無い |
| U3 取得を取り消す | 「Cancel」を押すこと（実行時の操作） | 半端な install の片付け（installer の今ある動き） | 目的の「未検証の bytes に切り替えない」の裏側。待ちを利用者が止められる |
| U4 結果と理由を読む | なし（窓と Console を読むだけ） | 結果の文（何をどの取得元から得たか、取得元ごとの理由、digest の出所、Play がどの revision を使うか、消えた古い install） | 目的の「取得元ごとの理由を示す」「Play の base に使えるようにする」 |
| U5 取得元の場所を変える（任意。既定で足りない人だけ） | directory の path 1 つ（開発時の宣言。`UserSettings` に残る） | 取得元の id と信頼の既定（host の既定値） | 既定の場所以外に publish する人のため。使わない人には入力が無い |
| U6 取得元の実装を足す（利用者 (b)） | `IContentOrigin` の実装と、host の取得元の列への追加（開発時の宣言） | 検証・install・cache・選択・記録（共通の側。変えない） | 目的の「code を変えずに済む差込口」。足す人が書くのは「何を勧めるか・持っているか」と bytes だけ |

自己点検: 目的の「所在・digest・revision を入力せずに」に対して、U2 に残る入力はボタンの押下だけで、所在は既定値（U5 は任意）、digest と revision は自動導出である。目的の「Play の base に使えるようにする」は、取得した revision が既に install 済みで最新の install でない時にも成り立つよう、選んだ base の記録で満たす（§2.4 の S7）。

## 2. 意思決定と受け入れ境界

### 2.1 用語

「供給元」は既に `IContentProvider` を指す語なので、本書が扱う物に同じ語を使わない。

- **供給元（既存）:** `IContentProvider`。Play の間、要求に対して候補の有無を答える。取得しない。
- **取得元（本書が足す。code の名前は `ContentOrigin`）:** 検証する前の bytes を持つ場所。手元の publish 済みの directory、LAN の共有や HTTP の server、cloud の object storage。
- **到着点（既存）:** installed cache。どの取得元から来た revision も、検証を通ればここに同じ形で入る。installed cache は取得元ではない。
- **head:** 取得元が、ある contentSet と target について勧める 1 件（revision と、申告の digest）。
- **pin と申告:** 期待 digest の出所の 2 種類。pin は bytes と別の経路で得た revision と digest の組（VCS の file、CI の結果 file、人の入力）。申告は取得元の head に書いてあった digest。
- **対象の組:** 1 回の取得が取ると決めた (revision, digest)。pin か、取得元の head から 1 回だけ決め、以後変えない。

流れは「対象の組を決める → 到着点に有るか → 無ければ bytes を取得して検証し、到着点へ install → 選んだ base を記録 → Play で登録 → 供給元が要求に答える」になる。取得元の順（Play の前）と、供給元の順（`ResolutionPolicy`、Play の間）は別の規則で、混ぜない（program §3.1 の 8）。program が候補に挙げた HANDOFF 名 `LOCAL_LAN_PROVIDER` は、PROVIDER が `IContentProvider` と紛れるので使わない。

### 2.2 行の分け方（owner が 2026-11-25 に承認した）

切り口は transport の種類ではなく、何を証明するかに置く。bytes の取得口は Local と LAN で既に同じで（§1 の現況の 1 行目）、独立して失敗しうる未知が別々にあるからである。

| 行 | 答える問い | gate の主な中身 |
|---|---|---|
| S4-3a（Local。本書） | 取得元の差込口（head と期待 digest、bytes）を決め、手元の publish 済みの directory からの取得・検証・install を、network なしで Play の base になる所まで通せるか。取得元を 1 つ足しても、検証・install・cache・選択・記録の code が変わらないか | §2.4 |
| S4-3b（LAN） | 同じ差込口に LAN の取得元を足して、Local miss → LAN hit を、network の失敗と、取得元を信用しない前提の下で通せるか | program §9 の G09・G13、LAN timeout、切断、遅い転送の間の取消と cache の lock、進捗、同時の取得の合流。別の PC で build した base に手元の上書きを重ねた時の答え（Editor） |
| S4-3c（取得を始めるきっかけ） | Play を押した時の取得、先読み、checkout hook が呼ぶ公開入口（U7）を、取得元の種類に依らずに持てるか | program §4.2 の表の S4-3 の行。Play の transaction に非同期の段を足す時の再入・domain reload。cache の掃除 |
| S4-3d（base の選び方） | 「自分の base」をどの規則で選び、pin をどの経路で運ぶか（program §5.3 の未詰め (2)） | CI の publish（U9）と VCS（S5）に依る |

計測だけの項目（G19 の段ごとの所要、Built の Play の繰り返しの再検証、streaming の cold load）は、取得元が出来た後の調査記録にする。

本書が「取得の中身（何を、どう確かめて取るか）」を持ち、S4-3c が「取得を始めるきっかけ」を持つ。Play の transaction（`PlayEntryTransaction`）は 1 つの同期の呼び出しの中で判定から再 Play までを行う作りで、取得（非同期で長い）を中に入れるには状態機械に段を足す必要がある。これは先読み・hook と同じ「いつ取得するか」の変更なので、S4-3c にまとめた。

### 2.3 取得元の差し替えの境界

差し替えの口は 3 つあり、足りないのは 1 つだけである。

| 口 | 今 | 本書での扱い |
|---|---|---|
| 供給元 `IContentProvider` | ある。取得しない契約 | **変えない。** Local・LAN・cloud をここに足さない。install の後は、どの取得元から来ても同じ「検証済みの Content Directory」（由来 `ContentDirectory`、受け入れ根拠 `VerifiedDigest`）で、`ContentSourceKind` に `Lan` や `Cloud` を足すと、network と配信形式が lifetime の側に漏れる（program §6 の I3） |
| bytes の取得口 `IContentArtifactSource` | ある。public。実装 2 つ | **変えない。** 進捗、再試行、認証を足す口、HTTP の期限と redirect は S4-3b が決める |
| 「何を勧めるか・持っているか」の口 | **無い** | **足す**（`IContentOrigin`。§3） |

**取得元に答えさせる物と、共通に残す物:**

- 取得元が答えるのは 2 つだけ。照会（contentSet と target について、revision を指定しなければ head を、指定すればその revision を持つかを、`Found` / `NotFound` / `Unavailable` で答える）と、見つけた 1 件の bytes を開くこと（今の `IContentArtifactSource` を返す）。「無い」は例外ではなく照会の答えで返す。
- 共通に残し、取得元に実装させない物: 対象の組を決めること、期待 digest の照合、互換の判定、install（staging から immutable の公開まで）、cache・budget・lock、取得元を当たる順と止まる規則、取消と例外の受け止め、記録。13 章の「HTTP と local directory は同じ validation/install 経路を使う」を、取得元が増えても保つ。
- 取得元を信用しない。照会の答えも bytes も、検証を通るまで使用対象にしない（program §6 の I2）。取得元の種類を理由に検証を省かない。取得元が取消に応じなくても、例外を投げても、共通の側が受け止める。共通の側は取得元の型・id で分岐しない。
- 期待 digest の出所（pin か申告か）を、取得の結果と、選んだ base の記録に残す。`ContentLoadRecord` の由来（`ContentSourceKind`）と受け入れ根拠（`ContentAcceptanceBasis`）は変えない。
- 差込口は `UnityEditor` に依存させない（S4-4 が同じ口を要求の時点で使う余地を残す）。

**期待 digest の出所の規則（owner が 2026-11-25 に A0 の推奨を採った）:**

- installer が必ず期待 digest を受け取る契約は変えない。
- 取得元ごとに、host が「申告を認める」か「pin だけ」かを決めて渡す。Framework の既定は「pin だけ」。SampleGame の手元の publish 済みの directory は「申告を認める」（書いた本人の同じ disk で、別の経路を求めても保証が増えない）。S4-3b の LAN の既定は「申告を認め、記録に残す」、cloud は「pin だけ」（最終の決定は cloud の行）。Player は今のまま（I10）。
- pin があれば申告は使わない。pin の revision を取得元に照会し、bytes を pin の digest で検証する。取得元の申告の digest は比べもしない。
- 申告を認める時の手当て: head は 1 つの小さな file で、bytes を全て置いた後に書く。到着点に同じ revision が違う digest で入っている時は、取得を拒む。
- 払う物: 申告だけの取得元は、そこに書ける人による差し替えを検出しない（記録には出るが止めない）。

**cloud（R2 など）から逆算した条件と、本書の設計での満たし方:**

| 条件 | 満たし方 |
|---|---|
| 1. directory の列挙に依らない | 照会は、名前の決まった 1 つの file（head）を読むか、revision の決まった所在を開くだけで答えられる。製品の directory の取得元も列挙を使わない |
| 2. 資格情報を OSM の Runtime に入れない | 取得元の実装は host が組み立てて渡す。差込口に資格情報の引数は無い |
| 3. 遅延・部分的な失敗・切断を契約に持つ | 照会と bytes は非同期で、取消の token を受ける。取得元が応じなくても共通の側が打ち切る。読んでいる途中の例外は「届かない」として次の取得元へ進む。timeout の値、進捗、同時の取得の合流は S4-3b（進捗は共通の側の installer に足すので、差込口は変わらない） |
| 4. 取得元の側の不変を当てにしない | 期待 digest の照合と、到着点の「同じ revision の違う digest を拒む」だけに置く |
| 5. file 単位の取得の形を崩さない | bytes は今の相対 path の取得口のまま |
| 6. 取得元ごとの鍵の形を共通の側が解釈しない | 見つけた 1 件の所在は不透明な文字列で、開くのは取得元だけ |
| 7. 遅い取得元を最後に置ける | 取得元を当たる順は host が渡す列の順 |

**「差し替えられる」の確かめ方（D5 の「差込口だけ確認」の中身）:** 調停の test の表の全体を、3 種類の取得元で同じように走らせ、結果（状態、install の結果、試行の記録）が同じであること。3 種類は、製品の directory の取得元、directory でも HTTP でもない memory の取得元（file system を使わず、答える時点を signal で制御できる）、directory の取得元を別の型と別の id で包んだ物（型や id での分岐を検出する）。振る舞いの悪い取得元（照会が例外を投げる、head と bytes が食い違う、途中で切れる、取消に応じない）に対して、共通の側が §2.4 の不変条件を保つこと。

### 2.4 このスライスの問いと、進める最低条件

- このスライスが答える問い: 取得元の差込口（head と期待 digest、bytes）を決め、手元の publish 済みの directory からの取得・検証・install を、network なしで Play（経路 Built）の base になる所まで通せるか。取得元を足しても共通の側が変わらないと言えるか。

#### 不変条件（起きてはいけないこと）

| ID | 守る主張 | 主張を崩しうる既存の経路と、探索した境界 |
|---|---|---|
| N1 | 期待 digest と互換の検証を通っていない bytes が、到着点（`installed/`）に現れない。結果を「取消」とした取得が、後から install を公開しない（installer が取消を見る最後の点（最後の file の、取得元からの最後の読みが戻る時）より後に来た取消は、installer が見ないので「取得した」で終わる） | 到着点へ書く経路は `ContentInstaller.InstallCore` の rename だけ（`Runtime/BuildContent/Distribution/` の `Directory.Move` を `grep`: installer と、削除の tombstone）。調停が自分で cache に書く経路を作ると崩れる。調停が先に読んだ manifest を install の根拠に使い回すと、読んだ後の差し替えを見落とす（installer は自分で読み直す）。取得元が取消に応じない時、調停だけが先に戻ると、installer の task が後から rename しうる。未確認: `tools/` の下に `installed/` へ直接書く code があるかは見ていない |
| N2 | 1 回の取得が、最初に決めた対象の組と違う revision・digest の物を結果にしない（pin と違う物、head と bytes が食い違う物、到着点の同じ revision と digest が違う物、失敗の後の別の revision） | installer の manifest の検査は digest の照合を最後に行うので、digest の違う manifest が「互換でない」「identity が違う」に化ける（§1 の現況）。失敗の code だけで次の動きを決めると、整合性の失敗を互換の不一致と読み違える。installer の「既にある revision の再利用」は request の digest で再検証するので、違う digest は `IntegrityMismatch` になるが、取得元の不整合と同じ code である |
| N3 | 取得の失敗・取消・例外が、半端な install、壊れた head、持ったままの cache の lock を残さない | installer の staging の片付け（失敗しても元の例外を保ち、残った path を例外に付ける）。installer は取得口の読みを待つ間 cache の lock を持つので、取得元が取消に応じないと lock が残る。head の書き換えの途中の失敗。取得元が取消に応じない時、guard が打ち切った読みが後で終わると、installer の buffer に書きうる・遅れて開いた stream が残りうる・遅れた例外が観測されないまま残りうる。取得元の同期の `OpenArtifacts` と `Dispose` は guard が打ち切れない。取得の間の script の compile（domain reload）で task が失われた時の staging は、次の install の `MakeRoom` が片付ける（印を書く前の staging だけは片付かない）。未確認: domain reload の時に lock の file の handle がいつ離れるか |
| N4 | 取得元の種類が、要求時解決・lifetime・起動に現れない | `Runtime/ContentResolution/`、`Runtime/AssetManagement/`、`Runtime/SceneSystem/`、`Runtime/Bootstrap/` に差分が無いこと。`ContentSourceKind` と `ContentAcceptanceBasis` に値を足さないこと |
| N5 | 取得を使わない利用者と Player の動きが変わらない（program §6 の I8・I10）。例外は publish の batch で、head の書き込みだけが失敗した時は、publish と結果 file を残したまま終了コード 6 で終わる（AC7。取得を使わない CI などの batch も 0 でない終了コードを見る。§2.9 の D-5 の 6） | installer・cache・receipt・検証・bytes の取得口の code を変えると、Delivery の窓・検証 Play・Player の起動が通る経路が変わる。本書はこれらの file を変えない。入口は Play のたびに選んだ base の記録を読むので、記録の file が壊れている・読めない時に例外が漏れると、取得を使わない利用者の Play も壊れる（AC6） |
| N6 | Play の入口の判定の規則と、利用者の明示設定を変えない。入口の候補の並びは、取得で選んだ base が、選んだ時刻より後に新しく install された revision が無い限り先頭になることだけが変わる（既にある revision の再利用は receipt の時刻を変えないので「後の install」に数えない） | `PlayEntryDecision` の行と回復の値（test が完全一致で見る）、`PlayEntryTransaction`、ledger（`ContentDeliveryPlayBridge`）。取得の操作が Play の間・compile の間に走ると、Play が見る cache が途中で変わる。選んだ base の記録が古いまま残ると、利用者が後から Delivery の窓で入れた revision より先になりうる。Delivery の窓と検証 Play が既にある revision を再利用しても receipt の時刻は変わらない |
| N7 | 共通の側が取得元の型・id で分岐しない。差込口が `UnityEditor` と資格情報を要らない | 調停に「directory の取得元なら」の分岐が入ると、次の取得元で共通の側を直すことになる |

#### 成立条件（起きなければならないこと）

| ID | 条件 |
|---|---|
| S1 | batch の全ての段が成功する（終了コード 0）と、取得元の head（その contentSet と target）が今回の publish の revision と、公開した `transport.json` の digest を指す。batch が head の段より前に失敗した時、head は今回の publish を指さない（前の値のまま、または無いまま）。head の書き込みだけが失敗した時は、publish と結果 file を残し、結果 file の欄と終了コード 6 で head の失敗を示し、head は前の値のまま（または無いまま）。検証 Play は、publish と宣言の照合が通ると head を書き、書けなかった時は Play を止めずに Warning を出す（head は前の値のまま） |
| S2 | 取得の操作で、到着点に無い revision が取得元から取得・検証・install され、結果と、選んだ base の記録に、対象の組・対象を決めた取得元・bytes を得た取得元・digest の出所（申告）が残る |
| S3 | 対象の組が既に到着点にあれば、取得元の bytes の取得口を 1 回も開かずに、再検証だけで「install 済み」と答える |
| S4 | 取得元が複数ある時、先の取得元が「答えない」「持っていない」「bytes に届かない」「bytes が対象の digest と違う」なら、理由を残して次の取得元へ進み、同じ対象の組をそこから取得する |
| S5 | pin を渡した時、pin の組だけが取得され、取得元の head と申告は使われない（head が別の revision を指していても、申告の digest が pin と違っていても、bytes が pin と一致すれば取得される） |
| S6 | §2.3 の 3 種類の取得元で、調停の test の表が同じ結果になる |
| S7 | 取得が「取得した」か「install 済み」で終わった後、選んだ時刻より後に新しく install された revision が無ければ、実 Editor の Play（経路 Built）は取得の対象の revision を base にする。取得の結果は、取得の成否と、Play が使う revision（とその理由）を分けて書く。取得した revision が Play に使えない時（宣言と合わない、選んだ base の記録を保存できなかった等）は、結果の文と Warning がそう言う |
| S8 | 別の checkout（別の `Library`）で publish した base を取得元から取得し、経路 Built + local edits で手元の上書きを重ねた実 Editor の Play の答えが記録される |

#### 取得の段と止まる規則（N2 と S4 の境目）

対象の組は 1 回だけ決め、以後、別の revision へは進まない。失敗の振り分けは、installer の失敗 code だけでは決めず、どの段で起きたかと、失敗の証拠がどちらの側から来たかで決める（改訂 3、A2 の IV3-2）。installer の同じ code は両方の側から出る（`InvalidManifest` は壊れた `control/known-good.json` の読み（容量の確保の中）や、再利用の経路の壊れた receipt からも出る。`MissingFile` は staging の木の検証だけが出し、取得元に file が無い時は `TransportFailure` になる）。取得元の側と読む証拠は次の 2 つだけで、ほかは到着点の側とする。(i) guard が取得元の open・read の失敗を観測した（guard はそれを `ContentDeliveryException(TransportFailure)` にして installer へ返す）。(ii) guard が installer へ渡した bytes そのものが対象と食い違った。guard は渡した bytes から自分で確かめる: `transport.json` の bytes の SHA-256 が対象の digest と違う、`transport.json` の bytes が `ContentManifestValidation.MaxManifestBytes` を超えた、content の file の bytes の大きさか SHA-256 が、調停が先に読んだ manifest の値と違う。hash の違いと「短い」は、その file の EOF まで渡した時だけ確定し、installer が出力の側の失敗などで途中で読むのを止めた時の部分の hash・大きさは取得元の不一致に数えない。「長い」は、渡した bytes が manifest の大きさ（または上限）を超えた時点で確定する（改訂 3 の外部計画レビュー XR-1 と、修正差分の確認の残件 2。installer の hash の照合は staging に書いた後の木の検証で、staging の側の異常でも同じ `IntegrityMismatch` が出るので、installer の code は証拠にしない）。調停は manifest を先に読み、digest を照合してから欄を解釈する（`ContentPurposeIndex` の読みの順と同じ）。取得元の答えの entry の contentSet・target が照会と違えば、その取得元は「答えない」として扱う。

| 段 | 起きたこと | 次にすること |
|---|---|---|
| 1 対象を決める | pin がある | pin の組を対象にする（出所は pin）。取得元の head は読まない |
| 1 | pin が無い | 「申告を認める」取得元を順に照会する（revision を指定しない）。最初に digest つきで `Found` と答えた head を対象にする（出所は申告）。`NotFound`・`Unavailable`・例外・digest の無い答え・照会と違う contentSet か target の entry は、理由を残して次へ。「pin だけ」の取得元は「pin が要る」と残して飛ばす |
| 1 | 対象が決まらない | 「見つからない」で終わる（全ての取得元が「pin だけ」の時もここ） |
| 2 到着点を見る | cache を読めない（`Inspect` が `Busy` などで失敗する） | 「到着点の失敗」で終わる |
| 2 | 対象の revision が到着点に無い（有無は cache の `RevisionRoot` の path で見る。Windows では大小文字だけが違う directory も在ると読まれ、installer の再利用の経路と同じになる） | 段 3 へ |
| 2 | 在って、receipt の digest が対象と同じで、再検証が通る | 「install 済み」で終わる（bytes の取得口は開かない） |
| 2 | 在って、receipt の digest が対象と違う | 「到着点の同じ revision が違う digest」で終わる |
| 2 | 在るが、receipt を読めない・壊れている（`Inspect` の壊れた path に入る）、または digest は同じだが再検証が通らない | 「到着点の失敗」で終わる（直す操作は対象外。理由に cache の側と書く） |
| 3 取得元から取る（host の順に全ての取得元） | 照会が `NotFound`・`Unavailable`・例外・照会と違う entry（対象を決めた取得元は照会し直さない） | 理由を残して次の取得元へ |
| 3 | `transport.json` に届かない、読んでいる途中で切れた・例外（証拠 (i)） | 理由を残して次の取得元へ |
| 3 | 調停の先の読みで、`transport.json` の bytes の digest が対象と違う、または bytes が上限を超える | 整合性の失敗として残し、次の取得元へ（同じ対象の組だけを探す） |
| 3 | digest は対象と同じだが、manifest が壊れている、identity・target が照会と違う、互換でない（protocol、Unity の版、root schema）、対象の大きさが budget を超える | 前の 4 つは「対象が使えない」で終わる（同じ bytes なので、他の取得元でも同じ。別の revision へは進まない）。budget は「到着点の失敗」で終わる |
| 3 | installer が失敗を返し、cache を読み直すと対象の revision が到着点に在る（段 2 の後に別の操作が同じ revision を入れた。code に依らない） | 到着点の側として扱い、段 2 の規則で終わる（取得元の不整合に数えない） |
| 3 | installer が失敗を返し、対象が到着点に無く、証拠 (i) か (ii) がある | 「届かない」か整合性の失敗として残し、次の取得元へ |
| 3 | installer が失敗を返し、対象が到着点に無く、証拠 (i) も (ii) も無い（`Busy`、`LockUnavailable`、`BudgetUnsatisfied`、`InstallConflict`、`IoFailure`、`MissingFile`、guard が渡した bytes が manifest と全て同じなのに出た `IntegrityMismatch`（staging の側の異常）、guard が渡した bytes が対象と同じなのに出た `InvalidManifest`・`IdentityMismatch`・`TargetMismatch`・`CompatibilityMismatch`） | 「到着点の失敗」で終わる（取得元を替えても直らない） |
| 3 | installer が成功を返し、再利用（`ContentInstallResult` の再利用の印）でない | 「取得した」で終わる |
| 3 | installer が成功を返し、再利用である（段 2 の後に別の操作が同じ revision を入れ、installer が完全な再検証だけで返した） | 「install 済み」で終わる。bytes を得た取得元は空で、試行の記録に `transport.json` を読んだ取得元を残す（自分が公開したとは書かない） |
| 3 | 取得元が尽きた | 整合性の失敗が 1 件でもあれば「整合性の失敗」、無ければ「見つからない」で終わる |
| どの段でも | 呼び手の token の取消（取得元が自分で投げた `OperationCanceledException` は取消に数えず、証拠 (i) とする） | 「取消」で終わる。取得元が応じなくても待たずに打ち切り、installer の片付けと lock の解放を待ってから戻る。installer が取消を見る最後の点（最後の file の、取得元からの最後の読みが installer に戻る時）より後の取消は installer が見ず、installer は木の検証・receipt・rename を終えて成功を返す。その時は token を見て取消に変えない（結果は「取得した」） |

合格例: 取得元 1 の R1 の content の file が壊れていて、取得元 2 に同じ digest の R1 があり、そこから取得する。合格例: cache の `known-good.json` が壊れていて installer が `InvalidManifest` を返す。guard が渡した `transport.json` は対象と同じ bytes なので「到着点の失敗」で終わり、他の取得元を当たらない。不合格例: 取得元 1 の head の R1 が取れなかったので、古い R0 を取得する。不合格例: head の R1 の `transport.json` が別の bytes（Unity の版の欄も違う）に書き換わっていて、「互換でない」と読んで別の物へ進む（正しくは、digest が違うので整合性の失敗）。不合格例: 壊れた `known-good.json` の `InvalidManifest` を、全ての取得元で数えて「整合性の失敗」で終わる。

### 2.5 スライス分割の判断（A1 の終わり）

- 契機 1（独立した変更理由の混在）: 差込口と調停（Framework の Runtime）、head の書き込み（Framework の Editor と、batch・検証 Play の呼び出し）、取得の操作と選んだ base の記録（SampleGame の Editor）の 3 つがある。**分けない。** head の書き手が無いと、読む形だけを相手なしで凍結することになる（§2.2 で避けた「Local の都合が契約に入る」と同じ失敗）。操作が無いと製品の呼び手が無く、目的の「1 つの操作」と「Play の base に使える」を示せない。3 つとも小さい（§3）。
- 契機 2（別の問いの判定に使う受け入れ条件）: S8（別の `Library` の base と上書き）は、本書の問いではなく「取得した base に上書きを重ねてよいか」の測定である。program の行が S4-3 の最初の gate に置いているので本書が持つが、**答えの中身は GO の条件にしない**（4 つの観測が記録されることだけを条件にする。§2.7 の判定定義）。別の spike の行に分ける案もある（§2.9 の D-3）。
- 契機 3（高リスクの初回実機で独立して失敗しうる未知）: 実機の未知は S7 と S8 の 2 つで、どちらも今ある経路（batch、経路 Built、経路 Built + local edits）の上にある。分けない。

### 2.6 ここでは答えない問いと所有する後続

program §8 の「S4-0 の A1 が振り分けた入力」が S4-3 へ送った項目と、S4-2・S5-0 が送った項目の振り分け。項目の中身は元の箇所を正とする。

| 入力 | 行き先 |
|---|---|
| 供給元が持つ目的 build と期待 digest の得方（head と、revision の照会まで） | **S4-3a（本書）** |
| 別の `Library` の base と手元の上書き（Editor）、上書きに依存として入った同じ id の物の読み先 | **S4-3a**（S8 の測定。答えが「違う」なら owner へ返す） |
| 別の PC の base と手元の上書き（Editor）、CI の Player を取る時の改行の正規化（S4-2 から）、受け入れ版 `SupportedUnityVersion` の literal、transport の protocol の版を採否に含めるか | S4-3b（別の機械が作った物を初めて受け取る行） |
| G09、G13、LAN timeout の値、進捗、遅い転送の間 cache の lock を持ち続けること（Play の入口と他の install が `Busy` になる）、同時の取得の合流、digest から登録までの窓と cache lease（S4-2 から）、`ContentInstaller`・`ContentCacheStore` の移動の再試行（S5-0 から）、既存の `HttpContentArtifactSource` を取得元にすること（404 の読み方、60 秒の期限）、`transport.json` を root に持つ 1 revision の directory（Artifacts の `content fetch` の取得物）を取得元として読むこと、取得を Editor の main thread の外で走らせること | S4-3b |
| 取得元が持つ revision の一覧（head の 1 件より多い物）、目的をまたぐ catalog が要るか、R2 から取った `transport.json` の索引が同じ bytes であることの実測 | 一覧と catalog は S4-3d（選ぶ規則が要る時に形を決める）。R2 の実測は cloud の取得元の行（D5 の後続） |
| Play の入口の先読み、checkout hook が呼ぶ公開入口と最小の hook（U7）、取得した transport の install を入口が代わりに行うこと（S4-2 から）、known-good の pin の呼び手と cache の掃除の入口（`Evict` の呼び手）、壊れた install を直す操作、取得の途中の script の compile（domain reload）の扱い（取得を止めるか、reload を止めるか。途中で失われた task の cache の lock の file の handle がいつ離れるかは未測定） | S4-3c |
| 「自分の base」を選ぶ規則、同じ目的の複数 revision から 1 つを選ぶ規則、`ContentPurposeIndex.Select` で選んだ build を install と登録の検証に通すこと、上書きの base の選択に索引を使う接続、base より古い checkout、記録の無い base で root の宣言を足した場合、記録に無い root の依存だけが無い場合、`NotInBase` の root、manifest の `index` からの見分け、複数の Player から選ぶ UI（S4-2 から）、expected identity の無い要求での同名異内容の検出、互換でない head の下の古い互換な物へ進むか | S4-3d（本書は「head の 1 件」と「pin を渡せる口」までを持つ） |
| S4-2 の「行き先を決めていない入力」の (1) 入口が `player-compat.json` の記録の `identity` を、選んだ Player の identity と比べていない（`PlayerOverlayLaunch` は schema の版と記録の digest の自己照合だけを見る。Compare と Prepare の前に ordinal で比べて止め、identity だけが違う compat の port の test を足す案）、(2) `PlayerOverlayStartupObserver` が Player の log を読んでから process の終了を確かめるので、その間に最後の行を書いて終わると古い写しで `Failed` に決まりうる（log の読みの `IOException` は空の文字列になる）。中身は program §8 の「S4-2 から移送した入力」を正とする（2026-11-25、owner が S4-3 の入力にした） | S4-3d。どちらも遊べる Player の入口（`PlayerOverlayLaunch`）の、Player を選んで起動を見る部分の欠陥で、取得元・LAN・取得のきっかけに依らない。S4-3 の 4 行のうち Player の入口に触れる見込みがあるのは、複数の Player から選ぶ UI を持つ S4-3d だけである。本書は Player を扱わない（§1 の対象外）。S4-3d より先に直す必要が出たら、owner が単独の修正として切り出してよい |
| 要求をまたぐ pin と revision の一貫、`IContentProvider.Id` / `SourceKind` の getter の契約、片方の directory の file が disk から消えた時の読み先 | S4-4（供給元を足す・登録の寿命を扱う行） |
| 閉包の記録が manifest の上限を超える時の置き場と実データでの大きさ、配布と保存を sha256 でまとめる実装 | 実データの集計の後に owner が決める（2026-11-12 の裁定のまま） |
| G19、Built の Play の繰り返しの再検証、streaming の cold load、D7 の「より安い解決の比較」 | 取得元が出来た後の調査記録 |
| 印を書く前に失われた staging の残り、Play の record に取得の出所を出すこと | 所有なし（program §10 の U11 に足す）。record は S5 の入力 |
| Android・iOS（S4-5）、観測 UI と VCS の adapter（S5）、Player での取得と cloud の tier（U10） | 既存の行のまま |

### 2.7 受け入れ条件・判定定義・停止規則

受け入れ条件（括弧は検証する条件）。test の名前は §5。「〜が無い」「〜が変わらない」は、操作の前と、戻った直後の、directory の一覧（または file の bytes）を比べて見る。

- **AC1（N1）:** 調停の全ての終わり方で、取得の前後の `installed/` の一覧の差は、(i)「取得した」の時だけの対象の 1 件の追加と、(ii) 結果の「消えた install」に載った削除だけである。消えた install は、調停が installer の呼び出しの前後で cache を読んだ観測の差で（cache の transaction の外で読むので、他の操作の追加・削除も入りうる。原因の帰属ではなく観測として結果に載せ、文もそう書く）、成功・失敗・取消のどれでも結果に載せる（installer は変えない）。この「一覧の差が上の 2 つだけ」の検査は、他に cache へ書く者がいない fixture で行う。同じ revision を別の操作が先に入れる競合は、結果（「install 済み」、bytes を得た取得元が空）と観測の差を別に見る。staging の片付けの失敗は結果の診断に載せる。失敗経路: content の file が manifest と違う取得元、`transport.json` が対象の digest と違う取得元、読んでいる途中で例外を投げる取得元、取消、容量の確保が古い install を消した後に bytes が壊れていて失敗する場合（消えた install が結果に載り、対象は増えない）。取消に応じない取得元（read が終わらない）でも、取消で戻った時点で対象は増えておらず、その後に test が取得元の read を解放しても増えない。installer が取消を見る最後の点の前後を作る: 最後の file の最後の読み（EOF）を取得元が保留している間に取消すと「取消」で対象が無い。guard の internal の test 用の通知（最後の読みの結果を installer へ返す直前に呼ばれ、通知の後に guard は token を見ない。installer の `CopyExact` は EOF の後に token を見ない）の中で取消すと「取得した」で対象が在る（結果と一覧が食い違わない。source の task を完了させるだけでは順を証明しないので、この通知で順を作る）。
- **AC2（N2・S5）:** §2.4 の段と止まる規則の表の各行が、表のとおりに進むか終わる。失敗の振り分け（段、失敗の種類、証拠 (i)(ii) の有無、cache の読み直しの結果を入力にして次の動きを返す純粋な関数）は、`ContentDeliveryFailureCode` の 12 個の値の全てと取得元の側の未知の例外を、証拠の有無と読み直しの結果の全ての組で、表の行に 1 つずつ割り当てる。失敗経路: 表の 3 つの不合格例（古い revision を取得しないこと、digest も Unity の版の欄も違う manifest を整合性の失敗と読むこと、壊れた `known-good.json` を取得元のせいにしないこと。最後は temp の cache に壊れた `known-good.json` を置き、本物の installer で作る）。pin の revision を持つ取得元が無い時に「見つからない」で終わること。全ての取得元が「pin だけ」で pin が無い時に、照会を 1 回もせずに「見つからない」で終わること。照会と違う contentSet・target の entry を返す取得元を飛ばすこと。取得元が自分で投げた `OperationCanceledException` を取消にしないこと。
- **AC3（N3）:** 取消と失敗で戻った直後に、cache の `staging/` の下に、取得の前の一覧に無かった directory が無く（installer は毎回、印の正しい前の残りを片付けるので、前の残りが減ることは許す。installer が片付けの失敗を診断に残した時だけ、その診断の path と同じ directory が残ることを許し、結果の診断に載る。AC1 と同じ）、cache の transaction の lock を取り直せる（直後の `Inspect` が `Busy` にならない）。取消に応じない取得元でも同じ。head の書き換えが途中で失敗した時、head の file は前の bytes のまま（無かった時は無いまま。単一の書き手の前提の下の主張で、別の書き手がいた時は、その書き手の head を上書きしない）で、別名の file が残らない。置き換えが head を backup の名前へ移したまま失敗した時は、head が無ければ backup を戻す（別の書き手の head が在れば戻さない。AC7）。戻すことも失敗した時だけ head が無く、診断に backup の path を残す（壊れた head と、今回の publish を指す head は残らない）。guard は取得元の読みを自分の buffer で受けて写すので、取消の後に終わった読みは installer の buffer を書かない。取消の後に開いた stream は破棄され、遅れた例外は guard が受け止める（観測されない task の例外を残さない）。`OpenArtifacts` と取得口の `Dispose` は同期で待たない契約とし、違反は host の不備として本書では打ち切らない。失敗経路: 置き換えを失敗させる注入（例外だけの場合と、Win32 の `ReplaceFile` の `ERROR_UNABLE_TO_MOVE_REPLACEMENT_2` のように head を backup へ移したまま失敗する場合）、取消に応じない取得元、取消の後に読みが終わる取得元、取消の後に stream を返す取得元、取消の後に例外を投げる取得元。
- **AC4（N4・N7）:** Phase B の commit が変える file に、`Runtime/ContentResolution/`・`Runtime/AssetManagement/`・`Runtime/SceneSystem/`・`Runtime/Bootstrap/` の file が無い。Runtime の新しい file が `UnityEditor` を参照しない（`pwsh tools/contract-audit.ps1` の検査 5）。差込口の member に資格情報を受ける引数が無い。調停の test の表が 3 種類の取得元で同じ結果になる（AC8）ことで、型・id での分岐が無いことを示す。失敗経路: 型か id で分岐した調停は、3 種類目（別の型と id で包んだ directory の取得元）で結果が違う。
- **AC5（N5）:** Phase B の commit が、`Runtime/BuildContent/Distribution/` の今ある file（`ContentInstaller.cs`・`ContentCacheStore.cs`・`ContentDeliveryFiles.cs`・`InstalledRevisionVerifier.cs`・`ContentManifestValidation.cs`・`ContentTransportManifest.cs`・`ContentArtifactSource.cs`・`ContentPurposeIndex.cs` など）を変えない。失敗経路: 取得で install した revision を、今ある検証（`ContentCacheStore.ValidateInstalled`）と Delivery の窓の Installed offline の経路がそのまま受け付ける（取得の install が、今ある install と同じ形であること）。
- **AC6（N6）:** Phase B の commit が `PlayEntryDecision.cs`・`PlayEntryTransaction.cs`・`PlayEntryRecord.cs`・`ContentDeliveryPlayBridge.cs`・`ContentDeliveryWindow.cs` を変えず、`PlayEntryDecisionTests` が変更なしで通る。`PlayEntryCandidates.Select` は、選んだ base の記録を渡さない時、今と同じ並びで答える（既存の `PlayEntryCandidatesTests` の test の method を変えずに通る。test の追加はしてよい）。記録を渡した時は、記録の組と同じ entry の時刻を「install の時刻と、選んだ時刻の新しい方」と読んで並べる。選んだ時刻は UTC の `O` 形式で保存し、receipt の `installedUtc` と UTC で比べる。receipt の時刻は installer が copy の前に作るので、「後の install」は receipt の時刻の順で、同時の操作の公開の順ではない（限定した規則の一部として結果の文に書く）。これは「直近の明示の操作が先」ではなく「選んだ後の新しい install が先」の限定した規則で、既にある revision の再利用（Delivery の窓・検証 Play）は receipt の時刻を変えないので数えない。この限定を取得の結果の文にも書く。記録で並びを変えた時も、入口の Warning の文（`using the latest`）は変えず（`PlayEntryDecision` を変えない）、「並びの先頭」の意味で読むと取得の結果の文と harvest 先の 18 章に書く。選んだ base の記録の保存に失敗した時は、取得の結果を「取得した」のまま Warning を付け、Play が使う revision を書く。失敗経路: 選んだ後に別の revision が新しく install された時は、後の install が先になる（製品の書く経路（実の時計）で記録を作り、その後に本物の installer で install して作る）。記録の revision が cache に在るが digest が違う時、または無い時は、記録を使わずに今の並びで答え、Warning に出す。記録の file が壊れている・読めない時は、例外を漏らさず、記録を使わずに今の並びで答えて Warning に出し、Play を拒まない。取得の操作は、Play の間・Play への移行の間・compile の間は始まらず、理由を返す（cache に触れない）。installer が cache の lock を持つ間に Play を押すと、入口は「cache を読めない」で拒否する（今の動きのまま。壊れない）。lock の外（段 1・段 2、記録を書く間）で Play が始まった時は、入口はその時の cache と記録で選び、取得は続く。
- **AC7（S1）:** batch が終了コード 0 で終わった後、head の file が、その publish の contentSet・target・revision と、公開した `transport.json` の bytes の SHA-256 を持つ。batch は head を、成功の結果 file の確定の後の最後の段で書き、head の取り消し（巻き戻し）を持たない。読み手は確定していない publish を指す head を見ない。head を書き終えた後に、head の path・revision・digest を持つ最後の log の行を 1 つ書く。head の置き換え（`File.Replace` か `File.Move`）が成功した時点が head の段の成功で、その後の backup・別名の file の削除や log の失敗は診断に残すだけで、失敗の処理（`Fail`）に入れない（公開先を消すと head が消えた revision を指すため）。失敗経路: 結果 file の確定が失敗した batch は head を書かない（今ある `Fail` が公開先を消す）。head の書き込みが失敗した batch は、公開先を消さず、成功の結果 file を、head の失敗の欄（理由）を足した結果で置き換え（結果 file の確定を、既にある file を置き換えられる形に変える。§3）、終了コード `ExitOriginHeadFailed`（6。新しい公開の定数）で終わる。その置き換えも失敗した時は欄の無い成功の結果 file が残りうるので、batch の成否の正は process の終了コードで、結果 file だけで成功と読まない（今の batch の設計と同じ。§5 の P1・P2 は終了コードと、head を書いた後の最後の log の行で読み、終了コード 6 は合格にしない）。この経路の test は本物の結果 file の確定（temp の directory）で行う（偽の `Commit` では置き換えの失敗を見ない）。head は前の bytes のまま（置き換えは原子的。部分失敗の扱いは AC3）。今ある head が未対応の版か壊れている時、書き込みは head を書き換えずに失敗する（上の head の失敗の経路）。Local の取得元の root の head の書き手は、同時に 1 つを前提にする（batch と検証 Play を同じ checkout で並べて走らせない。直列化の仕組みは作らない）。前提が破れて並んだ時も、他の書き手の head を壊さない: 後から書いた方が勝ち、head が無い時の最初の書き込みが並んで `File.Move` が「既にある」で失敗した側は置き換えでやり直す。置き換えの部分失敗の後の戻し（AC3）は、head が無い時だけ backup を戻し、既に別の書き手の head が在れば戻さずに自分の失敗を返す。検証 Play は S1 のとおり（書けなければ Play を止めずに Warning）。
- **AC8（S2・S3・S4・S6）:** 調停の test の表（AC1〜AC3 の失敗経路を含む全ての行）を、§2.3 の 3 種類の取得元で走らせ、結果の状態・install の結果・試行の記録が同じである。到着点に無い時は取得して、結果に対象の組・対象を決めた取得元・bytes を得た取得元・出所が載る。在る時は、`OpenArtifacts` の呼び出しと、それが返した取得口の上の `OpenReadAsync` が 0 回で（取得元が head を読むための読みは数えない）、結果の「bytes を得た取得元」は空である。段 2 の後に別の操作が同じ revision を入れた競合では、結果が「install 済み」で bytes を得た取得元が空である。先の取得元が使えない時は次から取得する。取得の前後で cache から消えた install があれば、結果に載る。
- **AC9（S7・N2・N6。実 Editor）:** 手順は §5 の E 系列の P1〜E5（別の checkout X で publish した R1・R2 を、checkout Y が取得元 X から取得する）。各 case の期待は §5 の「判定の担当が照合する値」の表のとおりで、要点は次の 4 つ。(a) 空の cache から R1 を取得した後と、R2 を取得した後の Play（経路 Built）の record の revision が、それぞれの publish の結果 file の `identity` と等しく、取得の結果・選んだ base の記録・`receipt.json` の digest が結果 file の `manifestSha256` と等しい（E1・E2）。(b) R1 の公開時の head の bytes だけを置いた取得元（revision の directory を持たない）から取得すると「install 済み」で終わり、bytes を得た取得元が空で、R1 の `receipt.json` の bytes と `installed/` の一覧が前後で同じ（E3）。(c) 新しい Editor の process で、R2 が cache に残ったまま、取得の操作なしに Play すると、record の revision が R1 である（E4。選んだ base の記録が process をまたいで残り、後から install された R2 より先になる）。(d) 取得元が無い時と、head が到着点の R1 と違う digest を申告する時は、それぞれ「見つからない」「到着点の同じ revision が違う digest」で終わり、`installed/`・`staging/` の一覧と選んだ base の記録が前後で同じで、その後の Play の record の revision が R1 である（E5）。失敗経路: (d)。bytes の取得口を開いた回数（S3）は AC8 の単体で見る。
- **AC10（S8。実 Editor。測定）:** 手順は §5 の E6（AC9 と同じ X・Y で、Y に固定 patch を当てて経路 Built + local edits で Play する）。4 つの観測（record の上書きの欄の `overlayRoots` と `unbuildable`、上書きの root が手元の版で出たか、上書きに入らない root が base の record で載ったか、Error の有無）が、対象の head・base の revision・2 つの checkout の識別と一緒に記録される。答えの中身は合否にせず、AC9 の合否とも混ぜない。
- **AC11（回帰）:** 全 EditMode 回帰が、XML の failed 0 と root result `Passed`（`-nographics` の VFX 25 件の Inconclusive は既知。program §8 の「全 EditMode 回帰の受け入れ条件の書き方」）。

- 判定定義: **GO** は AC1〜AC11 が全て満たされたこと。AC10 は 4 つの観測が全て記録されたことで満たす。**NO-GO** は AC1〜AC9・AC11 のどれかが満たされないこと。NO-GO は保留より先に決める（AC9 の未達と AC10 の未実施が同時なら NO-GO）。AC1〜AC9・AC11 が満たされ、AC10 を実施できなかった時（E6 の Editor が上書きの build で止まる等。AC9 と同じ 2 つの checkout を使うので、checkout が無いことは理由にならない）、または E6 で Play に届いたが 4 つの観測のどれかが「観測できなかった」時は、GO にも NO-GO にもせず、判定を保留して owner へ返す。CONDITIONAL ACCEPT は使わない（spike ではない）。AC10 の答えが「違う」「Error が出た」の時は、GO のまま owner へ返し、S4-3b の前に owner が裁定する。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。決定記録: なし。

### 2.8 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の参照を足さない（本書の配置は、今ある参照の中で閉じる。§3）。`UnityEditor` 依存を Runtime の asmdef に置かない。
- 公開 API のログは `ILogger<T>`。`ZLogger*` の型を公開面に出さない。本書の Runtime の新しい型は log を出さず、結果の値で理由を返す。
- Unity 側の C# で `record` を使わない。新規・編集する `.cs` は先頭に `#nullable enable`。
- test で `Task.Delay` / `Thread.Sleep` を使わない。遅い取得元・取消に応じない取得元・lock の競合の test は signal（`TaskCompletionSource` など）で順を作る。head の置き換えの再試行の待ちと時計は注入する。
- path は `tools/path-budget.json` の予算（repo 相対 150 字）に従う。
- 参照 0 を削除理由にしない（`MarkKnownGood`・`Evict` は触らない）。
- `await` で Editor の main thread を同期に待たない。Runtime の新しい code は installer と同じく `ConfigureAwait(false)` を付けず、呼び手の context に戻る。取得の操作は Delivery の窓の `Prepare` と同じく main thread の上で非同期に走る（読みが同期に終わる間は Editor が固まりうる。今ある `Prepare` と同じ。main thread の外で走らせるのは S4-3b）。
- 調停は `OpenArtifacts` が返した取得口を、取得元ごとに必ず破棄する。
- program §4.1: 整合性の失敗の後は、同じ期待 digest の正規の複製で直せる時だけ次へ進む。resolver も取得も、暗黙に Build を始めない。
- program §4.3: 通知は必要な警告だけで、Play のたびの popup は出さない（D6）。取得の結果は窓の文と Console の 1 行で示し、dialog を出さない。
- 13 章: install は caller が明示した local NTFS の cache の staging で検証し、同じ volume の rename で公開する。同じ revision の違う manifest は上書きしない。known-good は caller が明示して昇格する（本書は昇格しない）。
- head の置き換えは、同じ directory に別名で書いてから、既にある時は backup の名前つきの `File.Replace`、無い時は `File.Move` で行う（先例は known-good の file の `File.Replace`）。読み手に「file が無い」瞬間を見せない。`File.Move` が「既にある」で失敗した時（並んだ最初の書き込み）は `File.Replace` でやり直す。置き換えが失敗した後に head が無く backup がある時（Win32 の `ReplaceFile` は、置き換えられる側を backup へ移したまま失敗しうる）は、backup を `File.Move` で head へ戻してから失敗を返す（`File.Move` は既にある head を上書きしないので、その間に別の書き手が書いた head を壊さない）。戻せなければ head が無いまま失敗とし、診断に backup の path を書く。backup と別名の file は成功でも失敗でも残さない（戻せなかった backup だけは診断のために残す）。一時的な失敗（開発の PC の監視 tool。program §8 の S5-0 の行）は、`ContentStagingMove.IsTransient` と同じ判定、同じ上限の定数で置き換えをやり直す。`ContentStagingMove` の移し直しの条件（directory 用）は使えないので、やり直しの loop は head の書き手が持つ（§2.9 の D-5 の 1）。
- Runtime を触る Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない（program §0 の停止規則）。
- Unity の test と batch を実行する担当は、先に [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) を読む。build を伴う test と batch は、process の自然終了を合否にしない。

### 2.9 owner の裁定を待つ項目と、決定済みの項目

**owner の裁定（2026-11-26。PR #214 のコメント「外部計画レビュー・修正差分確認と owner 裁定の推奨（fb860a60 / base 9e7d4148、OpenAI ChatGPT）」の推奨を owner が採った。下の各項目の「決定」）:**

- **D-1（目的・利用者の文言）:** **決定: §1 の改訂後の文言を承認。** §1 の改訂後の文言でよいか。変える時は、変えた 2 欄で目的レビューをやり直す（やり直しは 1 回まで）。
- **D-3（S8 の置き場）:** **決定: AC10・E6 を Phase C の測定として残し、AC9 の合否と混ぜない。** 旧 program の S4-3 の行は、別の `Library` の base と上書きの答えを「A1 の最初の gate」に置いていた。本書は、凍結の前の harness ではなく、Phase C の実 Editor の測定（AC10）に置いた。理由は 3 つ。答えは差込口の設計を変えない。S4-2 で Player の答えが「同じ」だった。本書の製品の経路（別の checkout の publish を取得元から取得する）がそのまま測定の手順になる。答えが「違う」時は GO のまま owner へ返す。外部計画レビュー（EX-7）は、S4-2 の driver と patch の実物を見て、同じ X・Y の準備の上で測る追加の費用は小さいとして、同じ driver の観測の case（E6）に残す案を支持した。主担当の推奨もこの形（AC10 のまま、AC9 の合否と混ぜない）。凍結の前に測る案、別の spike の行にする案に変えるなら、ここで決める。
- **D-6（外部計画レビュー 1 件目の採否。案は §5.1 の EX-1〜EX-8）:** **決定: 外部計画レビュー 1〜4 件目（EX・RX・SX・XR）の採否の案を承認。** 実 Editor の検証（AC9・AC10）を、手作業の `eval` と文字列の目視の照合から、再実行できる driver・case・証拠の構成（§5 の E 系列）に改めること。主担当の案は採用で、変えた点は 2 つ。(1) Play を見る case は、S4-2 の `[Explicit]` の test と `run-tests.ps1` の組ではなく、driver が窓のある Editor を case ごとに起動して `eval` で操作する（入口が batch を素通りするため。§1 の現況）。(2) driver は合否を判定しない。identity と digest の照合は判定 C の担当が保存された原記録で行う（照合の script を Phase B が書いて git に置くことは workflow が許さない。S4-2 の driver と同じ扱い）。この採用で、責務マップに driver の 1 行が増え（§3）、AC9 の範囲が「空の cache から 1 件」から E1〜E5 に広がる（cache が空でない時の Play の選択を、実 Editor で見るようになる）。設計の他の部分と受け入れ条件の他の行は変えていない。**改訂 2:** 外部計画レビュー 2 件目（設計の 5 件 RX-1〜RX-5 と検証の追加 RX-6〜RX-10）は全て採る案（RX-6 は一部）。3 件目（検証の資産を code として再利用する）は、owner が 2026-11-25 に、S4-3a の中ではなく独立の行 S4-V（program §8、要求は §9.1 の利用者表）にすると決めた。S4-3a の driver は S4-V の基盤の上の scenario だけを持つ（§3、§5）。owner は同日、改訂 1・2 について A2 の観点の再レビューは要らないとしたが、2026-11-26 の Phase A のやり直しの決定で、作り直した版（改訂 3）で A2 を行うことにした（§5.1）。
- **D-4（risk）:** **決定: `high` を承認。C' の別の系列の予約を保つ。** `high` に上げた（外部計画レビュー 2 件目も支持した）。C' を B・C と別の系列の担当にする予約が要る（§5）。
- **D-5（A2 の指摘のうち owner の判断が要る物。採否の案は §5.1）:** **決定: 1〜5 は採用、6 は (b)。**
  1. head の置き換えの再試行を、file 用の新しい loop として持つこと（判定と上限は `ContentStagingMove` と同じ）。再試行なしにすると、監視 tool の一時的な失敗で、成功した publish を batch が捨てる。
  2. 検証 Play の publish も head を書くこと（`VerifyPlayLauncher.cs` に呼び出しを 1 つ足す。同じ root に publish するので、書かないと head が最新の publish を指さなくなる）。
  3. 取得の対象を head の 1 件に固定し、取れない時に古い revision へ進まないこと（head の directory を手で消すと、次の publish まで取得は失敗で終わる）。
  4. Play の入口の候補の並びを、選んだ base の記録で変えること（`PlayEntryCandidates.cs` と `PlayEntryInterceptor.cs` を変える。「選んだ後の新しい install が先」の限定した規則。AC6）。
  5. 取得が budget を超えると古い install を消しうること（今ある installer の動き）を、止めずに結果の文で示すだけにすること。
  6. （改訂 3、A2 の IV3-5、外部計画レビュー XR-3）head の書き込みが失敗した時の publish の batch。**決定: (b)。** 公開先と結果 file を残し、結果 file の欄と終了コード 6 で head の失敗を示す（AC7、S1、N5）。immutable な publish の成功と、取得元の head の更新の成功は別の事実で、head の失敗だけで正しく作れた公開物を消さない（古い道具が新しい版の head に当たった時の副作用も避ける）。終了コードは 0 でないので、取得を使わない CI の batch も失敗を見る。判定 C の P1・P2 は終了コード 0 だけを合格にするので、判定の境界は 1 つのまま。選ばなかった (a) は、失敗にして公開先を消す形（主担当の元の案）だった。

**決定済み:**

- Q-1（行の分け方）: owner が 2026-11-25 に §2.2 の 4 行を承認した。
- Q-3（期待 digest の出所）: owner が 2026-11-25 に A0 の推奨を採った（§2.3）。
- Q-2（名前）: 「取得元」（code は `ContentOrigin`）とした。owner から異論が無ければこのまま凍結する。
- D-7（改訂 3 の A3 の変更の再レビューの要否）: owner が 2026-11-26 に、A2 の観点の再レビューを行わないと決めた（主担当の案と同じ。変更は改訂 3 の A2 の指摘の解消に閉じていて、新しい責務・依存・公開面を足していない）。Draft PR の外部計画レビューが次の独立の目になる。
- 旧 D-2（S4-2 より前の凍結）: 凍結は S4-2 の Phase D の後に、現況を取り直してから行う案だった。S4-2 は 2026-11-25 に merge され、改訂 1 で base を取り直した（§0）ので、裁定待ちから外した。
- S4-2 の「行き先を決めていない入力」の (1)(2): owner が 2026-11-25 に S4-3 の入力にした。行の振り分け（S4-3d）は §2.6。

**後続の行の A0 へ送る問い（本書の凍結には要らない）:**

- Q-4（S4-3b）: 別の PC で build した base を測る環境があるか。無ければ、LAN は同じ PC の中の模擬で判定し、実の別の PC は未確認として残す。
- Q-5（S4-3b）: 想定する LAN の取得元は、file 共有（UNC）、HTTP の server、その両方のどれか。
- Q-6（S4-3b と cloud の行）: cloud を後で足す時、Unity から直接読む形と、Artifacts の `content fetch` の取得物を手元の directory として読む形のどちらを見込むか。
- Q-7（S4-3c）: 取得が自動になった時の cache の掃除と、消してはいけない base を守る印（known-good）の所有。

## 3. 責務マップ

新しい asmdef と参照は足さない。Runtime の新しい型は `OneStarMaker.Runtime` の `Runtime/BuildContent/Distribution/`（namespace `OneStarMaker.Runtime.BuildContent.Distribution`）に置く。理由は、installer・cache・bytes の取得口と同じ変更主体（Content Distribution。program §4.4）で、installer の internal の検証（`ContentManifestValidation`）と cache の internal の path を使うからである。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の境界では Distribution は BuildSystem の側で、AssetManagement と要求時解決からの参照は足さない。Editor の書き手は `OneStarMaker.Build.Content`（`Editor/Build/Content/`。Runtime を参照済みで、`ContentStagingMove` と同じ assembly）、host の code は `SampleGame.DependOnAll.Editor`（両方を参照済み）に置く。

| file（新規は ＋） | 責務（1 文） | 種別 | 状態・所有・寿命 | 依存 | 公開面 | 単体テスト | 行数（今 → 予想） |
|---|---|---|---|---|---|---|---|
| ＋ `Runtime/BuildContent/Distribution/ContentOrigin.cs` | 取得元の差込口と、照会の値の型を宣言する | 契約 | 状態なし | 同 folder の `IContentArtifactSource` だけ | `IContentOrigin`（`Id`、`FindAsync(query, token)`、`OpenArtifacts(entry)`）、`ContentOriginQuery`（contentSet・target・任意の revision）、`ContentOriginEntry`（contentSet・target・revision・任意の申告の digest・不透明な所在）、`ContentOriginAnswer`（`Found` / `NotFound` / `Unavailable` と理由）、`ContentDigestAuthority`（`Declared` / `Pinned`）、`ContentOriginTrust`（`PinnedOnly` / `AcceptDeclared`） | 値の型の constructor の検査 | 0 → 約 130 |
| ＋ `…/Distribution/ContentOriginHead.cs` | head の file の形の規則（作り方と、bytes からの読みと検査）を 1 か所に持つ | 永続化の形（作りと読み） | 状態なし | `ContentManifestValidation`（segment・相対 path・hash の形の検査、manifest の欄の読み）、`ContentDeliveryFiles.Sha256`、`JsonUtility` | head の DTO（public。書き手が別の assembly に居て、Runtime の `InternalsVisibleTo` に入っていない。`ContentPurposeIndex` と同じ扱い）、`Create(manifestBytes, relativePath)`（`transport.json` の bytes から contentSet・target・revision と SHA-256 を読んで head の bytes を作る。public。書き手が hash と manifest の解析を写さずに済む）、`Read(bytes, contentSet, target)` と結果（`Present` / `None` / `Unsupported` / `Invalid` と理由。照会と違う contentSet・target は `Invalid`）、head の相対 path を作る関数 | Unity の Editor API なしで全て（作りと読みの往復、形の表） | 0 → 約 150 |
| ＋ `…/Distribution/IndexedContentOrigin.cs` | head の file と revision の所在を読んで答える取得元と、directory 用の組み立て | infrastructure | 状態なし（呼ばれるたびに読む）。寿命は組み立てた host に従う | `ContentOriginHead`、`IContentArtifactSource`、`LocalContentArtifactSource` | `IndexedContentOrigin` は internal（id と、「root からの相対の所在を開いて、stream・不在・読めないの 3 つで答える関数」と、「所在から bytes の取得口を作る関数」を受ける）。public は `ContentOrigins.Directory(id, root)` だけ（directory の関数は `File.Exists` で不在を自分で確かめ、root が無い時も不在と答える）。LAN で HTTP の 404 を不在と読む関数は S4-3b が足し、その時に公開を決める | 3 種類の取得元の 1 つとして調停の表 + 照会の表（temp の directory）。振る舞いの悪い行は、関数を注入した `IndexedContentOrigin` でも走らせる（guard を型で外す実装を検出する） | 0 → 約 130 |
| ＋ `…/Distribution/ContentAcquisition.cs` | 取得元の列と pin から、対象の組を決め、到着点を見て、取得元を順に当たり、installer で install して結果を返す | orchestration | 1 回の呼び出しの間だけ。cache の所有は `ContentCacheStore` のまま | `IContentOrigin`、`ContentAcquisitionRules`、`ContentOriginGuard`、`ContentInstaller`、`ContentCacheStore`、`ContentManifestValidation` | `ContentAcquisition.AcquireAsync(request, origins, store, token)` | memory の取得元と temp の cache で、段の表の全ての行（Unity の Editor API なし） | 0 → 約 240 |
| ＋ `…/Distribution/ContentAcquisitionRules.cs` | 段、失敗の種類、§2.4 の証拠 (i)(ii) の有無、cache の読み直しの結果から、次の動き（次の取得元へ / どの状態で終わるか）を返す | policy（純粋な関数。読み直しは調停が行って結果を渡す） | 状態なし | `ContentDeliveryFailureCode` | internal | 失敗 code の 12 値と未知の例外を、証拠と読み直しの全ての組で表の行に割り当てる全射の表 | 0 → 約 110 |
| ＋ `…/Distribution/ContentOriginGuard.cs` | 取得元の照会と、返した取得口・stream を包み、取消に応じない読みを打ち切り、取得元の側の例外を「答えない」「届かない」に揃え、§2.4 の証拠 (i)(ii) を記録する | infrastructure | 包んだ取得口と同じ寿命 | `IContentOrigin`、`IContentArtifactSource`、`ContentDeliveryFiles.Sha256` | internal。installer へは、open と read の失敗を `ContentDeliveryException(TransportFailure)`、呼び手の取消を `OperationCanceledException` で返す（installer は他の例外を到着点の側の `IoFailure` に包むため）。取得元が自分で投げた `OperationCanceledException` も `TransportFailure` にする。調停へは、取得元の失敗を観測したかと、渡した bytes の照合の結果（`transport.json` の SHA-256 と大きさ、content の file ごとの大きさと SHA-256 を、調停が先に読んだ manifest と比べた結果）を返す。最後の読みの結果を返す直前の internal の test 用の通知を持つ（AC1） | 取消に応じない・例外を投げる memory の取得元。installer へ返す例外の型。正しい bytes を渡した後に staging の側に異常を入れる fixture（取得元の open の callback が staging に余分な file を置く）、上限を超える `transport.json`、正しい manifest と食い違う content、installer が途中で読むのを止めた時（部分の hash を不一致に数えない）を、本物の installer で区別する（XR-1） | 0 → 約 170 |
| ＋ `…/Distribution/ContentAcquisitionTypes.cs` | 取得の要求・結果・試行の記録の値の型 | 契約 | 状態なし | 同 folder | `ContentAcquisitionRequest`（contentSet・target・Unity の版・root schema・budget・任意の pin）、`ContentPin`（revision と digest）、`ContentAcquisitionOrigin`（取得元と信頼）、`ContentAcquisitionResult`（状態 8 つ: 取得した / install 済み / 見つからない / 整合性の失敗 / 対象が使えない / 到着点の同じ revision が違う digest / 到着点の失敗 / 取消、install の結果（再利用の印を含む）、対象の組、対象を決めた取得元、bytes を得た取得元、digest の出所、試行ごとの段と理由（`transport.json` を読んだ取得元を含む）、消えた install（観測の差）） | constructor の検査 | 0 → 約 190 |
| ＋ `Editor/Build/Content/ContentOriginHeadWriter.cs` | 公開済みの 1 revision の directory の head を、その親の directory（取得元の root）に置き換えで書く（head の bytes は `ContentOriginHead.Create` が作り、ここは file の操作だけ） | 永続化（書き） | head の file の所有は、その root に publish する host。lock は持たない（後から書いた方が勝つ） | `ContentOriginHead`（Runtime）、`ContentStagingMove.IsTransient` と上限の定数、`System.IO` | `ContentOriginHeadWriter.Write(publishedDirectory)`（取得元の root は公開先の親に固定し、呼び手は root を渡さない）。取り消しの口は持たない（呼び手は確定の後の最後の段で呼ぶ） | temp の directory。置き換え・待ち・時計は注入。置き換えの部分失敗（backup へ移したまま失敗）と、並んだ最初の書き込みの注入 | 0 → 約 140 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBatch.cs` | （既存）batch の入口 | — | — | — | 成功の結果 file の確定の後の最後の段で head を書き、書き終えたら head の path・revision・digest を持つ最後の log の行を書く（`Stages` に注入の口を 1 つ。取得元の root は公開先の親で、`ContentOriginSettings` に依らない）。head の失敗は公開先を消さず、成功の結果を head の失敗の欄を足した結果で置き換え、`ExitOriginHeadFailed`（6。public の定数）を返す（`Fail` を通さない）。head の置き換えの成功の後の掃除と log の失敗は診断だけ。結果 file の確定（`CommitResult`）を、既にある file を置き換えられる形にする（無ければ `File.Move`、あれば `File.Replace`。今は `File.Move` だけ）。結果 file の形に head の失敗の欄を 1 つ足す（BS5b-2 の From published directory が読む欄の名前と意味は変えない） | 既存の batch の test に、段の成功・head の失敗（公開先が残り、本物の `CommitResult` で結果が欄つきに置き換わり、終了コード 6）・確定の失敗（head を書かない、公開先を消す）・head の置き換えの後の掃除の失敗（終了コード 0、公開先が残る） | 202 → 約 235 |
| `SampleGame/…/Build/VerifyPlayLauncher.cs` | （既存）検証 Play | — | — | — | `Produce` の宣言の照合の後に head を書く呼び出しを 1 つ | 既存の段の test（head が書かれること） | 499 → 約 504 |
| ＋ `SampleGame/…/Build/ContentOriginSettings.cs` | この host の取得元の列（既定の root、信頼、場所の上書きの読み）を組み立てる | host の policy | `UserSettings/OSMContentOrigin.json`（git 管理外）に path 1 つ。Delivery の窓の `OSMContentDelivery.json` と同じく file にするのは、E 系列の driver が管理する file として書けるようにするため。取得の開始のたびに file を読み、Editor の中に写しを持たない（E3・E5 が Editor を開いたまま場所を変える）。上書きは利用者が file を手で編集する（U5 は任意で、窓に欄を足さない）。場所の directory が無くても拒まず、取得元が「無い」と答える（E5 の (a)） | `ContentOrigins`、`System.IO`、`JsonUtility` | internal | path の解決の表（file が無い・壊れている時は既定の root） | 0 → 約 60 |
| ＋ `SampleGame/…/Build/ContentBaseSelection.cs` | 取得で選んだ base の記録（cache root・contentSet・target ごとに、対象の組、対象を決めた取得元、bytes を得た取得元、出所、選んだ時刻）の読み書き | 永続化 | `UserSettings/OSMContentBaseSelection.json`（git 管理外。Editor を閉じても残る）。選んだ時刻は UTC の `O` 形式。key の cache root は `Path.GetFullPath` の後に末尾の区切りを除き、大小文字を区別せずに比べる（書き手と入口で同じ関数を使う）。file が壊れている・読めない時は「記録なし」と理由を返し、例外を漏らさない。entry は cache root ごとに足され、消す所有者を置かない（存在しない cache root の entry は読まれないだけ） | `System.IO`、`JsonUtility` | internal | 読み書きと照合の表（壊れた file、key の正規化、UTC の往復） | 0 → 約 100 |
| ＋ `SampleGame/…/Build/ContentAcquireLauncher.cs` | 取得の操作（RunPurpose から要求を組み立て、1 つずつ走らせ、取消を受け、選んだ base を記録し、Play が使う revision を確かめて結果の文を作る） | Editor の orchestration | 走っている操作 1 つと直近の結果を static に持つ（Editor の process の間。domain reload で失われる。選んだ base の記録は残る） | `ContentAcquisition`、`ContentOriginSettings`、`ContentBaseSelection`、`PlayEntrySettings.RunPurpose`、`SampleGameBuildPurposes`、`PlayEntryCandidates`、`VerifyPlayLauncher`（cache root、target）、`ContentDeliveryWindow.LoadSettings`。単体 test の port は、Editor の状態（Play の間・移行の間・compile の間）、設定（cache root・budget・RunPurpose・取得元の列）、時計、記録の保存、取得の実行の 5 つ | public static（`Start`・`Cancel`・`IsRunning`・直近の結果。`Start` は Editor の process の中で 1 ずつ増える操作の番号を返し（拒否した時も番号と理由を直近の結果に残す）、直近の結果はその番号を持つ。直近の結果は、状態、対象の組、対象を決めた取得元と bytes を得た取得元の id、出所、試行ごとの段と理由、消えた install、Play が使う revision とその理由、Warning を持つ JSON の文字列でも読める。`SampleGameContentBatch` と同じく、`eval` から呼べるようにする）。始まりと終わりに Console へ 1 行ずつ書く（終わりの行に状態） | port を差し替えた単体（Play の間の拒否、二重の開始、取消、結果の文、選んだ base が Play に使えない時の文、記録の保存の失敗） | 0 → 約 210 |
| `SampleGame/…/Build/PlayEntryCandidates.cs` | （既存）installed cache からの候補の選択 | policy | — | `ContentBaseSelection` の値 | `Select` に任意の引数（選んだ base の記録）を足す。渡さない時は今と同じ。S4-2 の `ResolvePlayerBase` は変えない | 既存 + 並びの表（AC6） | 175 → 約 200 |
| ＋ `tools/s43a-local-origin.ps1` | §5 の E 系列の case の列（`S0`・`P1`・`E1`・`P2`・`E2`・`E3`・`E4`・`E5`・`E6`・`Cleanup`）と、Local の取得元の fixture、設定の file、保存する証拠を、S4-V の基盤の scenario `s43a` として書く | 検証の driver（製品の code ではない） | 状態を持たない。run の root・外部 root・管理する file の寿命は基盤が持つ | `tools/Harness/Scenario.psm1` の公開関数だけ（`New-Scenario`、`Add-ScenarioCase`、`Invoke-Scenario`、`Invoke-ScenarioBatch`、`Start-`・`Send-`・`Read-`・`Wait-`・`Stop-ScenarioEditor`、`Set-ScenarioFile`、`Reset-ScenarioFiles`、`Get-ScenarioDirectory`、`Remove-ScenarioDirectories`、`Save-ScenarioArtifact`、`Write-ScenarioRecord`）。製品の入口は `eval` の C# の文字列から呼ぶ（`ContentAcquireLauncher` の public static、`EditorApplication`、`SessionState`、入口の設定の setter は reflection）。git・process の起動・削除・Editor の接続の code を書かない | 引数は S4-2 の driver と同じ形（`-RunId`、`-Only`、`-DryRun`、`-Commit`、任意の `-XRoot`・`-YRoot`） | 次の行の offline test（走査と `-DryRun`） | 0 → 約 200 |
| `tools/Harness/Scenario.psm1`（と配下の `ScenarioEditor.psm1`） | （既存）実機の検証の基盤 | — | — | — | `Wait-ScenarioEditor` に任意の `-QuietMs` を足す（最初の読む `eval` を、基盤の時計で `QuietMs` が過ぎるまで送らない。Play の移行中に `eval` を送ると Console に Error が残り、Play の record の件数に入りうるため。外部計画レビュー XR-4）。省けば今と同じ。program §9.1 の境界の規則（足りない時はその行の中で基盤を直す）に従う | 基盤の offline test に、時計の seam で `QuietMs` の間に `eval` を送らないことと、省いた時の互換を足す（`Scenario.Tests.ps1`） | 837 → 約 850 |
| `tools/Harness/Tests/ScenarioDriver.Tests.ps1` | （既存）基盤の driver の offline test | test | — | — | — | 走査（`Get-DriverScanFindings`）の列に S4-3a の driver を足す。用意した checkout での `-DryRun` が Unity を起動せずに全ての case を通る test を 1 つ足す（scenario-editor-smoke の test と同じ形）。基盤の module は変えない | 313 → 約 335 |
| `SampleGame/…/Build/PlayEntryInterceptor.cs` | （既存）入口の配線 | — | — | — | `CollectFacts` が選んだ base の記録を読んで `Select` に渡す | launcher と candidates の test で見る | 287 → 約 290 |
| `SampleGame/…/Build/PlayEntryWindow.cs` | （既存）入口の窓 | 表示 | — | — | 経路が Built の系統の時のボタン 1 つ、Cancel、直近の結果の文 | 表示は test しない（launcher の側を test） | 210 → 約 245 |

行数警報: `VerifyPlayLauncher.cs` が 499 行から 500 行を超える。足すのは publish の後の呼び出し 1 つで、検証 Play の段という今の責務の中にある。分割はこのスライスの変更理由ではないので、分けない。`ContentAcquireLauncher.cs` は、要求の組み立て・実行と取消・記録・Play の revision の確認・結果の文を持つ。どれも「利用者が始める 1 回の取得」という同じ変更理由と寿命（Editor の process の間の 1 操作）に属し、依存は port で差し替えて test できるので、分けない（S4-3c が Play の入口から取得を始める時に、取得と記録の部分を切り出すかはその行が決める。今は呼び手が 1 つで、切り出すと層の先取りになる）。他に 500 行、3 責務、50% 増に当たる file は無い。

分け方の理由: 調停（段の流れ）、止まる規則（表）、取得元の受け止め（取消と例外）を別の file にしたのは、変わる理由が違うからである（S4-3b が timeout の行を足す時は規則だけ、進捗を足す時は installer の側だけを変える）。head の読み（Runtime）と書き（Editor）を分けたのは、読みは全ての取得元が使いうる形の規則で、書きは publish する host の file system の操作だからである。`PlayEntryRecovery` は変えない（回復の値は test が完全一致で見ており、record にも書かれる）。取得のボタンは、経路が Built の系統の時に窓がいつも出す。

**head の file の形:** 置き場所は取得元の root の `.origin/<contentSet>/<target>.json`（contentSet と target は segment の検査を通った値。`.staging` と同じく、revision の directory と紛れない名前）。中身は `version`（1）・`contentSet`・`target`・`revision`・`manifestSha256`・`path`（root からの相対の directory で、その下に `transport.json` と `content/` がある）。上限は 64 KiB。読みの規則は `ContentPurposeIndex` に揃える: directory の取得元が head の file の不在を自分で確かめた時と、版 0 は「head なし」、取得口が開けない（`TransportFailure`。今の取得口は「無い」と「読めない」を区別しない）時は「読めない」、版 2 以上は「未対応」、形の失敗（型、欄の欠け、相対でない path、hash の形、contentSet・target が照会と違う）は「壊れている」。「head なし」は `NotFound`、「読めない」「未対応」「壊れている」は `Unavailable` と答える（どちらも次の取得元へ進むので、段と止まる規則の結果は変わらない）。revision を指定した照会には、head の revision と同じならその entry を、違えば「root の直下の revision の名前の directory」を所在にして、`transport.json` を開けた時に、申告の digest の無い `Found` を答える（directory の取得元の配置の規約。不在を確かめた時は `NotFound`、開けない時は `Unavailable`）。時刻の欄は持たない。

**選んだ base の記録の読み方（入口の並び）:** 入口は、記録の組（revision と digest）と同じ entry の時刻を「install の時刻と、選んだ時刻の新しい方」と読み、今と同じく新しい順に検証して最初に通った物を使う。取得の後に Delivery の窓や検証 Play で別の revision を**新しく** install すれば、そちらが先になる。既にある revision の再利用は receipt の時刻を変えないので先にならない（限定した規則。base を選ぶ規則そのものは S4-3d が持つ）。記録は cache root ごとに持つ。記録の revision が消えた・digest が違う時は、記録を使わずに今の並びで答えて Warning を出す。cache root の設定を変えれば、その root の記録だけが読まれる。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序: (1) 差込口の型、head の形と読み。(2) directory の取得元と、test の側の memory の取得元・包んだ取得元。(3) 止まる規則の関数と、その表の test。(4) 取得元の受け止め。(5) 調停と、段の表の test（3 種類の取得元）。(6) head の書き手と、batch・検証 Play の段。(7) host の取得元の列、選んだ base の記録、入口の並び、取得の操作、窓。(8) 実機を伴う統合の test。(9) E 系列の driver と、基盤の offline test への追加（Phase B は offline test と `-DryRun` までを通す。Unity を起動する初回は Phase C）。
- Phase B から Phase A へ差し戻す条件: [アーキテクチャゲート](../../.agents/skills/osm-workflow/references/architecture-gates.md)の停止条件に加えて、次のどれか。asmdef の参照を足さないと置けない。`Runtime/BuildContent/Distribution/` の今ある file、`IContentProvider`・`ContentSourceKind`・`ContentAcceptanceBasis` を変えないと満たせない。`PlayEntryDecision`・`PlayEntryTransaction`・`PlayEntryRecord` を変えないと AC6・AC9 が満たせない。取消に応じない取得元を、installer を変えずに打ち切れない。調停に取得元の型の分岐が要る。止まる規則の表に無い場合が出た。
- 対象外を維持する方法: AC4・AC5・AC6 の「変えない file」を Phase B の commit の差分で確かめる。範囲の条件は「Phase B の commit が変える file」で書いた（program §8 の「差分は責務マップの file だけ」の書き方）。

## 5. テストとレビュー計画

- 単体テスト（どれも EditMode。Unity の Editor API を使わない物は `OneStarMaker.Tests`、使う物は `OneStarMaker.Tests.Editor` と `SampleGame.Tests.Editor`）:
  - `ContentOriginHeadTests`（読みの規則の表）。
  - `ContentAcquisitionRulesTests`（段と失敗の種類の全射の表。AC2）。
  - `ContentOriginGuardTests`（取消に応じない読み、例外を投げる照会・open・read。AC1・AC3）。
  - `ContentAcquisitionTests`（§2.4 の段の表の全ての行、pin、信頼、install 済み、複数の取得元、取消、片付け、消えた install。3 種類の取得元で parametrize する。AC1・AC2・AC3・AC5・AC8）。memory の取得元と、directory の取得元を別の型と id で包んだ物は test の側に置く。
  - `IndexedContentOriginTests`（head と revision の照会の表）。
  - `ContentOriginHeadWriterTests`（AC3・AC7。置き換え・待ち・時計は注入）。
  - `SampleGameContentBatchTests` の追加（AC7 の batch の段: 成功、head の失敗（本物の結果 file の確定）、確定の失敗）、`VerifyPlaySequenceTests` の追加（AC7・S1 の検証 Play: head が書かれること、書けない時に Play を止めず Warning）、`ContentBaseSelectionTests`、`PlayEntryCandidatesTests` の追加（AC6 の並び、記録の失敗経路、cache の transaction の lock を持つ間は「cache を読めない」）、`ContentAcquireLauncherTests`（AC6・S7 の結果の文、Play の間・移行の間・compile の間の拒否）。
- 統合・Unity テスト: `PublishedOriginAcquireIntegrationTests`（合成の小さい宣言を製品の coordinator で build・publish し、head → 取得 → 選んだ base の記録 → `PlayEntryCandidates.Select` がその revision を返すまで。取得で install した revision を `ContentCacheStore.ValidateInstalled` と Delivery の窓の Installed offline の経路の検証が受け付けること（AC5 の失敗経路）。build を伴うので、process の自然終了を合否にしない）。
- 差し戻し中の起点 `-Filter`: `ContentOrigin|ContentAcquisition|IndexedContentOrigin|PublishedOriginAcquire|SampleGameContentBatch|ContentBaseSelection|ContentAcquireLauncher|PlayEntryCandidates|PlayEntryDecision`（C が根拠付きで変えてよい）。
- 判定必須テスト（GO 候補 head）: 上の単体と統合の全てと、全 EditMode 回帰（AC11）。合否は XML（failed、root result、total）で読み、process の終了（自然終了・強制終了）を合否の材料にしない。XML の完成と step の後に Unity が終了で stall した時は、[Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) のとおり調べ直さず、その process だけを CIM の Terminate で止め、時刻・PID・CPU の値を note に残す（強制終了を成功の証拠に数えない。XML は採る）。XML が完成していない run は採らない。
- 全 EditMode 回帰の適用除外: なし。
- 操作・実行時・目視条件の検証経路（E 系列。改訂 1 で外部計画レビューの推奨を採り、改訂 3 で S4-V の基盤の実の契約に合わせた。§5.1）: 担当は判定 C の実行担当。環境は Windows の開発の PC、Unity 6000.6.9f1。GO 候補 head の 2 つの検証用の checkout X と Y（別の `Library`）を使う。X は publish する checkout、Y は取得して Play する checkout。実行は `pwsh tools/s43a-local-origin.ps1 -RunId <id> -Commit <implementation head の SHA>` の 1 回で（続け方は約束の 8）、手で `eval` を打たない。`-Commit` は省かない（省くと driver のある checkout の HEAD になり、HANDOFF だけを変えた記録 commit で X と Y が用意されうる）。driver は Unity を起動するので sandbox の外で走らせる。checkout は基盤が `-Commit` の commit で用意する（既定の置き場は `<主の worktree の親>\osm-v\s43a-X` と `s43a-Y`）。run の root（driver のある checkout の `TestResults/scenario/s43a/<RunId>`）を、判定 evidence bundle に収録する（C' の bundle も同じ bundle から作る）。手で作った checkout を `-XRoot`・`-YRoot` で渡す時は、先に `pwsh tools/Harness/scenario-checkout.ps1 -Operation register -Path <checkout>` で登録する。Unity の起動が書き換えた tracked の file が残った checkout では基盤が止まるので、実行の担当が戻してから走らせる（Harness README「S4-2 の driver で前と変わる点」と同じ）。
  - **driver の約束:** process の起動と終了の確認、checkout の事前確認と復元、固定 patch の適用と復元、run の root と外部 root、証拠と原記録の置き場、Editor の adapter（送る `eval` を送り直さない、読む `eval` を読み直す）、失敗の記録は基盤が持ち（[Harness README](../../tools/Harness/README.md)「実機の検証の scenario」）、ここに写さない。driver が持つのは S4-3a に固有の次の物だけである。
    1. **設定:** 約束の 2 の前提の確認を通った後に、Y の管理する file（`Set-ScenarioFile`）として、Editor を開く前に、`unity/UserSettings/OSMContentDelivery.json` の `cacheRoot` を run の外部 root の `cache`（`Get-ScenarioDirectory -Purpose cache`）に、`budget` を既定の 10 GiB に、`unity/UserSettings/OSMContentOrigin.json` の取得元の場所を X の `artifacts/bs2b/transport` に書く。E3・E5 は取得元の場所を書き換える。`Cleanup` の case が `Reset-ScenarioFiles` と `Remove-ScenarioDirectories` で戻す。
    2. **前提の確認（S0）:** 設定の file を書く前に、Y に `unity/UserSettings/OneStarMaker/DeveloperVariantSettings.asset` が無いこと（Variant profile の選択があると入口が Play に入らない。program の「S4-1a から移送した入力」の (7)）。あれば何も書かずに止まる。実行の担当はその file を checkout の外へ退避し（時刻と退避先を note に残す）、新しい RunId で最初から走らせ、run の後に戻す。Y の `unity/UserSettings/OSMContentBaseSelection.json` があれば写しを証拠に残す（Y は run をまたいで使い回され、git 管理外の file は残る。記録は cache root ごとなので、run ごとの新しい cache root の entry は前の run に無い）。
    3. **疎通（S0）:** P1 の build より先に、Y の窓のある Editor を起動し、読む `eval`（Unity の版と project の path）、入口の設定（RunPurpose を `spring-full`、経路を Built）を製品の setter（`PlayEntrySettings`、reflection）で変える送る `eval`、それを読み直す `eval`、終了を 1 回通す。入口の設定は `EditorUserSettings` に残るが、E1〜E6 も各 case の最初に同じ送る `eval` で書き（E6 は経路を Built + local edits）、読み直して証拠に残す（前の case や前の run の値に依らない）。
    4. **publish（P1・P2）:** X で `Invoke-ScenarioBatch -Method SampleGame.DependOnAll.Editor.Build.SampleGameContentBatch.BuildAndPublish -Arguments '-osmPurpose','spring-full' -TimeoutMs 5400000 -KillOnTimeout`。`artifacts/bs2b/batch/last-result.json` と head の file（`artifacts/bs2b/transport/.origin/<last-result.json の declaration>/StandaloneWindows64-Player.json`）の写しと更新時刻を証拠に残す。batch の開始時刻と終わり方（終了コード、期限の後の強制終了なら `forced`）は基盤の行にある。publish の成否は、全ての段が終わった証拠で読む: 自然に終わった時は process の終了コード 0、既知の終了の stall で期限の後に止めた（`forced`）時は batch の log の最後の行（head を書き終えた後の行。AC7）があること。終了コード 6（head の失敗。publish は残る）は合格にしない。どちらでも `last-result.json` だけで成功と読まない（head の失敗の後に欄つきの結果への置き換えも失敗すると、欄の無い成功の結果 file が残りうる）。加えて、head の contentSet・target・revision・path・digest、`last-result.json` の `identity`・`manifestSha256`、公開先の `transport.json` の実の SHA-256 を個別に照合する（driver は公開先の `transport.json` の SHA-256 も証拠に残す）。P1 は続けて、E3 と E5 の (b) の fixture を作る: `Get-ScenarioDirectory -Purpose origin-e3` に P1 の head の bytes を X の取得元の root からの相対 path のまま写し、`origin-e5b` に同じ head の `manifestSha256` だけを別の 64 桁の小文字の hex に変えた写しを置く（後の case が別の case の証拠を読まずに済む）。
    5. **Editor の case（E1〜E6）:** case ごとに Y の Editor を `Start-ScenarioEditor -KillOnTimeout` で起動して終える（E4 が新しい process を要るので揃える。終了の stall は基盤が期限の後に止めて `forced` と記録し、証拠は止める前に保存してある）。E1〜E5 の各 Play の前に、送る `eval` で空の Scene を開く（`EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`。開いている Scene が入口の `BuiltWithReusableScenes` で拒まれないため。入口の回復の `EmptyScene` と同じ状態）。E6 は経路 Built + local edits で、保存した Scene が開いていないと入口が `SnapshotNothingToCarry` で拒むので、空の Scene ではなく、patch が触らない保存済みの `Assets/SampleGame/OutGame/OutGameScene.unity` を `EditorSceneManager.OpenScene(<path>, OpenSceneMode.Single)` で開く（開いた Scene は入口が複製（snapshot）として載せる。patch が変える `Title.unity` を開くと、patch の編集が snapshot で運ばれて上書きの root が 0 件になり、S8 を踏まない（判定 C の 2 回目の E6）。`Title` を開かなければ、patch の編集が上書きの root になる。18 章「宣言からの Play 経路」の 9。revision 1）。E6 は Play の前に、未保存の asset の確認への先置きの応答（`SessionState` の `PlayEntryInterceptor.SaveAssetsResponseKey` に `cancel`）を置く（確認の dialog は modal で `eval` が返らなくなる。`cancel` なら tracked の file を保存せず、record が `Blocked` になって理由が残る）。E6 の body は Editor を止めてから戻る（基盤は body の後に patch を戻すので、開いた Scene の file を戻す時の modal を避ける）。`eval` で呼ぶのは製品の入口（`ContentAcquireLauncher` の `Start`・`IsRunning`・直近の結果、`EditorApplication.EnterPlaymode`・`ExitPlaymode`・`isPlaying`、Play の record を持つ `SessionState`）、入口の設定の setter、E6 の先置きの応答（`SessionState`）、Scene を開く `EditorSceneManager`、Play 中の観測の reflection（`SceneDirector` の `GetContentLoadRecord`、E6 の `AddScene`、E6 の `UICommon` の private の `_panelRoot`）、印の行の `Debug.Log` だけ。取得は、読む `eval` で直近の結果（操作の番号）を読んで保存し、送る `eval` で `Start` を 1 回送り（返った操作の番号を記録する）、`Wait-ScenarioEditor -TimeoutMs 1800000` で `IsRunning` が偽で、直近の結果の操作の番号が今回の番号になるまで読み、直近の結果を読む。`Start` の応答が失われた時は送り直さず、直近の結果の番号が前の番号の次になっていればそれを今回の操作とし、なっていなければ証拠不足として case を止める（前の結果の残り、特に同じ Editor の E5 の (a)(b) を今回の結果と取り違えない）。30 分の期限は検証の予算で、超えたら `IsRunning` のまま直近の状態と Editor の log を残し、取得が終わらなかった（AC9 の未達。§5 の未確認の (ii) と同じく製品の不備として B へ戻す）と記録する。Play は、送る `eval` で Play の record の `SessionState` の key を消してから `EnterPlaymode` を送り、`Wait-ScenarioEditor -QuietMs 45000`（移行の間は `eval` を送らない。§3 の基盤の行）で record の state が `Blocked` になるか、入った時刻（`enteredPlayTime`）から `PlayEntryRecord.StartupObservationSeconds`（15 秒）を過ぎるまで読み（`StartupFailed` になっても止めない。読む `eval` で Editor の時刻と比べる。起動の失敗の観測の窓を切らない）、record を保存し、E1・E2・E4・E5 は reflection の経路（18 章「宣言からの Play 経路」の観測の注意）で、起動の経路が directory から load する初回 Scene `Title`（payload を持つ）の `GetContentLoadRecord("Title")` を、読む `eval` で null でなくなるまで読んで（`AddScene` は送らない。期限 2 分。13 章のとおり未 load・失敗の間は null。期限までに null のままなら、load の証拠を観測できなかったと記録する）、候補の `Identity` の `Revision` と `Digest`、候補の `SourceKind`・`ProviderId`・`AcceptanceBasis` を保存してから（実際に登録して読んだ content の肯定の証拠。record の revision は入口が選んだ予定の値なので、それだけにしない。payload の無い論理 node（`HomeScene` など）は、公開の契約で記録が常に null なので観測の対象にしない（revision 1。§6 の F-C3）。E3 は Play しないので対象外）、E6 は下の「E6 の観測」を読んで保存してから、`ExitPlaymode` を送り、`Wait-ScenarioEditor -QuietMs 45000` で `isPlaying` が偽になるまで読む。Play の待ちの期限は E1〜E5 が 10 分、E6 が上書きの build を含むので 30 分で、超えたら record と log を残して期限切れと記録する。`eval` で読む Play 中の値は record と `Title` の load の record と E6 の観測だけ。**E6 の観測（revision 1。record を保存した後、この順に）:** (1) `AddScene("Title")` を送る `eval` で 1 回だけ送り（送り直さない。開いた `OutGameScene` は snapshot で載り、`Title` は自動では load されない）、`GetContentLoadRecord("Title")` を null でなくなるまで読む（期限 2 分）。(2) `UICommon` の `_panelRoot`（`UICommon` は `PanelRenderer` で UI を載せ、`UIDocument` は無い）から `pulse-button` の文字列を、空でなくなるまで読む（期限 1 分）。(3) `AddScene("HpGauge")` を 1 回だけ送り、`GetContentLoadRecord("HpGauge")` を null でなくなるまで読む（期限 2 分。`HpGauge` は `OutGameScene` の子で payload を持ち、patch が触らず、開いてもいない）。(4) `GetContentLoadRecord("OutGameScene")` を読む（開いた Scene の snapshot の記録）。(5) Play の record を読み直す（観測の後の `errorCount`・`exceptionCount`）。load の record は、候補の `Identity` の `Revision`・`Digest` と `SourceKind`・`ProviderId`・`AcceptanceBasis` を保存する。期限までに読めなかった項目は、後の項目を続けて読み、「観測できなかった」と理由を残す。log の窓の区切りとして、driver は送る `eval` の `UnityEngine.Debug.Log("[s43a] <case> <点>")` で Editor の log に印の行を書く（点は `acquire-start`（取得の `Start` の直前）、`play-start`（`EnterPlaymode` の直前）、`play-stop`（record を保存した後、`ExitPlaymode` の直前））。
    6. **証拠:** case ごとに、取得の結果、選んだ base の記録の file の写し、cache の `installed/` と `staging/` の一覧（名前と `receipt.json` の SHA-256）、関係する `receipt.json` の写し、Play の record、入口の設定、checkout の `Library` の path と `ProjectVersion.txt`、設定の file の写しを、次の case が上書きする前に `Save-ScenarioArtifact` で残す。Editor と batch の log、`eval` の code と原応答、git の状態の原出力、checkout の HEAD（`run-start`・`case-start` の行）は基盤が残し、driver は git を呼ばない（走査が拒む）。`-DryRun` では基盤の関数が値を返さないので、driver は fixture の作成と証拠の保存を飛ばす。
    7. **合否を判定しない。** 照合は判定の担当が保存された原記録で行う（下の表）。driver の exit 0 は基盤の操作が通ったことだけを示す。
    8. **続きの実行:** case の名前は `S0`・`P1`・`E1`・`P2`・`E2`・`E3`・`E4`・`E5`・`E6`・`Cleanup`（基盤の名前の規則に合わせた ASCII）。P1〜E5 は cache と head の状態を積み上げるので、S0〜E5 のどこかで止まった時は新しい RunId で最初から走らせ直す（例: E2 を走らせ直すと R2 は既に在り「install 済み」になる）。その前に、前の RunId で `-Only Cleanup` を走らせて Y の管理する file と予約と外部 root を片付ける（隔離が残っていれば、先に [Harness README](../../tools/Harness/README.md)「検証用の checkout の操作（実行の担当）」の `release` を通す。`Cleanup` が走らない時は同じ節の `unreserve` を使う）。S0 が設定の file を書く前に止まった時は片付けは要らない。E6 で止まった時は同じ RunId で `-Only E6,Cleanup`、`Cleanup` で止まった時は `-Only Cleanup` で続けてよい。判定の担当は各 case の最後の試行の証拠を使い、`case-start` の時刻で S0 < P1 < E1 < P2 < E2 < E3 < E4 < E5 < E6 を確かめる。
    9. **残るもの:** 検証用の checkout は run をまたいで使い回すので、X の `artifacts/bs2b/transport/` の run ごとの publish（2 件）と head、Y の `EditorUserSettings` の入口の設定、`OSMContentBaseSelection.json` の entry（cache root ごと）は run の後も残る。どれも検証用の checkout の中の git 管理外の file で、driver は消さない（走査が削除を拒み、基盤は `artifacts/` を所有しない）。次の run が取り違えないことは、cache root が run ごとに新しいこと、head と結果 file の更新時刻の照合、各 case の最初の入口の設定の書き込みで保つ。
  - **case の列（順に走る）:**

| case | checkout | 用意（driver） | 操作 | 保存する証拠 |
|---|---|---|---|---|
| S0 | Y | 設定の file（約束の 1）、前提の確認（約束の 2） | 疎通（約束の 3） | 読んだ値と入口の設定、`OSMContentBaseSelection.json` の写し（あれば）、`Library` の path と `ProjectVersion.txt` |
| P1 | X | 無し | batch で R1 を publish → E3・E5 (b) の fixture を作る（約束の 4） | `last-result.json` と head の file の写しと更新時刻、公開先の `transport.json` の SHA-256、2 つの fixture の head の SHA-256 |
| E1 | Y | 無し（空の cache） | 取得 → Play に入り record と `Title` の load の record を読んで止める | 取得の結果（前と後の操作の番号）、選んだ base の記録、R1 の `receipt.json`、一覧、record、load の record の `Identity` |
| P2 | X | 無し | batch で R2 を publish（head は R2 を指す） | P1 と同じ |
| E2 | Y | 無し | 取得 → Play | E1 と同じ（R2 の `receipt.json`） |
| E3 | Y | 取得元の場所を P1 が作った `origin-e3`（P1 の head の bytes だけで、revision の directory は無い。bytes の取得口を開けば失敗するので、S3 を実 Editor でも見る）にする | 取得だけ（Play しない） | 取得の結果（前と後の操作の番号）、選んだ base の記録、R1 の `receipt.json` の前後の bytes の SHA-256、前後の一覧、fixture の head の SHA-256 |
| E4 | Y | 取得元の場所を X に戻す（新しい Editor の process） | 取得せずに Play | Play の前の一覧（R1 と R2）、選んだ base の記録、record、`Title` の load の record の `Identity` |
| E5 | Y | (a) 取得元の場所を `Get-ScenarioDirectory -Purpose origin-e5a -PathOnly`（作らない）にする。(b) 取得元の場所を P1 が作った `origin-e5b` にする | (a) 取得 → (b) 取得 → 取得元の場所を X に戻して Play | 2 つの取得の結果（それぞれ前と後の操作の番号）、前後の一覧（`installed/` と `staging/`）、前後の選んだ base の記録、fixture の head、record、`Title` の load の record の `Identity` |
| E6 | Y | patch `PlayerOverlayEdits/ac01-title.patch`（`Add-ScenarioCase -Patches`。S4-2 の固定 patch をそのまま使う）。patch が触らない `OutGameScene.unity` を開き、先置きの応答を置く（約束の 5） | 経路を Built + local edits にして Play → 約束の 5 の「E6 の観測」（`AddScene` した `Title` の load の record と title の値、`AddScene` した `HpGauge` の load の record、`OutGameScene` の記録。[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」の観測の注意の reflection の経路）→ 止める | record（上書きの欄。観測の前と後）、title の値、3 つの load の record、patch の名前。拒否や上書きの build の失敗で Play に届かない時も、理由と、届かなかった観測の項目を残す |
| Cleanup | なし | 無し | Y の管理する file を戻し（`Reset-ScenarioFiles`）、外部 root を消す（`Remove-ScenarioDirectories`） | 消した cache の root |

  - **Phase B が §6 に書く値（判定の担当が照合に使う）:** SampleGame の既定の directory の取得元の id、`Start` が返す操作の番号と、直近の結果の中の番号の欄の名前、batch の最後の log の行の書式、`ContentAcquireLauncher` の直近の結果の JSON の欄の名前と、状態（取得した / install 済み / 見つからない / 整合性の失敗 / 対象が使えない / 到着点の同じ revision が違う digest / 到着点の失敗 / 取消）と出所（pin / 申告）の値の綴り、launcher の始まりと終わりの Console の行の書式、driver の各 case が保存する証拠の名前、Play の record の `SessionState` の key。
  - **判定の担当が照合する値（AC9。P1・P2 の `last-result.json` を R1・R2 の正とする）:** 前提として、P1・P2 の全ての段が終わった証拠（自然に終わった時は終了コード 0、`forced` の時は batch の log の最後の行）があり、head と `last-result.json` と公開先の `transport.json` の SHA-256 が約束の 4 のとおり食い違わず、R1 と R2 の `identity` が違い、P1・P2 の `last-result.json` と head の file の更新時刻がそれぞれの batch の開始（基盤の `batch-start` の行）より後である（前の run の結果を取り違えない）。各 case の証拠は最後の試行の物で、`case-start` の順が約束の 8 のとおりである。S0 の `OSMContentBaseSelection.json` の写しに、この run の cache root の entry が無い。E1 は、結果の状態が「取得した」、対象の組が R1 の (`identity`, `manifestSha256`)、対象を決めた取得元と bytes を得た取得元が SampleGame の既定の directory の取得元の id、出所が申告、選んだ base の記録と R1 の `receipt.json` の digest が同じ値、record の rule が `BuiltRoute`、revision が R1 の `identity`、state が `EnteredPlay`、`Title` の load の record の `Identity` の `Revision` と `Digest` が R1 の (`identity`, `manifestSha256`)、取得の結果の操作の番号が `Start` の返した番号。E2 は同じことを R2 で見て、一覧に R1 と R2 がある。E2・E3・E5 の (a)(b) も、取得の結果の操作の番号が、その取得の `Start` の返した番号（応答が失われた時は、送る前に読んだ番号の次）である。E3 は、fixture の head の SHA-256 が P1 の head の写しと同じ、状態が「install 済み」、対象の組が R1、bytes を得た取得元が空、R1 の `receipt.json` の bytes と一覧が前後で同じ、選んだ base の記録が R1 で選んだ時刻が E2 の記録より新しい。E4 は、Play の前の一覧に R1 と R2 があり、record の revision が R1 で state が `EnteredPlay`、load の record の `Identity` が R1。E5 は、(a) が「見つからない」、(b) が「到着点の同じ revision が違う digest」、`installed/` と `staging/` の一覧と選んだ base の記録が前後で同じ、record の revision が R1 で state が `EnteredPlay`、load の record の `Identity` が R1。E4・E5 の record の rule も `BuiltRoute`。E1・E2・E4・E5 の record の `errorCount`・`exceptionCount` が 0 で、Editor の log の窓に Error と Exception が無い。窓は driver の印の行で切る: E1・E2 は `acquire-start` から `play-stop` まで、E4・E5 は `play-start` から `play-stop` まで（E5 は失敗で終わる取得を窓に入れない）。`eval` の timeout が残す `Failed to handle /api/exec request` の行は、基盤の原記録（`eval` の code と原応答）で timeout した呼び出しと時刻の対応が取れる行に限って数えない。対応の取れない行と、同じ窓の製品の Error は数える（観測の失敗で製品の Error を消さない）。record の件数と state は読み替えない（XR-4。後処理で状態を直さない）。driver は移行の間に `eval` を送らない（約束の 5）ので、この行は record に入らない見込みである。それでも record の `firstFailure` がこの行で `StartupFailed` になった時は、製品の合否ではなく観測の失敗として記録し、その case を GO にも NO-GO にも数えず、待ち方を直して（基盤か driver の不備。B への差し戻し）新しい RunId で取り直す。
  - **E6 の記録（AC10。測定。revision 1 で観測の対象を改めた）:** 開いた `OutGameScene.unity` は入口が複製（snapshot）として載せ、その記録は `SourceKind=EditorSnapshot` になる。上書きの答えには数えず、snapshot の観測として別に書く。record の `overlayRoots` は入口の計画の欄で、実際の登録や load の由来ではない。上書きの root が実際に使われたかは、record の `overlayRegistered`・`overlayCheck` と、`Title` の load の record の由来（`SourceKind` と `Identity`）で読み、snapshot の観測・計画の欄・実際の由来を分けて書く。title の値（patch の token か base の文字列か）だけでは由来を決めない。由来を確かめられない観測は「観測できなかった（理由）」と書く（XR-4、外部計画レビューの修正差分の確認の残件 1）。4 つの観測は次の値で読む。(1) record の `overlayRoots` と `unbuildable`。(2) 上書きの root が手元の版で出たか: `Title` の load の record の `SourceKind` と `Identity`（record の `overlayRegistered` と同じか）、title の値。(3) 上書きに入らない root が base の record で載ったか: `HpGauge` の load の record の `SourceKind` と `Identity`（base の revision と digest か）。(4) Error の有無: 観測の後の record の `errorCount`・`exceptionCount` と、Editor の log の `play-start` から `play-stop` までの窓。この 4 つを、base の revision（R1）、X と Y の HEAD と `Library` の path と一緒に記録する。各観測は「観測した（肯定）」「観測した（否定）」「観測できなかった（理由）」のどれかで書き、観測できなかったことを否定の答えにしない。合否は付けない。AC10 を満たすのは、4 つの観測の全てが前の 2 つのどちらかで記録された時である。
- 未知の操作経路の疎通結果: Phase A で code から確かめたこと: Play ボタンの入口は batch と Test Runner の Scene を素通りするので、Play を見る case は `run-tests.ps1` の test では組めない。Enter Play Mode Options で domain reload を無効にしているので、Play への移行は `eval` の接続の reload をまたがない（§1 の現況）。窓のある Editor の起動 → ready → 読む・送る `eval` → 終了と、管理する file、外部 root、固定 patch の適用と復元は、S4-V の判定 C で実機を通った基盤の操作（program §9.1 の (a)）である。**未確認。初回確認は Phase C**（Unity の起動と batch の build を伴うため。担当は判定 C の実行担当、確認地点は S0 と P1。Phase B は `-DryRun` まで）: (i) `Invoke-ScenarioBatch` の実の使い方（§9.1 の (b)。S4-3a が最初の利用者）。(ii) 取得の操作の static の入口（Phase B が作る）を `eval` から呼び、非同期の取得が終わるまで、操作の番号で今回の操作と結び付けて読むこと。窓が前面に無い Editor では main thread の非同期の継続が Editor の tick に依る（`EditorApplication.delayCall` が idle で非フォーカスの Editor で走らなかった実測が開発の PC にある）。driver は終わりを読む `eval` で繰り返し読むので、読むたびに Editor が動く見込みである。(iii) Play 中の読む `eval`（§9.1 の (b) の「Play 中の操作」。record、`Title` の load の record、E6 の観測を読む）、移行の間に `eval` を送らない待ち（`-QuietMs`。S4-3a が基盤に足す）、E 系列の case の列。**revision 1 の時点の確認（2026-11-27）:** (i)(ii)(iii) の経路そのものは判定 C の 2 回の実行で実機を通った（batch の publish、`eval` からの取得と操作の番号、Play 中の読み、`-QuietMs`、全 case の `case-end`）。成り立たなかったのは観測の対象（記録を持てない `HomeScene`、`UIDocument` を探す title の読み、`Title.unity` を開く E6 の形）で、改めた対象は凍結の前に実 Editor で値が取れることを確かめた（§6「凍結の前の観測の経路の確かめ」と §5.1 の revision 1 の記録）。**未確認のまま残るのは、改めた driver そのものの通し**（probe は使い捨ての script で、driver ではない。初回確認は Phase C の E 系列の取り直し。Phase B は `-DryRun` と offline test まで）と、E6 の base が R1 の時の値（probe の base は X に残っていた別の publish）。**不成立の時の対応（A3 で合意する案）:** 起動・ready・batch の起動・Play 中の読みの不成立は基盤の不備として、S4-3a の中で基盤を直し（offline test を付け、§9.1 の表を直す。program §9.1 の境界の規則）、driver に迂回を書かない。判定 C の中で基盤を直すことは B への差し戻しで、発見 C と B の巡に数える。既知の Unity の終了の stall（[Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則)）は基盤の不備に数えず、期限の後の強制終了を `forced` として記録し、証拠は止める前の物を使う（調べ直さない）。(ii) で取得が読まれ続けても進まない時は、利用者が窓を離れると取得が止まるのと同じなので、製品の不備として B へ戻す（S7 の「1 つの操作」）。`eval` から取得の入口を呼べない時も、製品の入口の不備なので B へ戻す。手作業の `eval` や人の操作に置き換えて未確認を埋めない。E6 だけが上書きの build で止まる時は、AC10 を未実施として判定を保留し owner へ返す（§2.7）。E 系列は Unity の起動と build を伴うので、Phase B の後の動作確認では `未実施` とし、判定 C の実行担当が取る。
- program §9.1 の利用者表の S4-3a の行の確かめ直し（改訂 3）: S4-3a が使う基盤の操作は、batch の Unity の起動（`-executeMethod`）、窓のある Editor の adapter、Editor を開く前の設定 file の書き込み、run ごとの fixture の root に加えて、E6 の固定 patch の適用と復元と、Play 中の読む `eval` である。表の S4-3a の行にこの 2 つを足した（A3 の統合）。Editor の再起動（1 つの case の中で Editor を開き直すこと）と reload をまたぐ読み直しは使わない（E4 は別の case で新しい Editor を起動し、Play への移行は reload をまたがない）。基盤に足りない操作は無い（Play 中の読みの実の使い方は Phase C の初回で確かめる）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（Player の build、実の R2、人の観察は無い）。
- 判定 C の照合 script の方式: なし。合否は test の assertion と、E 系列の保存された原記録を判定の担当が上の表のとおり照合して決める。driver は原記録を保存するだけで照合しない（Phase B が書く git の中の script に判定 C の照合を持たせない。[判定 C の照合 script](../../.agents/skills/osm-workflow/references/review-evidence.md#判定-c-の照合-script)）。
- 機械検査: AC4・AC5・AC6 の「変えない file」の差分、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、基盤の offline test `pwsh -NoProfile -File tools/Harness/Tests/Scenario.Tests.ps1`、`ScenarioDriver.Tests.ps1`、`ScenarioUnity.Tests.ps1`（`.github/workflows/offline-checks.yml` の 3 つ。driver の走査と `-DryRun` は `ScenarioDriver.Tests.ps1`）。
- A0/A1 主担当・モデル・ベンダー: Claude Code の cloud session（Anthropic）。この session の規則で、model の名前は repository に書かない（owner への報告で伝えた）。
- C' 用に予約した担当: Anthropic と OpenAI 以外の系列（Phase A の A2 は Anthropic、外部計画レビューは OpenAI）。Google の系列は使えないので、候補は Grok（`cursor-agent`）。割り当ては owner が行う。
- 独立性の強化条件を満たせない場合の理由: Phase A の A2 は、初回の 3 件と改訂 3 の 2 件のどれも主担当と同じ vendor である（§5.1）。別の vendor の観点は、Draft PR の外部計画レビューで補う。外部計画レビュー 3 件（PR のコメント）の担当は OpenAI の model（web の ChatGPT。owner が 2026-11-25 に伝えた）（§5.1）。
- 改訂 1 の主担当: 初版と別の Claude Code の session（Anthropic。初版の会話は引き継がず、PR と本書から始めた）。
- 改訂 3（やり直し）の主担当: 初版・改訂 1・2 と別の Claude Code の desktop の session（Anthropic。前の会話は引き継がず、commit `f9eebab6` の本書と PR から始めた）。

### 5.1 A2 の独立レビューと A3 の採否

**A2 の独立レビュー（2026-11-25。3 件とも Anthropic の model の新しい session で、互いの指摘は渡していない。主担当と違う model を 2 系統使った。model の名前は owner への報告で伝えた）:**

| 観点 | 担当 | 入力版 |
|---|---|---|
| 目的レビュー | 担当 P（主担当より上位の系統の model） | 目的と利用者の 2 欄の原文だけ。sha256 `6c7411f6445c954e07409669ca92e040b7c8b830c94dcd468714b8cb6431e491` |
| A0 だけからの代替の構成（高リスクの追加。目的レビューの回答を記録した後に、同じ担当 P へ A0 を渡した） | 担当 P | 固定 base `1fd200b5ba835c833035d153579216e14cb823af` と、A0 の packet（commit `c4a57fe` の版）の copy の sha256 `9a8dc96a8805e99d32ad6806c67ad9bd1fa54cdc6c0ff8fef1e33cd4958dfeb2`。初稿は渡していない |
| アーキテクチャゲート | 担当 G（担当 P と同じ系統の model の別の session） | 固定 base 同上と、A1 の初稿（commit `0bbb3d0` の版）の copy の sha256 `73237f11266d564e0c8ffe09d8c3dbe4046530f360c166d080d663e4fbec7a58` |
| 不変条件・受け入れ条件・検証経路 | 担当 V（中位の系統の model） | 同上 |

目的レビューのやり直し用に、目的・利用者以外を読んでいない担当を 1 件残してある（未使用）。

**A3 の統合の担当: 主担当（採否の案）。確定は owner。** 目的・利用者の 2 欄は A2 と統合で変えていないので、目的レビューのやり直しは要らない。初稿からの設計の変更（下の表）は、A2 の 3 件の指摘を採った結果で、同じ担当による再レビューは行っていない（変更後の版は、Draft PR の外部計画レビューが最初の独立の目になる）。

**採否（重要度は担当が付けた値）:**

| 指摘 | 内容 | 採否と反映先 |
|---|---|---|
| P-1 | 1 つの操作で、Play の base になる所まで終わること | **採用。** 初稿は、対象が install 済みで最新の install でない時に Play が別の revision を使った。選んだ base の記録を足した（S7、AC6、§3） |
| P-2 | 失敗の時に、古い revision や cache の別の版へ自動で戻らないこと | **採用。** 対象の組を 1 回だけ決める規則にした（§2.4） |
| P-3 | publish の側の変更を作らない | **不採用。** head の書き手が無いと、取得元は directory の列挙か手入力でしか答えられない。利用者の入力は増えない（U1） |
| P-4 | 失敗の時の、既にある install と base の扱いが書かれていない | **採用。** AC1（一覧が前後で同じ）と AC6（記録を使えない時は今の並び） |
| P-5 | 曖昧な点（「目的 build」の選び方、「一致しない」の中身、理由の出し先、差込口を確定する強さ） | **本文で定義。** head の 1 件（§2.1）、段の表（§2.4）、窓と Console（§2.8）、cloud の条件の表（§2.3） |
| ALT-1 | 「無い」を例外でなく照会の答えで返す。revision を指定する照会を持つ | **採用。** `FindAsync` と `ContentOriginAnswer`（§3） |
| ALT-2 | 共有の一覧の読み直しと書き足しをやめる | **採用（形を変えて）。** 一覧ではなく、置き換えるだけの head の 1 件にした。lock と掃除が要らない |
| ALT-3 | Local の pin を、project の `UserSettings` の publish の台帳から取る | **不採用。** 別の checkout の publish（S8）を読めない。owner は Local で申告を認めた（Q-3）。pin を運ぶ経路は S4-3d |
| ALT-4 | receipt を変えない。取得の出所は別の記録に置く | **採用。** `Runtime/BuildContent/Distribution/` の今ある file を変えない（AC5） |
| ALT-5 | 取得の後に選んだ組を記録し、入口がそれを先に試す | **採用。** `ContentBaseSelection`（§3） |
| ALT-6 | pin の無い時の順の関数を呼び手が渡す。取得元ごとの期限と時計の注入を今入れる。Play の record に 2 欄を足す | **不採用。** 選ぶ規則は S4-3d、期限は S4-3b、record は S5。本書は取消の token までを持つ |
| ALT-7 | A0 の packet の記述のうち code と合わない点 8 件 | **採用。** §1 の現況と §2.6 に反映した |
| AG-1（blocker）、IV-7 | 一覧の置き換えに `ContentStagingMove` をそのまま使えない | **採用。** head の置き換えは `File.Replace` と、同じ判定・同じ上限の file 用の loop にした（§2.8）。新しい loop を持つことは **owner の裁定待ち（D-5 の 1）** |
| AG-2（blocker） | `PlayEntryRecovery` に flag を足すと、既存の test が変更なしでは通らない | **採用。** `PlayEntryDecision` を変えない。ボタンは窓がいつも出す（§3） |
| IV-1（blocker）、AG-3 | digest の違う manifest が「互換でない」に化けて、別の revision へ進む | **採用。** 対象の組を固定し、調停が manifest を先に読んで digest を照合してから欄を解釈する。振り分けを段と種類の関数にした（§2.4、`ContentAcquisitionRules`） |
| IV-2 | 失敗 code の 12 値のうち、表の行が決まらない値がある | **採用。** 全射の表（§2.4、AC2） |
| IV-3、AG-3 (c) | 読んでいる途中の IO 例外が「到着点の失敗」になり、次の取得元へ進まない | **採用。** 取得元の側の例外を「届かない」に揃える（`ContentOriginGuard`） |
| IV-4 | pin の定義と「申告は使われない」の矛盾。全てが「pin だけ」の時の結果 | **採用。** `ContentPin`、S5、段 1（§2.4） |
| IV-5、AG-7、AG-10 (1) | 到着点を先に見るか。install 済みの時の取得元の帰属。壊れた install | **採用。** 段 2、S3、AC8。壊れた install を直す操作は S4-3c |
| IV-6、AG-6 | 届かない時に古い revision へ進む。確定の失敗で登録が残る | **採用。** 段の表。head の取り消し（AC7。改訂 2 で RX-3 により、取り消しを無くして結果の確定の後に head を書く形に変えた）。古い revision へ進まないことは **owner の裁定待ち（D-5 の 3）** |
| IV-8、AG-4 | 取消に応じない取得元で、取消の後に install が公開されうる・lock が残る | **採用。** `ContentOriginGuard`、AC1、AC3 |
| IV-9 | Play の record に digest の欄が無い | **採用。** E1 の照合の相手を、結果・選んだ base の記録・receipt にした |
| IV-10、AG-5 | staging の掃除と「cache は増える一方」が code と違う。取得が古い install を消しうる | **採用。** §1 を直した。消えた install を結果に載せる（AC8）。止めないことは **owner の裁定待ち（D-5 の 5）** |
| IV-11、AG-10 (1) | cache が空でない時、取得した revision が Play の base にならない | **採用。** P-1 と同じ。入口の並びを変えることは **owner の裁定待ち（D-5 の 4）** |
| IV-12、AG-3 (d) | host の述語の評価の時点が未定 | **採用（外した）。** 述語を持たない。取得の後に、launcher が入口と同じ選択を走らせて、Play が使う revision を結果に書く（S7） |
| IV-13、IV-14、IV-18 | E1・E2 が、そのまま実行できる粒度に足りない | **採用。** §5 の E1・E2 |
| IV-15 | 適合 test が通っても、型での分岐を検出できない | **採用。** 3 種類の取得元で parametrize（§2.3、AC4、AC8） |
| IV-16、AG-8 | 一覧の登録の順・lock・未対応の版の扱いが未定 | **採用。** head は lock なしで後勝ち。未対応・壊れている head は書き換えずに失敗（AC7） |
| IV-17 | 「〜が無い」の観測と、未実施の判定 | **採用。** 前後の一覧の比較（§2.7）、AC10 の保留 |
| IV-19 | 紐付けの漏れ（N6 の逆の向き、N7 の資格情報） | **採用。** AC6、AC4 |
| IV-20、AG-13 | 起点の filter、Harness の停止規則、取得口の破棄、`ConfigureAwait` | **採用。** §5、§2.8 |
| AG-9 | 取得元の root と root schema の定数の写しが増える | **一部採用。** root は `ContentOriginSettings` に 1 つ置き、batch が使う。検証 Play の root の literal と root schema の literal の重複は残す（検証 Play の変更を 1 呼び出しに留めるため） |
| AG-10 (2) | 検証 Play の publish が一覧に載らない | **採用。** 検証 Play も head を書く。**owner の裁定待ち（D-5 の 2）** |
| AG-11 | launcher の依存と static の状態の寿命 | **採用。** §3 の launcher の行 |
| AG-12 | 「compile が示す」は正確でない | **採用。** AC4 の根拠を contract-audit の検査 5 にした |

**設計の残る弱点（A2 が挙げ、本書では直さず後続へ送った物）:** install の間ずっと cache の lock を持つので、遅い取得元では Play の入口と他の install が `Busy` になる（S4-3b）。取得は Editor の main thread の上で走る（S4-3b）。head は最新の 1 件だけで、それが取れない時に選び直す操作が無い（S4-3d）。選んだ base の記録は project ごと、cache は既定では利用者ごとなので、別の project の install が選んだ revision を消しうる（入口は Warning を出して今の並びに戻る。S4-3c）。

**外部計画レビュー 1 件目（PR #214 のコメント「追加計画レビュー：S4-2 の検証資産を S4-3a に引き継ぐ」、2026-11-25。S4-2 の最終 head `7460ff44` の code を静的に読んだレビューで、Unity での流用の確認は無い）。採否は主担当の案で、確定は owner（§2.9 の D-6）:**

| 指摘 | 内容 | 採否の案と反映先 |
|---|---|---|
| EX-1 | E1・E2 を、個別の `eval` と人の文字列の照合から、再実行できる製品の経路の driver・case・証拠に改める | **採用（形を変えて）。** §5 の E 系列と driver（§3）。Play を見る case は、`[Explicit]` の test と `run-tests.ps1` の組ではなく、driver が窓のある Editor を case ごとに起動して `eval` で操作する（入口が batch を素通りするため。§1） |
| EX-2 | X で同じ目的の R1・R2 を batch で publish し、結果と head を上書きの前に保存する | **採用。** P1・P2。E1 の後に P2 を走らせるので、E1 は製品が書いた head をそのまま使い、fixture は E3 の head の戻しだけになる |
| EX-3 | Y の取得元を X の directory にし、run ごとの空の cache と独立した設定を使う | **採用。** 設定は worktree ごとの `UserSettings`。driver が Editor を開く前に書けるよう、取得元の場所の上書きを `EditorUserSettings` から `UserSettings/OSMContentOrigin.json` に変えた（§3 の `ContentOriginSettings` の行） |
| EX-4 | case の順の候補 1〜6（R1 の取得、R2 の取得、R1 の head の戻しで install 済み、新しい process で R1 が選ばれる、取得元の破損・不在、固定 patch の上書き） | **採用。** E1〜E6。case 3 の bytes の取得口を開いた回数は、レビューが許すとおり単体（AC8）で見る。case 5 は、(a) 取得元が無い、(b) head が到着点の R1 と違う digest を申告する、にした。対象が未 install で bytes が壊れている場合は、cache に R1・R2 が既にある Y では 2 つ目の cache が要るので採らず、3 種類の取得元の単体（AC1・AC8）に任せる |
| EX-5 | case ごとに証拠を上書きの前に保存し、identity・digest は可能な限り assertion で照合する。失敗でも証拠を残し、起動の失敗・timeout・test 0 件を成功にしない | **一部採用。** 証拠の保存と、失敗を成功にしないことは driver の約束（§5 の (6)(7)）。assertion による照合は採らない。判定 C の照合を Phase B が書く git の中の script に持たせることを workflow が禁じているので、driver は原記録を保存するだけにし、照合する値は §5 の表に文章で書いた（S4-2 の driver と同じ扱い）。test 0 件の検出に当たる物は、全ての段が走った時だけ書く完了の印 |
| EX-6 | driver は clean な検証専用の worktree だけを対象にし、既定の共用 cache と開発者の checkout を変えない。patch の戻しと `git status` の確認を引き継ぐ | **採用。** §5 の driver の約束 (1)(2)(4) |
| EX-7 | D-3: S8 は同じ driver の観測の case に残し、S4-3a の取得口の合否と混ぜない | **採用。** E6 と AC10。D-3 の推奨をこの形にした |
| EX-8 | S4-2 の Player の process の監視が Editor の Play にそのまま使えるとは限らない。Play の interception を迂回せずに観測できるかを Phase A で確かめる。既存の driver を無理に汎用化せず、Player の build を持ち込まない | **採用。** code から、test の経路は interception を迂回する（batch の素通り）ことを確かめ、Editor の Play は `eval` の経路にした（§5 の疎通結果）。driver は S4-2 の物を変えずに別に書き、Player の build は持ち込まない。Editor の起動と終了の自動化は未確認で、初回確認は Phase C |

A2 の観点の再レビューの要否: 要らない（owner が 2026-11-25 に決めた。EX-1〜EX-8 の採否そのものは D-6 で裁定待ち）。主担当の理由は、製品の設計（差込口・段と止まる規則・不変条件・責務の配置）と AC1〜AC8・AC11 を変えておらず、変えたのは検証の経路（AC9 の範囲を広げ、AC10 の手順を固定した）と、設定 1 つの保存の形だからである。検証の経路そのものは外部計画レビューが最初の独立の目で、driver の code は発見 C・判定 C・C' が見る。

**外部計画レビュー 2 件目（PR #214 のコメント「外部再レビュー（改訂1 / d9a3baf4、OpenAI ChatGPT）」、2026-11-25。静的なレビュー）と 3 件目（同「追加設計提案：S4-2 の検証資産をコードとして再利用する（OpenAI ChatGPT）」）。採否は主担当の案で、確定は owner（§2.9 の D-6）。主担当は各指摘の根拠の code を読んで確かめた:**

| 指摘 | 内容 | 採否の案と反映先 |
|---|---|---|
| RX-1（P1） | installer は read の前に容量を確保して古い install を消すので、AC1 の「一覧が前後で同じ」は失敗の時に成り立たない | **採用。** `ContentInstaller.cs:93`、`ContentCacheStore.cs:186-232` で確かめた。AC1 を「対象の追加」と「結果に載った消えた install」に分け、消えた install を調停が installer の前後の差から作る（installer は変えない）。staging の片付けの失敗は診断（§1、N1、AC1） |
| RX-2（P1） | 最後の copy の後に取消を見ないので、確定点を決める。guard が読みを放棄する時の buffer、遅れた stream と例外、同期の open と破棄の契約 | **採用。** rename を確定点にし、成功の後に取消へ変えない。guard は自分の buffer で読む。遅れた stream と例外を guard が受け止める。同期の `OpenArtifacts` と `Dispose` は待たない契約（N3、段の表の取消の行、AC1、AC3）。改訂 3 の IV3-1 で、取消の境界を rename から「installer が取消を見る最後の点」に直した |
| RX-3（P1） | lock の無い確認と置き換えの head の巻き戻しは、並んだ書き手で別の publish を消しうる。確定前の head を読み手が見る | **採用（巻き戻しを無くす形）。** batch は `publish → 結果の確定` の順で、失敗の処理が公開先を消して結果を上書きする（`SampleGameContentBatch.cs:71-83,105-127`）。head をその後の最後の段で書けば、巻き戻しも確定前の head も無くなる。並んだ書き手は後勝ちとだけ明記する（AC7、§3） |
| RX-4（P1） | 「install の時刻と選んだ時刻の新しい方」は「直近の明示の操作」ではない。再利用は receipt を変えない。取得の成功と Play の選択を分け、失敗・記録の消失・保存の失敗・設定の変更・同時の install を定める | **採用。** 限定した規則として N6・S7・AC6・§3 に書き、結果の文にも出す。Delivery の窓と検証 Play にも記録を書かせる案は、変えない file（AC6）に触れ、base を選ぶ規則を持つ S4-3d の先取りになるので採らない |
| RX-5（P2） | 取得口は「無い」と「読めない」を区別しない。段 2 の後に別の操作が同じ revision を入れた時の `IntegrityMismatch` | **採用。** 見分けられない時は `Unavailable`、directory の取得元は不在を自分で確かめた時だけ `NotFound`（§3 の head の形）。`IntegrityMismatch` の時は cache を読み直し、対象が増えていれば到着点の側として扱う（段の表） |
| RX-6 | driver が最終の合否を判定しないことに同意。判定の担当が独立した照合 script を作る選択肢は残してよい | **一部採用。** 原記録は機械で読める JSON で残す。照合 script の方式は「なし」のまま（判定 C の offline 実行の方式は、資格情報と通信が遮断された環境を要し、開発の PC はそれを満たさない） |
| RX-7 | R1・R2 が違う identity で今回の新しい出力であることを確かめる。E4・E5 も state を照合する。古い `SessionState` の record を読まない | **採用。** §5 の照合する値の前提、E4・E5、driver の約束 (4)(5) |
| RX-8 | E6 は観測の完遂が条件。拒否された時も理由と届かなかった観測を残し、観測不能と否定を分ける | **採用。** §5 の case の表と E6 の記録、AC10 |
| RX-9 | P1 の build より先に Editor の短い疎通。`eval` の timeout を操作と読みで分け、二重に送らない | **採用。** 疎通と `eval` の扱いは S4-V の基盤が持ち、S4-3a は run の最初に走らせる（driver の約束 (1)） |
| RX-10 | D-2 済み、D-3 は E6、D-4 は high を支持。OpenAI を C' の未関与に数えない | **採用。** §2.9、§5 の C' の予約 |
| SX（3 件目） | S4-2 の driver・`run-tests.ps1`・`tools/unity-editor.ps1` の共通部分を小さな基盤にし、S4-2 と S4-3a の driver が使う。写してはいけない弱点の一覧と offline test | **採用（独立の行にする形）。** owner の決定で S4-V にした。要求は S4-3a の都合ではなく program §9.1 の利用者表（S4-2〜S4-4、S5）から決め、各行の driver は基盤の外に process と worktree の扱いを書かない（§9.1 の境界の規則）。弱点の一覧と offline test の項目は S4-V の A0 の入力 |

**改訂 3（Phase A のやり直し、2026-11-26）の変更点（改訂 2 から）:** 製品の設計（§2.3・§2.4、差込口・段と止まる規則・不変条件・責務の配置）と AC1〜AC8・AC10・AC11 は変えていない。

1. base を `9e7d4148` に取り直した（§0、§1。製品の file は改訂 2 のまま）。§1 の Play の入口の行に、domain reload を無効にした Enter Play Mode Options と、入口の設定が `EditorUserSettings` に残ることを足した。S4-2 の判定の資産の行を、S4-V の基盤の行に置き換えた。
2. §3: driver の行を、基盤の公開関数だけを使う scenario `s43a` に書き直した。基盤の offline test（`ScenarioDriver.Tests.ps1`）の走査の列への追加と `-DryRun` の test の行を足した。`ContentOriginSettings` が取得の開始のたびに file を読むことを足した。
3. §5 の E 系列: driver の約束を基盤の関数で書き直し、基盤が持つ事を写さないようにした。S0（前提の確認と、P1 の build の前の Editor の疎通）と片付けの case を足した。E3 の用意を「X の head を P1 の bytes に戻す」から「P1 の head の bytes だけを置いた fixture の root を取得元にする」に変えた（X の公開物を書き換えず、bytes の取得口を開けば失敗するので S3 を実 Editor でも見る。AC9 の (b) の文を合わせた）。E4 の前に取得元の場所を X に戻すことを明記した。未知の操作経路を (i)〜(iii) に分け、不成立の時の対応の案を書いた。program §9.1 の S4-3a の行に固定 patch の適用と復元を足すことにした（A3 で Play 中の読む `eval` も足した）。

**改訂 3 の A2（作り直した版。改訂 1〜3 の変更は独立レビューを受けていないため。owner の 2026-11-26 の指示）:** 目的・利用者の 2 欄は変えていないので、目的レビューはやり直さない。A0 は変わっていないので、A0 だけからの代替の構成もやり直さない（初回の担当 P の回答を使う）。

| 観点 | 担当 | 入力版 |
|---|---|---|
| アーキテクチャゲート | 担当 G3（Anthropic の model。主担当と同じ系統の、新しい subagent。前の A2・外部計画レビューの指摘は渡していない） | 固定 checkout `f9eebab6dc54571f991760f551be46b73f0c7353`（implementation base `9e7d4148` に本 branch の HANDOFF と program の 7 行を足した commit。clean な detached worktree）と、改訂 3 の A2 の入力版の copy の sha256 `f3c07ee4b2d8a77b8126e31916a02581a4d9f4134eb49d0f8c5ab15a4358e98b`。checkout の中の古い HANDOFF は読ませていない |
| 不変条件・受け入れ条件・検証経路 | 担当 V3（同じ系統の別の新しい subagent。担当 G3 の指摘は渡していない） | 同上 |

担当は 2 件とも、読む前に HEAD・clean・copy の hash を照合して一致を報告した。

**改訂 3 の A3 の統合の担当: 主担当（採否の案）。確定は owner。** 主担当は、指摘の根拠の code を読んで確かめた（AG3-1・IV3-4 の `SampleGameContentBatch.cs:71-83,140-152`、AG3-5 の `ContentInstaller.cs:33-36,118-127`、IV3-1 の `ContentInstaller.cs:100-116`、IV3-2 の `ContentDeliveryFiles.cs:111,148`・`ContentCacheStore.cs:351-355`・`InstalledRevisionVerifier.cs:82-95`、IV3-9 の `PlayEntryRecord.cs:12,27,271-277`、IV3-21 の `PlayEntryDecision.cs:219-224,251-253`）。目的・利用者の 2 欄は変えていない。A2 の観点の再レビューは行わない（owner が 2026-11-26 に決めた。§2.9 の D-7）。

**採否（重要度は担当が付けた値）:**

| 指摘 | 内容 | 採否の案と反映先 |
|---|---|---|
| AG3-1（major）、IV3-4（major） | 結果 file の確定は上書きしない `File.Move` なので、head の失敗の後に `Fail` が失敗の結果を書けず、成功の結果 file が消した公開先を指したまま残る。偽の `Commit` の test は検出しない。head の失敗の終了コードが無い | **採用。** 確定を置き換えられる形にし、`ExitOriginHeadFailed`（6）を足し、本物の確定で test する（AC7、§3 の batch の行） |
| AG3-2 | 取得元の root が 3 か所で別々に導かれる。batch が host の設定に依存する | **採用。** writer を `Write(publishedDirectory)` にし、root を公開先の親に固定した。初回の AG-9 の一部採用（root を `ContentOriginSettings` に置いて batch が使う）はこれで置き換える（§3） |
| AG3-3 | directory の取得元が不在を確かめる口が constructor に無い | **採用。** 開く関数を stream・不在・読めないの 3 値にした（§3） |
| AG3-4 | `IndexedContentOrigin` を public にする理由が無い | **採用。** internal にし、public は `ContentOrigins.Directory` だけ（§3） |
| AG3-5、IV3-2 の一部 | guard が installer へ返す例外の型が契約に無く、他の型は `IoFailure`（到着点の側）に化ける | **採用。** `TransportFailure` と `OperationCanceledException` に限ると §3 の guard の行に書いた |
| AG3-6 | 並んだ最初の書き込みで `File.Move` が「既にある」で失敗し、batch が自分の公開先を消す | **採用。** 「既にある」の時は置き換えでやり直す（§2.8、AC7） |
| AG3-7 | head の writer が hash と manifest の解析を写し、形の規則が Runtime と Editor に分かれる | **採用。** `ContentOriginHead.Create` を public に置き、writer は file の操作だけにした。`Read` に照会の contentSet・target を渡す（§3） |
| AG3-8 | launcher の 3 責務の警報の判断と port の集合が無い | **採用（非分割）。** 判断と 5 つの port を §3 に書いた。S4-3c の切り出しはその行が決める |
| AG3-9 | 入口の設定、選んだ base の記録の entry、X の publish が検証用の checkout に残る寿命が書かれていない | **採用。** 残ることを受け入れると §5 の約束の 9 に書いた。記録の entry は消す所有者を置かない（§3） |
| AG3-10、IV3-20 の一部 | driver は git を呼べないので HEAD を取れない。E3 の fixture が別の case の証拠に依る | **採用。** HEAD は基盤の行を正にした。E3・E5 (b) の fixture は P1 の中で作る（§5 の約束の 4・6） |
| AG3-11 | U5 の入力面が責務マップに無い | **採用。** 利用者が file を手で編集する（窓に欄を足さない）と §3 に書いた |
| AG3-12、IV3-12（major） | batch と Editor の期限・強制終了、既知の終了の stall の時の読み方が無い。P1・P2 の成否の読み方が無い | **採用。** `-TimeoutMs` と `-KillOnTimeout` を約束に書き、publish の成否は `last-result.json` の `exitCode` で読む。既知の stall は基盤の不備に数えず、`forced` として記録する（§5） |
| IV3-1（major） | installer は最後の読みの後に取消を見ないので、AC1 の「rename の前なら取消」は作れない | **採用。** 境界を「installer が取消を見る最後の点（最後の file の最後の読みが戻る時）」に直した（N1、段の表の取消の行、AC1） |
| IV3-2（major） | 失敗 code の振り分けが code の出所と合わない（壊れた `known-good.json` の `InvalidManifest`、staging の `MissingFile` など）。段 2 の表に無い場合がある | **採用（設計を直した）。** 振り分けを code ではなく、失敗の証拠がどちらの側から来たか（guard の観測と、guard が渡した bytes）で決め、読み直しの行を全ての code に広げ、段 2 に 2 行足した（§2.4、AC2、§3 の規則と guard の行） |
| IV3-3（major） | installer は前の staging を片付けるので、AC3 の「一覧が前と同じ」は正しい実装でも落ちる | **採用。** 「前の一覧に無かった directory が無い」に直した（AC3） |
| IV3-5（major） | head の失敗で batch が失敗することが N5 と矛盾する | **採用（明記）。** N5 の例外として書き、owner の判断に足した（§2.9 の D-5 の 6） |
| IV3-6（major） | `File.Replace` が head を backup へ移したまま失敗すると head が消える | **採用。** backup の名前つきで置き換え、失敗の後に戻す（§2.8、AC3）。Mono での動きは未確認のまま、注入で test する |
| IV3-7（major） | 選んだ時刻の表し方が無く、Kind を無視する比較で N6 を破りうる | **採用。** UTC の `O` 形式と UTC での比較、実の時計と本物の installer での失敗経路（AC6、§3） |
| IV3-8（major） | 選んだ base の記録の file が壊れた時の失敗経路が無い | **採用。** 例外を漏らさず今の並び + Warning（AC6、§3、N5） |
| IV3-9（major） | record を EnteredPlay の直後に読むと、起動の失敗の観測の窓（15 秒）を切る | **採用。** 窓を過ぎるまで読み、`Blocked`・`StartupFailed` を終わる状態にした（§5 の約束の 5） |
| IV3-10（major） | Play の移行中の `eval` の Error 行が record の `errorCount` に入りうる | **採用。** 判定の担当の読み方を照合する値に書いた（§5） |
| IV3-11（major） | E5 は失敗の取得を含み、log の窓の区切りが無い | **採用。** launcher が始まりと終わりの行を書き、E5 の窓は Play の開始からにした（§3、§5） |
| IV3-13（major） | `-Only` で続けると積み上げた状態のために期待が外れる。E6 の経路が残る | **採用。** 続けてよい case と走らせ直す規則、最後の試行と `case-start` の順の照合、各 case の最初の入口の設定の書き込み（§5 の約束の 3・8） |
| IV3-14 | 「取得の途中の Play は拒否」は installer の lock の間だけ | **採用。** AC6 を直した |
| IV3-15 | S1 と AC7 の検証 Play の食い違い | **採用。** S1 を batch と検証 Play で分けた |
| IV3-16 | 規則の関数が読み直しの結果を要る。取得元が自分で投げた取消の扱い | **採用。** 関数の入力に足し、取消は呼び手の token だけとした（AC2、§3） |
| IV3-17 | 照会と違う entry を返す取得元 | **採用。** 段 1・段 3 の行と AC2 に足した |
| IV3-18 | 「取得口を開かない」の数え方 | **採用。** AC8 の数え方は「`OpenArtifacts` が返した取得口と、その上の `OpenReadAsync`」で読む（head の読みは数えない）。§3 の `IndexedContentOrigin` の行の説明と合わせる |
| IV3-19 | E5 の hex の大小文字、無い場所を設定が拒む恐れ | **採用。** 小文字の hex、無い場所は取得元が「無い」と答える（§5、§3） |
| IV3-20 | 直近の結果の形、取得元の id の値 | **採用。** 結果を JSON の文字列でも読めるようにした（§3）。id の値は Phase B が §6 に書く |
| IV3-21 | 使い回す Y の開いている Scene、明示の設定、budget で正しい製品が拒まれうる。rule の照合が無い | **採用。** 各 Play の前に空の Scene を開き、budget も書き、照合に rule `BuiltRoute` を足した（§5）。明示の設定は rule の照合で検出する |
| IV3-22 | Play 中の `eval` が §9.1 の (b) なのに、未確認と表に入っていない | **採用。** 未確認の (iii) と、program §9.1 の S4-3a の行に足した |
| IV3-23 | NO-GO と保留の優先、E6 の一部の観測ができなかった時の隙間 | **採用。** 判定定義に書いた |
| IV3-24 | 判定 C の中で基盤を直すことの巡の数え方、動作確認での E 系列の扱い | **採用。** §5 の不成立の対応に書いた |
| IV3-25 | 取得の途中の domain reload の扱いが無い | **採用（移送）。** S4-3c へ送った（§2.6） |
| IV3-26 | 記録で並びを変えても入口の Warning の文は「latest」のまま | **採用。** 文は変えず読み方を結果の文と 18 章に書く（AC6） |
| IV3-27 | 記録の key の cache root の正規化 | **採用。** §3 の `ContentBaseSelection` の行 |
| IV3-28 | 型での分岐の検出に、振る舞いの悪い行を関数を注入した `IndexedContentOrigin` でも走らせる必要 | **採用。** §3 の `IndexedContentOrigin` の行 |

**Phase C の立場の事前の読み（2026-11-26、owner の指示。外部計画レビューの前）:** 主担当と別の新しい subagent（Anthropic）が、commit `c40db327` を Phase C の担当として読み、迷う点 17 件（PC-1〜PC-17）を挙げた。主担当は根拠の code を確かめ（E6 の `PlayEntryDecision.cs:268-275` の `SnapshotNothingToCarry`、`PlayEntryRecord.cs:202-210,266-280` の `StartupFailed`、`PlayEntryInterceptor.cs:58-64,106-107` の先置きの応答）、全件を本文に反映した: 続きの実行と予約の片付け（約束の 8）、E6 で `Title.unity` を開くことと先置きの応答（約束の 5）、`StartupFailed` と `eval` の timeout の行の読み方と、driver の印の行による log の窓（照合する値）、Phase B が §6 に書く値の列、取得と Play の待ちの期限、`run-tests.ps1` の終了の stall の時の XML の採り方、`-Commit` を省かないこと、Variant の設定の退避、失敗経路ごとの test の class、D-5 の 4 の古い言い方、証拠の列の不揃い、case の名前、機械検査の列、bundle と sandbox の外の実行、AC6 の「変更なし」の読み方。凍結の条件の意味は変えていない（E6 の開く Scene は、改訂 2 の E6 の手順のままでは入口が必ず拒む欠落を埋めた）。

**外部計画レビュー 4 件目（PR #214 のコメント「外部計画再レビュー（改訂3 / c40db327、base 9e7d4148、OpenAI ChatGPT）」と、その補足「直前更新との照合（fb0e8870 / 改訂3）」、2026-11-26。静的なレビュー）。採否は主担当の案で、確定は owner。主担当は根拠の code を読んで確かめた（`ContentInstaller.cs:60-72,100-116,130-163`、`ContentDeliveryFiles.cs:120-150`、`SampleGameContentBatch.cs:71-83,140-152`、`PlayEntryRecord.cs:202-210,266-280`、`ContentLoadRecord.cs:88-103`、`ContentIdentity.cs:20-34`）。D-7 の決定（A2 の再レビューをしない）は保ち、レビューの補足も「修正の差分の確認で足りる」とした:**

| 指摘 | 内容 | 採否の案と反映先 |
|---|---|---|
| XR-1（P1-1） | 証拠 (ii) の「installer の `IntegrityMismatch`」は staging の側の異常でも出るので、取得元の証拠にならない。上限を超える manifest の扱い。純粋な規則の test だけでは証拠を作る部分を検出できない | **採用。** 証拠 (ii) を guard が渡した bytes から自分で確かめる形にし、installer の code を証拠にしない（§2.4、§3 の guard の行）。本物の installer で、正しい bytes の後の staging の異常・上限を超える manifest・食い違う content を区別する test を足した |
| XR-2（P1-2） | 段 2 の後の競合で installer は再利用の成功を返すので「取得した」と区別が消える。前後の差は transaction の外の観測で、原因を帰属できない | **採用。** 再利用の成功は「install 済み」（bytes を得た取得元は空）にし、消えた install を観測の差と定義した。一覧の差の厳密な検査は他の書き手がいない fixture に限った（§2.4、AC1、AC8、§3） |
| XR-3（P1-3） | 結果 file を確定してから head を書くので、置き換えの失敗で成功の結果 file が残りうる。§5 が `last-result.json` だけで成否を読む。並んだ書き手と部分失敗の戻しが両立しない | **採用。** 成否の正を終了コード（`forced` の時は head の後の最後の log の行）にし、head・結果・`transport.json` を個別に照合する（AC7、§5）。Local の root は同時に 1 つの書き手を前提にし、戻しは head が無い時だけにして他の書き手の head を壊さない（§2.8、AC3、AC7）。publish の成功と head の成功を別の結果にする選択肢を D-5 の 6 に足した |
| XR-4（P1-4） | record の revision は予定の値で、実際の登録の証拠ではない。`StartupFailed` の後処理の読み替えは不可。`IsRunning` が偽なだけでは今回の操作の終わりと言えない。E6 の snapshot と上書きの観測を分ける | **採用。** `HomeScene` の load の record の `Identity` を肯定の証拠に足した。読み替えをやめ、移行の間に `eval` を送らない待ち（基盤に `-QuietMs` を足す。§3）にし、それでも入れば観測の失敗として取り直す。操作の番号で今回の取得と結果を結び付けた。E6 は snapshot と上書きを分けて記録する（§5） |
| XR-5（P2） | AC1 と AC3 の staging の片付けの失敗の扱い、取消の順を証明する signal の位置、receipt の時刻の意味 | **採用。** AC3 に AC1 と同じ例外を書き、取消の順は guard の test 用の通知で作り、「後の install」は receipt の時刻の順と書いた（AC1、AC3、AC6） |
| owner の裁定への意見 | D-1・D-3・D-4・D-6 を支持。D-5 の 6 は完了の境界を直してから裁定。D-7 は差分の再レビューで足りる | 記録した。D-5 の 6 は XR-3 の後の形で裁定を待つ |

**外部計画レビューの修正差分の確認（PR #214 のコメント「外部計画レビュー・修正差分確認と owner 裁定の推奨（fb860a60 / base 9e7d4148、OpenAI ChatGPT）」、2026-11-26）:** blocker なし。XR-1〜XR-5 の修正を支持し、D の裁定の推奨（D-5 の 6 は (b)。主担当の (a) と違う）と、凍結の前に揃える残件 4 件を挙げた。owner は推奨を全て採った（§2.9）。残件は全て反映した: (1) E 系列の load の証拠は `AddScene` を 1 回だけ送り null でなくなるまで読む、E3 は対象外、E4・E5 の証拠の列と E2・E3・E5 の操作の番号の照合、E6 の `overlayRoots` は計画の欄で実際の由来と分ける（§5）。(2) guard の hash と「短い」は EOF まで渡した時だけ確定する（§2.4、§3）。(3) head の置き換えの成功を head の段の成功の点にし、その後の掃除の失敗を `Fail` に入れない。単一の書き手の前提を AC3 に明記（AC3、AC7、§3）。(4) `eval` の timeout の行の除外を原記録で対応が取れる行に限り、製品の Error を消さない（§5）。PR 本文の古い記述を直した。`-QuietMs` は既定を変えない小さな拡張で、S4-V の互換の test を保つ（§3）。

**改訂 3 の A2 と A3 の後に残る弱点:** `File.Replace` の部分失敗と `File.Move` の上書きの扱いを Unity の Mono で実測していない（注入の test で守る）。窓が前面に無い Editor の非同期の継続と、Play の移行中の `eval` の Error 行が record に入るかは、Phase C の初回で確かめる（§5 の未確認）。

**revision 1（2026-11-27。判定 C の F-C3 を受けた Phase A の改訂。主担当は Claude Code の desktop の session（Anthropic）で、前の会話は引き継がず、引き継ぐ commit `7c32b9af` の本書と PR の状態ブロックから始めた）:**

- 範囲: §5 の観測の対象だけ（約束の 5、case の表の E1・E2・E4・E5・E6 の行、判定の担当が照合する値、E6 の記録、未知の操作経路の疎通結果）。§1 の目的・利用者、§2 の設計・不変条件・成立条件・AC1〜AC11 の本文・判定定義、§3 の責務マップ、§4 は変えていない（AC9・AC10 の本文は観測する Scene の名前を持たない）。製品の code を変える案は採らない（§6 の「直さない理由」のとおり、AC4 と `GetContentLoadRecord` の公開の契約に当たる）。
- 変更と理由:

| 項目 | 前（revision 0） | 後（revision 1） | 理由と、凍結の前の実機の確かめ |
|---|---|---|---|
| E1・E2・E4・E5 の load の肯定の証拠 | `AddScene("HomeScene")` を送り、`HomeScene` の記録を読む | `AddScene` を送らず、初回 Scene `Title` の記録を読む | `HomeScene` は payload の無い論理 node で、記録は契約上いつも null（F-C3 の原因）。`Title` は起動の経路が directory から load し、payload を持つ。probe1〜5 の case A で、取得した revision と manifest の digest、`SourceKind=ContentDirectory`・`AcceptanceBasis=VerifiedDigest` が読めた |
| E6 で開く Scene | patch を当てた `Title.unity` | patch が触らない `OutGameScene.unity` | `Title` を開くと patch の編集が snapshot で運ばれ、上書きの root が 0 件になって S8 を踏まない（判定 C の 2 回目の E6 の `overlayRoots=0`）。probe2〜5 で `overlayRoots=1`・`unbuildable` 空・`overlayCheck=matched`。空の Scene は入口が `SnapshotNothingToCarry` で拒む（probe1） |
| E6 の「上書きの root が手元の版で出たか」 | `Title` の token（由来は log の行で読む） | `AddScene("Title")` の後の `Title` の記録の `SourceKind`・`Identity` と、title の値 | probe4・5 で `SourceKind=ContentOverlay`、`Identity` が record の `overlayRegistered` と同じ、title が patch の token |
| E6 の title の読み | `UIDocument` を探す | `UICommon` の private の `_panelRoot` | `UICommon` は `PanelRenderer` で UI を載せ、`UIDocument` は無い（前の E6 の `title` が空だった理由）。probe1〜5 で base の文字列と patch の token の両方が読めた |
| E6 の「上書きに入らない root が base の record で載ったか」 | `HomeScene` の記録 | `AddScene("HpGauge")` の後の `HpGauge` の記録 | `HpGauge` は `OutGameScene` の子で payload を持ち、patch が触らない。probe5 で、base の revision と digest、`SourceKind=ContentDirectory`・`AcceptanceBasis=VerifiedDigest`。同じ probe で `ConfirmDialog` も同じ答えだった（親が `HpGauge` で 1 段深いので、対象は `HpGauge` にした） |
| E6 の Error の有無 | record の件数と log の窓 | 同じ。record は観測の後に読み直す | `AddScene` を 2 回送った後の件数を見るため。probe5 で観測の後も `errorCount`・`exceptionCount` が 0、state は `EnteredPlay` |

- probe5（この改訂の主担当が走らせた。2026-11-27）: checkout は X・Y（head `cd4a16b1`）、base は X に残っていた publish（revision `20261126T173119184Z-6e8a…`）。使い捨ての scenario（基盤の公開関数だけ。git 管理外の `TestResults/probe-s43a/probe5.ps1`）で、原記録は `TestResults/scenario/s43a/probe5`（git 管理外、未保存）。case A（取得 → 空の Scene で経路 Built の Play）と case B（固定 patch、`OutGameScene.unity` を開いて経路 Built + local edits の Play、`Title`・`HpGauge`・`ConfirmDialog` を順に `AddScene`）は attempt 1 で `case-end`、Editor は 2 回とも自然に終わった。Y は clean に戻り、外部 root は手で消した。probe は判定の入力にしない。
- 確かめていないこと: 改めた driver の通し（Phase C）。E6 の base が R1 の時の値。`HpGauge` を `AddScene` した後の画面の見た目（値と件数だけを読んだ）。
- 再レビューの要否（主担当の案。確定は owner）: **A2 の観点の再レビューと目的レビューのやり直しは行わない。** 目的・利用者の 2 欄を変えていない。変更は検証経路の観測の対象に閉じ、責務・依存・公開面・不変条件・受け入れ条件の本文を変えていない。前回の不成立は「対象が値を返すかを凍結の前に確かめていなかった」ことから出ており、静的なレビューではなく実機の確かめが当たる手当てで、それは済ませた。改めた §5 は、発見 C と判定 C の判断担当（Phase B と別の系列）が driver と突き合わせて読む。
- 採らなかった案: 論理 node にも記録を持たせる（製品の契約の変更）。E6 を別の spike の行に分ける（同じ X・Y の上で値が取れると分かったので、D-3 の決定のまま）。E1〜E5 でも `HpGauge` を足して読む（`Title` の 1 件で「実際に登録して読んだ content」の証拠になり、送る `eval` が増えるだけ）。

## 6. Phase B 実装結果

- 担当: Claude Code の desktop の session（Anthropic）。Phase A の主担当・A2 の担当とは別の session で、会話は引き継がず、起動指示の SHA `5a906078` の本書から始めた。開始時の照合（HEAD と SHA、clean、凍結記録 revision 0、票の次の担当 B、PR の状態ブロック、`workflow-task.ps1 status` の active）は全て一致した。
- 実装は §3 の責務マップのとおり（path は repo の実の置き場 `unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent/Distribution/` 等。本書の `Runtime/...` はその略）。新しい asmdef と参照は足していない。

**作った・変えた file（§3 の行と同じ順）:**

| file | 行数 | 中身 |
|---|---|---|
| ＋ `…/Distribution/ContentOrigin.cs` | 118 | `IContentOrigin`、`ContentOriginQuery`、`ContentOriginEntry`、`ContentOriginAnswer`、`ContentDigestAuthority`、`ContentOriginTrust` |
| ＋ `…/Distribution/ContentOriginHead.cs` | 219 | head の DTO、`Create`、`Read`、`RelativePath`。型の規則は raw の JSON の字面で見る（JsonUtility は型の違う値を黙って読むため） |
| ＋ `…/Distribution/IndexedContentOrigin.cs` | 147 | internal の `IndexedContentOrigin` と public の `ContentOrigins.Directory` |
| ＋ `…/Distribution/ContentAcquisition.cs` | 323 | 調停（段 1〜3、消えた install の観測、staging の片付けの診断） |
| ＋ `…/Distribution/ContentAcquisitionRules.cs` | 125 | 止まる規則の純粋な関数 |
| ＋ `…/Distribution/ContentOriginGuard.cs` | 330 | 照会と取得口・stream の受け止め、証拠 (i)(ii)、test 用の EOF の通知 |
| ＋ `…/Distribution/ContentAcquisitionTypes.cs` | 143 | 要求・pin・結果・試行の記録 |
| ＋ `Editor/Build/Content/ContentOriginHeadWriter.cs` | 177 | head の置き換え（再試行、部分失敗の戻し、並んだ最初の書き込み） |
| `SampleGame/…/SampleGameContentBatch.cs` | 202 → 251 | 最後の段の head、`ExitOriginHeadFailed`（6）、置き換えられる `CommitResult`、結果 file の `originHeadFailure` |
| `SampleGame/…/VerifyPlayLauncher.cs` | 499 → 510 | `Produce` の後の `RecordOriginHead`（書けなければ Warning） |
| ＋ `SampleGame/…/ContentOriginSettings.cs` | 57 | 既定の root と `UserSettings/OSMContentOrigin.json` の上書き |
| ＋ `SampleGame/…/ContentBaseSelection.cs` | 135 | 選んだ base の記録 |
| ＋ `SampleGame/…/ContentAcquireLauncher.cs` | 286 | 取得の操作（5 つの port） |
| `SampleGame/…/PlayEntryCandidates.cs` | 175 → 209 | `Select` の任意の引数（選んだ base の記録） |
| `SampleGame/…/PlayEntryInterceptor.cs` | 287 → 289 | `CollectFacts` が記録を読んで渡す |
| `SampleGame/…/PlayEntryWindow.cs` | 210 → 240 | 経路が Built の系統の時の「Acquire content」、Cancel、直近の結果の文 |
| ＋ `tools/s43a-local-origin.ps1` | 441 | E 系列の driver |
| `tools/Harness/Scenario.psm1`・`ScenarioEditor.psm1` | 837 → 839、168 → 178 | `Wait-ScenarioEditor -QuietMs`（省けば今と同じ） |

行数が §3 の予想を超えた file（`ContentAcquisition.cs`・`ContentOriginGuard.cs`・`ContentAcquireLauncher.cs`・driver）は、どれも 500 行未満で、責務は §3 の行のまま（増えたのは comment と、失敗の理由の文と、driver の eval の code）。分割はしていない。

**B 適応（凍結した契約と責務配置を変えない実装の詳細。Phase A は再開していない）:**

1. `ContentOriginHead.Create` は head の bytes だけでなく、その中身（`ContentOriginHeadDraft`。bytes、head の DTO、head の相対 path）を返す。writer が head の置き場（contentSet と target）を知るために manifest を読み直さずに済むため（AG3-7 の「writer は file の操作だけ」を保つ）。
2. head の失敗の後に置き換える結果 file は、成功の欄をそのまま持ち、`exitCode` を 6（process の終了コードと同じ値）、`originHeadFailure` に理由を書く。From published directory（`ValidateResultFile`）は `exitCode` が 0 でない結果 file を受け付けないので、この publish を受け取るには公開先を直接選ぶか、batch をやり直す（成否の正は終了コードという今の設計と同じ読み）。欄の名前と意味は変えていない。
3. head の置き換えの部分失敗の後に、別の書き手が head を置いていた時は、戻さずに失敗を返し、前の head は backup の名前のまま残して診断に path を書く（AC3 の「戻せなかった backup だけは診断のために残す」と同じ扱い）。
4. 取得の操作の直近の結果の状態には、調停の 8 つの状態の他に、操作が始まらなかった `Rejected`（Play の間・移行の間・compile の間、または二重の開始）と、予期しない例外の `Failed` がある。
5. `-QuietMs` の offline test は、偽の Editor と偽の時計がある `ScenarioUnity.Tests.ps1` に置いた（§3 は `Scenario.Tests.ps1` と書いていたが、同じ基盤の offline test の 3 本の 1 つで、機械検査の範囲は変わらない）。
6. 取得の結果の「消えた install」は `<contentSet>/<revision>` の文字列の列にした。
7. `ContentAcquireLauncher` に、単体 test が取得の終わりを待つための internal の `Running`（直近に始めた操作の task）を置いた。窓は `OnInspectorUpdate` で、取得の間と結果が変わった時だけ描き直す。
8. 全体の統合の test（`PublishedOriginAcquireIntegrationTests`）は `SampleGame.Tests.Editor` に置いた（host の取得元の列と入口の選択を使うため）。

**判定の担当が照合に使う値（§5 の「Phase B が §6 に書く値」）:**

- SampleGame の既定の directory の取得元の id: `samplegame-local-publish`。
- `ContentAcquireLauncher.Start()` は操作の番号（`int`、Editor の process の中で 1 ずつ増える）を返す。直近の結果の JSON の番号の欄は `operation`。`LastOperation`（直近の結果の番号、無ければ 0）と `IsRunning` も public static。読む `eval`: `return SampleGame.DependOnAll.Editor.Build.ContentAcquireLauncher.LastResultJson;`。
- batch の最後の log の行: `[SampleGameContentBatch] origin head written path=<head の絶対 path> revision=<revision> manifestSha256=<digest>`。head の失敗は Error の行 `[SampleGameContentBatch] exitCode=6 The origin head was not updated: …` で、終了コード 6。
- 直近の結果の JSON の欄: `operation`、`state`、`reason`、`cacheRoot`、`contentSet`、`target`、`revision`、`manifestSha256`、`targetOrigin`、`bytesOrigin`、`digestAuthority`、`attempts`（各要素は `stage`・`origin`・`outcome`・`reason`・`readManifest`）、`removedInstalls`、`diagnostics`、`selectionSaved`、`playRevision`、`playReason`、`warnings`、`startedUtc`、`finishedUtc`。
- `state` の綴り: 取得した `Acquired`、install 済み `AlreadyInstalled`、見つからない `NotFound`、整合性の失敗 `IntegrityFailure`、対象が使えない `TargetUnusable`、到着点の同じ revision が違う digest `ArrivalDigestConflict`、到着点の失敗 `ArrivalFailure`、取消 `Canceled`。操作が始まらなかった `Rejected`、予期しない例外 `Failed`。`digestAuthority` の綴り: pin `Pinned`、申告 `Declared`（対象が決まらなければ空）。`attempts[].stage` は `Target`・`Arrival`・`Fetch`、`outcome` は `Target`・`PinRequired`・`NotFound`・`Unavailable`・`Threw`・`MismatchedEntry`・`MissingDigest`・`Unreachable`・`DigestMismatch`・`TooLarge`・`ManifestInvalid`・`IdentityMismatch`・`TargetMismatch`・`Incompatible`・`OverBudget`・`Integrity`・`ArrivalFailure`・`ArrivedMeanwhile`・`Installed`・`AlreadyInstalled`・`Canceled`。
- launcher の Console の行: 始まり `[ContentAcquire] #<operation> start <contentSet> cache=<cache root>`、終わり `[ContentAcquire] #<operation> end state=<state> revision=<revision> play=<playRevision>`（拒否は `[ContentAcquire] #<operation> end state=Rejected <理由>`）。Warning は `[ContentAcquire] #<operation> <文>`。
- driver の印の行: `[s43a] <case> acquire-start`・`play-start`・`play-stop`（Editor の log）。
- driver が保存する証拠（`evidence/<case>/<試行>/` の下の名前）:
  - S0: `S0-selection-before.json`（あれば）、`entry-settings.txt`、`Y-ProjectVersion.txt`、`Y-OSMContentDelivery.json`、`Y-OSMContentOrigin.json`。Unity の版と project の path は case の record（`summary.identity`）。
  - P1・P2: `last-result.json`、`head.json`、`publish.json`（`status`・`exitCode`・`forced`・`logPath`・`last-result.json-utc`・`head.json-utc`・公開先の `transportSha256`）。P1 は加えて `fixtures.json`（P1 の head、`origin-e3`・`origin-e5b` の head の SHA-256 と、`origin-e5b` の申告の digest）。
  - E1・E2: `before-cache.json`、`before-selection.json`（あれば）、`before-receipt-<番号>.json` と `before-receipts.json`（receipt の写しがある時だけ。写しは `installed/` の directory の名前の順に 1 からの番号で、時点ごとに振り直す。`before-receipts.json` は写しごとの `evidence`（証拠の名前）と `installed`（`<contentSet>/<revision>`。`before-cache.json` の `name` と同じ形））、`acquire-result.json`（直近の結果の JSON）、`acquire-operation.json`（前の番号・`Start` の応答の有無・今回の番号・終わったか）、`after-acquire-*`（同じ組）、`entry-settings.txt`、`play-record.txt`（Play の record の JSON と `\n@@` の後の Editor の時刻）、`play-observations.json`、`Y-*` の 3 つ。`play-observations.json`（revision 1）の欄は `playExpired`、`playing`（`isPlaying` の読んだ値）、`loads`（Scene の名前ごとの観測。E1〜E5 は `Title` だけ）。`loads` の各要素は `scene`・`observed`（記録を読めたか）・`value`（原の `<Revision>|<Digest>|<SourceKind>|<ProviderId>|<AcceptanceBasis>`）と、読めた時は `revision`・`digest`・`sourceKind`・`providerId`・`acceptanceBasis`、読めなかった時は `reason`（期限切れ、null、Play に入っていない `not playing (isPlaying=<値>)`）。
  - E3: E1 の取得の組（Play の組は無い）。E4: `before-play-*` と Play の組。E5: `before-*`、`acquire-a-*`、`after-a-*`、`acquire-b-*`、`after-b-*`、Play の組。E6: Play の組、`play-record-after.txt`（観測の後に読み直した record。書式は `play-record.txt` と同じ）、`entry-settings.txt`。E6 の `play-observations.json` は、`loads` に `Title`・`HpGauge`・`OutGameScene` の 3 つ、`title`（`observed`・`value`（`pulse-button` の文字列）・読めなかった時の `reason`）、`addTitle`・`addHpGauge`（送った `AddScene` の `scene`・`answered`・`value`（`requested`））を持つ。観測の順は §5 の「E6 の観測」の (1)〜(5) で、`OutGameScene` は 1 回だけ読む。case の record の `summary` に `openedScene`（`Assets/SampleGame/OutGame/OutGameScene.unity`）。case の record（`step='case'`）に `summary`（入口の設定の読み直し、取得と Play の要約、Editor の終了コードと `forced`）、取得元の場所の変更ごとに `step='origin'`、各 case に `step='checkout'`（`Library` の path）。
  - `-DryRun` では証拠を保存しない（基盤の関数が値を返さないため）。
- Play の record の `SessionState` の key: `OSM.PlayEntry.Record.v1`。未保存の asset の確認の先置きの応答の key: `OSM.PlayEntry.SaveAssetsResponse.v1`。

**機械検査（Phase B で走らせた物。結果は下の「実行した確認」）:** AC4・AC5・AC6 の変えない file の差分、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、基盤の offline test 3 本（`Scenario.Tests.ps1`・`ScenarioDriver.Tests.ps1`・`ScenarioUnity.Tests.ps1`。driver の走査と `-DryRun` は `ScenarioDriver.Tests.ps1`）。

**実行した確認:**

- Editor のコンパイル確認: Unity 6000.6.9f1 の `-batchmode -nographics -quit` でこの worktree の project を開き、compile error 0、変えた file と新しい file の warning 0（既存の MessagePack の生成 code の CS0436 だけ）。窓のある Editor は、最初の import の間に test の途中の版を compile して error（`Stages` の constructor の引数の数）になり止まったので、自分で起動したその Editor だけを CIM で止め、batchmode でやり直した。1 回目の batchmode は test の public method に internal の enum を引数にした CS0051 で落ち、名前で受ける形に直した 2 回目で通った。
- AC4・AC5・AC6 の変えない file: Phase B の差分に `Runtime/ContentResolution/`・`AssetManagement/`・`SceneSystem/`・`Bootstrap/`、`Distribution/` の今ある file、`PlayEntryDecision.cs`・`PlayEntryTransaction.cs`・`PlayEntryRecord.cs`・`ContentDeliveryPlayBridge.cs`・`ContentDeliveryWindow.cs` は無い。`PlayEntryDecisionTests` と既存の `PlayEntryCandidatesTests` の method は変えていない（test の追加だけ）。
- `pwsh tools/contract-audit.ps1`: 違反なし（errors 0、warnings 0、検査 1〜9）。
- `pwsh tools/docs-audit.ps1`: errors 0（警告 1 件は program の harvest の時期の既存の物）。
- 基盤の offline test: `Scenario.Tests.ps1` 24 件 0 失敗、`ScenarioDriver.Tests.ps1` 13 件 0 失敗（S4-3a の driver の走査と、用意した 2 つの checkout での `-DryRun` が全ての case を Unity を起動せずに通り、checkout が戻ること）、`ScenarioUnity.Tests.ps1` 28 件 0 失敗（`-QuietMs` の test を含む）。

**未実行（Phase C の責任）:** §5 の単体・統合の test と全 EditMode 回帰（Unity のバッチテスト）。E 系列（P1〜E6）は Unity の起動と build を伴うので、動作確認では `未実施` とし、判定 C の実行担当が取る（§5）。

### 動作確認

- 対象 head: `2526ae39f8c0b0030b8f3abb25af85ac4f42f724`（implementation head）。この PC、Windows、2026-11-26。
- 開始時の照合: 渡された SHA と HEAD が一致、作業ツリーは clean、implementation head 欄は「この票を含む commit」で `git diff --stat HEAD^ HEAD` に `docs/handoff/` 以外の file（57 file）がある、PR #214 の状態ブロックの `引き継ぐ commit` と implementation base / head が一致した（HEAD は未 push で、状態ブロックにもそう書いてあった）。
- 条件ごとの実行担当・環境・実行したコマンド: §5 の検証経路で offline の test で閉じない条件は E 系列（S0・P1・E1・P2・E2・E3・E4・E5・E6）だけで、どれも Unity の起動と batch の build を伴う。§5 の定め（「Phase B の後の動作確認では `未実施` とし、判定 C の実行担当が取る」）のとおり実行していない。
- 生の観測と保存先・状態: なし（実行していない）。
- 検証経路の期待と違う観測と、B への差し戻し: なし。
- `未実施` の条件と理由・担当: E 系列の全 case（S0〜E6、Cleanup）。理由は Unity の起動と build を伴うこと（動作確認の対象外）。担当は判定 C の実行担当。判定 C を完了と記録する前に解消する。

### 発見 C 1 巡目の修正

- 担当: Claude Code の desktop の session（Anthropic、Claude Sonnet 5.5）、2026-11-26。起動指示の SHA `b438849d` の本書の票から始めた。開始時の照合（HEAD と SHA、clean、implementation head `2526ae39` が祖先で差分が本書だけ、PR #214 の状態ブロックの引き継ぐ commit と implementation base / head、`workflow-task.ps1 status` の active）は一致した。
- 変更（implementation head `4f0af2c4`、1 file）: `PublishedOriginAcquireIntegrationTests` が build に渡す出力 root を `TestResults/s43a-fixture` から `TestResults/bs3-fixture` へ変え、理由の comment を 1 行足した。`BuildContentCoordinator` が出力を受け付けるのは `artifacts/bs2b` とこの test 用の root の配下だけである。許可の root を広げる案は、§3 の責務マップに無い `BuildContentCoordinator` の契約を変えるので採らず、build を伴う他の統合 test と同じ root にした。
- 分類: B 適応（凍結した契約と責務配置は変えていない。製品の code と tools の変更は無く、§4 の差し戻し条件に当たらない。Phase A は再開していない）。
- compile の確認: Unity 6000.6.9f1 の `-batchmode -nographics -quit`、exit 0、log に `error CS` は無い（`TestResults/b-fix1/compile.log`）。
- Unity テスト（sandbox の外。実装 commit を切る前の、変更がこの 1 file だけの作業ツリー）: `pwsh tools/run-tests.ps1 -Filter 'ContentOrigin|ContentAcquisition|IndexedContentOrigin|PublishedOriginAcquire|SampleGameContentBatch|ContentBaseSelection|ContentAcquireLauncher|PlayEntryCandidates|PlayEntryDecision' -OutputRoot <worktree>\TestResults\b-fix1`。XML（`b-fix1/975f392f-45a6-447c-aefb-713c75903ea1/results.xml`）: total 317、passed 317、failed 0、inconclusive 0、skipped 0、root result `Passed`、14:13:12Z〜14:14:31Z。`PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase` は Passed。`step.json`: status `passed`、exitCode 0。終了の後に Unity.exe は残っていない。この実行は判定の入力にしない。原記録は git 管理外（未保存）。
- 機械検査: `pwsh tools/contract-audit.ps1` exit 0（errors 0、warnings 0）、`pwsh tools/docs-audit.ps1` は本書の記録の後に errors 0。基盤の offline test は走らせていない（`tools/` は変えていない）。
- 未実行（Phase C の責任）: 全 EditMode 回帰、E 系列（S0〜E6、Cleanup）。E 系列の動作確認は上の記録と同じ理由で `未実施` のまま。

### 判定 C の実行で見つかった driver の修正

- 担当: Claude Code の desktop の session（Anthropic、Claude Sonnet 5.5）、2026-11-27。起動指示の SHA `f57587b3` の本書の票から始めた。開始時の照合（HEAD と SHA、clean、implementation head `4f0af2c4` が祖先で差分が本書だけ、PR #214 の状態ブロックの引き継ぐ commit と implementation base / head、PR の timeline に実装の後の新しい comment と review が無いこと）は一致した。
- 変更（implementation head `cd4a16b1`、2 file）:
  - `tools/s43a-local-origin.ps1`（441 → 448 行）の `Save-YState`: receipt の写しの名前を revision の名前（長さの決まらない build の identity）から番号へ変え、番号と revision の対応を時点ごとの別の証拠に残す（名前と欄は上の「driver が保存する証拠」）。写しの bytes、cache の一覧、他の証拠の名前と中身は変えていない。長さが製品の値に依る証拠の名前はこの 1 つだけで、残りは固定の語と、driver が決める時点・checkout・取得の名前の組み合わせである。
  - `tools/Harness/Tests/ScenarioDriver.Tests.ps1`: `AC9 the S4-3a driver saves receipt copies under names within the foundation limit` を足した。driver の `Save-Evidence`・`Get-CacheListing`・`Save-YState` の定義を構文木から取り出し、基盤の `Save-ScenarioArtifact`（上限の検査を含む本物）で、実際の長さの revision の名前と 100 文字の名前を、driver が呼ぶ全ての時点について保存し、対応の file から各写しが元の receipt と同じであることを見る。
  - 基盤の file と `unity/` は変えていない。
- 分類: B 適応（凍結した契約と責務配置は変えていない。§4 の差し戻し条件に当たらない。Phase A は再開していない）。
- 実行した確認（この実行は判定の入力にしない。log は `TestResults/b-fix2-machine/`、git 管理外、未保存）:
  - 足した test は、`f57587b3` の版の driver に対しては証拠の名前の 64 文字の上限の例外で失敗し、修正後の driver では通った。
  - `Scenario.Tests.ps1` 24 件 0 失敗、`ScenarioDriver.Tests.ps1` 14 件 0 失敗、`ScenarioUnity.Tests.ps1` 28 件 0 失敗、`tools/contract-audit.ps1` exit 0。いずれも code と test が implementation head と同じ内容の作業ツリーで走らせた。`tools/docs-audit.ps1` は本書の記録の後に errors 0。
- 未実行（Phase C の責任）: E 系列の全 case、全 EditMode 回帰、GO 候補 head の機械検査。Unity は起動していない。driver の E1 の取得の後の保存より先の経路（E1 の Play の組と、P2 以降の case）は、`-DryRun` でない形では実機でも offline でも通っていない。

### 判定 C で見つかった F-C3 の修正（直していない。Phase A へ戻す）

- 担当: Claude Code の desktop の session（Anthropic、Claude Sonnet 5.5）、2026-11-27。起動指示の SHA `4bd25394` の本書の票から始めた。開始時の照合（HEAD と SHA、clean、implementation head `cd4a16b1` が祖先で差分が本書だけ、PR #214 の状態ブロックの引き継ぐ commit と implementation base / head、PR の timeline に実装の後の新しい comment と review が無いこと、`workflow-task.ps1 status -Task s4-3a-local-content-origin` の active）は一致した。
- 結果: code と test は変えていない。implementation head は `cd4a16b1` のまま。
- **原因: 凍結した §5 の観測の対象 `HomeScene` は、load の記録を持てない Scene である。** driver が呼ぶ時点や待ちの長さの誤りでも、製品の不備でもない。
  - `unity/Assets/OneStarMakerCommon/SceneMap/HomeScene.asset` は `_payloads: []`（base から変えていない）。
  - `SceneDirector.PerformUnitySceneLoad`（`SceneDirector.Loading.cs`）は、payload の無い node を loader を呼ばずに返す。組の `ContentRecord` を入れるのは `LoadThroughContentLoaderAsync` だけである。
  - 公開の契約（`SceneDirector.GetContentLoadRecord` の doc comment）は、payload の無い論理 node に null を返すと定める。`SceneDirectorContentLoaderTests.LogicalNodeWithoutPayload_DoesNotCallTheLoader` がこれを固定している。
  - したがって `AddScene("HomeScene")` が成功しても、記録は常に null になる。
- 判定担当が挙げた候補（content directory の `SceneResourceMap` の読み込みの失敗、登録の経路の不成立）の側の観測: E1 の `editor-1.log` に directory の供給元の配線（`Content resolution: wired=true`）があり、E1・E2・E4・E5 の log に初回 Scene の `Editor content directory first scene: scene=Title … stable=True` がある。
- log に `HomeScene` の行が無いことは根拠にならない。load できた `Title` の `Create TitleScene`（ZLogger の行）も E1 の log に無い。
- 未確認: E1 の `AddScene("HomeScene")` が返した task の成否（driver は返る UniTask を受けないので、証拠が無い）。上の原因は成否のどちらでも成り立つ。
- **直さない理由:** `HomeScene` は §5 の約束の 5、case の表、「判定の担当が照合する値」、E6 の記録に凍結した観測の対象で、AC9 はその表を参照し、AC10 の観測もこれで読む。対象を変えると受け入れ条件の合否の読みが変わるので、B 適応でも決定記録でもなく Phase A の再開である（[新しい事実の分類](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#新しい事実の分類)、[決定記録](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#決定記録)）。製品の側で論理 node に記録を持たせる案は、AC4（`Runtime/SceneSystem/` に差分が無い）と今の公開の契約に反するので採らない。実 Editor は起動していない（原因はコードと保存済みの原記録で決まり、直した物が無いので E1 で確かめる対象が無い）。
- 同じ調べで見つけた E6 の観測の 2 件（どちらも直していない。実機の確かめはしていない）:
  - (a) driver の `$readTitleToken` は `UIDocument` を探すが、`UICommon` は `PanelRenderer` で UI を載せる。`UIScene.unity` は `PanelRenderer` を持ち、Framework と SampleGame の runtime の code と、Scene・prefab に `UIDocument` は無い。E6 の `title` が空だったことと合い、この読みは今の形では値を返さない。driver の中で直せるが、直すのは上の改訂と同じ回にする。
  - (b) E6 の record は `overlay.state=NotRequired`、`overlayRoots=0` だった。固定 patch は `Title.uxml`・`Title.unity`・新しい uxml を変えるが、開いた `Title` は snapshot に載り、上書きの root は無かった。S8（別の `Library` の base に上書きを重ねた答え）をこの patch で踏めるかは調べていない。
- **Phase A への案（決定は Phase A と owner）:**
  - E1・E2・E4・E5: `AddScene` を送らず、起動の経路が directory から load する初回 Scene `Title`（payload を持つ）の `GetContentLoadRecord("Title")` の `Identity` を読む。送る `eval` が 1 つ減る。`HomeScene` の `AddScene` は、成功すると `OnStabledImpl` が InGame へ切り替えて `OutGameScene` の下を unload する（コードからの読み）ので残さない。
  - E6: 上書きに入らない root は、`OutGameScene`（payload を持ち、`Title` の親として load される）の記録で読む。コードからの読みで、実機では未確認。(a) の読み先の候補は `UICommon` が持つ panel の root（private の `_panelRoot`）への reflection で、未確認。(b) は S8 の測り方の問いとして改訂で扱いを決める。
  - AC9・AC10 の本文（§2.7）は観測する Scene の名前を持たないので変えず、§5 の観測の対象と driver を変える改訂にする。
  - 凍結の前に、`Title` と `OutGameScene` の記録と title の値が取れることを実 Editor で小さく確かめる（[検証経路の成立](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#検証経路の成立)）。今回の不成立は、観測の対象が記録を持てるかを凍結の前に確かめていなかったことから出た。
- 実行した確認: 本書の記録の後に `pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1`。基盤の offline test 3 本と Unity の test は、`tools/` と `unity/` を変えていないので走らせていない。
- 巡: B と発見 C の差し戻しの 3 巡目は、修正も再レビューもせずに止めた。発見 C の 4 回目は起動しない。

### 凍結の前の観測の経路の確かめ（進行役、2026-11-27）

owner の指示で、Phase A の改訂より先に、上の案の観測の経路を実 Editor で確かめた（[検証経路の成立](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#検証経路の成立)）。checkout は E 系列と同じ Y（`osm-v\s43a-Y`、head `cd4a16b1`）で、取得元は X に残っていた P2 の publish（revision `20261126T173119184Z-6e8a…`）。使い捨ての scenario（`s43a` の基盤の公開関数だけを使う。git 管理外の `TestResults/probe-s43a/probe.ps1`）で走らせ、原記録は `TestResults/scenario/s43a/probe1`〜`probe4`。採用の判断は Phase A と owner が行う。

- 経路 Built（空の Scene で Play。probe1〜4 の case A で同じ結果）: `GetContentLoadRecord("Title")` と `GetContentLoadRecord("OutGameScene")` が、取得した revision と manifest の digest、`SourceKind=ContentDirectory`・`ProviderId=registered-content-directory`・`AcceptanceBasis=VerifiedDigest` を返した。`AddScene` を送らずに読めた。`UICommon` の private の `_panelRoot` から `pulse-button` の文字列（`タイトルを揺らす`）が読めた。
- 経路 Built + local edits、固定 patch、**空の Scene**（probe1）: 入口が `Reject`（`SnapshotNothingToCarry`。保存済みの Scene が開いていないと運ぶ物が無い）。Play に入らない。
- 同、**patch が触らない `OutGameScene.unity` を開く**（probe2〜4）: `overlay.state=Built`（2 回目以降は `Reused`）、`overlayRoots=1`、`unbuildable` は空、record の `overlayRegistered` が上書きの identity、`overlayCheck=matched`、Error と例外は 0。開いた `OutGameScene` は snapshot（`SourceKind=EditorSnapshot`、`LoadPath=AlreadyLoadedScene`）で、`Title` は自動では load されなかった（2 分読んで null）。Play の後に `AddScene("Title")` を送ると（probe4）、`Title` の記録は上書きの identity、`SourceKind=ContentOverlay`・`ProviderId=registered-content-overlay`・`AcceptanceBasis=RecordedBaseMatch` で、title の値は patch の `S42-AC1-UXML-TOKEN` だった。
- 前の E6（`Title.unity` を開く）の `overlayRoots=0` の理由: patch が変える `Title.unity` と `Title.uxml` は、開いた `Title` の snapshot で運ばれ、上書きに入る root が残らない。S8 を踏むには、patch が触らない保存済みの Scene を開く。
- 確かめていないこと: 上の S8 の形で「上書きに入らない root が base の record で載ったか」（AC10 の 4 つ目）に当たる Scene。`OutGameScene` は開いたので snapshot になり、base の記録ではない。開いていない payload 付きの別の Scene を `AddScene` で読む等の形は試していない。
- probe の不具合 2 件（製品ではない）: 片付けの `Remove-ScenarioDirectories` が「外部 root に基盤の印が無い」で止まった（cache の root を `-PathOnly` で取り、作ったのは製品）。外部 root は手で消した。probe3 は PowerShell の変数の大小の衝突（switch の `$AddTitle` と code の `$addTitle`）で `AddScene` が compile に失敗し、probe4 で直した。

### revision 1 の driver の観測の修正

- 担当: Claude Code の desktop の session（Anthropic）、2026-11-27。起動指示の SHA `d8bd66b3` の本書の票から始めた。開始時の照合（HEAD と SHA、clean、implementation head 欄が `未固定` で A3 凍結記録が revision 1・次の担当 B、前の implementation head `cd4a16b1` が祖先で差分が本書だけ、PR #214 の状態ブロックの引き継ぐ commit と implementation base）は一致した。
- 変更（implementation head `346732372c19ee5cdaa8ad9b8d00495e91e6d7a5`、2 file。製品の code（`unity/`）と基盤（`tools/Harness/Scenario*.psm1`）は変えていない）:
  - `tools/s43a-local-origin.ps1`（448 → 507 行）: §5 の約束の 5・case の表・「E6 の観測」のとおりに観測を改めた。E1・E2・E4・E5 は `AddScene` を送らず、`GetContentLoadRecord("Title")` を null でなくなるまで読む（期限 2 分）。E6 は `OutGameScene.unity` を開き、(1) `AddScene("Title")` を 1 回 → `Title` の記録（2 分）、(2) `UICommon` の `_panelRoot` から `pulse-button` の文字列（1 分）、(3) `AddScene("HpGauge")` を 1 回 → `HpGauge` の記録（2 分）、(4) `OutGameScene` の記録を 1 回、(5) record の読み直し、の順に読む。load の記録は候補の `Identity` の `Revision`・`Digest` と `SourceKind`・`ProviderId`・`AcceptanceBasis` を返す。読めなかった項目は理由を残して後の項目を続け、Play に入っていない時は全ての項目に理由を残す。`AddScene` と記録の読みの eval は Scene の名前を差し込む共通の code にした（`HomeScene` 専用の code と `UIDocument` を探す読みは消した）。保存する証拠の名前と欄は上の「driver が保存する証拠」に直した。
  - `tools/Harness/tests/ScenarioDriver.Tests.ps1`: `AC9 AC10 the S4-3a driver observes the Play targets of HANDOFF section 5 in order` を足した。`-DryRun` は Play の後の観測を飛ばすので、driver の `Read-LoadRecord`・`Send-AddScene`・`Invoke-Play` と eval の code を構文木から取り出し、偽の Editor で、経路 Built が `AddScene` を送らず `Title` だけを読むこと、E6 の観測の順と欄（`Title` が期限切れでも後の項目を読み、理由を残すこと）、Play に入っていない時に Scene を要求せず理由を残すことを見る。
- 分類: B 適応（revision 1 で凍結した §5 の観測の実装。契約と責務配置は変えていない。§5 に無い観測の対象や手順は足していない）。
- 実行した確認（この実行は判定の入力にしない。Unity は起動していない）: `ScenarioDriver.Tests.ps1` 15 件 0 失敗（S4-3a の driver の走査、用意した 2 つの checkout での `-DryRun` の全 case、足した test を含む）、`Scenario.Tests.ps1` 24 件 0 失敗、`ScenarioUnity.Tests.ps1` 28 件 0 失敗、`pwsh tools/contract-audit.ps1` exit 0（違反なし）。`pwsh tools/docs-audit.ps1` は本書の記録の後に errors 0（警告 1 件は program の harvest の時期の既存の物）。
- 未実行（Phase C の責任）: 改めた driver の実機の通し（判定 C の E 系列の取り直し）、全 EditMode 回帰。eval の C# の code は Unity で compile していない（probe5 の読み方と同じ reflection だが、driver の文字列としては実機で通っていない）。E 系列の動作確認は前の記録と同じ理由で `未実施`。

## 7. Phase C

### 発見 C の実行担当の記録（1 回目、2026-11-26）

実行担当（進行役の session。判断はしない）が、発見 C の開始前に implementation head `2526ae39` で走らせた物の生の結果。原記録はこの worktree の `TestResults/c-disc1/`・`TestResults/c-disc1-machine/`（git 管理外、未保存）。

- Unity テスト: `pwsh tools/run-tests.ps1 -Filter 'ContentOrigin|ContentAcquisition|IndexedContentOrigin|PublishedOriginAcquire|SampleGameContentBatch|ContentBaseSelection|ContentAcquireLauncher|PlayEntryCandidates|PlayEntryDecision' -OutputRoot <worktree>\TestResults\c-disc1`（§5 の起点 filter をそのまま。sandbox の外）。XML（`c-disc1/e66b565d-…/results.xml`）: total 317、passed 316、failed 1、inconclusive 0、skipped 0、root result `Failed(Child)`、13:54:09Z〜13:54:37Z。`step.json`: status `failed`、exitCode 1、failure の行は「失敗したテストがあります」「XML root result が Passed ではありません」「Unity process が正常終了しませんでした: 2」。class ごとの件数: `ContentAcquireLauncherTests` 7、`ContentAcquisitionRulesTests` 121、`ContentAcquisitionTests` 41、`ContentBaseSelectionTests` 8、`ContentOriginGuardTests` 23、`ContentOriginHeadTests` 16、`ContentOriginHeadWriterTests` 11、`IndexedContentOriginTests` 5、`PlayEntryCandidatesTests` 18、`PlayEntryDecisionTests` 48、`PublishedOriginAcquireIntegrationTests` 1、`SampleGameContentBatchTests` 18。失敗した 1 件は `SampleGame.Tests.Editor.Build.PublishedOriginAcquireIntegrationTests.PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase` で、message は `System.ArgumentException : Output root must be inside artifacts/bs2b or TestResults/bs3-fixture.`、stack の先頭は `BuildContentCoordinator.Build`（`BuildContentCoordinator.cs:54`）← `PublishedOriginAcquireIntegrationTests.cs:69`。Unity の終了の stall は起きなかった（XML の約 10 秒後に step が書かれ、Unity.exe は残っていない）。
- 機械検査（どれも `pwsh -NoProfile -File <script>`、sandbox の外。log は `c-disc1-machine/<名前>.log`）: `tools/contract-audit.ps1` exit 0（「機械で判定できる契約に違反なし」、検査 1 の差分 29 file、基点 `origin/develop` = `9e7d414`）。`tools/docs-audit.ps1` exit 0（検査 1・2 に違反なし、警告 1 件は `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の頃合い）。`tools/Harness/Tests/Scenario.Tests.ps1` exit 0（24 executed, 0 failed）、`ScenarioDriver.Tests.ps1` exit 0（13 executed, 0 failed）、`ScenarioUnity.Tests.ps1` exit 0（28 executed, 0 failed）。
- 実行の前後で作業ツリーは clean のまま。

### 発見 C の判断担当の記録（1 回目、2026-11-26）

- 種別: 発見 C（判断担当）。指定された引継ぎ commit は `8ba5e901c2a311b0ab253f241422ee1d1ce95557` で、開始時の HEAD と一致した。実装の比較対象は base `9e7d414833456e2c9dd930c5f96432b24d9ee919` から implementation head `2526ae39f8c0b0030b8f3abb25af85ac4f42f724` まで。作業ツリーは記録の追記前は clean だった。
- 使用した evidence bundle: Phase B result snapshot / evidence bundle は未生成。実行担当の原記録は §7 冒頭の `TestResults/c-disc1/`・`TestResults/c-disc1-machine/`（git 管理外、未保存）だけで、追加の bundle は使用していない。
- 構造適合: base..head の差分を §2.4 の N1〜N7、S1〜S8、§2.8、§3 の責務分担に照合した。Framework Runtime の取得元・調停・guard、Framework Editor の head 書き手、SampleGame Editor の取得操作・base 記録という配置は差分の責務と整合し、取得を使わない共通側の差分も見当たらない。`PinnedOnly` と `AcceptDeclared` の意味、失敗の段と証拠の振り分け、Play の選択記録の境界はコード上に実装されている。
- 現在の問いを阻害する findings:
  - **F-C2（blocker、AC9 の検証入力不成立）:** 実行担当の XML で `PublishedOriginAcquireIntegrationTests.PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase` が 1 件 failed。テストは `PublishedOriginAcquireIntegrationTests.cs:68` で `TestResults/s43a-fixture` を渡すが、`BuildContentCoordinator.Build` は `BuildContentCoordinator.cs:50-56` で `TestResults/bs3-fixture` のみを許可するため、build 内容の検証に入る前に `ArgumentException` で終了している。これは §7 冒頭の raw result（317 total / 316 passed / 1 failed）と一致し、Unity native stall ではない。テスト側または許可 root の契約を B で整合させ、起点 filter を再実行する必要がある。
- 後続スライスへ移送する findings:
  - **F-C1（後続への入力、blocker なし）:** 最初の記録は blocker としたが、進行役が凍結文の根拠を問い、判断担当が撤回した（問いと答えの原記録は `TestResults/c-disc1-codex-q1.log`）。§2.3 の 135 行の信頼の設定は期待 digest の出所を定め、§2.4 の 190 行は段 1（対象を決める）で「『pin だけ』の取得元は『pin が要る』と残して飛ばす」と定める。197 行は段 3 を「取得元から取る（host の順に全ての取得元）」と定める。`AcceptDeclared` の head で対象を固定した後、`PinnedOnly` の取得元からも bytes を取得し、固定済みの digest で検証する今の実装（`ContentAcquisition.cs:129-145`）はこの契約に合う。後続（S4-3b）で複数の取得元を組み合わせる時の test の例として、対象を決めた取得元が `AcceptDeclared`、検証済みの bytes を出した取得元が `PinnedOnly` の case を引き継ぐ。
- F-C2 は frozen contract / AC9 を阻害するので、後続や C' に先送りしない。
- 巡: B と発見 C の差し戻しの 1 巡目（上限 4。最初の動作確認は数えない）。F-C2 を B へ返す。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 判断担当は追加の Unity test、offline test、機械検査を実行していない（§0 の指示どおり、実行担当の無改変結果だけを読んだ）。起点コマンドは §7 冒頭の `pwsh tools/run-tests.ps1 -Filter 'ContentOrigin|ContentAcquisition|IndexedContentOrigin|PublishedOriginAcquire|SampleGameContentBatch|ContentBaseSelection|ContentAcquireLauncher|PlayEntryCandidates|PlayEntryDecision' -OutputRoot <worktree>\\TestResults\\c-disc1`。この filter は §5 の Phase C 起点指定をそのまま対象にしたもの。機械検査は同じく §7 冒頭の実行担当記録（contract/docs audit と Harness offline tests）を参照した。
- テスト結果（XML 上の実行テスト名と件数）: XML は total 317、passed 316、failed 1、inconclusive 0、skipped 0、root `Failed(Child)`。失敗名は上記 F-C2 の `SampleGame.Tests.Editor.Build.PublishedOriginAcquireIntegrationTests.PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase`。機械検査は contract-audit exit 0、docs-audit exit 0、Scenario 24/24、ScenarioDriver 13/13、ScenarioUnity 28/28（いずれも実行担当記録）。
- 判定必須のうち未実行: E 系列（S0、P1、E1、P2、E2、E3、E4、E5、E6、Cleanup）の実機操作は未実行で、AC10 の 4 観測も未取得。Phase C の判断担当として GO/NO-GO は出さない。F-C1/F-C2 の修正後に B が起点 test を再実行し、別担当が E 系列を実行する必要がある。
- 重い検証を発見段階で限定実行した場合の理由と範囲: 該当なし。判断担当は静的な base..head review と、既存の実行担当記録の照合に限定した。
- 未確認事項: `PinnedOnly` を含む複数 origin の実際の取得順、F-C1 修正後の unit test、F-C2 修正後の PublishedOrigin integration test、E 系列の実 Editor / 別 Library / Built + local edits の全観測、domain reload・非前面 Editor の継続、部分失敗時の Unity Mono 実測。これらは未確認のまま C' を起動しない。
- 担当・モデル: 実行担当は進行役の別 session（判断なし、モデル記録はこの票に無い）。判断担当は本 session の OpenAI。Phase B と系列は異なるが、Phase A の外部計画レビューと同系列であり、完全な独立性ではない。この制約を明記した上で、C' 用の xAI/Grok は未使用・予約のままとする。

### 発見 C の実行担当の記録（2 回目、2026-11-26）

実行担当（進行役の session。判断はしない）が、発見 C の 2 回目の開始前に走らせた物の生の結果。HEAD は `83f42118`（implementation head `4f0af2c4` との差は `docs/handoff/` だけで、test と製品の file は同じ）。原記録はこの worktree の `TestResults/c-disc2/`・`TestResults/c-disc2-machine/`（git 管理外、未保存）。

- Unity テスト: `pwsh tools/run-tests.ps1 -Filter '<§5 の起点 filter、1 回目と同じ文字列>' -OutputRoot <worktree>\TestResults\c-disc2`（sandbox の外）。XML（`c-disc2/8860d970-…/results.xml`）: total 317、passed 317、failed 0、inconclusive 0、skipped 0、root result `Passed`、14:17:21Z〜14:18:08Z。`step.json`: status `passed`、exitCode 0、failure の行なし。Unity の終了の stall は起きなかった（run の後に Unity.exe は残っていない）。
- 機械検査（どれも `pwsh -NoProfile -File <script>`、sandbox の外。log は `c-disc2-machine/<名前>.log`）: `tools/contract-audit.ps1` exit 0（「機械で判定できる契約に違反なし」）。`tools/docs-audit.ps1` exit 0（検査 1・2 に違反なし、既存の警告 1 件）。`Scenario.Tests.ps1` exit 0（24 executed, 0 failed）、`ScenarioDriver.Tests.ps1` exit 0（13 executed, 0 failed）、`ScenarioUnity.Tests.ps1` exit 0（28 executed, 0 failed）。
- 実行の前後で作業ツリーは clean のまま。

### 発見 C の判断担当の記録（2 回目、2026-11-26）

- 種別: 発見 C（判断担当）。指定された引継ぎ commit は `ab4b3d860dd15d1f223a73516a814fb944dde2c` で、開始時の HEAD と一致した。implementation base は `9e7d414833456e2c9dd930c5f96432b24d9ee919`、implementation head は `4f0af2c412c1eb6f845aafce585dda0956de62da`。開始時の作業ツリーは clean だった。
- 使用した evidence bundle: Phase B result snapshot / evidence bundle は未生成。round 2 の実行担当の原記録は §7 の `TestResults/c-disc2/`・`TestResults/c-disc2-machine/`（git 管理外、未保存）だけで、追加の bundle は使用していない。
- 構造適合: 1 回目の review 以後の implementation 差分は `PublishedOriginAcquireIntegrationTests.cs` だけで、`BuildContentCoordinator` が許可する `TestResults/bs3-fixture` 配下へ build fixture の root を合わせた変更である。許可 root を広げる製品変更ではなく、AC9 の統合 test が build 本体へ到達できるようにする B 適応であり、§2 の不変条件・成立条件・AC・§2.8 の制約・§3 の責務分担を崩していない。`docs/handoff/` 以外の差分に Runtime / Editor の新規変更、asmdef 参照、Game → Framework 以外の依存、既存の対象外 file の変更はない。`git diff --check` も問題なし。
- 現在の問いを阻害する findings: なし。前回の **F-C2**（AC9 の入力不成立）は、fixture の出力 root を `TestResults/s43a-fixture` から既存の許可 root `TestResults/bs3-fixture` へ変更したことで解消された。round 2 の XML は `PublishedOriginAcquireIntegrationTests.PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase` を含めて failed 0 であり、前回の `ArgumentException` は再現していない。既存の `BuildContentCoordinator` の許可契約を変更していないため、凍結条件への新たな違反もない。
- 後続スライスへ移送する findings: なし（前回の F-C1 は引き続き blocker ではなく、複数 origin の `AcceptDeclared` / `PinnedOnly` の組合せを後続 S4-3b の test 例として引き継ぐ入力のまま）。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 判断担当は追加の Unity test、offline test、機械検査を実行していない（§0 の指示どおり、実行担当の無改変結果だけを読んだ）。実行担当の起点コマンドは `pwsh tools/run-tests.ps1 -Filter 'ContentOrigin|ContentAcquisition|IndexedContentOrigin|PublishedOriginAcquire|SampleGameContentBatch|ContentBaseSelection|ContentAcquireLauncher|PlayEntryCandidates|PlayEntryDecision' -OutputRoot <worktree>\\TestResults\\c-disc2`。この filter は §5 の差し戻し中の起点指定をそのまま対象にしたもの。機械検査は `tools/contract-audit.ps1`、`tools/docs-audit.ps1`、基盤 offline test 3 本で、いずれも実行担当の §7 記録を参照した。
- テスト結果（XML 上の実行テスト名と件数）: XML は total 317、passed 317、failed 0、inconclusive 0、skipped 0、root `Passed`。`PublishedOriginAcquireIntegrationTests.PublishedContent_IsAcquiredFromTheOrigin_AndBecomesThePlayBase` を含む起点 filter の対象は全て passed。`step.json` は status `passed`、exitCode 0。`tools/contract-audit.ps1` exit 0、`tools/docs-audit.ps1` exit 0（既存の警告 1 件）、`Scenario.Tests.ps1` 24/24、`ScenarioDriver.Tests.ps1` 13/13、`ScenarioUnity.Tests.ps1` 28/28。Unity の終了 stall は無く、実行前後の作業ツリーも clean のまま。
- 判定必須のうち未実行: 発見 C のため GO/NO-GO は判定しない。判定 C に必要な全 EditMode 回帰（空 filter）、E 系列（S0、P1、E1、P2、E2、E3、E4、E5、E6、Cleanup）の実 Editor 操作、AC10 の 4 観測、別 Library / Built + local edits / domain reload・非前面 Editor 継続 / 部分失敗時の Unity Mono 実測は未実行である。これらは判定 C の入力として別担当が取得する。
- 重い検証を発見段階で限定実行した場合の理由と範囲: 実行担当は §5 の差し戻し中の起点 filter と機械検査だけを実行した。判断担当は静的な base..head review と、その無改変の実行記録の照合に限定した。E 系列と全回帰は発見 C で追加実行しない。
- 未確認事項: 上記の判定必須テスト・E 系列・AC10、および `PinnedOnly` を含む複数 origin の実際の取得順は未確認のまま。F-C2 修正後の起点 filter は通過したが、これは実装全体の GO を意味しない。C' は起動しない。
- 担当・モデル: 実行担当は進行役の別 session（判断なし、モデル記録はこの票に無い）。判断担当は本 session の OpenAI。Phase B は Anthropic で、系列は異なる。Phase A の外部計画レビューと判断担当の系列が同じであるため完全な独立性ではない。C' 用の xAI/Grok は未使用・予約のままとする。

### 判定 C の実行担当の記録（1 回目、2026-11-26〜27。判定 C は未完了）

実行担当（進行役の session。判断はしない）が、GO 候補 head（implementation head `4f0af2c4`）で §5 の凍結したコマンドを走らせた生の結果。HEAD は `54644bf4`（implementation head との差は `docs/handoff/` だけ）。原記録はこの worktree の `TestResults/` の下（git 管理外、未保存）。

- (1) 全 EditMode 回帰: `pwsh tools/run-tests.ps1 -OutputRoot <worktree>\TestResults\c-judge1`（空 filter、sandbox の外）。XML（`c-judge1/7f27c297-…/results.xml`）: total 2688、passed 2628、failed 0、inconclusive 25、skipped 35、root result `Passed`、14:29:51Z〜14:44:14Z。Inconclusive の 25 件は全て `SampleGame.Tests.Rendering.Vfx.*`。`step.json`: status `failed`、exitCode 1、failure の行は「Inconclusive のテストがあります」だけ。§5 の test class の件数: `ContentOriginHeadTests` 16、`ContentAcquisitionRulesTests` 121、`ContentOriginGuardTests` 23、`ContentAcquisitionTests` 41、`IndexedContentOriginTests` 5、`ContentOriginHeadWriterTests` 11、`SampleGameContentBatchTests` 18、`VerifyPlaySequenceTests` 7、`ContentBaseSelectionTests` 8、`PlayEntryCandidatesTests` 18、`ContentAcquireLauncherTests` 7、`PublishedOriginAcquireIntegrationTests` 1。XML の完成の後に Unity（PID 40496）が終わらなかった。既知の終了の stall として調べ直さず、CIM の Terminate で止めた（時刻・CPU の値は `c-judge1/executor-note.txt`。10 秒で user の CPU 時間が約 10 秒増え、thread 1）。強制終了は成功の証拠に数えない。
- (2) E 系列: `pwsh tools/s43a-local-origin.ps1 -RunId j1 -Commit 4f0af2c412c1eb6f845aafce585dda0956de62da`（sandbox の外。checkout は基盤が `D:\repositories\unity\osm-v\s43a-X`・`s43a-Y` に新しく作った）。driver は exit 1（23:50〜00:04 JST）。S0 と P1 は `case-end` まで進み、E1 は Editor の起動・ready・`eval` の後、body が `[scenario] E1: body: 証拠の名前 は 64 文字までです: after-acquire-receipt-20261126T150018464Z-690b81d7359d46c2828215ed2fb10f46.json` で止まった。基盤は Editor の終了（`process-exit`）、復元（`restore`）、使用中の記録の解除を行い、`case-end` を書いた。E1 以降の case は走っていない。原因の位置は driver の証拠の名前 `"$Point-receipt-$($revision.Name).json"`（`tools/s43a-local-origin.ps1:165`）で、revision の名前（identity）が長いと基盤の証拠の名前の上限（64 文字）を超える。§5 の約束の 8 のとおり、続けて `-RunId j1 -Only Cleanup` を走らせ exit 0（Y の管理する file を戻し、外部 root を消した。X・Y とも clean）。原記録は `TestResults/scenario/s43a/j1/`、driver の出力は `TestResults/e-j1-driver.log`・`e-j1-cleanup.log`。
- (3) 機械検査: 未実行（E 系列が揃わないので、判定の入力を揃え直す時に GO 候補 head で走らせる）。
- 実行担当の扱い: E 系列の証拠が揃わないので判定 C を完了と記録せず、判断担当を起動していない。driver の不備は B への差し戻しで、発見 C と B の巡に数える（§5 の未知の操作経路の「不成立の時の対応」、[1 巡の定義](../../.agents/skills/osm-workflow/references/phase-c.md)）。implementation head が変わるので、この run の結果（(1) を含む）は新しい head の判定に使わない（[判定後の head 変更](../../.agents/skills/osm-workflow/references/review-evidence.md#判定後の-head-変更)）。

### 発見 C の実行担当の記録（3 回目、2026-11-27）

実行担当（進行役の session。判断はしない）の生の結果。HEAD は `69aee6ec`（implementation head `cd4a16b1` との差は `docs/handoff/` だけ）。log はこの worktree の `TestResults/c-disc3-machine/`（git 管理外、未保存）。Unity テストは票のとおり走らせていない。

- `tools/contract-audit.ps1` exit 0（最後の行「設計判断（…）は対象外。」、違反の行なし）。`tools/docs-audit.ps1` exit 0（既存の警告 1 件）。`Scenario.Tests.ps1` exit 0（24 executed, 0 failed）、`ScenarioDriver.Tests.ps1` exit 0（14 executed, 0 failed）、`ScenarioUnity.Tests.ps1` exit 0（28 executed, 0 failed）。どれも `pwsh -NoProfile -File <script>`、sandbox の外。
- 実行の前後で作業ツリーは clean のまま。

### 発見 C の判断担当の記録（3 回目、2026-11-27）

- 種別: 発見 C（判断担当）。指定された引継ぎ commit は `0618dde2fd4d21cbd86498cf7655f37a162d322b` で、開始時の HEAD と一致した。作業ツリーは開始時 clean だった。implementation base は `9e7d414833456e2c9dd930c5f96432b24d9ee919`、implementation head は `cd4a16b18dfa53cc5b607eb978f88ca46be05316`。今回の再レビュー対象は、前回の判定 C 実行で見つかった driver の不備を直した `4f0af2c412c1eb6f845aafce585dda0956de62da..cd4a16b18dfa53cc5b607eb978f88ca46be05316` の `tools/s43a-local-origin.ps1` と `tools/Harness/tests/ScenarioDriver.Tests.ps1` の 2 file（`docs/handoff/` を除く）だけである。
- 使用した evidence bundle: Phase B result snapshot / evidence bundle は未生成。round 3 の実行担当の原記録は §7 の `TestResults/c-disc3-machine/`（git 管理外、未保存）だけで、追加の bundle は使用していない。
- 構造適合: driver は `Save-YState` の receipt 写しを revision 名ではなく時点ごとの連番で保存し、`<Point>-receipts.json` に `evidence` と `installed` の対応を残す。revision 名の長さに依存する evidence 名をなくし、bytes の写しと対応関係は保っている。`ScenarioDriver.Tests.ps1` の AC9 は、実際の build identity 相当の名前と 100 文字の名前、receipt の無い directory を使い、各 `Save-YState` point について基盤の本物の保存処理、写しの対応、bytes の一致、名前の一意性を検査する。基盤の上限や `Scenario.psm1`、`unity/`、製品の Runtime / Editor code、asmdef、依存方向、凍結契約は変更されていない。`Sort-Object -Property Name` により同一時点の対応順も固定されている。§5 の driver の約束（約束 6・7）と、判定担当が照合する receipt の値を保存した証拠から読める構造に整合する。
- 現在の問いを阻害する findings: なし。前回の判定 C 実行を E1 の証拠名上限で止めた不備は、revision 名を evidence 名に埋め込まない実装と、`<Point>-receipts.json` の対応表で解消されている。round 3 の `ScenarioDriver.Tests.ps1` は 14/14 で、追加 AC9 を含めて通過した。機械検査も contract-audit exit 0、docs-audit exit 0、Scenario 24/24、ScenarioUnity 28/28 であり、round 3 の実行担当記録に新しい設計・driver blocker はない。
- 後続スライスへ移送する findings: なし。前回からの未確認事項（実機 E 系列、全 EditMode 回帰、AC10 の 4 観測、別 Library / Built + local edits / domain reload・非前面 Editor 継続、部分失敗時の Unity Mono 実測、複数 origin の実際の取得順）は判定 C の入力として残るが、今回の driver 差分の blocker ではない。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 判断担当は追加の Unity test、offline test、機械検査を実行していない（§0 の指示どおり、実行担当の無改変結果だけを読んだ）。実行担当は `pwsh -NoProfile -File tools/contract-audit.ps1`、`pwsh -NoProfile -File tools/docs-audit.ps1`、`pwsh -NoProfile -File tools/Harness/Tests/Scenario.Tests.ps1`、`pwsh -NoProfile -File tools/Harness/Tests/ScenarioDriver.Tests.ps1`、`pwsh -NoProfile -File tools/Harness/Tests/ScenarioUnity.Tests.ps1` を implementation head で実行した。`unity/` に変更がないため Unity test は §0 の ticket どおり実行対象外である。
- テスト結果（XML 上の実行テスト名と件数）: Unity XML は今回未生成。`tools/contract-audit.ps1` exit 0（違反行なし）、`tools/docs-audit.ps1` exit 0（既存の警告 1 件）、`Scenario.Tests.ps1` 24 executed / 0 failed、`ScenarioDriver.Tests.ps1` 14 executed / 0 failed、`ScenarioUnity.Tests.ps1` 28 executed / 0 failed。作業ツリーは実行前後とも clean だった。
- 判定必須のうち未実行: 発見 C では GO/NO-GO を判定しない。判定 C に必要な全 EditMode 回帰、E 系列（S0、P1、E1、P2、E2、E3、E4、E5、E6、Cleanup）の実 Editor 操作、AC10 の 4 観測、その他の上記未確認事項は未実行である。したがって今回の記録は driver 修正の阻害有無を返すもので、判定 C の完了や C' の開始を意味しない。
- 重い検証を発見段階で限定実行した場合の理由と範囲: `unity/` の implementation 差分がなく、ticket が `4f0af2c4` からの driver 差分だけを対象に指定しているため、実行担当は機械検査と offline test 3 本に限定した。判断担当はその無改変の結果と 2 file の静的差分を照合した。
- 未確認事項: 実機 E 系列で長い revision identity を含む receipt 写しが実際に保存され、`before-receipts.json` から判定担当が各 receipt の bytes と revision の対応を読めること、E1 の Play 以降の経路、全 EditMode 回帰、AC10 の 4 観測、複数 origin の実際の取得順は未確認である。これらは判定 C の入力として別担当が取得する。
- 担当・モデル: 実行担当は進行役の別 session（判断なし、モデル記録はこの票にない）。判断担当は本 session の OpenAI。Phase B は Anthropic で系列は異なるが、Phase A の外部計画レビューと発見 C の判断担当は同系列であり完全な独立性ではない。C' 用の xAI/Grok は未使用・予約のままとする。

### 判定 C の実行担当の記録（2 回目、2026-11-27）

実行担当（進行役の session。判断はしない）が、GO 候補 head（implementation head `cd4a16b1`）で §5 の凍結したコマンドを走らせた生の結果。HEAD は `59a2bf98`（implementation head との差は `docs/handoff/` だけ）。原記録はこの worktree の `TestResults/` の下（git 管理外、未保存）。どれも sandbox の外で走らせた。

- (1) 全 EditMode 回帰: `pwsh tools/run-tests.ps1 -OutputRoot <worktree>\TestResults\c-judge2`（空 filter）。XML（`c-judge2/fa971eca-1a72-4cb4-a1f2-ca291b7c2a96/results.xml`）: total 2688、passed 2628、failed 0、inconclusive 25、skipped 35、root result `Passed`、15:35:24Z〜15:49:20Z。`step.json`: status `failed`、exitCode 1。§5 の test class の件数（XML の `TestFixture` の名前で合計、failed は全て 0）: `ContentOriginHeadTests` 16、`ContentAcquisitionRulesTests` 121、`ContentOriginGuardTests` 23、`ContentAcquisitionTests` 41、`IndexedContentOriginTests` 5、`ContentOriginHeadWriterTests` 11、`SampleGameContentBatchTests` 18、`VerifyPlaySequenceTests` 13（同名の fixture の合計）、`ContentBaseSelectionTests` 8、`PlayEntryCandidatesTests` 18、`ContentAcquireLauncherTests` 7、`PublishedOriginAcquireIntegrationTests` 1。XML ができた後も Unity（PID 21356）が終わらなかった。既知の終了の stall として調べずに CIM の Terminate で止めた。時刻と CPU の値は `c-judge2/fa971eca-…/executor-note.txt` にある（10 秒で user の CPU 時間が約 10 秒増え、thread は 1）。強制終了は成功の証拠に数えない。
- (2) E 系列: `pwsh tools/s43a-local-origin.ps1 -RunId j2 -Commit cd4a16b18dfa53cc5b607eb978f88ca46be05316` は exit 0（00:54〜02:51 JST）。driver の出力は `TestResults/e-j2-driver.log`、原記録は `TestResults/scenario/s43a/j2/`（`driver.jsonl`、`base/<case>/1/`、`evidence/<case>/1/`）。S0・P1・E1・P2・E2・E3・E4・E5・E6・Cleanup は全て attempt 1 で `case-end` に達し、各 `case-end` の `base` は `ok`、`failure` は空、head は `cd4a16b1`。終わった後、X と Y の作業ツリーは clean で、外部 root `D:\ovr\s43a\j2` は無い。主な生の値（照合はしていない）:
  - P1: `publish.json` は `exitCode` -1、`forced` true。`last-result.json` は `exitCode` 0、`originHeadFailure` は空。batch の log の末尾は Unity の終了処理の行（`Found no leaked weakptrs.` など）。
  - P2: `publish.json` は `exitCode` 0、`forced` false。
  - E1・E2・E4・E5・E6 の Play: `play-observations.json` はどれも `loadObserved` false、`title` と `load` は空、`loadReason` は「HomeScene load record was not observed within 2 minutes: 状態が期限までに条件を満たしませんでした」。E1 では `AddScene("HomeScene")` を送る `eval`（`base/E1/1/editor-1-eval-024-send`）が `requested` を返したが、その後の `GetContentLoadRecord("HomeScene")` を読む `eval`（025〜061）は 2 分間ずっと空を返した。`play-record.txt` の `record.state` は `EnteredPlay`、`errorCount` は 0。Editor の log（`base/E1/1/editor-1.log`）には `Editor content directory first scene: scene=Title` の行があり、`HomeScene` の load の行と例外の行は無い。
  - E6: `play-record.txt` の `route` は `BuiltWithLocalEdits`、`overlay.state` は `NotRequired`、`snapshot` は `Title`（dirty false）。`warnings` は「Several verified revisions exist for 'spring-full'; using the latest: 20261126T155606240Z-…」で、R1 の identity を指していた。
- (3) 機械検査（implementation head、`pwsh -NoProfile -File <script>`、log は `TestResults/c-judge2-machine/`）: `tools/contract-audit.ps1` exit 0（違反の行は無い）。`tools/docs-audit.ps1` exit 0（既存の警告 1 件）。`Scenario.Tests.ps1` 24 executed / 0 failed。`ScenarioDriver.Tests.ps1` 14 executed / 0 failed。`ScenarioUnity.Tests.ps1` 28 executed / 0 failed。
- 実行担当の扱い: 合否と GO / NO-GO は書かない。(1)〜(3) は揃ったので、判断担当を起動する。

### 判定 C（判断担当、1 回目、2026-11-27）

- 種別: 判定 C（判断担当）。指定された引継ぎ commit は `6b1040bd8e1aef28467cfc27779760dc51c38496` で、開始時の HEAD と一致した。作業ツリーは開始時 clean だった。implementation base は `9e7d414833456e2c9dd930c5f96432b24d9ee919`、implementation head は `cd4a16b18dfa53cc5b607eb978f88ca46be05316`。固定対象の code / test / tools はこの implementation head のままで、今回の変更は本記録の追記だけである。
- 使用した evidence bundle: Phase B result snapshot / evidence bundle は未生成。判定 C の実行担当の原記録 `TestResults/c-judge2/`、`TestResults/scenario/s43a/j2/`、`TestResults/c-judge2-machine/` と、§5・§6 の frozen contract / 判定表だけを使用した。判定担当は追加の実行をしていない。
- 構造適合: implementation head の固定対象について、§2.3・§2.4 の取得元・調停・guard、head writer、SampleGame の取得操作・選択記録の責務配置は、§6 と前回の発見 C 記録の照合結果から継続して整合する。今回の原記録には、driver の証拠名修正を再び阻害する事実はない。機械検査も contract-audit exit 0、docs-audit exit 0（既存警告 1 件）、Scenario 24/24、ScenarioDriver 14/14、ScenarioUnity 28/28 だった。
- 現在の問いを阻害する findings:
  - **F-C3（blocker、AC9 の Play 到達証拠不成立）:** E1・E2・E4・E5・E6 の `play-observations.json` が全て `loadObserved=false`、`load` 空で、`HomeScene load record was not observed within 2 minutes` となった。E1 の raw eval では `AddScene("HomeScene")` は `requested` を返したが、その後の read が期限まで null のままで、`play-record.txt` は `EnteredPlay` / `errorCount=0` に留まる。したがって Play record の revision が存在するだけでは、§5 AC9(a)(d) が要求する実際に登録・読込された `HomeScene` の Identity の肯定証拠にならない。E6 も `BuiltWithLocalEdits`、snapshot `Title`、`overlay.state=NotRequired` は記録されたが、AC10 の `HomeScene` load と title の肯定観測は得られていない。
- AC9 の個別照合:
  - E1 は `Acquired`、R1 の revision / digest、selection、receipt、installed 一覧を確認できたが、Play の `HomeScene` load Identity が無く不成立。
  - E2 は `Acquired`、R2 の revision / digest、selection、receipt、installed 一覧を確認できたが、Play の `HomeScene` load Identity が無く不成立。
  - E3 は `AlreadyInstalled`、`bytesOrigin` 空、R1 の前後一覧・receipt が維持され、期待する再利用経路を満たす。
  - E4 は取得なしで R1 を Play record に選んだが、`HomeScene` load Identity が無く不成立。
  - E5(a) は `NotFound`、E5(b) は `ArrivalDigestConflict`、どちらも `selectionSaved=false` で、後続 Play は R1 を選んだ。しかし `HomeScene` load Identity が無く不成立。
- AC10 の扱い: E6 の record は `route=BuiltWithLocalEdits`、snapshot `Title`、`overlayRoots=0`、`unbuildable=[]`、Error/exception 0 を保持するが、§5 AC10 の 4 観測のうち `HomeScene` の base record と、上書き root の由来を肯定的に観測した記録がない。もっとも、判定定義では AC9 未達があれば NO-GO であり、AC10 の観測不能を保留扱いにして NO-GO を弱めない。
- AC11 の照合: `TestResults/c-judge2/fa971eca-1a72-4cb4-a1f2-ca291b7c2a96/results.xml` は total 2688、passed 2628、failed 0、inconclusive 25、skipped 35、root `Passed`。§5 が既知差分として許容する VFX inconclusive 25 件以外の失敗は無く、全 EditMode 回帰はこの点では阻害しない。`step.json` の status `failed` は inconclusive と既知 Unity 終了 stall の運用結果であり、XML の合否判定を置き換えない。Unity 終了 stall は XML 完成後に既知停止規則どおり terminate されたもので、成功証拠には数えていない。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 判定担当は追加の Unity test、E 系列、offline test、機械検査を実行していない。実行担当の無改変記録から、全 EditMode 回帰 `pwsh tools/run-tests.ps1 -OutputRoot <worktree>\\TestResults\\c-judge2`（空 filter）、E 系列 `pwsh tools/s43a-local-origin.ps1 -RunId j2 -Commit cd4a16b18dfa53cc5b607eb978f88ca46be05316`、および `tools/contract-audit.ps1`、`tools/docs-audit.ps1`、`Scenario.Tests.ps1`、`ScenarioDriver.Tests.ps1`、`ScenarioUnity.Tests.ps1` の結果を照合した。空 filter は AC11、E 系列は AC9・AC10、機械検査は凍結契約と driver の健全性を対象にした。
- テスト結果（原記録の実行テスト名と件数）: XML は total 2688、passed 2628、failed 0、inconclusive 25、skipped 35、root `Passed`。§5 の対象 fixture は `ContentOriginHeadTests` 16、`ContentAcquisitionRulesTests` 121、`ContentOriginGuardTests` 23、`ContentAcquisitionTests` 41、`IndexedContentOriginTests` 5、`ContentOriginHeadWriterTests` 11、`SampleGameContentBatchTests` 18、`VerifyPlaySequenceTests` 13、`ContentBaseSelectionTests` 8、`PlayEntryCandidatesTests` 18、`ContentAcquireLauncherTests` 7、`PublishedOriginAcquireIntegrationTests` 1 で、いずれも failed 0。E 系列は S0・P1・E1・P2・E2・E3・E4・E5・E6・Cleanup が全て attempt 1 の `case-end`、driver exit 0 だったが、上記 F-C3 の実測を含む。
- 判定必須のうち未達: AC9 は E1/E2/E4/E5 の `HomeScene` load Identity が得られず不成立。AC10 は 4 観測が揃わず、AC9 未達により判定を保留へは進めない。AC1〜AC8 の単体・統合テストは今回の全回帰 XML で failed 0、AC11 も既知 inconclusive を除き failed 0 だが、AC9 の blocker のため総合判定は **NO-GO**。
- 結論と差し戻し: **NO-GO**。F-C3 は §5 の AC9 を直接阻害するため、C'・Phase D・次スライスへ先送りしない。B へ戻し、`HomeScene` を実際に登録して `GetContentLoadRecord` が肯定的な Identity を返すまでの原因を、E1 を起点に調査・修正すること。修正後は implementation head を更新し、全回帰と E 系列を新しい head で再取得する。AC10 の4観測は、その再取得で AC9 が成立した後に改めて評価する。
- 重い検証を判定段階で追加実行しなかった理由と範囲: §0 の指示どおり、判定担当は原記録だけを照合し、追加実行をしなかった。E 系列は全 case が完了しており、未観測という結果そのものが製品の判定入力である。`HomeScene` load の無い記録を追加の同一 run で補完することはしない。
- 未確認事項: F-C3 の原因（content directory の `SceneResourceMap` 読込失敗または `HomeScene` 登録経路の不成立）の切り分け、AC9 再実行後の Play load Identity、AC10 の4観測、部分失敗時の Unity Mono 実測、複数 origin の実際の取得順は未確認である。判定後の head 変更が必要なため、今回の結果は `cd4a16b18dfa53cc5b607eb978f88ca46be05316` に対する記録として扱う。
- 担当・モデル: 実行担当は進行役の別 session（判断なし、モデル記録はこの票にない）。判断担当は本 session の OpenAI。Phase B は Anthropic で系列は異なるが、Phase A の外部計画レビューと発見 C の判断担当の一部が同系列であるため完全な独立性ではない。C' 用の xAI/Grok は未使用・予約のままとする。

### 発見 C の実行担当の記録（4 回目、2026-11-27）

実行担当（進行役の session。判断はしない）の生の結果。HEAD は `573a6fb9`（implementation head `34673237` との差は `docs/handoff/` だけ）。log はこの worktree の `TestResults/c-disc4-machine/`（git 管理外、未保存）。Unity テストは票のとおり走らせていない。

- `tools/contract-audit.ps1` exit 0（最後の行「設計判断（…）は対象外。」、違反の行なし）。`tools/docs-audit.ps1` exit 0（既存の警告 1 件）。`Scenario.Tests.ps1` exit 0（24 executed, 0 failed）、`ScenarioDriver.Tests.ps1` exit 0（15 executed, 0 failed）、`ScenarioUnity.Tests.ps1` exit 0（28 executed, 0 failed）。どれも `pwsh -NoProfile -File <script>`、sandbox の外。
- 実行の前後で作業ツリーは clean のまま。

### 発見 C の判断担当の記録（4 回目、2026-11-27）

- 種別: 発見 C（判断担当）。指定された引継ぎ commit は `97d56546ed0736ef44fef2bea2a4024d768523b6` で、開始時の HEAD と一致した。作業ツリーは開始時 clean だった。PR #214 は open・draft、base は `develop`、implementation base / head は `9e7d414833456e2c9dd930c5f96432b24d9ee919` / `346732372c19ee5cdaa8ad9b8d00495e91e6d7a5` で、状態ブロックと一致した。
- 使用した evidence bundle: Phase B result snapshot / evidence bundle は未生成。§7 の発見 C 4 回目の実行担当の原記録、implementation head 間の 2 file の commit 固定差分、§5・§5.1・§6 の frozen contract、`tools/contract-audit.ps1` の結果を使用した。
- 構造適合: `tools/s43a-local-origin.ps1` は E 系列の revision 1 の観測対象と順序を実装し、Built では `Title` の load record を読むだけ、E6 では `Title`・UI token・`HpGauge`・`OutGameScene`・Play record の再読を順に保存する。`ScenarioDriver.Tests.ps1` は Built / overlay / Play 未到達の順序と証拠を偽 Editor で検査する。製品 Runtime / Editor code、Harness 基盤、asmdef、依存方向、凍結契約は変更されていない。`contract-audit` は errors 0、warnings 0 だった。
- 現在の問いを阻害する findings: なし。長い revision 名を証拠名へ埋め込んでいた前回の blocker は、時点連番の receipt 写しと対応表で解消済みであり、今回の revision 1 観測差分にも §5 の対象・順序・「合否を判定しない」約束への違反は見つからなかった。
- 後続スライスへ移送する findings: なし。判定 C の全 EditMode 回帰、E 系列の実 Editor 操作、AC10 の 4 観測、部分失敗時の Unity Mono 実測、複数 origin の実際の取得順は、発見 C の blocker ではなく判定 C の入力として残る。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 判断担当は追加の Unity test を実行していない。実行担当は `pwsh -NoProfile -File tools/contract-audit.ps1`、`tools/docs-audit.ps1`、`tools/Harness/Tests/Scenario.Tests.ps1`、`tools/Harness/Tests/ScenarioDriver.Tests.ps1`、`tools/Harness/Tests/ScenarioUnity.Tests.ps1` を implementation head で実行した。`contract-audit` は Phase C の必須構造契約検査であり、その他は今回の driver と Harness 変更の offline 回帰を確認するためである。
- テスト結果（原記録の実行テスト名と件数）: `contract-audit.ps1` exit 0（errors 0、warnings 0）、`docs-audit.ps1` exit 0（既存の警告 1 件）、`Scenario.Tests.ps1` 24 executed / 0 failed、`ScenarioDriver.Tests.ps1` 15 executed / 0 failed、`ScenarioUnity.Tests.ps1` 28 executed / 0 failed。Unity XML は追加生成していない。作業ツリーは実行担当の前後と判断担当の変更前とも clean だった。
- 判定必須のうち未実行: 発見 C では GO/NO-GO を判定しない。全 EditMode 回帰、E 系列（S0、P1、E1、P2、E2、E3、E4、E5、E6、Cleanup）の実 Editor 操作、AC10 の 4 観測、部分失敗時の Unity Mono 実測、複数 origin の実際の取得順は未実行であり、判定 C へ引き継ぐ。
- 重い検証を発見段階で限定実行した場合の理由と範囲: 今回は implementation head の製品 code と基盤に変更がなく、対象は driver と offline test の 2 file なので、実行担当は機械検査と offline test 3 本に限定した。追加 Unity テストを走らせない起動指示に従った。
- 未確認事項: 新しい implementation head での全 EditMode 回帰と E 系列の実 Editor 観測、AC10 の 4 観測、部分失敗時の Unity Mono 実測、複数 origin の実際の取得順は未確認である。
- 担当・モデル: 判断担当は OpenAI（Phase A の外部計画レビューと同じ系列。独立性制約あり（owner 受容））。

### 判定 C の実行担当の記録（3 回目、2026-11-27）

実行担当（進行役の session。判断はしない）が、implementation head `34673237` で §5 の凍結したコマンドを走らせた生の結果。HEAD は `88cfcba0`（implementation head との差は `docs/handoff/` だけ）。原記録はこの worktree の `TestResults/` の下（git 管理外、未保存）。どれも sandbox の外で走らせた。同じ時間帯に、別 project（`osm-s4c`）の Editor が同じ機械で動いていた（この記録の Unity とは command line の project path で区別した。触っていない）。

- (1) 全 EditMode 回帰: `pwsh tools/run-tests.ps1 -OutputRoot <worktree>\TestResults\c-judge3`（空 filter）。run-tests の出力は `TestResults/c-judge3-run.log`（09:17:57〜09:35:50 JST）。XML（`c-judge3/26155294-946d-4051-8f44-29673bfe6b6f/results.xml`）: total 2688、passed 2628、failed 0、inconclusive 25、skipped 35、root result `Passed`、00:21:00Z〜00:35:43Z。`step.json`: status `failed`、exitCode 1。§5 の test class の件数（XML の `TestFixture` の名前で合計、failed は全て 0）: `ContentOriginHeadTests` 16、`ContentAcquisitionRulesTests` 121、`ContentOriginGuardTests` 23、`ContentAcquisitionTests` 41、`IndexedContentOriginTests` 5、`ContentOriginHeadWriterTests` 11、`SampleGameContentBatchTests` 18、`VerifyPlaySequenceTests` 13（同名の fixture の合計）、`ContentBaseSelectionTests` 8、`PlayEntryCandidatesTests` 18、`ContentAcquireLauncherTests` 7、`PublishedOriginAcquireIntegrationTests` 1。run-tests の終了後（09:36:04 JST）に CIM で見た osm-s43a の Unity は 0 件で、止める操作はしていない。
- (2) E 系列: `pwsh tools/s43a-local-origin.ps1 -RunId j3 -Commit 346732372c19ee5cdaa8ad9b8d00495e91e6d7a5` は exit 0（09:36〜11:26 JST）。driver の出力は `TestResults/e-j3-driver.log`、原記録は `TestResults/scenario/s43a/j3/`（`driver.jsonl`、`base/<case>/1/`、`evidence/<case>/1/`）。S0・P1・E1・P2・E2・E3・E4・E5・E6・Cleanup は全て attempt 1 で `case-end` に達し、各 `case-end` の `base` は `ok`、`failure` は空、head は `34673237`（Cleanup は null）。終わった後、X と Y の作業ツリーは clean（`git status --porcelain` が空）で、外部 root `D:\ovr\s43a\j3` は無い（`scenario-external-removed.json` は 02:26:34Z）。11:26 JST に CIM で見た osm-s43a の Unity は 0 件。主な生の値（照合はしていない）:
  - P1: batch（PID 39476）は 00:39:24Z に始まり、`last-result.json` と `head.json` は 00:42:00Z に書かれた。process は 02:09:24Z に driver が止めた（`process-exit` は `exitCode` -1、`forced` true）。`last-result.json` は `exitCode` 0、`originHeadFailure` は空、`manifestSha256` `858b3fe2…`。batch の log の末尾は Unity の終了処理の行（`Found no leaked weakptrs.`、`~StackAllocator(ALLOC_TEMP_MAIN) m_LastAlloc not NULL…`）。
  - P2: `publish.json` は `exitCode` 0、`forced` false。`last-result.json` の `manifestSha256` は `ad6afbf5…`。
  - E1: `acquire-result.json` は `state` `Acquired`、revision `20261127T004123788Z-e77d2fe1…`、`manifestSha256` `858b3fe2…`、`bytesOrigin` `samplegame-local-publish`、`selectionSaved` true、`playRevision` は同じ revision。`play-observations.json` は `playExpired` false、`Title` の load record が観測され（`observed` true）、値は `<R1 revision>|858b3fe2…|ContentDirectory|registered-content-directory|VerifiedDigest`。`play-record.txt` の `record.state` は `EnteredPlay`、`revision` は R1、`errorCount` 0、`exceptionCount` 0。
  - E2: `acquire-result.json` は `state` `Acquired`、revision `20261127T021253059Z-428c2118…`、`manifestSha256` `ad6afbf5…`、`selectionSaved` true。`Title` の load record は `<R2 revision>|ad6afbf5…|ContentDirectory|registered-content-directory|VerifiedDigest`。`play-record.txt` の `revision` は R2、warnings は「Several verified revisions exist for 'spring-full'; using the latest: <R2>」、`errorCount` 0。
  - E3: `acquire-result.json` は `state` `AlreadyInstalled`、revision R1、`bytesOrigin` 空、attempts は `Target` の 1 件、`selectionSaved` true。Play はしない case で、`play-observations.json` は無い。
  - E4: 取得の操作はなく、`Title` の load record は R1（`858b3fe2…`、`ContentDirectory`、`VerifiedDigest`）。`play-record.txt` の `revision` は R1、warnings は「… using the latest: <R1>」、`errorCount` 0。
  - E5: `acquire-a-result.json` は `state` `NotFound`（「No head: …\origin-e5a\.origin\spring-full\StandaloneWindows64-Player.json」）、`selectionSaved` false。`acquire-b-result.json` は `state` `ArrivalDigestConflict`（installed R1 の digest `858b3fe2…` が target `ffff…ffff` と違う）、`selectionSaved` false。どちらも `playRevision` は R1。Play の `Title` の load record は R1（`VerifiedDigest`）、`errorCount` 0。
  - E6: patch `ac01-title.patch`、開いた scene は `Assets/SampleGame/OutGame/OutGameScene.unity`。`play-record.txt` の `route` は `BuiltWithLocalEdits`、`overlay.state` `Built`、`overlayRoots` 1、`unbuildable` 空、overlay identity `20261127T022255444Z-89cfeba7…`、`record.overlayRegistered` は同じ identity、`overlayCheck` `matched`、snapshot は `OutGameScene`（dirty false）、`record.revision` R1、`errorCount` 0、`exceptionCount` 0（`play-record-after.txt` も同じ値）。`play-observations.json`: `Title` は `<overlay identity>||ContentOverlay|registered-content-overlay|RecordedBaseMatch`、`HpGauge` は `<R1>|858b3fe2…|ContentDirectory|registered-content-directory|VerifiedDigest`、`OutGameScene` は `||EditorSnapshot|editor-play-snapshot|OpenSceneSnapshot`、`addTitle` と `addHpGauge` は `requested`、`title` は `S42-AC1-UXML-TOKEN`。Editor（PID 13628）は終了の `eval` 2 回に応答せず、02:26:32Z に driver が止めた（`process-exit` は `exitCode` -1、`forced` true）。Editor の log（`base/E6/1/editor-1.log`）の末尾は Unity の終了処理の行。
- (3) 機械検査（HEAD `88cfcba0`、`pwsh -NoProfile -File <script>`、log は `TestResults/c-judge3-machine/`）: `tools/contract-audit.ps1` exit 0（違反の行は無い）。`tools/docs-audit.ps1` exit 0（既存の警告 1 件）。`Scenario.Tests.ps1` 24 executed / 0 failed。`ScenarioDriver.Tests.ps1` 15 executed / 0 failed。`ScenarioUnity.Tests.ps1` 28 executed / 0 failed。実行の前後で作業ツリーは clean。
- 実行担当の扱い: 合否と GO / NO-GO は書かない。(1)〜(3) は揃ったので、判断担当を起動する。

### 判定 C（判断担当、2 回目、2026-11-27）

- 種別: 判定 C（判断担当）。指定された引継ぎ commit は `95bd1c0f26a1b98e10289b0136ef06c03dbc4858` で、開始時の HEAD と一致した。作業ツリーは開始時 clean だった。PR #214 は open・draft、base は `develop`、状態ブロックと一致した。implementation base / head は `9e7d414833456e2c9dd930c5f96432b24d9ee919` / `346732372c19ee5cdaa8ad9b8d00495e91e6d7a5` で、§0 と一致した。
- 使用した evidence bundle: Phase B result snapshot / evidence bundle は未生成。指定された判定 C の実行担当の原記録 `D:\repositories\unity\osm-s43a\TestResults\c-judge3\`（`results.xml`、`step.json`、`unity.log`）、`D:\repositories\unity\osm-s43a\TestResults\c-judge3-run.log`、`D:\repositories\unity\osm-s43a\TestResults\scenario\s43a\j3\`、`D:\repositories\unity\osm-s43a\TestResults\e-j3-driver.log`、`D:\repositories\unity\osm-s43a\TestResults\c-judge3-machine\` と、§5・§6 の frozen contract / 判定表を使用した。判定担当は追加の Unity test、E 系列、offline test、機械検査を実行していない。
- 構造適合: implementation head の固定対象は revision 1 の driver と `ScenarioDriver.Tests.ps1` の差分であり、製品 Runtime / Editor code、Harness 基盤、asmdef、依存方向、凍結契約を新たに変えていない。発見 C（4 回目）の機械検査と driver の offline test は errors 0、failed 0 で、revision 1 の観測対象・順序・証拠保存に違反はない。判定対象 head は実行担当の全記録で `34673237` と一致した。
- 現在の問いを阻害する findings: なし。
- AC1〜AC8 の照合: 全 EditMode 回帰 XML の §5 対象 fixture は failed 0（`ContentOriginHeadTests` 16、`ContentAcquisitionRulesTests` 121、`ContentOriginGuardTests` 23、`ContentAcquisitionTests` 41、`IndexedContentOriginTests` 5、`ContentOriginHeadWriterTests` 11、`SampleGameContentBatchTests` 18、`VerifyPlaySequenceTests` 13、`ContentBaseSelectionTests` 8、`PlayEntryCandidatesTests` 18、`ContentAcquireLauncherTests` 7、`PublishedOriginAcquireIntegrationTests` 1）。E 系列の P1/E1/P2/E2/E3/E4/E5/E6 は全て attempt 1 の `case-end`、`failure=[]` で、P1 の forced 終了は head 書込み後の最後の log 行があり、凍結した AC7 の読み方を満たす。単体・統合の合格は AC1〜AC8 の blocker を示さない。
- AC9 の照合: P1 は `exitCode=-1`・`forced=true` だが `last-result.json` と head の identity / digest / `transport.json` SHA-256 が一致し、log に head 書込み行がある。P2 は `exitCode=0`・`forced=false` で同じ照合を満たし、R1 と R2 の identity は異なる。E1 は `Acquired` で R1 の `Title` load Identity が `ContentDirectory|registered-content-directory|VerifiedDigest` として観測され、digest は R1 と一致する。E2 は同じ形で R2 と一致する。E3 は `AlreadyInstalled`、`bytesOrigin` 空、installed 一覧と receipt の前後が維持される。E4 は取得なしで R1 を選び、R1 の `Title` load Identity を観測する。E5(a) は `NotFound`、E5(b) は `ArrivalDigestConflict`、どちらも `selectionSaved=false` で、一覧・選択記録を変えず、その後の Play は R1 の `Title` load Identity を観測する。各 acquire の operation 番号は `Start` の番号と一致する。record は対象 case で `BuiltRoute`、`EnteredPlay`、`errorCount=0`、`exceptionCount=0` だった。AC9 は成立する。
- AC10 の照合: E6 は `BuiltWithLocalEdits` で Play に到達し、`overlayRoots=1`、`unbuildable=[]`、overlay は `Built`・`overlayRegistered` と `overlayCheck=matched`。`Title` は overlay identity の `ContentOverlay` / `RecordedBaseMatch` と patch token、`HpGauge` は R1 の `ContentDirectory` / `VerifiedDigest`、`OutGameScene` は `EditorSnapshot` / `OpenSceneSnapshot` としてそれぞれ観測された。観測後の record は `errorCount=0`、`exceptionCount=0`。4 観測はすべて肯定的に記録され、AC10 は成立する。E6 の Editor は終了時に forced だが、証拠は保存後であり、凍結規則上の AC10 の未実施・観測不能ではない。
- AC11 の照合: `results.xml` は total 2688、passed 2628、failed 0、inconclusive 25、skipped 35、root `Passed`。既知の VFX inconclusive 25 件以外の失敗は無い。`step.json` の status `failed` / exitCode 1 は既知の inconclusive と Unity native 終了 stall の運用結果であり、§5 の XML 判定を置き換えない。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 判定担当は追加実行なし。実行担当の無改変記録から、全 EditMode 回帰 `pwsh tools/run-tests.ps1 -OutputRoot <worktree>\\TestResults\\c-judge3`（空 filter）、E 系列 `pwsh tools/s43a-local-origin.ps1 -RunId j3 -Commit 346732372c19ee5cdaa8ad9b8d00495e91e6d7a5`、および `tools/contract-audit.ps1`、`tools/docs-audit.ps1`、`Scenario.Tests.ps1`、`ScenarioDriver.Tests.ps1`、`ScenarioUnity.Tests.ps1` の結果を照合した。空 filter は AC11、E 系列は AC9・AC10、機械検査は構造契約と driver の健全性を対象にした。
- テスト結果（原記録の実行テスト名と件数）: XML は上記の total 2688 / passed 2628 / failed 0 / inconclusive 25 / skipped 35 / `Passed`。機械検査は `contract-audit` exit 0（errors 0、warnings 0）、`docs-audit` exit 0（既存警告 1 件）、`Scenario.Tests` 24/24、`ScenarioDriver.Tests` 15/15、`ScenarioUnity.Tests` 28/28。E 系列は S0・P1・E1・P2・E2・E3・E4・E5・E6・Cleanup が全て `case-end`、failure 空、driver exit 0 だった。
- 判定必須のうち未達: なし。AC1〜AC11 は判定表の定義を満たす。
- 結論と引き渡し: **GO**。現在の問いを阻害する blocker は無い。高コスト証拠は分類されていないため、次は同じ implementation head と判定入力から作る C' blind bundle による独立監査であり、C' 完了前に Phase D へ進めない。E 系列で観測した DebugSocket 接続失敗、終了時の UniTask / `/api/exec` 行、P1/E6 の forced 終了は、record の Error/Exception 0、凍結した log 窓、head 書込み後の終了規則に照らして現在の問いを阻害する指摘とはしない。
- 重い検証を判定段階で追加実行しなかった理由と範囲: §0 の指示どおり、判定担当は原記録だけを照合した。今回の execution record は全 EditMode 回帰、全 E 系列、機械検査を含み、AC9・AC10 の必要な実 Editor 観測も揃っているため、同一 run の補完実行はしなかった。
- 未確認事項: 判定必須条件に関する未確認事項はない。Evidence bundle と C' blind bundle は未生成であり、進行役が取得案内を作成した後、C' 担当が保存・転送・閲覧可能性を確認する。部分失敗時の Unity Mono 実測や複数 origin の実際の取得順は §2.6 の後続スライスの入力であり、今回の GO 条件ではない。
- 担当・モデル: 実行担当は進行役の別 session（判断なし、モデル記録はこの票にない）。判断担当は OpenAI（Phase A の外部計画レビューと同じ系列。独立性制約あり（owner 受容））。Phase B は Anthropic。C' 用の xAI/Grok は未使用・予約のままとする。

## 8. Phase C'

### C'（1 巡目、2026-11-27）

- 種別: C'（独立監査、1 巡目）。対象は implementation base / head `9e7d414833456e2c9dd930c5f96432b24d9ee919` / `346732372c19ee5cdaa8ad9b8d00495e91e6d7a5`。
- 入力: C' blind bundle（1 巡目。§0 の取得案内のとおりローカル固定済み、未昇格）を取得した無改変の写し、その zip を展開したもの、implementation head の tracked file の写し（この HANDOFF と binary asset を除く）、起動指示 1 file。PR と可変 HANDOFF は渡していない。C' 用決定記録は 1 巡目なので無い。
- 担当・モデル: xAI の Grok（cursor-agent、ask mode、読み取りだけ）。Phase A（Anthropic・OpenAI）、Phase B（Anthropic）、発見 C・判定 C（OpenAI）と別の系列で、§5 の C' の予約のとおり。進行役（Anthropic）が bundle を作った。Phase A の §5 の本文にある §6 の節への参照 1 か所はそのまま渡した（§0）。独立性制約あり（owner 受容）。実行時間は約 10 分。
- 結論: **blocker あり（1 件）**。
- **CP-1（blocker）:** Phase A §2.4 の N5（取得を使わない利用者と Player の動きが変わらない）への違反。`unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の `m_RuntimeSettings.m_List` が、base の 16 件から head で `[]` になっている。この file は §3 の責務マップに無い。program の文書（S4-1 から移送した入力の (7)）は、Library の無い worktree の最初の Unity 起動がこの一覧を `[]` に書き換えると書いている。head にはそれを戻す変更が無い。進行役は事実だけを確かめた: `git diff 9e7d4148 34673237 -- <この asset>` は 1 insertion・17 deletions（16 件の rid を `m_List: []` に置き換え）で、入った commit は `2526ae39`（Phase B の実装 commit）。
- 後続の入力（C' の記述の要約。現スライスを広げない）: driver の `Invoke-Acquire` は `Start` の応答が失われた時に case を止めず最大 30 分待つ（j3 では踏んでいない）。別の書き手が head を置いた部分失敗で前の head が backup 名のまま残る。壊れた選んだ base の記録を書き直す時に他の cache root の entry を捨てる。pin の revision が head の revision 欄と一致する時の所在の解決。driver の行数（507 行。§3 の予想は約 200 行）。E6 の観測は記録されている。
- 監査できなかった範囲（C' の申告）: manifest の sha256 の再計算（shell を使わない設定）。test 本体の細部（合否は XML に依る）。Editor log の全文。Player 上の描画。
- 引き渡し: CP-1 は凍結条件への違反なので B へ戻す（§0 の票）。CP-1 への人間の裁定は未記録。

## 9. Phase D

未着手
