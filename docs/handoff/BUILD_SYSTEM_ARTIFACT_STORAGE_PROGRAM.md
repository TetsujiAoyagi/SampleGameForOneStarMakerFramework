# BuildSystem Artifact Storage Program

## 0. Metadata

- type: `program`
- status: **完了（保留list付き）**。ownerが2026-10-17にArtifact Storageの開発をここで完了と決めた。完了の範囲は同一Windowsユーザー・非仮想化processで検証した基本操作と運用契約（[Artifacts README](../../tools/Artifacts/README.md)冒頭）で、次に実装するスライスは置かない。残りは§6の保留listにあり、再開条件を満たした時にownerが再開を判断する。スライスAはPR #109で実装・限定運用検証と正式C/C′を完了（両方GO、blocker 0）。固定仕様と実行集合・原観測の入口は `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle`。**注記（合流前、2026-10-17の `store plan`）:** このCURRENTは仮想化されたhost（Codex）が書いた側にだけあり、実ストアには無いので、非仮想化processからは読めない。`store plan` ではこのtaskを含む単位は `hold`（理由 `dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role`）で未合流（§6）。旧限定resetは完了済みで、再実行しない。**2026-10-18にownerが保留項目「Virtualized Host Route」を再開し**、問いを「store rootをAppData外へ移す」に置き換えたスライス `ARTIFACT_STORE_ROOT_RELOCATION.md`（Phase A）を切った。他の保留項目は再開していない。
- program policy revision: `r7` — 単一の開発用保存契約にContent種別を追加（§2）。旧データと互換性を引き継がない。
- branch: `codex/artifact-storage-development-policy`
- planning base commit: `a027bf04b4f5ce16494b93daf10e5b5bccf15a28` (`origin/develop`; PR #106・#107を含む)
- implementation head commit: not applicable; 文書改訂のみ。
- risk: `normal`（文書改訂）。リセットと削除機能の対象・検証は実装スライスで具体化する。
- owner: OSM maintainers
- created: 2026-09-26
- updated: 2026-10-19
- expires: 2026-12-26 または置換revision（program文書の見直し期限）
- harvest to: 実装済みの通常操作を `tools/Artifacts/README.md`、program状態を `docs/README.md` へ反映し、program完了時に削除する。 完了宣言後、Phase Dでこの文書を削除するか、保留listだけを公開面へ移すかはownerが決める。

## 1. 目的・要件変更・現在地

開発中の成果物を非公開で手軽に保存・受け渡しし、不要になったものは自動で片付ける。人間が毎回ファイルを運んだり、保存・清掃を個別承認したりせずに使えることを成果とする。

[ownerの要件変更](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/108#issuecomment-6049302344)と本チャットの修正指示を採用する。本番運用はなく、過去の開発用Artifact・試験設定はリセット可能。旧証拠の保存継続、旧参照/readerとの互換、PRごとの移行台帳を要求しない。物理的な試験物の不存在を確認した、という意味ではない。

この指示を根拠に、r5のlegacy分岐、旧最低保存義務と日程、参照中/参照不明の無期限保持、keyごとの削除承認を置き換える。**PR #107のE1/E2へのIndefinite追加、reader互換、新保護事実recordの実適用タスクは取りやめる。** 過去の方式確認の成果は否定せず、経緯はPR/Git履歴に残す。撤去する方式を一度完成させてから移行する工程は作らない。

現在の実装機能は [Artifacts README](../../tools/Artifacts/README.md) を正とする。スライスAのEvidence v2、workflow taskの終了/再開配送、純粋保持policy、自動清掃と一回の限定resetは検証を完了した。正式C/C′は同じ実装headでGO。検証集合、限定実観測、既知ACL不適合copyの保護によるpartial結果と未確認範囲は、PR #109およびCURRENTの固定記録に残す。

**注記（合流前、2026-10-17の `store plan`）:** ここで言うCURRENT（`artifact-evidence-lifecycle`）は仮想化されたhost（Codex）が書いた側にだけあり、実ストアには無いので、非仮想化processの `harness.ps1 current -Task artifact-evidence-lifecycle` では読めない。`store plan` では `hold`（理由 `dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role`）で未合流（§6）。

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

## 4. 完了したスライス

各スライスの経緯の全文はPRと、削除前のHANDOFFのcommitにある。各スライスが後続へ送った問いは、§6の保留listへ移した。

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

**注記（合流前、2026-10-17の `store plan`）:** スライスAのCURRENT（`artifact-evidence-lifecycle`）は仮想化されたhost（Codex）が書いた側にだけあり、実ストアには無いので、非仮想化processの `harness.ps1 current -Task artifact-evidence-lifecycle` では読めない。`store plan` ではこのtaskを含む単位は `hold`（理由 `dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role`）で未合流（§6の「LocalCache側copyの扱い」）。

**停止規則:** 上記の通常利用・期限清掃と一回の切替が成立したら閉じる。Build、Cloud、GUI、常駐broker、別provider、長期保護・互換維持を追加条件にしない。

Aの完了後の小さな追加として、[PR #121](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/121) でEvidence CLIの失敗理由を区別した（判定C GO、最終C′ PASS）。publish・fetch・inspect・use・releaseは、configの読取・検証の失敗を `invalid-evidence-config`、deployment receiptの不在・読取不能を `unknown-deployment`、publishのselectionの読取失敗を `invalid-selection` で返す。相対pathは従来どおり受け付けない。操作は[Artifacts README](../../tools/Artifacts/README.md)にある。

Evidence清掃schedulerの登録済みtaskの切替は[PR #129](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/129)で扱った（判定C GO、最終C′（別ベンダー）PASS、2026-10-16）。`schedule install` / `schedule status` が `EvidenceSchedule.psm1` の固定code 9件（`scheduler-identity-mismatch` 等）でthrowしたとき、CLIはstdoutの `{"status":"failed","reasonCode":...}`（exit 1）で返す。allow-listはschedule分岐だけが設定し完全一致でだけ通すので、それ以外の例外は従来どおり `Artifact operation failed.` のみ。`install` は既存taskを直さない設計のままで、identityが変わる3条件（新headのinstall、Store版pwshの更新、config変更）と、ownerが旧taskを `Unregister-ScheduledTask` してから `install` する切替手順を[Artifacts README](../../tools/Artifacts/README.md)「自動清掃」に書いた。

### B. 開発Buildと開発Contentの保存と件数管理（完了）

単一Build publishは[PR #111](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/111)で実装し、offline 60caseと既存実Build 266fileのR2往復・全hash一致を確認、正式C/C′を完了した。現在の入口・成功確定・上限・限界は[Artifacts README](../../tools/Artifacts/README.md#開発build-1件の保存と取得)へ集約した。

最初の最小スライスは[PR #110](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/110)の純粋な保持選別policyです。1系列のsnapshotとNから保持・削除候補・保留を決める内部APIとoffline検証は実装済みで、契約は[Artifacts README](../../tools/Artifacts/README.md#開発buildの件数保持policy)へ集約しています。系列・利用状態の収集、policyへの運用接続、実DELETEは下記のPR #119で接続しました。

Player Buildからpublishまでの接続は[PR #115](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/115)の `build collect` で完了しました（正式C GO。C′の証拠不足はownerによるR2の直接観測で解消し、Phase Dで採用）。BS4の配布形Player 1件を `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` へ退避し、`build-info.json` とselectionを生成して既存publishへ渡します。判定用の実Player 266 fileで、実R2往復と再実行時のPUT無しを確認しました。現在の操作は[Artifacts README](../../tools/Artifacts/README.md#bs4-player出力の退避と保存build-collect)にあります。

Contentの保存契約は§2の開発Contentでr7として固定した。Content保存は[PR #118](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/118)で完了した（判定C GO。C′の残り1件はownerが非阻害と判断）。`content collect`／`inspect`／`fetch` で、Content出力1件をArtifactsRootへ退避し、file単位objectとしてR2へ置き、別processで取得して全fileのhashを照合する。実Spring出力（219 file、PUT 220、760 s）で最終headの実R2往復を確認した。操作・上限・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#開発content-1件の保存と取得content-collect--inspect--fetch)にある。

- 決めたこと: 完了確定点はローカルreceiptのrenameだけ（R2に完了マーカーを置かない）。公開は全objectを「子processのGETで照合→404ならPUT→子processの読戻し」の1経路で行い、`transport.json` を最後に置く。target表は `StandaloneWindows64-Player` → `windows-x64-player` の1行。contentSetは小文字英数字とハイフンの安全な部分集合（既存5値は全て入る）。R2 keyは `development/content/v1/<repositoryId>/<contentSet>/<target>/<revision>/`。PlayerとContentの保存順は決めない（Content分離が決める）。途中で止まったR2 objectは「未完了uploadとstaging」に載せず、同じrevisionの再実行で続行する。
- r7のレビューで決めたこと: 「fetchは常に新規private展開先、再利用はContent取得と再利用が取得fileを再hashして行う」（§5「新規private展開先」と不足fileの再利用の両立）。

Buildの件数保持の運用接続は[PR #119](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/119)の `build cleanup`／`use`／`release` で完了した（判定C GO。C′の残り2件はownerが非阻害と判断）。Build系列1つについて、このPCのreceiptと有限期限の利用中保護を集めてpolicyに渡し、Nを超えた古いBuildのR2 objectだけを「削除意図→DELETE→GET 404 `NoSuchKey`→削除済み記録」で消す。読めない記録・未知のentry・時計のずれでは系列全体を止める。実R2で最古1件だけの削除と再実行時の送信0を確認した。操作・契約・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#開発buildの件数清掃build-cleanup--use--release)にある。

- 決めたこと: Nは10の定数（CLI・build-configから変えない）。保護はこのPCの明示記録（最長90日、`build use`／`build release`）とこのPCでの取得中のaccess lockだけ。件数保持はR2のobjectにだけ効き、ローカルの記録は削除済みとして残す。Build DELETEはtransport入口で `BuildRetentionDelete=$true` の印付きrequestだけを通し、Content DELETEは拒否のまま。1回の実行はDELETE 20回・600秒まで。

Contentの件数保持の運用接続は[PR #123](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/123)の `content cleanup` で完了した（判定C GO、最終C′ PASS）。Content系列1つについて、このPCのreceiptと保持中Playerからの保護を集めて同じpolicyに渡し、Nを超えた古いrevisionの全objectを「削除意図→`transport.json`のDELETE→GET 404で確定→残りのobjectをGET→DELETE→GET 404→削除済み記録」で消す。実R2で検証用系列 `retention-probe-1` の最古1 revisionだけの削除（DELETE 2・GET 3）と再実行時の送信0を確認した。操作・契約・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#開発contentの件数清掃content-cleanup)にある。A3の決定はowner不在中に主担当が代行し、ownerがPhase D完了の指示で追認した。

- 決めたこと: 保持中Playerは、このPCの `builds-v1` のBuild（削除済みと読めたもの以外。状態を読めないものも保護）とArtifactsRootの `players/` のPlayer（`build collect` のstagingを含む）の両方とし、buildIdとrevisionの末尾32 hexで照合する（`build-info.json` の中身は読まない。過剰保護側）。ArtifactsRootは必須。明示の保護（use／release）と取得中の保護は足さない。`transport.json` のGET 404を確定点にし、確定点の後は候補・保護の判定によらず残りを消し切る。中断したrunの観測は記録しない（Phase A revision 2）。保護がNを超えた場合はBuildの超過条項を引き継ぎ、件数保持はR2のobjectにだけ効く。観測の写像と記録の観測検査は `ArtifactDeletion.psm1` に共通化した（Evidence清掃・resetは寄せない）。Content DELETEは `ContentRetentionDelete=$true` の印付きrequestだけを通す。receiptの無いBuildのintentは所属5 fieldだけを検査することをREADMEに契約として書いた（PR #119のC′指摘の決着）。確定記録には触れず、削除の正本はローカルの `deletion/` 記録。

系列の退役は[PR #124](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/124)の `build retire`／`content retire` で完了した（判定C 阻害0、最終C′（別ベンダー）阻害0、ownerがGO。2026-10-16）。ownerが指示ID（Workflowの `Decision` 書式）で明示した系列1つについて、系列ごとに1件の不変の退役記録（`retirements-v1/…`）を残し、このPCの成功記録にあるR2 objectを保護中のものを除いて既存の削除試行で消し切る。退役後の系列はpublish・collect・useが `series-retired` で止まり、cleanupは退役の選別で動く。退役記録を確認できなければ `retirement-unknown` で止まる（fail-closed）。実R2で検証用5系列（Build 4・Content 1、計60 object）を退役させ、判定Cの独立照合script（期待keyをreceiptから作り、削除を記録の連鎖で再判定。合成fixtureの不合格例を含む）が全19 checkで合格、再実行の送信0、実系列の記録不変を確認した。検証用系列 `retention-probe/*` の40 objectと、Content件数清掃の検証用60 objectも同PRで回収した。操作・契約・既知の限界は[Artifacts README](../../tools/Artifacts/README.md#系列の退役build-retire--content-retire)にある。

- 決めたこと: 起点はownerの明示指示だけ（イベント終了の自動検知はしない）。退役は事前承認の通常操作に含めず、ownerが系列と指示IDを明示した範囲でだけ実行する。取消は無い。退役は保護を外さず、保護中・未完了が残れば `retired-held`。消すのはR2のobjectだけで、ローカル記録・ArtifactsRoot・統合フォルダ・DIST cacheには触れない。結果JSONは件数清掃と同じ固定fieldで、`mode`（`retention`／`retirement`）を件数保持の結果にも常に出す。Buildの系列不明の未完了は `held` に混ぜず `unattributed.count` に出す。
- 残置: 退役の検証で失敗したfetch 50件のoperationと、拒否された `content collect` の空operationがtransfersに残る（既存の扱いどおり）。実行器のgit管理外の作業directoryも残してある。

### C. Evidenceのローカル固定とR2昇格（完了）

**ローカル固定は完了（PR #128、2026-10-16）。** 同一PCの担当間は `evidence stage`（state `staged`、R2・資格情報不使用）と fetch の local 分岐で固定入力を渡し、`osm-workflow` は公開点だけ push する規則になった。操作は `tools/Artifacts/README.md` の「ローカル固定」、規則は `osm-workflow` 本文を正とする。共有storeで `evidence stage` を使い始める前に README の導入手順（`schedule status` → 新headで `schedule install`）を行う。経緯の全文は削除前のHANDOFF（`docs/handoff/ARTIFACT_EVIDENCE_LOCAL_STAGE.md`、commit `72a8b0c`）。

**R2昇格の送信は完了（PR #130、2026-10-17）。** `evidence promote` が `staged` のartifactを同じreference・packageSha256・bytesのままR2へ置き、取得案内は `locality` を `promoted` に変えるだけで済む。PUT応答不明は同keyへ再送せず、既存の `put-pending` 回復へ接続した。操作と既知の制限は `tools/Artifacts/README.md` の「R2への昇格（evidence promote）」、配布の規則は `review-evidence.md` の「保存先とリモートへの配布」を正とする。経緯の全文は削除前のHANDOFF（`docs/handoff/ARTIFACT_EVIDENCE_R2_PROMOTION.md`、commit `b44d5f6`）。

**昇格の回復と送信済み `staged` の寿命は完了（PR #131、2026-10-17）。** 昇格由来（`origin: stage`）の `put-pending` と、旧runtimeが作った回復できる `failed` は、回復GETの404で `staged`（putIntent保持）に戻る。その後はlocal fetchと、同じbytes・同じkeyへの再昇格ができる。回復できる `failed` を回復するのは、そのartifact自身のpromote・fetchと日次清掃だけ。putIntentを持つ `staged` は、remote DELETEとGET 404を経てだけ `deleted` になる。保持判定と削除経路の振り分け（`Get-EvidenceDeletionRoute`）は分けた。publish由来の挙動は変えていない。操作・回復の規則・残存リスク・導入手順は `tools/Artifacts/README.md` の「R2への昇格（evidence promote）」「終了・再開・利用中と清掃」「自動清掃」、配布の規則は `review-evidence.md` の「保存先とリモートへの配布」を正とする。共有storeで新しいshapeが生じる前に、README の導入手順（`schedule status` → 新headで `schedule install`）を行う。経緯の全文は削除前のHANDOFF（`docs/handoff/ARTIFACT_EVIDENCE_PROMOTE_RECOVERY.md`、commit `749c238d`）。

Cloud・別PCがR2から取得する経路とtokenの受け渡しは未着手で、§6の保留listにある。

### E. 仮想化 host の判定と分裂したストアの合流機能（完了）

PR #126 で、3つのCLI（artifacts / workflow-task / harness）がstoreに触れる前にAppData書込みの仮想化を判定して拒否するようになり、すでに分かれた記録を安全に合流する `store plan` / `store consolidate` が入った。判定の方式と実行条件は `tools/Artifacts/README.md` の「実行するprocessの条件（AppData仮想化）」と「分裂したストアの合流」を正とする。実環境で分かったこと: 仮想化されたprocessの新規fileの書込み先はpathごとに違い、実体側にだけあるdirectoryの中では実体側へ書かれる。そのため固定名のprobe領域は誤判定の原因になり、呼出しごとの一意名の新規directoryで判定する。実環境の確認（Codex内のAC4とClaude内のAC5）は、事前実行との間隔を詰め、一つの時刻付きログに前後の一覧を残さないと証拠として成立しない。

実データへの読取のみの `store plan` は2026-10-17に1回行った（結果は§6の「LocalCache側copyの扱い」）。`store consolidate` は実行していない。

## 5. 責務・残す安全性・採否

- **workflow:** task終了/再開の意味とイベント、明示入力、レビューの固定入力/所見分離を所有する。
- **Artifact application:** 梱包、publish/fetch結果、Storageコピーの単一保存policy、利用中保護、清掃を所有する。時刻/状態を注入してofflineで検証可能にし、レビューの合否を再判定しない。
- **R2 adapter:** 認証通信とobject I/Oを扱い、SDK型やbucket設定を上位へ漏らさない。別provider/registryを先回りして作らない。
- **Credentials:** 既存DPAPI/ACL・bucket限定鍵を再利用する。鍵・署名URL・認証headerをprompt、引数、ログ、Git、同期領域へ漏らさない。同一Windowsユーザー内の信頼を前提にする。
- **入力と取得:** 明示file集合、照合したsnapshot、固有key、信頼した期待hash、新規private展開先、path/reparse/衝突と有限の展開量制限を維持する。fetch/inspectだけで取得物を実行しない。
- **非公開:** `osm-artifacts`の公開development URL（r2.dev）/custom domainは有効にしない。prefix分離を公開境界と扱わず、payloadをGitへ置かない。

**採用:** ownerの単一契約・一括置換を採用し、legacy節、#107実適用、旧最低保存日/判断日程、旧reader互換、PR別の継承台帳を廃止する。task終了イベント、再開/再適用、7日の一時物清掃案、原sourceとの所有分離を新しい目的に必要な条件として残す。Build10件と一時物7日は初期値案で、実装スライスで設定を固定する。

**採用しないもの:** 未終了Evidenceの年齢による強制削除、lease/heartbeat/定期owner承認、無条件bucket全消去、削除拒否時の自動rule解除。気軽な開発用途でも、利用中・原source・無関係な領域を壊す理由にはしない。

**残件:** 次に実装するスライスは置かない。後続は§6の保留listに再開条件付きで置き、条件を満たした時にownerが再開を判断する。旧probe/試験rule/生成物の整理はAの一回のリセットへまとめる。公開synthetic Release `artifact-probe-20260926` は別サービスのため必要時の個別後始末とし、Storage清掃にGitHub Releases操作を足さない。旧レビューの任意テスト強化や操作性候補は関連コードを直す際に必要性を評価し、全件を次の必須条件にしない。

**レビュー:** r5までのレビューは旧前提への結果としてPR履歴に残す。r6は今回の要件変更に対するPhase A改訂で、旧レビューの合格を新しい方針の合格へ読み替えない。3文書の独立A2は、終了イベントの所有、単一保持契約、リセット対象、未実装との区別を確認しblockerなし（同一継承モデル・同系列の制約あり、C′ではない）。実装・実データ操作は今回の対象外。

**文書改訂のPhase D:** ownerは2026-10-08にPR #108のPhase D完了・後始末・マージを指示した。最終方針head `f53c7216980a8dc27b69fd3a917429cb786060c2` の追加レビューは指摘2件解消・blockerなし、docs/contract/diff検査は問題なし。これを実装のC/C′ GOやリセット実行承認とは扱わない。保存方針と開始条件はArtifacts READMEとAGENTSへ反映済み。その後のスライスの間、本書はowner・見直し期限・harvest先付きで保持し、2026-10-17の完了宣言の後は保留listを持つ文書として残す。

## 6. 保留list（再開条件付き）

ownerは2026-10-17に、Artifact Storageの開発はここで完了と決めた。以下は次の仕事の一覧ではない。各項目は、下の再開条件のどれかを満たした時にだけ、ownerが再開を判断する。

- 再開条件（2026-10-17にownerが採用）:
  - RC1: エンジン側スライス（S-5 / S-8 / S-9、InGame 縦糸）が Storage の不足で実際に止まった。
  - RC2: 同じ障害が実運用で 2 回以上起き、既存の reasonCode と README の手順で回復できなかった。
  - RC3: Cloud / 別 PC の読み手が実際に稼働を始めた（HANDOFF に担当として割り当てられた）。
  - 再開は、どれかを A0 に観測事実（日付・task・reasonCode）付きで書けた時に限る。仮定上の障害では再開しない。
  - 2026-10-18 に owner が追加: 仮想化された host（packaged app 内の process）を Phase 担当として割り当てた事実も、RC3 と同じ種類の観測事実として扱う。希望や予定ではなく、割り当ての決定を A0 に書けた時に限る。
  - 安全上の例外: 秘密の露出、保護対象の誤削除など既存の安全契約に違反したことが確認された場合は、RC1〜RC3 を待たず初回で該当操作の利用を止め、owner が評価する。これは機能開発の再開条件ではなく、保留listの項目にもしない。

### 取得経路と配布

- **Cloud・別PCからの取得** — Cloud Agent・別PCがR2からEvidenceを閲覧する経路（Evidenceは公開しない。2026-10-11 owner）と、Build・Contentの取得。論点はtokenの受け渡しで、第一候補は受け取る側の環境に読み取り専用・bucket限定のtoken（R2 Object Read only）を登録する方式。署名付きURLは発行主体（誰が親鍵を持って発行するか）とURLを届ける経路が未解決。必要な環境ごとに到達性、限定grant、無人保存/取得、ログ/画像閲覧を確認し、未成立は当該環境の未対応として残してローカル利用を止めない。親鍵を通常file/promptへ複写しない。入力: 別PC・Cloudと共有する利用中保護（R2上の保護記録）、Evidenceが参照するBuildの保護、Cloudに削除権を渡さないこと、`ready` のローカルcopyからのnetwork無しfetch、R2 lifecycleの採用、別PCのpublishを数えることとR2一覧の要否、旧rootの無い新しいhostで移行済みの印を発行する手段（`ARTIFACT_STORE_ROOT_MIGRATION.md` の移行プログラムはmerge前に削除するため、別PCでstoreを使うには印の発行手段が要る）。再開: RC3。判断: owner。
- **Virtualized Host Route（2026-10-18 に再開、問いを置換）** — 旧い問いは「仮想化されたhost（Codex等）からStorageを使う新しい実行経路・保存先」で、資格情報の保存先・ACL・DPAPIの前提を変えうるものだった。ownerはCodexデスクトップをPhase担当として割り当てる決定を再開理由とし、問いを「store rootをAppData書込み仮想化の対象外へ移す」に置き換えて `ARTIFACT_STORE_ROOT_RELOCATION.md` を切った。資格情報・ACL・DPAPIの前提は変えない。旧い入力のうち、`LocalCache\Local` が無い初回状態での判定とLocalAppData自体がjunctionのhostでの誤判定は、rootをLocalAppDataの外へ出すことで対象外になる見込みで、残るならそのスライスのA1が後続へ送る。Codex内の仮想化の仕組みの解析は行わない。
- **store root 移設の実行（2026-10-19 に Phase A を凍結）** — `ARTIFACT_STORE_ROOT_RELOCATION.md` のGO候補head（`34cf6607`）を前提に、そのmergeの前に行う一回の運用スライス `ARTIFACT_STORE_ROOT_MIGRATION.md`。実ストアの所有role（`Artifacts`、`Workflow`、`Harness`）の同一volumeのrenameによる新rootへの移設、旧rootのtombstone、移行済みの印の発行、Task Schedulerの清掃jobの再登録、smoke。LocalCache側copyの合流は含めない（owner決定、2026-10-19。下の「LocalCache側copyの扱い」）。入口条件: 当該スライスのO1の決定（GO候補headで判定C・C′を完了し、mergeの直前。2026-10-19に成立）。判断: owner。

### 分裂したストア

- **LocalCache側copyの扱い（合流の価値の判断と削除）** — owner決定（2026-10-19）で旧2項目「分裂したストアの合流」と「Codex側残置の後始末」をまとめた。問い: LocalCache側copy（packaged appが旧rootの `Artifacts` / `Workflow` / `Harness` として書いた記録）を新rootへ合流させる価値があるか。価値が無ければ合流せずにcopyを削除して終える。価値があると判断した単位だけを、`store plan` の再実行 → ownerのmanifest承認 → `consolidate`（承認までdry-runも含めて実行しない）→ 合流後の確認（H1適用taskのCURRENT・receiptのpath bytesと新rootの両立を含む）へ進める。価値の判断の入力: 2026-10-17の読取のみの `store plan`（単位20、`publish` 4・`skip` 0・`hold` 16、deployment単位1件の `conflict` とそれへの `dependency-held`）。Harnessの4 task（`h2c-unity-gate`、`h2c-unity-gate-r2`、`h1-transport-identity`、`artifact-evidence-lifecycle`）のCURRENTがcopy側にだけあり、非仮想化processから読めない。`ARTIFACT_STORE_ROOT_MIGRATION.md` は新rootでのHarness CURRENTの読み戻しを実証していない（smokeは空状態の確認に縮退）。tombstoneを置いた後のpackaged appの仮想化されたviewで、実体側の通常fileとLocalCache側のdirectoryが同じ名前で重なったときの見え方（未観測）。合流に進む場合だけの入力: 保留単位の解消方式（公開途中の不可視性。必要なら設計スライスに分ける）、名前表の `retirements-v1`、仮想化以外のpath不一致の診断、`StoreConsolidation.psm1` のreasonCodeの畳み込み、実ストアの鍵で認証に失敗した場合は資格情報の複写・置換で直さずownerへ返すこと。削除の扱い: copyの削除は不可逆なので、対象（packageとcopy内のrole、旧rootに残るtombstone）・除外・開始条件をそのスライスのA3で固定し、ownerの承認で1回行う（通常操作の事前承認に含めない）。削除の後にpackaged appをPhase担当にできる（`legacy-store-present` が解ける）。Storageの30日清掃がcopyに届かないことは削除で解消する。入口条件: `ARTIFACT_STORE_ROOT_MIGRATION.md` のGO（印あり・旧rootに所有roleなし・smoke合格）。旧RC1・RC2の再開条件はowner決定で置き換えた。判断: owner（価値の判断、manifestの承認、削除の承認）。

### Content

- **Content確定記録** — 新旧順序とmanifest digestを持つ追記だけの記録の形式と書込み方式（LISTまたは条件付き書込みの要否）、revision削除時の記録の扱い（削除の正本のローカル `deletion/` を参照するか、削除を追記するか）、「最新」の解決の保存側の部分、単調なpublish順序（時計巻戻りへの耐性）、別PCからの同じrevisionの同時公開の排他（R2側に上書き防止が無い）。再開: RC2（別PCからの同時公開の衝突が実運用で2回以上、READMEの手順で回復できない）またはRC3。判断: owner。
- **Contentの分離・取得・取り込み** — Content分離（Playerが `content/` を内包しない場合のUnity側の変更。候補はU1: 今のまま内包して重複保存、U2: 既存の `content:installedRevisionPath`+`content:manifestSha256` の組で外部revision rootを指す、U3: 新しいconfig keyの追加。U2は外部revision rootがDIST cacheの `installed/` 配置とreceiptを要し、BuildIdentity一致を要求する点、baked `relativeDirectory` の扱い、起動時のlatchとの両立を確認する。`build collect` の検査・`build-info.json`・selectionの変更、互換キー案の判断。baked targetと外部revisionの照合に使うtargetを分けるか）、Content取得と再利用（R2からArtifactsRootへの配置と不足fileの再利用）、Content取り込み（取り込み元からDIST cacheへの不足fileの再利用、cacheのeviction・lease、アプリ更新を越える永続性、Unityバージョンとroot schemaの束縛、HTTPのfileごとのdeadline）、統合Contentフォルダ（取得型／監視型、複数ワークスペースの衝突規則、ArtifactsRootとの同一性、共有時の削除主体、ローカル設定と選択UI、「最新」の解決の所有）、`contentSet` の命名規約（用途×構成×性能の合成名、既存値 `bs4-spring-full` を改名しないこと、大文字小文字とパス要素の安全規則、hd/sdを1 revisionに同梱した場合の部分取得）、Windows以外のtarget（transport manifest v1の固定 `target` とDIST cacheの配置の拡張）。再開: RC1。判断: owner。
- **Contentの容量上限と孤児回収** — Content固有の上限値、R2のプラン変更の要否、R2の操作回数の枠（file単位objectのPUT数）、転送中の展開量上限、0 byte fileの拒否、4季節 `all-full` の実測（1 revision・約881 object・約22 MiB以下の見積もり、未実施）。R2の途中失敗や削除後の遅延PUTで残る追跡外object、receiptを失ったobject、既存publishの404 → `failed` で残るremote residueの回収。再開: RC2（容量・孤児objectによる障害が2回以上）。判断: owner。

### 清掃と運用

- **ローカルの残置の清掃** — ArtifactsRootの `players/`・`content/` の件数清掃、異常終了で残る `.staging` の回収、ArtifactsRootの所在の登録、公開・取得で残るprivate operationの期限清掃（`content fetch` の取得先を含む。今は自動清掃されない）、準備helperの失敗による未記録・未送信のprivate残置、満了削除後に `staging-protected` のままcatalogに残るentry。再開: RC2（残置による障害が2回以上）。判断: owner。
- **件数清掃の自動起動** — `build cleanup`／`content cleanup` のscheduler接続とNの系列ごとの設定。再開: RC2（手動実行の漏れによる障害が2回以上）。判断: owner。
- **Evidence task catalogの上限** — 1,000 file級のbundleで task の `catalog.json` が1 MiBの記録上限に達し、以降のfetchが `record-limit`（外からは `unconfirmed`）で失敗する。運用上は新しいtaskIdでpublishし直すか、bundleを1つのZIPにまとめて回避する。再開: RC2。判断: owner。
- **開発環境セットアップscript** — `OSM_ARTIFACTS_ROOT` などの設定。再開: RC1。判断: owner。
- **BuildSystem / Harness接続** — 外部CLIを主入口にUnityを交換可能なbackendとして接続する。H2d/H3・CURRENT連携、同じ実体の所有権整理（Harnessの原sourceとStorageのcopy）、端末喪失に備えた未昇格 `staged` のbackup（Harness H3）。Storage単独の利用をUnity GUIやEditorに依存させず、Unityテスト/BuildのPhase責任と既知native終了stallの調査停止規則を維持する。H1/CURRENT方式は各A3で明示する。再開: RC1。判断: owner。

### 診断・送信・テストの整理

- **診断の改善** — 再開: RC2（同じ診断不足で回復できない障害が2回以上）。判断: owner。入力:
  - cleanup・schedule-install・schedule-statusのconfig失敗のreasonCode（結果の形とexitの意味を変える判断が要る）。schedule分岐でもallow-list外のruntime系固定code（`runtime-binding-invalid`・`runtime-path-invalid`・`runtime-binary-mismatch`・`runtime-context-switch-rejected`・`runtime-credential-root-unavailable`・`evidence-deadline`）は伏せたまま。CLI境界で実測したのは `scheduler-identity-mismatch` だけ。CLIからのtask削除はownerの操作のままで、失敗した `install` は旧headの `runtime/<head>/` を残す。
  - receiptの `baselineSha256` 等のhex不正がまだ `unconfirmed` になる。`Read-EvidenceConfig` のtryが広く、想定外の例外も `invalid-evidence-config` になる（fail-closed）。`workflow-task.ps1` は例外messageをreasonCodeに写すため、configを2回読む間に壊れる競合でだけ `workflow-unavailable` が新しいcodeに変わる（受容済み）。
  - Build・Content: 失敗時も `publish` 表示が `published` になる、相対path 240字超が `unconfirmed` で返る、build-configの検証と複写が二度読み、並行実行の敗者が一致時も `stage-conflict` で止まる、receiptの無い（未完了の）Buildの `intent.json` をどこまで検査するか、content storeが既存directoryのACL不一致を付け直す（Build storeは拒否）、各objectの照合結果をoperationに記録しない、`existing` 結果に `objects` が無い、予算の確認とreceipt確定の間の窓、予算が退避・snapshotの区間に掛からない、collectが各fileを約7回読む。
  - Content件数清掃: run全体のcatchで `catalog-incomplete` が `unconfirmed` になる、終了時snapshotがblockedのとき同じrunの削除済みが分類に出ない、成功revisionの `publish.lock` 欠落で系列全体が止まりinspectが `receipt-unavailable` になる、観測fieldのgenerationのscalar型検査、同時刻の未解決試行を再開する順が最古からのtie-breakと逆。Contentのcleanup結果で退役の `protected` が `held` と `protected` の両方に出る。退役記録を読む前に止まるcleanupと `retirement-unknown` のblockedが `mode=retention` を返す。Content collectの退役確認が開始時の1度だけ（READMEのknown limits）。
  - Evidence: `evidence stage` が約1,100 fileのbundleで `record-limit` になり `unconfirmed` だけを返す、古いBuildの `inspect`／`fetch` はpublish時のbuild-configと同じbytesを要し書き直したconfigでは `receipt-unavailable` で理由が出ない、`build use` の `--until` が往復形式だけ。回復できない `failed` へのcleanupの試行が残すoperationが、そのprocessの生存中だけ同じtaskの他のartifactのnetwork付きpreflightを `operation-pending` で止めうる。`put-pending` の回復が解決しない間にtask全体のnetwork付き操作が止まる既存挙動、publish由来のremote削除の確定順序、catalogの組み合わせ検査の厳格化。`use` / `release` のpreflightはnetwork形のままで、READMEの事前承認の一文と範囲が合っているかを決める。
- **送信まわりの整理** — 削除intentの照合GETが一致して `staged` に戻ったartifactをresumeしてpromoteすると、objectが在るkeyへ同じbytesをPUTする（安全だが「re-PUTは404の後だけ」の前提の外）。receipt再利用の照合の拡張（今はkey・packageSha256・manifestSha256の3つ）、回復一致時にpromoteのreadback stagingが未登録fileごとadoptedになる、送信部の共有（`EvidenceSend.psm1`）、intentの `createdAt`・`process` の型検査、「DELETE → GET 404 → 記録」の共通primitive化（Evidence清掃・reset・Build清掃・Content）。再開: RC2。判断: owner。
- **テストと証拠の整理** — suiteのhermetic化（既存suiteは `SeriesRetirementStore` のテスト用rootを注入せず実storeの `retirements-v1` を読む）、退役確認の規則が2か所にある、回復できる `failed` のfixtureとpromote停止のhelperの重複（`EvidenceTestSupport.ps1` へ）、`Get-EvidenceDeletionRoute` の `catch{}` が fail-closed で `none` にする理由のコメント、一部のsuiteが集計行だけを出しcase名を復元できない、`EvidenceCleanup.Tests.ps1` の1 caseが `rg` をPATHに要する（READMEに記載）、Buildのpublish intentのpath置換した写しを `receipt.intentSha256` と結び付けられない（証拠の加工規則）、`changes.patch` が `.gitignore` に該当する未追跡fileを含まないことと、改行を含むpathの列挙。試験範囲の不足: CLI経由のconfig失敗のstdout・stderr、fetch・use・releaseの `unknown-deployment`、guard非取得の直接観測。再開: RC2。判断: owner。
