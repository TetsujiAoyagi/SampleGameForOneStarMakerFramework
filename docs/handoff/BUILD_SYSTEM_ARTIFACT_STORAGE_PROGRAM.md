# BuildSystem Artifact Storage Program

## 0. Metadata

- type: `program`
- status: programは進行中。スライスAはPR #109で実装・限定運用検証と正式C/C′を完了（両方GO、blocker 0）。固定仕様と実行集合・原観測の入口は `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle`。旧限定resetは完了済みで、再実行しない。後続B〜DのA3は別途行う。
- program policy revision: `r7` — 単一の開発用保存契約にContent種別を追加（§2）。旧データと互換性を引き継がない。
- branch: `codex/artifact-storage-development-policy`
- planning base commit: `a027bf04b4f5ce16494b93daf10e5b5bccf15a28` (`origin/develop`; PR #106・#107を含む)
- implementation head commit: not applicable; 文書改訂のみ。
- risk: `normal`（文書改訂）。リセットと削除機能の対象・検証は実装スライスで具体化する。
- owner: OSM maintainers
- created: 2026-09-26
- updated: 2026-10-15
- expires: 2026-12-26 または置換revision（program文書の見直し期限）
- harvest to: 実装済みの通常操作を `tools/Artifacts/README.md`、program状態を `docs/README.md` へ反映し、program完了時に削除する。

## 1. 目的・要件変更・現在地

開発中の成果物を非公開で手軽に保存・受け渡しし、不要になったものは自動で片付ける。人間が毎回ファイルを運んだり、保存・清掃を個別承認したりせずに使えることを成果とする。

[ownerの要件変更](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/108#issuecomment-6049302344)と本チャットの修正指示を採用する。本番運用はなく、過去の開発用Artifact・試験設定はリセット可能。旧証拠の保存継続、旧参照/readerとの互換、PRごとの移行台帳を要求しない。物理的な試験物の不存在を確認した、という意味ではない。

この指示を根拠に、r5のlegacy分岐、旧最低保存義務と日程、参照中/参照不明の無期限保持、keyごとの削除承認を置き換える。**PR #107のE1/E2へのIndefinite追加、reader互換、新保護事実recordの実適用タスクは取りやめる。** 過去の方式確認の成果は否定せず、経緯はPR/Git履歴に残す。撤去する方式を一度完成させてから移行する工程は作らない。

現在の実装機能は [Artifacts README](../../tools/Artifacts/README.md) を正とする。スライスAのEvidence v2、workflow taskの終了/再開配送、純粋保持policy、自動清掃と一回の限定resetは検証を完了した。正式C/C′は同じ実装headでGO。検証集合、限定実観測、既知ACL不適合copyの保護によるpartial結果と未確認範囲は、PR #109およびCURRENTの固定記録に残す。

- 資格情報管理、Route proof、synthetic publish/fetch、固定E1/E2の別session取得・hash検証・ログ/画像閲覧はPR #76/#81/#96/#104で成立した。
- PR #106のreader offline可搬性は完了。判定head `19720ed7c6065ffcc55ee2643f811462025a940d`、非秘密dummy fixture、7suite 120/120、3 build、限定不在probe 2/2。現在も役立つ部品・テストを再利用する。
- PR #107の方式確認・移行契約はPhase D完了。判定head `d05277dc02d4cb159e1c4c0068fb947cff22e39b`。本番適用は未実施のまま取りやめる。試験用W/Lと2rule等は一回のリセットで整理する対象候補とする。

主な利用者は同一Windowsユーザーで動くローカルAgent。既存R2・DPAPI・検証済みの転送部品を使い、Cloud、別provider、BuildSystem全体の刷新を先行条件にしない。

## 2. 単一の保存契約

### 開発Build

成功publishをBuild系列ごとに最新N件保持し、超過分は削除可能な古いものから清掃する。既定値案は10件。系列はproject・target/platform・configurationを出発点にBで固定し、branchごとに無制限の枠を作らない。

利用中のBuildは削除対象から除外し、N件には数える。保護だけでNを超える間は超過を表示して許容する。失敗/未完了publishは成功件数に含めず、失敗によって既存の成功Buildを追い出さない。最低保存日数やowner Close待ちは設けない。リリースの保存・配布は必要になった時の別責務とし、今回作らない。

### 開発Evidence

- 作業中は保持する。workflowがtaskの完了/打切りを一度記録し、同じ終了イベントからStorageへ`taskEndedAt`、`endReason = completed | cancelled`、`deleteEligibleAt = taskEndedAt + 30日`を登録する。Evidence専用のowner Close承認は作らない。
- workflowがtask終了の意味を所有し、Storageは終了日時・削除可能日時・利用中保護だけを扱う。Phase D等の品質判断を変えず、Storageにそのイベント形式やレビューの合否を再判定させない。C/C′が揃っただけ、Agentの終了、失敗、timeout、一時停止をtask終了と推測しない。
- Harnessの現行`close` / `closed.json`は実行記録の確定であり、そのままtask終了へ読み替えない。新Storageのコピーはこの単一契約で管理し、Harnessの原sourceの清掃はHarness側が所有する。Harnessの`retainUntil`をStorageコピーの第二の保持時計にしない。同じ実体を双方が管理する接続はDで所有者を一方へ寄せる。
- 終了イベントの反映失敗は再実行でき、同じイベントの再適用で起算日を更新しない。再開時は削除資格を取り消し、再終了から30日を数え直す。再開と終了の版・順序を区別し、古い終了イベントの再送で再開を打ち消さない。
- 期限に達したら利用中でない対象を通常清掃する。古いPRリンク、inspect、GETだけでは延長しない。調査等で必要なものだけ明示的に利用中保護し、解除条件または期限を見えるようにする。
- 未終了Evidenceをupload後の日数だけで強制削除しない。終了記録の取りこぼしは接続と再実行で直し、本当に継続/打切りが未決のtaskはinspectに表示する。新しいlease、heartbeat、定期的なowner再承認制度を足さない。
- 恒久的な設計判断・制約・検証要約は通常の公開文書へ残す。生ログ・画像・bundle・派生copy・witnessに長期監査保存を課さない。削除後の参照は期限切れと分かればよく、payloadの復元は保証しない。

### 開発Content

Content Directory（`transport.json` + `content/`）はPlayerとは独立した第三の保存種別とする。BuildとEvidenceの契約は変えない。ここは保存契約だけを決め、実装・Unity変更・取り込み方式は§4の後続に送る。

- **単位:** revisionごとに `transport.json` + `content/` の配置を丸ごと不変単位とし、上書きしない。manifestのschemaは保存側が中身を見ず、検証は消費側が行う。差分upload・content-addressed・GCは採らない。
- **保存形式:** ZIP 1個にせず、revisionのprefix配下にfile単位のobject（`transport.json` と `content/<path>`）として置く。取得側がmanifestに載った各fileのSHA-256で、不足fileだけを判定して取れる形にする。再利用する箇所は「R2からArtifactsRoot」と「取り込み元からDIST cache」の2つで、仕組みは後続が所有する。Buildの選択集合の上限（512 file・240 MiB）はContentの前提にせず、r7は新しい上限を足さない。Content固有の上限とR2のプラン変更の要否は、実測後に決める。
- **系列:** `contentSet/target`。platformはtarget値で表す。パスに使うtargetの値は1つで、manifestの`target`（`build-info.json`では`contentTarget`）から一意に写像する（具体的な対応表は実装スライス）。ローカル（NTFSとDIST cacheの配置判定）は大文字小文字を区別しないため、大文字小文字だけが違う`contentSet`やrevisionを別のものとして受け入れない。また、パス要素として安全な範囲に限る（具体規則は命名規約の後続）。用途（通常開発・期間限定イベント・次期大型アップデート等）は`contentSet`の名前で表し（例 `mainline`、`event-<名>`、`next-major`）、階層は足さない。manifest・検証・Playerのbaked設定は変えず、系列と件数保持は用途ごとに独立する。revisionの昇格・改名は行わず、系列間の統合はソース側で行い、統合先の`contentSet`でBuildし直す。
- **保持:** Buildと同じ件数保持policyを系列ごとに適用する（成功のみNに数える、利用中保護、失敗は既存を追い出さない）。Nの既定値案はBuildに揃えて10件。保持中のPlayerが受け入れうるContent revisionは利用中として保護しNに数える（現状のUnity契約では、Playerのbaked設定の`content:buildIdentity`・`content:contentSet`・`content:target`で1件に決まる。保存側が引く手がかりは、`build collect`が退避先に記録する`build-info.json`の`contentSet`・`contentTarget`・`buildIdentity`・`transportManifestSha256`である。Unity側にも同名で別schemaの`build-info.json`があるが、ここでは指さない）。保護状態を収集できないときは削除しない。
- **置き場:** ローカルは`<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/`。これは取り込み元（install source）で、消費側のDIST cache（known-good・lease・eviction）とは別の層であり、削除する主体は層ごとに別とする。ArtifactsRootはPlayerと共用の任意のローカルfolderで、同じrevisionを二重に消さない。R2にもrevision単位で全fileを丸ごと上げる（差分uploadは採らない）。
- **消費側:** ストレージに可変の別名（latest等）を持たせない。消費側はrevisionとmanifestのSHA-256で固定する。見る系列の選択（git管理外のローカル設定）と「最新」の解決はUnityのDISTの外で行い、新旧の順序はrevision名ではなく保存側の確定記録で決める。確定記録は順序とmanifestのdigestを持つ追記だけの記録で、指す先が更新で変わる可変の別名（latest等）としては使わない。
- **hd/sd:** 端末性能別はrepresentationを第一候補とし、revisionを割る場合は`contentSet`に含める。系列keyは`contentSet/target`から変えない。根拠は、representationがContentの候補ごとに1値を持つ属性で、Playerでは起動時に1値に固定されること。1 revisionへの複数representationの同梱可否は未確認で、後続が確認する。

**前提（Unityの現状。r7は変えない）:** Playerは自分のBuild時の`contentSet`とBuildIdentityに結び付き、配布済みPlayerにContentだけを差し替える運用はできない。起動時の`contentSet`切り替えは行わない。Playerとの互換を計算した「互換キー」で判定する案は、Content分離の実装スライスのA1で最初の選択肢とする。保存契約はどちらの案でも変わらない。全platformで保存形式は1つだが、transport manifest v1の`target`は固定値`StandaloneWindows64-Player`で、Windows以外への拡張は後続とする。

### 未完了uploadとstaging

初期値案は7日。ツール所有の一時物で、転送中でなく、レビュー入力へ採用されていないものだけを短期清掃する。失敗ログをEvidenceに採用した場合は通常のEvidence契約へ載せる。利用者の原source、資格情報store、他の作業領域は対象外。

### 日常操作

通常publishは明示した非秘密file集合をsnapshotし、送信・読戻し検証・必要な記録を内部で完了して、参照と信頼できる期待hashを返す一操作とする。利用者による独立台帳確定、毎runの設定撮影、保護witnessの破壊試験は要求しない。fetchは別に渡された期待hashで同一性と安全な展開を検証する。

設定した開発領域・契約内の保存/取得/清掃は通常の事前承認で回す。WORM、長期server lock、旧証拠の継続取得、二重policyを共通条件にしない。通常writerにbucket管理権限は与えず、削除拒否をrule解除で自動突破しない。

## 3. 一回の開発環境リセット

切替は **対象確認 → 旧利用停止 → 旧生成物と不要な試験設定の整理 → 新契約で開始** の一回にまとめる。旧payloadを移行・救済せず、旧hash橋渡し台帳や旧close復元を作らない。旧schema/参照は新CLIで明確に非対応を返し、自動変換や旧readerの継続利用を保証しない。

対象確認は誤操作防止のために行う。この開発Storageの旧objectとツール所有の生成物・試験ruleに限定し、E1/E2、locked witness、過去のraw/受取copy、#107のW/Lと試験2ruleも区別なく整理対象へ含められる。資格情報store、利用者の原source、Harness原source、無関係なbucket/ruleや他作業領域は除外する。PR別の保存期限や保全審査は設けない。

実行担当は実在するkey/path/ruleと解除可否、他対象への影響を確かめ、対象を列挙してから操作する。bucket全消去や全rule collectionの無条件置換はしない。旧利用停止後の部分失敗は結果を残して対象限定で再実行し、旧経路への自動復帰や二重運用は作らない。設定整理に必要な管理操作はこの一回の担当が行い、日常清掃とは分離する。

r6の方針改訂だけではリセットを開始しない。スライスAの実行は固定対象と開始条件、CURRENTの固定入力に従う。旧保存義務がなくなったことを、現rule解除済み・物理データ不存在・任意領域を削除可能という主張に置き換えない。具体的なコマンドはArtifacts README、対象と除外はスライスAの固定仕様で確認する。

## 4. 次に進めるスライス

### A. Evidenceの通常利用と期限清掃（完了）

**問い:** 任意の明示した非秘密file集合を保存し、別sessionで取得・hash照合・ログ/画像閲覧でき、作業中は残り、終了後は自動で片付くか。

**最低条件:**

1. 固定E1/E2の入力定数を通常のtask/file集合契約へ置換する。publishを内部検証・記録・参照/hash返却まで一操作にし、別sessionで取得・必要な内容を閲覧できる。旧形式対応は作らず非対応を明示する。
2. workflowの終了/再開イベントとStorageの期限を自動接続する。終了の意味と発行元をA3で固定し、Evidence独自の承認やHarness closeの推測接続をしない。イベント再適用は同じ結果になり、再開後は再終了まで削除資格を持たない。
3. inspectと実際に動く清掃、自動起動を同じpolicy判定へ接続する。30日の境界、利用開始/再開と削除の競合、対象外・転送中の非削除、部分失敗の再実行を扱う。利用中保護の状態を判定できなければその対象は削除せず理由を表示する。終了未決taskを年齢だけで消さない。
4. §3の対象限定リセットと、旧経路・schema・不要な互換説明の置換を一回の切替手順にする。原sourceの保護、現ruleの実態確認、設定整理と日常清掃の権限分離を満たす。古い証拠の保存継続を完成条件にしない。
5. Artifacts READMEを実際の新CLIの操作・状態に合わせる。旧保存義務やlegacy分岐を復活させず、切替時に旧経路の説明を除く。新しく設定した範囲内の通常清掃にkeyごとの承認を求めない。

**検証:** 保存→別session取得/hash・ログ/画像閲覧、30日境界、終了イベントの再適用、再開後への古いイベント到着、利用開始/再開と削除の競合、対象外の非削除、部分失敗の再実行、秘密非露出、安全な展開。時刻注入、#106のdummy fixture、隔離した使い捨てデータを使う。実R2の限定往復/清掃は運用経路の確認として行い、旧E1/E2継続取得や30日実待機を要求しない。自動清掃の起動・再実行が人の反復操作なしで成立する経路もA3で具体化する。

判定は関連Artifacts/Harness offline suiteと限定R2検証。Unityを変更しないため全EditMode回帰は適用外。スライスAのschema/API、file配置、清掃間隔、7日の初期値、リセット対象/操作はA3 r6に固定済みで、Git外CURRENTから取得する。実装時に必要な終了イベント接続だけを扱い、Harness全体の改造へ膨らませない。

**停止規則:** 上記の通常利用・期限清掃と一回の切替が成立したら閉じる。Build、Cloud、GUI、常駐broker、別provider、長期保護・互換維持を追加条件にしない。

Aの完了後の小さな追加として、[PR #121](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/121) でEvidence CLIの失敗理由を区別した（判定C GO、最終C′ PASS）。publish・fetch・inspect・use・releaseは、configの読取・検証の失敗を `invalid-evidence-config`、deployment receiptの不在・読取不能を `unknown-deployment`、publishのselectionの読取失敗を `invalid-selection` で返す。相対pathは従来どおり受け付けない。操作は[Artifacts README](../../tools/Artifacts/README.md)にある。後続への入力（所有スライスは未定）:

- cleanup・schedule-install・schedule-statusのconfig失敗のreasonCode（結果の形とexitの意味を変える判断が要る）。
- receiptの `baselineSha256` 等のhex不正がまだ `unconfirmed` になる。`Read-EvidenceConfig` のtryが広く、想定外の例外も `invalid-evidence-config` になる（fail-closed）。
- `workflow-task.ps1` は例外messageをreasonCodeに写すため、configを2回読む間に壊れる競合でだけ `workflow-unavailable` が新しいcodeに変わる（受容済み）。
- 試験範囲の不足: CLI経由のconfig失敗のstdout・stderr、fetch・use・releaseの `unknown-deployment`、guard非取得の直接観測。
- `EvidenceCleanup.Tests.ps1` の1 caseは `rg` をPATHに要する（READMEに記載）。判定必須で全suiteを流す前に通す。

### B. 開発Buildの保存と件数管理

単一Build publishは[PR #111](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/111)で実装し、offline 60caseと既存実Build 266fileのR2往復・全hash一致を確認、正式C/C′を完了した。現在の入口・成功確定・上限・限界は[Artifacts README](../../tools/Artifacts/README.md#開発build-1件の保存と取得)へ集約し、完了スライスHANDOFFはharvestした。利用中状態・実DELETEとN件清掃は下記のPR #119で接続した（R2上の系列全件一覧は後続のまま）。準備helper失敗による未記録・未送信private残置も有限清掃への入力とする。

最初の最小スライスは[PR #110](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/110)の純粋な保持選別policyです。1系列のsnapshotとNから保持・削除候補・保留を決める内部APIとoffline検証は実装済みで、契約は[Artifacts README](../../tools/Artifacts/README.md#開発buildの件数保持policy)へ集約しています。系列・利用状態の収集、policyへの運用接続、実DELETEは下記のPR #119で接続しました。

Player Buildからpublishまでの接続は[PR #115](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/115)の `build collect` で完了しました（正式C GO。C′の証拠不足はownerによるR2の直接観測で解消し、Phase Dで採用）。BS4の配布形Player 1件を `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` へ退避し、`build-info.json` とselectionを生成して既存publishへ渡します。判定用の実Player 266 fileで、実R2往復と再実行時のPUT無しを確認しました。現在の操作は[Artifacts README](../../tools/Artifacts/README.md#bs4-player出力の退避と保存build-collect)にあります。このスライスから後続へ送った問いは次のとおりです。

- Contentの保存契約は§2の開発Contentでr7として固定した。実装は次のスライスに分ける。Content分離・Content保存・Content取り込み・統合Contentフォルダ・`contentSet`の命名規約は新規、Contentの容量上限と件数保持の運用接続は既存の後続への入力とする。各スライスは着手時にA1から行う。
  - Content分離: Playerが`content/`を内包しない場合のUnity側の変更（候補は、U1: 今のまま内包して重複保存、U2: 既存の`content:installedRevisionPath`+`content:manifestSha256`の組で外部revision rootを指す、U3: 新しいconfig keyの追加。U2は、外部revision rootがDIST cacheの`installed/`配置とreceiptを要し、BuildIdentity一致を要求する点、baked `relativeDirectory`の扱い、起動時のlatchとの両立を実装スライスで確認する。EditorはUse For Next Playでlocal directoryのrevisionを取り込み元にでき、private なR2はUnityのHTTPの口では直接読めない）、`build collect`の検査・`build-info.json`・selectionの変更、互換キー案の判断。
  - Content保存は[PR #118](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/118)で完了した（判定C GO。C′の残り1件はownerが非阻害と判断）。`content collect`／`inspect`／`fetch` で、Content出力1件をArtifactsRootへ退避し、file単位objectとしてR2へ置き、別processで取得して全fileのhashを照合する。実Spring出力（219 file、PUT 220、760 s）で最終headの実R2往復を確認した。操作・上限・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#開発content-1件の保存と取得content-collect--inspect--fetch)にある。HANDOFFはharvestして削除した。決めたことと後続への入力は次のとおり。
    - 決めたこと: 完了確定点はローカルreceiptのrenameだけ（R2に完了マーカーを置かない）。公開は全objectを「子processのGETで照合→404ならPUT→子processの読戻し」の1経路で行い、`transport.json` を最後に置く。target表は `StandaloneWindows64-Player` → `windows-x64-player` の1行。contentSetは小文字英数字とハイフンの安全な部分集合（既存5値は全て入る）。R2 keyは `development/content/v1/<repositoryId>/<contentSet>/<target>/<revision>/`。PlayerとContentの保存順は決めない（Content分離が決める）。途中で止まったR2 objectは「未完了uploadとstaging」に載せず、同じrevisionの再実行で続行する。
    - 後続への入力: R2側に上書き防止が無く、別PCの同時公開を防がない（Content確定記録）。予算の確認とreceipt確定の間の窓、予算が退避・snapshotの区間に掛からないこと、collectが各fileを約7回読むこと、0 byte fileの拒否、転送中の展開量上限（Contentの容量上限）。公開・取得で残るprivate operationの清掃（未完了uploadとstaging）。content storeが既存directoryのACL不一致を付け直すこと（Build storeは拒否）、各objectの照合結果をoperationに記録しないこと、`existing` 結果に `objects` が無いこと、失敗時の `publish` 表示（下の診断の改善）。4季節 `all-full` の実測（1 revision・約881 object・約22 MiB以下の見積もり）は未実施（Contentの容量上限）。
  - Content確定記録（Content保存のA3で新設）: 新旧順序とmanifest digestを持つ追記だけの記録の形式と書込み方式（LISTまたは条件付き書込みの要否）、revision削除時の記録の扱い、「最新」の解決の保存側の部分、別PCからの同じrevisionの同時公開の排他。
  - Content取得と再利用（Content保存のA3で新設）: R2からArtifactsRootへの配置と、その際の不足fileの再利用（既存revisionのhash一致fileの流用）。
  - Content取り込み: 取り込み元からDIST cacheへの不足fileの再利用（read lease付きのコピー）、cacheのeviction・leaseとの関係、アプリ更新を越えるcacheの永続性、Unityバージョンとroot schemaの束縛、HTTPのfileごとのdeadline。
  - 統合Contentフォルダ: 取得型／監視型、複数ワークスペースの衝突規則、統合フォルダとArtifactsRootの同一性、共有時の削除主体、ローカル設定と選択UI、「最新」の解決の所有。
  - `contentSet`の命名規約（Content保存のA1で扱う）: 用途×構成×性能の合成名、既存値`bs4-spring-full`を改名しないこと、大文字小文字とパス要素の安全規則、ブランチやBuild設定から決める方法、hd/sdを1 revisionに同梱した場合の部分取得。
  - Contentの容量上限: Content固有の上限値、R2のプラン変更の要否、R2の操作回数の枠（file単位objectのPUT数）、R2の途中失敗で残るobjectの孤児回収。上記のBの容量上限の固定（BのA3）の入力とし、Content保存のA0では手元にSpringの出力しか無く、4季節（`all-full` 1 revision）の実測は未実施のままこのスライスへ送った。
  - 件数保持の運用接続スライス: Contentの実DELETE、利用中状態の収集（保持中Playerとの対応）、保護がNを超えた場合の挙動、系列の退役（イベント終了時のlocal・R2・統合フォルダ・DIST cacheの片付け）、件数保持がR2とローカルのどちらの層に効くか。
- r7のレビューで挙がり、後続のA1で扱う入力:
  - 保護の収集源の範囲（ローカルの`players/`の記録の外にある配布済みPlayerや他machineのBuildは収集源に入らない。収集できない場合と存在しない場合を区別する）と、DIST cacheの再取得元であるArtifactsRootの削除規則との関係 → 件数保持の運用接続。保護がNを超えた場合はBuildの超過条項を引き継ぐかもここで決める。
  - 確定記録を追記だけの記録とするため、revisionを削除したときの記録の扱い → Content確定記録。§5「新規private展開先」と不足fileの再利用の両立 → Content保存で「fetchは常に新規private展開先、再利用はContent取得と再利用が取得fileを再hashして行う」と決めた。
  - baked targetと、外部revisionの照合に使うtargetを分けて扱うか → Content分離とWindows以外のtargetを扱うスライス。
- `OSM_ARTIFACTS_ROOT` などを設定する開発環境セットアップscript → 別途のセットアップスライス。
- ArtifactsRootの件数清掃、異常終了で残る `.staging` の回収 → 件数保持の運用接続スライス。
- 診断の改善 → 同上の入力。失敗時も `publish` 表示が `published` になる点、相対path 240字超が `unconfirmed` で返る点、build-configの検証と複写が二度読みである点を含む。並行実行の敗者が一致時も `stage-conflict` で止まる仕様も同じく入力とする。
- Windows以外のtarget値 → transport manifest v1の固定`target`とDIST cacheの配置にtargetの階層が無く対応targetを1つに固定している点の拡張として、扱うスライスは未定（r7は前提として明記した）。端末性能別の系列値は§2の開発Contentで受け止めた。

Buildの件数保持の運用接続は[PR #119](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/119)の `build cleanup`／`use`／`release` で完了した（判定C GO。C′の残り2件はownerが非阻害と判断）。Build系列1つについて、このPCのreceiptと有限期限の利用中保護を集めてpolicyに渡し、Nを超えた古いBuildのR2 objectだけを「削除意図→DELETE→GET 404 `NoSuchKey`→削除済み記録」で消す。読めない記録・未知のentry・時計のずれでは系列全体を止める。実R2で最古1件だけの削除と再実行時の送信0を確認した。操作・契約・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#開発buildの件数清掃build-cleanup--use--release)にある。HANDOFFはharvestして削除した。決めたことと後続への入力は次のとおり。

- 決めたこと: Nは10の定数（CLI・build-configから変えない）。保護はこのPCの明示記録（最長90日、`build use`／`build release`）とこのPCでの取得中のaccess lockだけ。件数保持はR2のobjectにだけ効き、ローカルの記録は削除済みとして残す。Build DELETEはtransport入口で `BuildRetentionDelete=$true` の印付きrequestだけを通し、Content DELETEは拒否のまま。1回の実行はDELETE 20回・600秒まで。
- 後続への入力:
  - Contentの実DELETE、保持中Player（ArtifactsRootの `build-info.json`）からのContent保護の収集、R2のBuildとArtifactsRootのPlayerのどちらを「保持中」とみなすか、確定記録との関係 → 新規「Content件数保持の運用接続」。
  - ArtifactsRootの `players/`・`content/` の件数清掃、`.staging` 回収、ArtifactsRootの所在の登録 → 「ArtifactsRootの件数清掃と `.staging` 回収」（独立スライスとして扱う）。
  - 系列の退役 → 件数保持の運用接続の残り（Content側と同時に扱う）。R2に検証用系列 `retention-probe/windows-x64-player/probe-1`〜`probe-4` の小さなobjectが計40件残っている（退役手段が無いことはA3で受け入れ済み）。
  - 自動起動（scheduler接続）とNの系列ごとの設定 → 新規「件数清掃の自動起動」。
  - 単調なpublish順序の記録（時計巻戻りへの耐性） → Content確定記録。
  - 「DELETE → GET 404 → 記録」の共通primitive化（Evidence清掃・reset・Build清掃・将来のContent） → Content件数保持の運用接続のA1。
  - receiptの無い（未完了の）Buildの `intent.json` をどこまで検査するか（AC2の分類とAC4(a)の文言の隙間。C′の指摘でownerが現スライスでは直さないと判断） → 件数保持の運用接続の残り。
  - 別PCのpublishを数える、R2一覧、孤児回収（storeを失ったPCのBuildを含む） → Contentの容量上限の孤児回収と、R2一覧の要否。

Contentの件数保持の運用接続は[PR #123](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/123)の `content cleanup` で完了した（判定C GO、最終C′ PASS）。Content系列1つについて、このPCのreceiptと保持中Playerからの保護を集めて同じpolicyに渡し、Nを超えた古いrevisionの全objectを「削除意図→`transport.json`のDELETE→GET 404で確定→残りのobjectをGET→DELETE→GET 404→削除済み記録」で消す。実R2で検証用系列 `retention-probe-1` の最古1 revisionだけの削除（DELETE 2・GET 3）と再実行時の送信0を確認した。操作・契約・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#開発contentの件数清掃content-cleanup)にある。HANDOFFはharvestして削除した。A3の決定はowner不在中に主担当が代行し、ownerがPhase D完了の指示で追認した。決めたことと後続への入力は次のとおり。

- 決めたこと: 保持中Playerは、このPCの `builds-v1` のBuild（削除済みと読めたもの以外。状態を読めないものも保護）とArtifactsRootの `players/` のPlayer（`build collect` のstagingを含む）の両方とし、buildIdとrevisionの末尾32 hexで照合する（`build-info.json` の中身は読まない。過剰保護側）。ArtifactsRootは必須。明示の保護（use／release）と取得中の保護は足さない。`transport.json` のGET 404を確定点にし、確定点の後は候補・保護の判定によらず残りを消し切る。中断したrunの観測は記録しない（Phase A revision 2）。保護がNを超えた場合はBuildの超過条項を引き継ぎ、件数保持はR2のobjectにだけ効く。観測の写像と記録の観測検査は `ArtifactDeletion.psm1` に共通化した（Evidence清掃・resetは寄せない）。Content DELETEは `ContentRetentionDelete=$true` の印付きrequestだけを通す。receiptの無いBuildのintentは所属5 fieldだけを検査することをREADMEに契約として書いた（PR #119のC′指摘の決着）。確定記録には触れず、削除の正本はローカルの `deletion/` 記録。
- 後続への入力:
  - 系列の退役（新規「系列の退役」）: R2に残る検証用object（Build `retention-probe/*` 40件、Content `retention-probe-1` 20件）の回収を含む。次の実R2検証では、照合scriptが期待keyをreceiptの系列から独立に作ること、削除済みBuildの一覧を記録の妥当性まで再判定できる原記録を残すこと（C′の指摘）。
  - 診断の改善: run全体のcatchで `catalog-incomplete` が `unconfirmed` になる、終了時snapshotがblockedのとき同じrunの削除済みが分類に出ない、成功revisionの `publish.lock` 欠落で系列全体が止まりinspectが `receipt-unavailable` になる、観測fieldのgenerationのscalar型検査、同時刻の未解決試行を再開する順がAC3のtie-break（最古から）と逆である点（外部レビュー。I1〜I3には当たらない）。
  - Evidence task catalogの上限: 1,000 file超のbundleをpublishすると、task の `catalog.json` が1 MiBの記録上限に近づき、以降のfetchが `record-limit`（外からは `unconfirmed`）で失敗する。PR #123では同じbundleを1 fileのZIPにまとめて保存し直して回避した。所有スライスは未定。
  - Content確定記録: revision削除時の記録の扱いは、削除の正本（ローカル `deletion/`）を参照するか削除を追記するかを確定記録の側で決める。

既存Build出力を普通のpath/metadataとして受け、成功publish、別process fetch/hash、最新N件と利用中の保持、失敗publish時の既存保持を確認する。Nの既定値案10件、系列識別、容量上限をBのA3で固定する。大容量に必要なmultipartは対象サイズに応じて扱い、BuildSystemの作り直しを前提にしない。

### C. 別host / Cloud

必要な環境から個別に、到達性、限定grant、無人保存/取得、ログ/画像閲覧を確認する。未成立は当該環境の未対応として残し、ローカル利用を止めない。親鍵を通常file/promptへ複写せず、platform仕様は着手時に再確認する。

目的はCloud Agent・別PCからのEvidence閲覧で、Evidenceを公開しない（2026-10-11 owner）。Buildの件数清掃（PR #119）からの入力: 別PC・Cloudと共有する利用中保護（R2上の保護記録）、Evidenceが参照するBuildの保護、Cloudに削除権を渡さないこと。論点はtokenの受け渡し方法。第一候補は、受け取る側の環境に読み取り専用・bucket限定のtokenを登録する方式。署名付きURLは、発行主体（誰が親鍵を持って発行するか）とURLを届ける経路が未解決。

### D. BuildSystem / Harness接続

外部CLIを主入口にUnityを交換可能なbackendとして接続する。H2d/H3・CURRENT連携、同じ実体の所有権整理は必要になった範囲で行う。Storage単独の利用をUnity GUIやEditorに依存させない。Unityテスト/BuildのPhase責任と既知native終了stallの調査停止規則を維持する。

各スライスは担当・責務配置・変更file・発見用/判定必須テストをHANDOFFへ固定する。H1/CURRENT方式は各A3で明示する。ここはprogramであり、実装スライスのA3済みとは扱わない。

### E. 仮想化 host と分裂したストア（実装・正式C/C′完了。後続は未着手）

PR #126 で、3つのCLI（artifacts / workflow-task / harness）がstoreに触れる前にAppData書込みの仮想化を判定して拒否するようになり、すでに分かれた記録を安全に合流する `store plan` / `store consolidate` が入った。判定の方式と実行条件は `tools/Artifacts/README.md` の「実行するprocessの条件（AppData仮想化）」と「分裂したストアの合流」を正とする。実環境で分かったこと: 仮想化されたprocessの新規fileの書込み先はpathごとに違い、実体側にだけあるdirectoryの中では実体側へ書かれる。そのため固定名のprobe領域は誤判定の原因になり、呼出しごとの一意名の新規directoryで判定する。実環境の確認（Codex内のAC4とClaude内のAC5）は、事前実行との間隔を詰め、一つの時刻付きログに前後の一覧を残さないと証拠として成立しない。

後続（所有スライスは着手時にA0で確定する）:

- **Artifact Storage / Split Store Recovery:** 実ストアへの合流の実行と、取得の回復。最初の手順は読取りだけの `store plan` を実データに1回行い、対象の単位がACL・JSON検査・`multi-workflow-unit`・`workflow-already-visible` のどれで保留されるかを確定すること。保留した単位の扱い（公開途中の不可視性を保つ方式）は、必要なら別の設計スライスに分ける。実ストアの鍵で認証に失敗した場合は資格情報の複写・置換で直さずownerへ返す。
- **Artifact Storage / Virtualized Host Route:** 仮想化されたhost（Codex等）からStorageを使う新しい実行経路・保存先。資格情報の保存先・ACL・DPAPIの前提を変えうる。入力: `LocalCache\Local` が無い初回状態での判定（未観測。危険として受け入れ済み）、LocalAppData自体がjunctionのhostでの誤判定、Codex内の仮想化の仕組み。
- **Codex側残置の後始末:** 合流済みの記録のCodex側copyの扱い（保持・削除・期限）。Storageの30日清掃はCodex側に届かない。定期清掃jobのbindingと、Codex側だけにある記録の清掃が行われていないことの再確認。
- **Evidence task catalog:** 未採用で実体の無いstaging（過去のfetchの残骸）が残ると、そのtaskへのpublishが `unconfirmed` で拒否され、`evidence cleanup --dry-run` もそのtaskを回復できない。1,000 file級のbundleによるcatalog 1 MiB上限（上の「Evidence task catalogの上限」）と併せ、所有スライスは未定。運用上は新しいtaskIdでpublishし直し、bundleを1つのZIPにまとめて回避した。
- Harness CURRENTの合流方式、`invalid-evidence-path` 等の仮想化以外のpath不一致の診断を `unconfirmed` から分けるか。

## 5. 責務・残す安全性・採否

- **workflow:** task終了/再開の意味とイベント、明示入力、レビューの固定入力/所見分離を所有する。
- **Artifact application:** 梱包、publish/fetch結果、Storageコピーの単一保存policy、利用中保護、清掃を所有する。時刻/状態を注入してofflineで検証可能にし、レビューの合否を再判定しない。
- **R2 adapter:** 認証通信とobject I/Oを扱い、SDK型やbucket設定を上位へ漏らさない。別provider/registryを先回りして作らない。
- **Credentials:** 既存DPAPI/ACL・bucket限定鍵を再利用する。鍵・署名URL・認証headerをprompt、引数、ログ、Git、同期領域へ漏らさない。同一Windowsユーザー内の信頼を前提にする。
- **入力と取得:** 明示file集合、照合したsnapshot、固有key、信頼した期待hash、新規private展開先、path/reparse/衝突と有限の展開量制限を維持する。fetch/inspectだけで取得物を実行しない。
- **非公開:** `osm-artifacts`の公開development URL（r2.dev）/custom domainは有効にしない。prefix分離を公開境界と扱わず、payloadをGitへ置かない。

**採用:** ownerの単一契約・一括置換を採用し、legacy節、#107実適用、旧最低保存日/判断日程、旧reader互換、PR別の継承台帳を廃止する。task終了イベント、再開/再適用、7日の一時物清掃案、原sourceとの所有分離を新しい目的に必要な条件として残す。Build10件と一時物7日は初期値案で、実装スライスで設定を固定する。

**採用しないもの:** 未終了Evidenceの年齢による強制削除、lease/heartbeat/定期owner承認、無条件bucket全消去、削除拒否時の自動rule解除。気軽な開発用途でも、利用中・原source・無関係な領域を壊す理由にはしない。

**残件:** A〜Dのみを実装キューとする。旧probe/試験rule/生成物の整理はAの一回のリセットへまとめる。公開synthetic Release `artifact-probe-20260926` は別サービスのため必要時の個別後始末とし、Storage清掃にGitHub Releases操作を足さない。旧レビューの任意テスト強化や操作性候補は関連コードを直す際に必要性を評価し、全件を次の必須条件にしない。

**レビュー:** r5までのレビューは旧前提への結果としてPR履歴に残す。r6は今回の要件変更に対するPhase A改訂で、旧レビューの合格を新しい方針の合格へ読み替えない。3文書の独立A2は、終了イベントの所有、単一保持契約、リセット対象、未実装との区別を確認しblockerなし（同一継承モデル・同系列の制約あり、C′ではない）。実装・実データ操作は今回の対象外。

**文書改訂のPhase D:** ownerは2026-10-08にPR #108のPhase D完了・後始末・マージを指示した。最終方針head `f53c7216980a8dc27b69fd3a917429cb786060c2` の追加レビューは指摘2件解消・blockerなし、docs/contract/diff検査は問題なし。これを実装のC/C′ GOやリセット実行承認とは扱わない。保存方針と開始条件はArtifacts READMEとAGENTSへ反映済み。進行中programのため本書はowner・見直し期限・harvest先付きで保持し、次スライスへの入力とする。
