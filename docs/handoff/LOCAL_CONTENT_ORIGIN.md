# 検証済み content の取得元（Local）— S4-3a

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 を 4 行に分けた最初の行 S4-3a。分け方は §2.2）
- status: `A`（A3 の統合案の改訂 2。改訂 1 は外部計画レビュー 1 件目の採否の案と base の取り直し、改訂 2 は外部計画レビュー 2・3 件目の採否の案（設計の 5 件の修正と、実機の検証を S4-V の基盤の上に置くこと）を入れた。**未凍結。** owner の裁定待ちは §2.9）
- branch: `claude/trusting-ramanujan-kxphtr`（PR の base は `develop`）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`develop` の head、2026-11-25、S4-2 の Phase D の merge。branch に merge した。§1 の現況と §3 の行数はこれで取り直した。A2 の固定 base は前の `1fd200b5ba835c833035d153579216e14cb823af` のまま。§5.1）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `high`（Framework の公開 API に取得元の差込口を足し、cache への書き込みを呼ぶ調停と、Play の入口の候補の並びに触れる。program の行は `normal` だったが、高リスクの例の「公開 API」に当たるので上げた）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「DIST transport と disk cache の境界」（取得元の差込口、取得元の head、digest の出所、取得の調停と止まる規則、取得が古い install を消しうること）と「まだ保証しないこと」、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「配信用 transport の公開境界」（head の登録）と「宣言からの Play 経路」（取得の操作、選んだ base の記録）、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（別の `Library` の base と上書きの答え）、repo の README の「試す」、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 の行と後続へ送る入力
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と base の code だけ）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（owner の裁定と、Draft PR での外部計画レビュー）。
  - 次の作業: S4-V（program §8、検証の基盤）の A3 を待ち、§5 の driver の約束を S4-V の凍結した契約と突き合わせる。owner が §2.9 の裁定待ち（D-1、D-3〜D-6）を決める。外部計画レビューに続きがあれば、その採否を §5.1 に足す。裁定が設計・受け入れ条件を変えたら、影響する A2 の観点の再レビューの要否を §5.1 に記録する。凍結の記録（owner の凍結判断、対象 revision、次の担当 B）を書いて凍結版を commit する。
  - 読む範囲: 既定のとおり（全節）。
  - 引き継ぐ commit: この票を含む commit（SHA は PR の状態ブロックで渡す）。
  - 必要な権限: なし（文書のみ）。凍結の後の Phase C は Unity（Windows）と Editor の操作、2 つの worktree が要る（§5 の E 系列）。
  - 停止規則: 凍結の記録が無いまま Phase B を始めない。S4-V の A3 より前に凍結しない。S4-V の merge より前に Phase B を始めない（program §8 の S4-V の行）。S4-2 の Phase D の後に凍結するという条件（旧 program §8 の S4-3 の行）は、2026-11-25 の S4-2 の merge と、この改訂の base の取り直しで満たした。
  - 引き渡さないもの: なし。

### 実測の方法

- base の code と文書を読み、`grep` と `wc -l` で確かめた。Unity は起動していない。
- A1〜A3 の初版は base `1fd200b5` で書き、S4-2 の HANDOFF は branch `claude/player-content-overlay` の head `86a04d1`（Phase B と発見 C の往復中）の版を読んだ。改訂 1 で base を `07cc2c97`（S4-2 の merge の後）に取り直し、`1fd200b5..07cc2c97` の差分のうち本書の §1・§3 が挙げる file に当たる物は `PlayEntryCandidates.cs` と `PlayEntryInterceptor.cs` だけだった（§1 の該当行に反映）。S4-2 の結論は、merge した program §8 の S4-2 の行と「S4-2 から移送した入力」を正とした。
- 改訂 1 で、Windows の開発の PC の `pwsh` で `tools/docs-audit.ps1` と `tools/contract-audit.ps1` を通した（結果は PR の本文）。`tools/workflow-task.ps1 start` は凍結の時に行う。

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
- 現況（base `07cc2c97`。初版の `1fd200b5` から取り直した）:

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
| Play ボタンの入口は、batch と、開いている Scene の名前が `InitTestScene` で始まる Play を素通りする（rule `BatchMode` / `TestRunnerScene` の `PassThrough` で、候補を選ばず設定を補わない）。`run-tests.ps1` は batch で走るので、そこから走る test の `EnterPlayMode` は入口の判定を通らない。実 Editor の Play を `eval` で操作して record を読む経路は S3-4b の判定と S4-2 の判定 C の追加で使われた。Play の record は `SessionState` にあり、Editor の process をまたがない。`tools/` に、窓のある Editor を起動して閉じる script は無い | `PlayEntryInterceptor.cs:184-196`、`PlayEntryDecision.cs:197-201`、`PlayEntryRecord.Load`、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」の 1 行目と観測の注意 | 実 Editor の Play を見る検証（§5 の E 系列）は、S4-2 の `[Explicit]` の test と `run-tests.ps1` の組では組めない。窓のある Editor を case ごとに起動し、`eval` で操作する |
| S4-2 の判定の資産: driver `tools/s42-player-overlay.ps1`（2 つの worktree、case ごとに別の Unity process、case の前に固定 patch を `git apply`、後に `git checkout` と `git clean -fd` で戻して `git status --porcelain` を前と比べる、run ごとの cache を repo の外の短い path に作る、各 case の XML の total が 1 であることを見る、合否は判定しない）と、Title の閉包を編集する固定 patch `PlayerOverlayEdits/ac01-title.patch` | `tools/s42-player-overlay.ps1`（186 行）、`SampleGame/Tests/Editor/Build/PlayerOverlayEdits/` | §5 の E 系列の driver はこの構成に倣う。patch はそのまま使う |
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
| N1 | 期待 digest と互換の検証を通っていない bytes が、到着点（`installed/`）に現れない。結果を「取消」とした取得が、後から install を公開しない（rename が取消より先に終わった時は「取得した」で終わる） | 到着点へ書く経路は `ContentInstaller.InstallCore` の rename だけ（`Runtime/BuildContent/Distribution/` の `Directory.Move` を `grep`: installer と、削除の tombstone）。調停が自分で cache に書く経路を作ると崩れる。調停が先に読んだ manifest を install の根拠に使い回すと、読んだ後の差し替えを見落とす（installer は自分で読み直す）。取得元が取消に応じない時、調停だけが先に戻ると、installer の task が後から rename しうる。未確認: `tools/` の下に `installed/` へ直接書く code があるかは見ていない |
| N2 | 1 回の取得が、最初に決めた対象の組と違う revision・digest の物を結果にしない（pin と違う物、head と bytes が食い違う物、到着点の同じ revision と digest が違う物、失敗の後の別の revision） | installer の manifest の検査は digest の照合を最後に行うので、digest の違う manifest が「互換でない」「identity が違う」に化ける（§1 の現況）。失敗の code だけで次の動きを決めると、整合性の失敗を互換の不一致と読み違える。installer の「既にある revision の再利用」は request の digest で再検証するので、違う digest は `IntegrityMismatch` になるが、取得元の不整合と同じ code である |
| N3 | 取得の失敗・取消・例外が、半端な install、壊れた head、持ったままの cache の lock を残さない | installer の staging の片付け（失敗しても元の例外を保ち、残った path を例外に付ける）。installer は取得口の読みを待つ間 cache の lock を持つので、取得元が取消に応じないと lock が残る。head の書き換えの途中の失敗。取得元が取消に応じない時、guard が打ち切った読みが後で終わると、installer の buffer に書きうる・遅れて開いた stream が残りうる・遅れた例外が観測されないまま残りうる。取得元の同期の `OpenArtifacts` と `Dispose` は guard が打ち切れない。取得の間の script の compile（domain reload）で task が失われた時の staging は、次の install の `MakeRoom` が片付ける（印を書く前の staging だけは片付かない）。未確認: domain reload の時に lock の file の handle がいつ離れるか |
| N4 | 取得元の種類が、要求時解決・lifetime・起動に現れない | `Runtime/ContentResolution/`、`Runtime/AssetManagement/`、`Runtime/SceneSystem/`、`Runtime/Bootstrap/` に差分が無いこと。`ContentSourceKind` と `ContentAcceptanceBasis` に値を足さないこと |
| N5 | 取得を使わない利用者と Player の動きが変わらない（program §6 の I8・I10） | installer・cache・receipt・検証・bytes の取得口の code を変えると、Delivery の窓・検証 Play・Player の起動が通る経路が変わる。本書はこれらの file を変えない |
| N6 | Play の入口の判定の規則と、利用者の明示設定を変えない。入口の候補の並びは、取得で選んだ base が、選んだ時刻より後に新しく install された revision が無い限り先頭になることだけが変わる（既にある revision の再利用は receipt の時刻を変えないので「後の install」に数えない） | `PlayEntryDecision` の行と回復の値（test が完全一致で見る）、`PlayEntryTransaction`、ledger（`ContentDeliveryPlayBridge`）。取得の操作が Play の間・compile の間に走ると、Play が見る cache が途中で変わる。選んだ base の記録が古いまま残ると、利用者が後から Delivery の窓で入れた revision より先になりうる。Delivery の窓と検証 Play が既にある revision を再利用しても receipt の時刻は変わらない |
| N7 | 共通の側が取得元の型・id で分岐しない。差込口が `UnityEditor` と資格情報を要らない | 調停に「directory の取得元なら」の分岐が入ると、次の取得元で共通の側を直すことになる |

#### 成立条件（起きなければならないこと）

| ID | 条件 |
|---|---|
| S1 | batch と検証 Play の publish が成功で終わると、取得元の head（その contentSet と target）が今回の publish の revision と、公開した `transport.json` の digest を指す。batch が成功で終わらなかった時、head は今回の publish を指さない（前の値のまま、または無いまま） |
| S2 | 取得の操作で、到着点に無い revision が取得元から取得・検証・install され、結果と、選んだ base の記録に、対象の組・対象を決めた取得元・bytes を得た取得元・digest の出所（申告）が残る |
| S3 | 対象の組が既に到着点にあれば、取得元の bytes の取得口を 1 回も開かずに、再検証だけで「install 済み」と答える |
| S4 | 取得元が複数ある時、先の取得元が「答えない」「持っていない」「bytes に届かない」「bytes が対象の digest と違う」なら、理由を残して次の取得元へ進み、同じ対象の組をそこから取得する |
| S5 | pin を渡した時、pin の組だけが取得され、取得元の head と申告は使われない（head が別の revision を指していても、申告の digest が pin と違っていても、bytes が pin と一致すれば取得される） |
| S6 | §2.3 の 3 種類の取得元で、調停の test の表が同じ結果になる |
| S7 | 取得が「取得した」か「install 済み」で終わった後、選んだ時刻より後に新しく install された revision が無ければ、実 Editor の Play（経路 Built）は取得の対象の revision を base にする。取得の結果は、取得の成否と、Play が使う revision（とその理由）を分けて書く。取得した revision が Play に使えない時（宣言と合わない、選んだ base の記録を保存できなかった等）は、結果の文と Warning がそう言う |
| S8 | 別の checkout（別の `Library`）で publish した base を取得元から取得し、経路 Built + local edits で手元の上書きを重ねた実 Editor の Play の答えが記録される |

#### 取得の段と止まる規則（N2 と S4 の境目）

対象の組は 1 回だけ決め、以後、別の revision へは進まない。失敗の振り分けは、installer の失敗 code だけでは決めず、どの段で起きたかと合わせて決める（調停が manifest を先に読み、digest を照合してから欄を解釈する。`ContentPurposeIndex` の読みの順と同じ）。

| 段 | 起きたこと | 次にすること |
|---|---|---|
| 1 対象を決める | pin がある | pin の組を対象にする（出所は pin）。取得元の head は読まない |
| 1 | pin が無い | 「申告を認める」取得元を順に照会する（revision を指定しない）。最初に digest つきで `Found` と答えた head を対象にする（出所は申告）。`NotFound`・`Unavailable`・例外・digest の無い答えは、理由を残して次へ。「pin だけ」の取得元は「pin が要る」と残して飛ばす |
| 1 | 対象が決まらない | 「見つからない」で終わる（全ての取得元が「pin だけ」の時もここ） |
| 2 到着点を見る | 対象の revision が到着点に無い | 段 3 へ |
| 2 | 在って、receipt の digest が対象と同じで、再検証が通る | 「install 済み」で終わる（bytes の取得口は開かない） |
| 2 | 在って、receipt の digest が対象と違う | 「到着点の同じ revision が違う digest」で終わる |
| 2 | 在って、digest は同じだが再検証が通らない（install が壊れている） | 「到着点の失敗」で終わる（直す操作は対象外。理由に cache の側と書く） |
| 3 取得元から取る（host の順に全ての取得元） | 照会が `NotFound`・`Unavailable`・例外（対象を決めた取得元は照会し直さない） | 理由を残して次の取得元へ |
| 3 | `transport.json` に届かない、読んでいる途中で切れた・例外（取得元の側の例外は全て「届かない」に揃える） | 理由を残して次の取得元へ |
| 3 | `transport.json` の bytes の digest が対象と違う | 整合性の失敗として残し、次の取得元へ（同じ対象の組だけを探す） |
| 3 | digest は対象と同じだが、manifest が壊れている、identity・target が照会と違う、互換でない（protocol、Unity の版、root schema） | 「対象が使えない」で終わる（同じ bytes なので、他の取得元でも同じ。別の revision へは進まない） |
| 3 | installer が取得元の側の失敗を返した（`TransportFailure`、`IntegrityMismatch`、`MissingFile`。先読みの後に bytes が変わった時の `InvalidManifest`・`IdentityMismatch`・`TargetMismatch`・`CompatibilityMismatch` もここ） | 整合性の失敗か「届かない」として残し、次の取得元へ |
| 3 | installer が `IntegrityMismatch` を返し、cache を読み直すと対象の revision が到着点に在る（段 2 の後に別の操作が同じ revision を入れた） | 到着点の側として扱い、「到着点の同じ revision が違う digest」か「到着点の失敗」で終わる（取得元の不整合に数えない） |
| 3 | installer が到着点の側の失敗を返した（`Busy`、`LockUnavailable`、`BudgetUnsatisfied`、`InstallConflict`、`IoFailure`） | 「到着点の失敗」で終わる（取得元を替えても直らない） |
| 3 | install が成功した | 「取得した」で終わる |
| 3 | 取得元が尽きた | 整合性の失敗が 1 件でもあれば「整合性の失敗」、無ければ「見つからない」で終わる |
| どの段でも | 取消 | 「取消」で終わる。取得元が応じなくても待たずに打ち切り、installer の片付けと lock の解放を待ってから戻る。installer が rename を終えて成功を返した後は、token を見て取消に変えない（確定点は rename。結果は「取得した」） |

合格例: 取得元 1 の R1 の content の file が壊れていて、取得元 2 に同じ digest の R1 があり、そこから取得する。不合格例: 取得元 1 の head の R1 が取れなかったので、古い R0 を取得する。不合格例: head の R1 の `transport.json` が別の bytes（Unity の版の欄も違う）に書き換わっていて、「互換でない」と読んで別の物へ進む（正しくは、digest が違うので整合性の失敗）。

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
| Play の入口の先読み、checkout hook が呼ぶ公開入口と最小の hook（U7）、取得した transport の install を入口が代わりに行うこと（S4-2 から）、known-good の pin の呼び手と cache の掃除の入口（`Evict` の呼び手）、壊れた install を直す操作 | S4-3c |
| 「自分の base」を選ぶ規則、同じ目的の複数 revision から 1 つを選ぶ規則、`ContentPurposeIndex.Select` で選んだ build を install と登録の検証に通すこと、上書きの base の選択に索引を使う接続、base より古い checkout、記録の無い base で root の宣言を足した場合、記録に無い root の依存だけが無い場合、`NotInBase` の root、manifest の `index` からの見分け、複数の Player から選ぶ UI（S4-2 から）、expected identity の無い要求での同名異内容の検出、互換でない head の下の古い互換な物へ進むか | S4-3d（本書は「head の 1 件」と「pin を渡せる口」までを持つ） |
| S4-2 の「行き先を決めていない入力」の (1) 入口が `player-compat.json` の記録の `identity` を、選んだ Player の identity と比べていない（`PlayerOverlayLaunch` は schema の版と記録の digest の自己照合だけを見る。Compare と Prepare の前に ordinal で比べて止め、identity だけが違う compat の port の test を足す案）、(2) `PlayerOverlayStartupObserver` が Player の log を読んでから process の終了を確かめるので、その間に最後の行を書いて終わると古い写しで `Failed` に決まりうる（log の読みの `IOException` は空の文字列になる）。中身は program §8 の「S4-2 から移送した入力」を正とする（2026-11-25、owner が S4-3 の入力にした） | S4-3d。どちらも遊べる Player の入口（`PlayerOverlayLaunch`）の、Player を選んで起動を見る部分の欠陥で、取得元・LAN・取得のきっかけに依らない。S4-3 の 4 行のうち Player の入口に触れる見込みがあるのは、複数の Player から選ぶ UI を持つ S4-3d だけである。本書は Player を扱わない（§1 の対象外）。S4-3d より先に直す必要が出たら、owner が単独の修正として切り出してよい |
| 要求をまたぐ pin と revision の一貫、`IContentProvider.Id` / `SourceKind` の getter の契約、片方の directory の file が disk から消えた時の読み先 | S4-4（供給元を足す・登録の寿命を扱う行） |
| 閉包の記録が manifest の上限を超える時の置き場と実データでの大きさ、配布と保存を sha256 でまとめる実装 | 実データの集計の後に owner が決める（2026-11-12 の裁定のまま） |
| G19、Built の Play の繰り返しの再検証、streaming の cold load、D7 の「より安い解決の比較」 | 取得元が出来た後の調査記録 |
| 印を書く前に失われた staging の残り、Play の record に取得の出所を出すこと | 所有なし（program §10 の U11 に足す）。record は S5 の入力 |
| Android・iOS（S4-5）、観測 UI と VCS の adapter（S5）、Player での取得と cloud の tier（U10） | 既存の行のまま |

### 2.7 受け入れ条件・判定定義・停止規則

受け入れ条件（括弧は検証する条件）。test の名前は §5。「〜が無い」「〜が変わらない」は、操作の前と、戻った直後の、directory の一覧（または file の bytes）を比べて見る。

- **AC1（N1）:** 調停の全ての終わり方で、取得の前後の `installed/` の一覧の差は、(i)「取得した」の時だけの対象の 1 件の追加と、(ii) 結果の「消えた install」に載った削除だけである。消えた install は、installer の容量の確保（今の動き）が消した物で、調停が installer の呼び出しの前後で cache を読んだ差から作り、成功・失敗・取消のどれでも結果に載せる（installer は変えない）。staging の片付けの失敗は結果の診断に載せる。失敗経路: content の file が manifest と違う取得元、`transport.json` が対象の digest と違う取得元、読んでいる途中で例外を投げる取得元、取消、容量の確保が古い install を消した後に bytes が壊れていて失敗する場合（消えた install が結果に載り、対象は増えない）。取消に応じない取得元（read が終わらない）でも、取消で戻った時点で対象は増えておらず、その後に test が取得元の read を解放しても増えない。最後の読みの後に取消が来る場合を signal で両方の順に作り、rename の前なら「取消」で対象が無く、rename の後なら「取得した」で対象が在る（結果と一覧が食い違わない）。
- **AC2（N2・S5）:** §2.4 の段と止まる規則の表の各行が、表のとおりに進むか終わる。失敗の振り分け（段と失敗の種類から次の動きを返す関数）は、`ContentDeliveryFailureCode` の 12 個の値の全てと、取得元の側の未知の例外に、表の行を 1 つずつ割り当てる。失敗経路: 表の 2 つの不合格例（古い revision を取得しないこと、digest も Unity の版の欄も違う manifest を整合性の失敗と読むこと）。pin の revision を持つ取得元が無い時に「見つからない」で終わること。全ての取得元が「pin だけ」で pin が無い時に、照会を 1 回もせずに「見つからない」で終わること。
- **AC3（N3）:** 取消と失敗で戻った直後に、cache の `staging/` の下の directory の一覧が前と同じで、cache の transaction の lock を取り直せる（直後の `Inspect` が `Busy` にならない）。取消に応じない取得元でも同じ。head の書き換えが途中で失敗した時、head の file は前の bytes のまま（無かった時は無いまま）で、別名の file が残らない。guard は取得元の読みを自分の buffer で受けて写すので、取消の後に終わった読みは installer の buffer を書かない。取消の後に開いた stream は破棄され、遅れた例外は guard が受け止める（観測されない task の例外を残さない）。`OpenArtifacts` と取得口の `Dispose` は同期で待たない契約とし、違反は host の不備として本書では打ち切らない。失敗経路: 置き換えを失敗させる注入、取消に応じない取得元、取消の後に読みが終わる取得元、取消の後に stream を返す取得元、取消の後に例外を投げる取得元。
- **AC4（N4・N7）:** Phase B の commit が変える file に、`Runtime/ContentResolution/`・`Runtime/AssetManagement/`・`Runtime/SceneSystem/`・`Runtime/Bootstrap/` の file が無い。Runtime の新しい file が `UnityEditor` を参照しない（`pwsh tools/contract-audit.ps1` の検査 5）。差込口の member に資格情報を受ける引数が無い。調停の test の表が 3 種類の取得元で同じ結果になる（AC8）ことで、型・id での分岐が無いことを示す。失敗経路: 型か id で分岐した調停は、3 種類目（別の型と id で包んだ directory の取得元）で結果が違う。
- **AC5（N5）:** Phase B の commit が、`Runtime/BuildContent/Distribution/` の今ある file（`ContentInstaller.cs`・`ContentCacheStore.cs`・`ContentDeliveryFiles.cs`・`InstalledRevisionVerifier.cs`・`ContentManifestValidation.cs`・`ContentTransportManifest.cs`・`ContentArtifactSource.cs`・`ContentPurposeIndex.cs` など）を変えない。失敗経路: 取得で install した revision を、今ある検証（`ContentCacheStore.ValidateInstalled`）と Delivery の窓の Installed offline の経路がそのまま受け付ける（取得の install が、今ある install と同じ形であること）。
- **AC6（N6）:** Phase B の commit が `PlayEntryDecision.cs`・`PlayEntryTransaction.cs`・`PlayEntryRecord.cs`・`ContentDeliveryPlayBridge.cs`・`ContentDeliveryWindow.cs` を変えず、`PlayEntryDecisionTests` が変更なしで通る。`PlayEntryCandidates.Select` は、選んだ base の記録を渡さない時、今と同じ並びで答える（既存の `PlayEntryCandidatesTests` が変更なしで通る）。記録を渡した時は、記録の組と同じ entry の時刻を「install の時刻と、選んだ時刻の新しい方」と読んで並べる。これは「直近の明示の操作が先」ではなく「選んだ後の新しい install が先」の限定した規則で、既にある revision の再利用（Delivery の窓・検証 Play）は receipt の時刻を変えないので数えない。この限定を取得の結果の文にも書く。選んだ base の記録の保存に失敗した時は、取得の結果を「取得した」のまま Warning を付け、Play が使う revision を書く。失敗経路: 選んだ後に別の revision が新しく install された時は、後の install が先になる。記録の revision が cache に在るが digest が違う時、または無い時は、記録を使わずに今の並びで答え、Warning に出す。取得の操作は、Play の間・Play への移行の間・compile の間は始まらず、理由を返す（cache に触れない）。取得の途中で Play を押すと、入口は「cache を読めない」で拒否する（今の動きのまま。壊れない）。
- **AC7（S1）:** batch が終了コード 0 で終わった後、head の file が、その publish の contentSet・target・revision と、公開した `transport.json` の bytes の SHA-256 を持つ。batch は head を、結果 file の確定の後の最後の段で書き、head の取り消し（巻き戻し）を持たない。読み手は確定していない publish を指す head を見ない。失敗経路: 結果 file の確定が失敗した batch は head を書かない。head の書き込みが失敗した batch は、今ある失敗の処理（`Fail`）で今回の公開先を消し、結果 file を失敗で上書きし、終了コードが head の失敗を示す。head は前の bytes のまま（置き換えは原子的）。今ある head が未対応の版か壊れている時、書き込みは head を書き換えずに失敗する。同じ root に 2 つの publish が並んで head を書く時は、後から書いた方が勝つ（直列化しない。どちらの head も確定した publish を指す）。検証 Play は、publish と宣言の照合の後に head を書き、書けなければ Play を止めずに Warning を出す。
- **AC8（S2・S3・S4・S6）:** 調停の test の表（AC1〜AC3 の失敗経路を含む全ての行）を、§2.3 の 3 種類の取得元で走らせ、結果の状態・install の結果・試行の記録が同じである。到着点に無い時は取得して、結果に対象の組・対象を決めた取得元・bytes を得た取得元・出所が載る。在る時は、bytes の取得口の `OpenArtifacts` と `OpenReadAsync` の呼び出しが 0 回で、結果の「bytes を得た取得元」は空である。先の取得元が使えない時は次から取得する。取得の前後で cache から消えた install があれば、結果に載る。
- **AC9（S7・N2・N6。実 Editor）:** 手順は §5 の E 系列の P1〜E5（別の checkout X で publish した R1・R2 を、checkout Y が取得元 X から取得する）。各 case の期待は §5 の「判定の担当が照合する値」の表のとおりで、要点は次の 4 つ。(a) 空の cache から R1 を取得した後と、R2 を取得した後の Play（経路 Built）の record の revision が、それぞれの publish の結果 file の `identity` と等しく、取得の結果・選んだ base の記録・`receipt.json` の digest が結果 file の `manifestSha256` と等しい（E1・E2）。(b) head を R1 の公開時の bytes に戻して取得すると「install 済み」で終わり、bytes を得た取得元が空で、R1 の `receipt.json` の bytes と `installed/` の一覧が前後で同じ（E3）。(c) 新しい Editor の process で、R2 が cache に残ったまま、取得の操作なしに Play すると、record の revision が R1 である（E4。選んだ base の記録が process をまたいで残り、後から install された R2 より先になる）。(d) 取得元が無い時と、head が到着点の R1 と違う digest を申告する時は、それぞれ「見つからない」「到着点の同じ revision が違う digest」で終わり、`installed/`・`staging/` の一覧と選んだ base の記録が前後で同じで、その後の Play の record の revision が R1 である（E5）。失敗経路: (d)。bytes の取得口を開いた回数（S3）は AC8 の単体で見る。
- **AC10（S8。実 Editor。測定）:** 手順は §5 の E6（AC9 と同じ X・Y で、Y に固定 patch を当てて経路 Built + local edits で Play する）。4 つの観測（record の上書きの欄の `overlayRoots` と `unbuildable`、上書きの root が手元の版で出たか、上書きに入らない root が base の record で載ったか、Error の有無）が、対象の head・base の revision・2 つの checkout の識別と一緒に記録される。答えの中身は合否にせず、AC9 の合否とも混ぜない。
- **AC11（回帰）:** 全 EditMode 回帰が、XML の failed 0 と root result `Passed`（`-nographics` の VFX 25 件の Inconclusive は既知。program §8 の「全 EditMode 回帰の受け入れ条件の書き方」）。

- 判定定義: **GO** は AC1〜AC11 が全て満たされたこと。AC10 は 4 つの観測が全て記録されたことで満たす。AC10 を実施できなかった時（E6 の Editor が上書きの build で止まる等。AC9 と同じ 2 つの checkout を使うので、checkout が無いことは理由にならない）は、GO にも NO-GO にもせず、判定を保留して owner へ返す。**NO-GO** は AC1〜AC9・AC11 のどれかが満たされないこと。CONDITIONAL ACCEPT は使わない（spike ではない）。AC10 の答えが「違う」「Error が出た」の時は、GO のまま owner へ返し、S4-3b の前に owner が裁定する。
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
- head の置き換えは、同じ directory に別名で書いてから、既にある時は `File.Replace`、無い時は `File.Move` で行う（先例は known-good の file の `File.Replace`）。読み手に「file が無い」瞬間を見せない。一時的な失敗（開発の PC の監視 tool。program §8 の S5-0 の行）は、`ContentStagingMove.IsTransient` と同じ判定、同じ上限の定数で置き換えをやり直す。`ContentStagingMove` の移し直しの条件（directory 用）は使えないので、やり直しの loop は head の書き手が持つ（§2.9 の D-5 の 1）。
- Runtime を触る Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない（program §0 の停止規則）。
- Unity の test と batch を実行する担当は、先に [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) を読む。build を伴う test と batch は、process の自然終了を合否にしない。

### 2.9 owner の裁定を待つ項目と、決定済みの項目

**裁定待ち（凍結の前に要る）:**

- **D-1（目的・利用者の文言）:** §1 の改訂後の文言でよいか。変える時は、変えた 2 欄で目的レビューをやり直す（やり直しは 1 回まで）。
- **D-3（S8 の置き場）:** 旧 program の S4-3 の行は、別の `Library` の base と上書きの答えを「A1 の最初の gate」に置いていた。本書は、凍結の前の harness ではなく、Phase C の実 Editor の測定（AC10）に置いた。理由は 3 つ。答えは差込口の設計を変えない。S4-2 で Player の答えが「同じ」だった。本書の製品の経路（別の checkout の publish を取得元から取得する）がそのまま測定の手順になる。答えが「違う」時は GO のまま owner へ返す。外部計画レビュー（EX-7）は、S4-2 の driver と patch の実物を見て、同じ X・Y の準備の上で測る追加の費用は小さいとして、同じ driver の観測の case（E6）に残す案を支持した。主担当の推奨もこの形（AC10 のまま、AC9 の合否と混ぜない）。凍結の前に測る案、別の spike の行にする案に変えるなら、ここで決める。
- **D-6（外部計画レビュー 1 件目の採否。案は §5.1 の EX-1〜EX-8）:** 実 Editor の検証（AC9・AC10）を、手作業の `eval` と文字列の目視の照合から、再実行できる driver・case・証拠の構成（§5 の E 系列）に改めること。主担当の案は採用で、変えた点は 2 つ。(1) Play を見る case は、S4-2 の `[Explicit]` の test と `run-tests.ps1` の組ではなく、driver が窓のある Editor を case ごとに起動して `eval` で操作する（入口が batch を素通りするため。§1 の現況）。(2) driver は合否を判定しない。identity と digest の照合は判定 C の担当が保存された原記録で行う（照合の script を Phase B が書いて git に置くことは workflow が許さない。S4-2 の driver と同じ扱い）。この採用で、責務マップに driver の 1 行が増え（§3）、AC9 の範囲が「空の cache から 1 件」から E1〜E5 に広がる（cache が空でない時の Play の選択を、実 Editor で見るようになる）。設計の他の部分と受け入れ条件の他の行は変えていない。**改訂 2:** 外部計画レビュー 2 件目（設計の 5 件 RX-1〜RX-5 と検証の追加 RX-6〜RX-10）は全て採る案（RX-6 は一部）。3 件目（検証の資産を code として再利用する）は、owner が 2026-11-25 に、S4-3a の中ではなく独立の行 S4-V（program §8、要求は §9.1 の利用者表）にすると決めた。S4-3a の driver は S4-V の基盤の上の scenario だけを持つ（§3、§5）。owner は同日、改訂 1・2 について A2 の観点の再レビューは要らないとした。
- **D-4（risk）:** `high` に上げた（外部計画レビュー 2 件目も支持した）。C' を B・C と別の系列の担当にする予約が要る（§5）。
- **D-5（A2 の指摘のうち owner の判断が要る物。採否の案は §5.1）:**
  1. head の置き換えの再試行を、file 用の新しい loop として持つこと（判定と上限は `ContentStagingMove` と同じ）。再試行なしにすると、監視 tool の一時的な失敗で、成功した publish を batch が捨てる。
  2. 検証 Play の publish も head を書くこと（`VerifyPlayLauncher.cs` に呼び出しを 1 つ足す。同じ root に publish するので、書かないと head が最新の publish を指さなくなる）。
  3. 取得の対象を head の 1 件に固定し、取れない時に古い revision へ進まないこと（head の directory を手で消すと、次の publish まで取得は失敗で終わる）。
  4. Play の入口の候補の並びを、選んだ base の記録で変えること（`PlayEntryCandidates.cs` と `PlayEntryInterceptor.cs` を変える。「直近の明示の操作が先」の規則）。
  5. 取得が budget を超えると古い install を消しうること（今ある installer の動き）を、止めずに結果の文で示すだけにすること。

**決定済み:**

- Q-1（行の分け方）: owner が 2026-11-25 に §2.2 の 4 行を承認した。
- Q-3（期待 digest の出所）: owner が 2026-11-25 に A0 の推奨を採った（§2.3）。
- Q-2（名前）: 「取得元」（code は `ContentOrigin`）とした。owner から異論が無ければこのまま凍結する。
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
| ＋ `…/Distribution/ContentOriginHead.cs` | head の file の形と、bytes からの読みと検査を持つ | 永続化の形（読み） | 状態なし | `ContentManifestValidation`（segment・相対 path・hash の形の検査）、`JsonUtility` | head の DTO（public。書き手が別の assembly に居て、Runtime の `InternalsVisibleTo` に入っていない。`ContentPurposeIndex` と同じ扱い）、`Read(bytes)` と結果（`Present` / `None` / `Unsupported` / `Invalid` と理由）、head の相対 path を作る関数 | Unity の Editor API なしで全て（形の表） | 0 → 約 120 |
| ＋ `…/Distribution/IndexedContentOrigin.cs` | head の file と revision の所在を、bytes の取得口から読んで答える取得元（directory 用の組み立てを含む） | infrastructure | 状態なし（呼ばれるたびに読む）。寿命は組み立てた host に従う | `ContentOriginHead`、`IContentArtifactSource`、`LocalContentArtifactSource` | `IndexedContentOrigin`（id と「root からの相対の所在から bytes の取得口を作る関数」を受ける）、`ContentOrigins.Directory(id, root)` | 3 種類の取得元の 1 つとして調停の表 + 照会の表（temp の directory） | 0 → 約 120 |
| ＋ `…/Distribution/ContentAcquisition.cs` | 取得元の列と pin から、対象の組を決め、到着点を見て、取得元を順に当たり、installer で install して結果を返す | orchestration | 1 回の呼び出しの間だけ。cache の所有は `ContentCacheStore` のまま | `IContentOrigin`、`ContentAcquisitionRules`、`ContentOriginGuard`、`ContentInstaller`、`ContentCacheStore`、`ContentManifestValidation` | `ContentAcquisition.AcquireAsync(request, origins, store, token)` | memory の取得元と temp の cache で、段の表の全ての行（Unity の Editor API なし） | 0 → 約 240 |
| ＋ `…/Distribution/ContentAcquisitionRules.cs` | 段と失敗の種類から、次の動き（次の取得元へ / どの状態で終わるか）を返す | policy（純粋な関数） | 状態なし | `ContentDeliveryFailureCode` | internal | 失敗 code の 12 値と未知の例外の全射の表 | 0 → 約 90 |
| ＋ `…/Distribution/ContentOriginGuard.cs` | 取得元の照会と、返した取得口・stream を包み、取消に応じない読みを打ち切り、取得元の側の例外を「答えない」「届かない」に揃える | infrastructure | 包んだ取得口と同じ寿命 | `IContentOrigin`、`IContentArtifactSource` | internal | 取消に応じない・例外を投げる memory の取得元 | 0 → 約 130 |
| ＋ `…/Distribution/ContentAcquisitionTypes.cs` | 取得の要求・結果・試行の記録の値の型 | 契約 | 状態なし | 同 folder | `ContentAcquisitionRequest`（contentSet・target・Unity の版・root schema・budget・任意の pin）、`ContentPin`（revision と digest）、`ContentAcquisitionOrigin`（取得元と信頼）、`ContentAcquisitionResult`（状態 8 つ: 取得した / install 済み / 見つからない / 整合性の失敗 / 対象が使えない / 到着点の同じ revision が違う digest / 到着点の失敗 / 取消、install の結果、対象の組、対象を決めた取得元、bytes を得た取得元、digest の出所、試行ごとの段と理由、消えた install） | constructor の検査 | 0 → 約 190 |
| ＋ `Editor/Build/Content/ContentOriginHeadWriter.cs` | 公開済みの 1 revision の directory を、親の directory の head に書く（読み直しての確認、置き換え） | 永続化（書き） | head の file の所有は、その root に publish する host。lock は持たない（後から書いた方が勝つ） | `ContentOriginHead`（Runtime）、`ContentStagingMove.IsTransient` と上限の定数、`System.IO` | `ContentOriginHeadWriter.Write(originRoot, publishedDirectory)`。取り消しの口は持たない（呼び手は確定の後の最後の段で呼ぶ） | temp の directory。置き換え・待ち・時計は注入 | 0 → 約 120 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBatch.cs` | （既存）batch の入口 | — | — | — | 成功の結果 file の確定の後の最後の段で head を書く（`Stages` に注入の口を 1 つ。失敗は今ある `Fail` が公開先を消し、結果を失敗で上書きする）。取得元の root は `ContentOriginSettings` の既定値を使う | 既存の batch の test に、段の成功・head の失敗・確定の失敗（head を書かない） | 202 → 約 220 |
| `SampleGame/…/Build/VerifyPlayLauncher.cs` | （既存）検証 Play | — | — | — | `Produce` の宣言の照合の後に head を書く呼び出しを 1 つ | 既存の段の test（head が書かれること） | 499 → 約 504 |
| ＋ `SampleGame/…/Build/ContentOriginSettings.cs` | この host の取得元の列（既定の root、信頼、場所の上書きの保存）を組み立てる | host の policy | `UserSettings/OSMContentOrigin.json`（git 管理外）に path 1 つ。Delivery の窓の `OSMContentDelivery.json` と同じく file にするのは、E 系列の driver が Editor を開く前に書けるようにするため | `ContentOrigins`、`System.IO`、`JsonUtility` | internal | path の解決の表 | 0 → 約 60 |
| ＋ `SampleGame/…/Build/ContentBaseSelection.cs` | 取得で選んだ base の記録（cache root・contentSet・target ごとに、対象の組、対象を決めた取得元、bytes を得た取得元、出所、選んだ時刻）の読み書き | 永続化 | `UserSettings/OSMContentBaseSelection.json`（git 管理外。Editor を閉じても残る） | `System.IO`、`JsonUtility` | internal | 読み書きと照合の表 | 0 → 約 80 |
| ＋ `SampleGame/…/Build/ContentAcquireLauncher.cs` | 取得の操作（RunPurpose から要求を組み立て、1 つずつ走らせ、取消を受け、選んだ base を記録し、Play が使う revision を確かめて結果の文を作る） | Editor の orchestration | 走っている操作 1 つと直近の結果を static に持つ（Editor の process の間。domain reload で失われる。選んだ base の記録は残る） | `ContentAcquisition`、`ContentOriginSettings`、`ContentBaseSelection`、`PlayEntrySettings.RunPurpose`、`SampleGameBuildPurposes`、`PlayEntryCandidates`、`VerifyPlayLauncher`（cache root、target）、`ContentDeliveryWindow.LoadSettings` | public static（`Start`・`Cancel`・`IsRunning`・直近の結果。`SampleGameContentBatch` と同じく、`eval` から呼べるようにする） | port を差し替えた単体（Play の間の拒否、二重の開始、取消、結果の文、選んだ base が Play に使えない時の文） | 0 → 約 190 |
| `SampleGame/…/Build/PlayEntryCandidates.cs` | （既存）installed cache からの候補の選択 | policy | — | `ContentBaseSelection` の値 | `Select` に任意の引数（選んだ base の記録）を足す。渡さない時は今と同じ。S4-2 の `ResolvePlayerBase` は変えない | 既存 + 並びの表（AC6） | 175 → 約 200 |
| ＋ `tools/s43a-local-origin.ps1` | §5 の E 系列の process の列（X の batch の publish、Y の Editor の case ごとの起動・`eval` の操作・終了、固定 patch の適用と戻し、fixture の用意、証拠の保存）を順に走らせる | 検証の driver（製品の code ではない。S4-V の基盤の上の scenario） | 状態は S4-V の基盤が持つ run の directory の中だけ | S4-V の基盤の module（process・worktree・run の directory・証拠・patch・Editor の adapter）。製品の入口は `eval` から呼ぶ（`ContentAcquireLauncher` の public static、`EditorApplication`、`SessionState`、入口の設定の setter は reflection） | 引数は S4-V の基盤の形に従う | 基盤の offline test が事故を見る。driver は `-DryRun`（case の列と fixture の組み立てだけ） | 0 → 約 150 |
| `SampleGame/…/Build/PlayEntryInterceptor.cs` | （既存）入口の配線 | — | — | — | `CollectFacts` が選んだ base の記録を読んで `Select` に渡す | launcher と candidates の test で見る | 287 → 約 290 |
| `SampleGame/…/Build/PlayEntryWindow.cs` | （既存）入口の窓 | 表示 | — | — | 経路が Built の系統の時のボタン 1 つ、Cancel、直近の結果の文 | 表示は test しない（launcher の側を test） | 210 → 約 245 |

行数警報: `VerifyPlayLauncher.cs` が 499 行から 500 行を超える。足すのは publish の後の呼び出し 1 つで、検証 Play の段という今の責務の中にある。分割はこのスライスの変更理由ではないので、分けない。他に 500 行、3 責務、50% 増に当たる file は無い。

分け方の理由: 調停（段の流れ）、止まる規則（表）、取得元の受け止め（取消と例外）を別の file にしたのは、変わる理由が違うからである（S4-3b が timeout の行を足す時は規則だけ、進捗を足す時は installer の側だけを変える）。head の読み（Runtime）と書き（Editor）を分けたのは、読みは全ての取得元が使いうる形の規則で、書きは publish する host の file system の操作だからである。`PlayEntryRecovery` は変えない（回復の値は test が完全一致で見ており、record にも書かれる）。取得のボタンは、経路が Built の系統の時に窓がいつも出す。

**head の file の形:** 置き場所は取得元の root の `.origin/<contentSet>/<target>.json`（contentSet と target は segment の検査を通った値。`.staging` と同じく、revision の directory と紛れない名前）。中身は `version`（1）・`contentSet`・`target`・`revision`・`manifestSha256`・`path`（root からの相対の directory で、その下に `transport.json` と `content/` がある）。上限は 64 KiB。読みの規則は `ContentPurposeIndex` に揃える: directory の取得元が head の file の不在を自分で確かめた時と、版 0 は「head なし」、取得口が開けない（`TransportFailure`。今の取得口は「無い」と「読めない」を区別しない）時は「読めない」、版 2 以上は「未対応」、形の失敗（型、欄の欠け、相対でない path、hash の形、contentSet・target が照会と違う）は「壊れている」。「head なし」は `NotFound`、「読めない」「未対応」「壊れている」は `Unavailable` と答える（どちらも次の取得元へ進むので、段と止まる規則の結果は変わらない）。revision を指定した照会には、head の revision と同じならその entry を、違えば「root の直下の revision の名前の directory」を所在にして、`transport.json` を開けた時に、申告の digest の無い `Found` を答える（directory の取得元の配置の規約。不在を確かめた時は `NotFound`、開けない時は `Unavailable`）。時刻の欄は持たない。

**選んだ base の記録の読み方（入口の並び）:** 入口は、記録の組（revision と digest）と同じ entry の時刻を「install の時刻と、選んだ時刻の新しい方」と読み、今と同じく新しい順に検証して最初に通った物を使う。取得の後に Delivery の窓や検証 Play で別の revision を**新しく** install すれば、そちらが先になる。既にある revision の再利用は receipt の時刻を変えないので先にならない（限定した規則。base を選ぶ規則そのものは S4-3d が持つ）。記録は cache root ごとに持つ。記録の revision が消えた・digest が違う時は、記録を使わずに今の並びで答えて Warning を出す。cache root の設定を変えれば、その root の記録だけが読まれる。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序: (1) 差込口の型、head の形と読み。(2) directory の取得元と、test の側の memory の取得元・包んだ取得元。(3) 止まる規則の関数と、その表の test。(4) 取得元の受け止め。(5) 調停と、段の表の test（3 種類の取得元）。(6) head の書き手と、batch・検証 Play の段。(7) host の取得元の列、選んだ base の記録、入口の並び、取得の操作、窓。(8) 実機を伴う統合の test。(9) E 系列の driver（Phase B は `-DryRun` までを通す。Unity を起動する初回は Phase C）。
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
  - `SampleGameContentBatchTests` の追加（AC7 の batch の段: 成功、head の失敗、確定の失敗での取り消し）、`ContentBaseSelectionTests`、`PlayEntryCandidatesTests` の追加（AC6 の並び）、`ContentAcquireLauncherTests`（AC6・S7 の結果の文）。
- 統合・Unity テスト: `PublishedOriginAcquireIntegrationTests`（合成の小さい宣言を製品の coordinator で build・publish し、head → 取得 → 選んだ base の記録 → `PlayEntryCandidates.Select` がその revision を返すまで。build を伴うので、process の自然終了を合否にしない）。
- 差し戻し中の起点 `-Filter`: `ContentOrigin|ContentAcquisition|IndexedContentOrigin|PublishedOriginAcquire|SampleGameContentBatch|ContentBaseSelection|ContentAcquireLauncher|PlayEntryCandidates|PlayEntryDecision`（C が根拠付きで変えてよい）。
- 判定必須テスト（GO 候補 head）: 上の単体と統合の全てと、全 EditMode 回帰（AC11）。
- 全 EditMode 回帰の適用除外: なし。
- 操作・実行時・目視条件の検証経路（E 系列。改訂 1 で外部計画レビューの推奨を採った。§5.1 の EX-1〜EX-8）: 担当は判定 C の実行担当。環境は Windows の開発の PC、Unity 6000.6.9f1。GO 候補 head の、clean な 2 つの検証専用の worktree X と Y（別の `Library`）を使う。X は publish する checkout、Y は取得して Play する checkout で、Y の取得元の場所（U5）を X の `artifacts/bs2b/transport` にする。実行は driver `tools/s43a-local-origin.ps1 -XRoot <X> -YRoot <Y> -RunId <id>` の 1 回で、手で `eval` を打たない。
  - **driver の約束:** driver は S4-V（program §8 の行と §9.1。検証の基盤）の上に、S4-3a の case の列・Local の取得元の fixture・照合する値だけを書く。process の起動と終了、worktree の事前確認と復元、run の directory と cache、証拠の退避、patch の適用と復元、窓のある Editor の adapter（操作を送る `eval` は送り直さずに状態を読んで効いたかを確かめ、状態を読む `eval` は読み直す）、未完了の run の記録は基盤が持ち、ここに写さない。S4-3a に固有の約束: (1) run の最初に、基盤の Editor の疎通（起動 → ready → `eval` → 終了）を Y で走らせてから P1 に入る。(2) Editor を開く前に Y の `unity/UserSettings/OSMContentDelivery.json` の `cacheRoot` を run の cache に、`OSMContentOrigin.json` の場所を X の `artifacts/bs2b/transport` に書き、`OSMContentBaseSelection.json` が無いことを確かめる。(3) batch の publish は X で `-batchmode -nographics -executeMethod SampleGame.DependOnAll.Editor.Build.SampleGameContentBatch.BuildAndPublish -osmPurpose spring-full` を基盤の batch の起動で走らせ、終了コード 0 と `last-result.json` を得る。(4) `eval` で呼ぶのは製品の入口（`ContentAcquireLauncher` の `Start`・`IsRunning`・直近の結果、`EditorApplication.EnterPlaymode`・`ExitPlaymode`、Play の record を持つ `SessionState`）と、入口の設定（RunPurpose・経路）の製品の setter（reflection）だけ。各 Play の前に `SessionState` の Play の record を消し、読んだ record が今回の Play の物であることを保つ。(5) case ごとに、下の表の証拠と、checkout の識別（HEAD の SHA、`Library` の path、`ProjectVersion.txt`）、設定の file の写し、`installed/` と `staging/` の一覧（名前と `receipt.json` の SHA-256）、batch の process の開始時刻を、次の case が上書きする前に基盤の証拠の置き場へ退避する。(6) driver は合否を判定しない。照合は判定の担当が保存された原記録で行う（下の表）。
  - **case の列（順に走る）:**

| case | checkout | 用意（driver） | 操作 | 保存する証拠 |
|---|---|---|---|---|
| P1 | X | 無し | batch で R1 を publish | `last-result.json`、head の file、`batch.log` |
| E1 | Y | 無し（空の cache） | RunPurpose を `spring-full`、経路を Built にする → 取得 → Play に入り record を読んで止める | 取得の結果、選んだ base の記録、R1 の `receipt.json`、一覧、record、`editor.log` |
| P2 | X | 無し | batch で R2 を publish（head は R2 を指す） | P1 と同じ |
| E2 | Y | 無し | 取得 → Play | E1 と同じ（R2 の `receipt.json`） |
| E3 | Y | fixture: X の head を P1 で保存した bytes に戻す（前後の SHA-256 を記録） | 取得だけ（Play しない） | 取得の結果、選んだ base の記録、R1 の `receipt.json` の前後の bytes の SHA-256、前後の一覧 |
| E4 | Y | 無し（新しい Editor の process） | 取得せずに Play | Play の前の一覧（R1 と R2）、選んだ base の記録、record、`editor.log` |
| E5 | Y | fixture: (a) 取得元の場所を run の fixture の root の下の無い directory にする。(b) 取得元の場所を fixture の root にし、R1 を指す head の写しの `manifestSha256` だけを別の 64 桁の hex に変えて置く | (a) 取得 → (b) 取得 → 取得元の場所を X に戻して Play | 2 つの取得の結果、前後の一覧（`installed/` と `staging/`）、前後の選んだ base の記録、fixture の head、record、`editor.log` |
| E6 | Y | patch `PlayerOverlayEdits/ac01-title.patch`（S4-2 の固定 patch をそのまま使う） | 経路を Built + local edits にして Play → `Title` の上書きの値と、`AddScene` した `HomeScene` の `GetContentLoadRecord` を読む（[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」の観測の注意の reflection の経路）→ 止める | record（上書きの欄）、読んだ値、load の record、patch の名前と適用後の差分、`editor.log`。拒否や上書きの build の失敗で Play に届かない時も、理由と、届かなかった観測の項目を残す |

  - **判定の担当が照合する値（AC9。P1・P2 の `last-result.json` を R1・R2 の正とする）:** 前提として、R1 と R2 の `identity` が違い、P1・P2 の `last-result.json` と head の file の更新時刻がそれぞれの batch の process の開始より後である（前の run の結果を取り違えない）。E1 は、結果の状態が「取得した」、対象の組が R1 の (`identity`, `manifestSha256`)、対象を決めた取得元と bytes を得た取得元が SampleGame の既定の directory の取得元の id、出所が申告、選んだ base の記録と R1 の `receipt.json` の digest が同じ値、record の revision が R1 の `identity` で state が `EnteredPlay`。E2 は同じことを R2 で見て、一覧に R1 と R2 がある。E3 は、状態が「install 済み」、対象の組が R1、bytes を得た取得元が空、R1 の `receipt.json` の bytes と一覧が前後で同じ、選んだ base の記録が R1 で選んだ時刻が E2 の記録より新しい。E4 は、Play の前の一覧に R1 と R2 があり、record の revision が R1 で state が `EnteredPlay`。E5 は、(a) が「見つからない」、(b) が「到着点の同じ revision が違う digest」、`installed/` と `staging/` の一覧と選んだ base の記録が前後で同じ、record の revision が R1 で state が `EnteredPlay`。E1・E2・E4・E5 の record の `errorCount`・`exceptionCount` が 0 で、取得の開始から Play の停止までの `editor.log` に Error と Exception が無い（`Failed to handle /api/exec request` の行は数えない）。
  - **E6 の記録（AC10。測定）:** 4 つの観測（record の `overlayRoots` と `unbuildable`、`Title` が手元の版で出たか、`HomeScene` が base の record で載ったか、Error の有無）を、base の revision（R1）、X と Y の HEAD と `Library` の path と一緒に記録する。各観測は「観測した（肯定）」「観測した（否定）」「観測できなかった（理由）」のどれかで書き、観測できなかったことを否定の答えにしない。合否は付けない。AC10 を満たすのは、4 つの観測の全てが前の 2 つのどちらかで記録された時である。
- 未知の操作経路の疎通結果: Phase A で code から確かめたこと: Play ボタンの入口は batch と Test Runner の Scene を素通りするので、Play を見る case は `run-tests.ps1` の test では組めない（§1 の現況）。実 Editor の Play を `eval` で操作して `SessionState` の record を読む経路は、S3-4b の判定と S4-2 の判定 C の追加で通った既存の経路である。窓のある Editor の起動・ready・`eval`・終了の自動化は S4-V が持ち、S4-V の Phase C の疎通で確かめる。S4-3a で未確認なのは取得の操作の static の入口（Phase B が作る）と、E 系列の case の列で、初回確認は Phase C（Unity の起動と batch の build を伴うため。Phase B は `-DryRun` まで）。不成立の時の対応: 起動・ready・終了の不成立は基盤の不備として S4-V の基盤で直す（program §9.1 の境界の規則。S4-3a の driver に迂回を書かない）。手作業の `eval` や人の操作に置き換えて未確認を埋めない。`eval` から取得の入口を呼べない時は、製品の入口の不備なので B へ戻す。E6 だけが上書きの build で止まる時は、AC10 を未実施として判定を保留し owner へ返す（§2.7）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（Player の build、実の R2、人の観察は無い）。
- 判定 C の照合 script の方式: なし。合否は test の assertion と、E 系列の保存された原記録を判定の担当が上の表のとおり照合して決める。driver は原記録を保存するだけで照合しない（Phase B が書く git の中の script に判定 C の照合を持たせない。[判定 C の照合 script](../../.agents/skills/osm-workflow/references/review-evidence.md#判定-c-の照合-script)）。
- 機械検査: AC4・AC5・AC6 の「変えない file」の差分、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: Claude Code の cloud session（Anthropic）。この session の規則で、model の名前は repository に書かない（owner への報告で伝えた）。
- C' 用に予約した担当: Anthropic と OpenAI 以外の系列（Phase A の A2 は Anthropic、外部計画レビューは OpenAI）。Google の系列は使えないので、候補は Grok（`cursor-agent`）。割り当ては owner が行う。
- 独立性の強化条件を満たせない場合の理由: Phase A の A2 は 3 件とも主担当と同じ vendor である（§5.1）。別の vendor の観点は、Draft PR の外部計画レビューで補う。外部計画レビュー 3 件（PR のコメント）の担当は OpenAI の model（web の ChatGPT。owner が 2026-11-25 に伝えた）（§5.1）。
- 改訂 1 の主担当: 初版と別の Claude Code の session（Anthropic。初版の会話は引き継がず、PR と本書から始めた）。

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
| RX-2（P1） | 最後の copy の後に取消を見ないので、確定点を決める。guard が読みを放棄する時の buffer、遅れた stream と例外、同期の open と破棄の契約 | **採用。** rename を確定点にし、成功の後に取消へ変えない。guard は自分の buffer で読む。遅れた stream と例外を guard が受け止める。同期の `OpenArtifacts` と `Dispose` は待たない契約（N3、段の表の取消の行、AC1、AC3） |
| RX-3（P1） | lock の無い確認と置き換えの head の巻き戻しは、並んだ書き手で別の publish を消しうる。確定前の head を読み手が見る | **採用（巻き戻しを無くす形）。** batch は `publish → 結果の確定` の順で、失敗の処理が公開先を消して結果を上書きする（`SampleGameContentBatch.cs:71-83,105-127`）。head をその後の最後の段で書けば、巻き戻しも確定前の head も無くなる。並んだ書き手は後勝ちとだけ明記する（AC7、§3） |
| RX-4（P1） | 「install の時刻と選んだ時刻の新しい方」は「直近の明示の操作」ではない。再利用は receipt を変えない。取得の成功と Play の選択を分け、失敗・記録の消失・保存の失敗・設定の変更・同時の install を定める | **採用。** 限定した規則として N6・S7・AC6・§3 に書き、結果の文にも出す。Delivery の窓と検証 Play にも記録を書かせる案は、変えない file（AC6）に触れ、base を選ぶ規則を持つ S4-3d の先取りになるので採らない |
| RX-5（P2） | 取得口は「無い」と「読めない」を区別しない。段 2 の後に別の操作が同じ revision を入れた時の `IntegrityMismatch` | **採用。** 見分けられない時は `Unavailable`、directory の取得元は不在を自分で確かめた時だけ `NotFound`（§3 の head の形）。`IntegrityMismatch` の時は cache を読み直し、対象が増えていれば到着点の側として扱う（段の表） |
| RX-6 | driver が最終の合否を判定しないことに同意。判定の担当が独立した照合 script を作る選択肢は残してよい | **一部採用。** 原記録は機械で読める JSON で残す。照合 script の方式は「なし」のまま（判定 C の offline 実行の方式は、資格情報と通信が遮断された環境を要し、開発の PC はそれを満たさない） |
| RX-7 | R1・R2 が違う identity で今回の新しい出力であることを確かめる。E4・E5 も state を照合する。古い `SessionState` の record を読まない | **採用。** §5 の照合する値の前提、E4・E5、driver の約束 (4)(5) |
| RX-8 | E6 は観測の完遂が条件。拒否された時も理由と届かなかった観測を残し、観測不能と否定を分ける | **採用。** §5 の case の表と E6 の記録、AC10 |
| RX-9 | P1 の build より先に Editor の短い疎通。`eval` の timeout を操作と読みで分け、二重に送らない | **採用。** 疎通と `eval` の扱いは S4-V の基盤が持ち、S4-3a は run の最初に走らせる（driver の約束 (1)） |
| RX-10 | D-2 済み、D-3 は E6、D-4 は high を支持。OpenAI を C' の未関与に数えない | **採用。** §2.9、§5 の C' の予約 |
| SX（3 件目） | S4-2 の driver・`run-tests.ps1`・`tools/unity-editor.ps1` の共通部分を小さな基盤にし、S4-2 と S4-3a の driver が使う。写してはいけない弱点の一覧と offline test | **採用（独立の行にする形）。** owner の決定で S4-V にした。要求は S4-3a の都合ではなく program §9.1 の利用者表（S4-2〜S4-4、S5）から決め、各行の driver は基盤の外に process と worktree の扱いを書かない（§9.1 の境界の規則）。弱点の一覧と offline test の項目は S4-V の A0 の入力 |

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
