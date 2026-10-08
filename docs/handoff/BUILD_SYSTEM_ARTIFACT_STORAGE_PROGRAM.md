# BuildSystem Artifact Storage Program

## 0. Metadata

- type: `program`
- status: r6方針改訂は2026-10-08にownerのPhase D承認で完了。programは進行中。スライスAは2026-10-08にA3 r5を凍結し、Phase BからC/C′までの進行をownerが承認した。現行仕様と採用runの入口は `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle`。本番なし・過去の開発証拠の継承不要を前提に保存契約を一括置換する。新CLI、終了イベント接続、清掃の実装を検証中。運用切替、限定R2/scheduler/環境リセットと最終判定C/C′の実績はCURRENTの固定入力で確認する。後続スライスのA3は別途行う。
- program policy revision: `r6` — 単一の開発用保存契約。旧データと互換性を引き継がない。
- branch: `codex/artifact-storage-development-policy`
- planning base commit: `a027bf04b4f5ce16494b93daf10e5b5bccf15a28` (`origin/develop`; PR #106・#107を含む)
- implementation head commit: not applicable; 文書改訂のみ。
- risk: `normal`（文書改訂）。リセットと削除機能の対象・検証は実装スライスで具体化する。
- owner: OSM maintainers
- created: 2026-09-26
- updated: 2026-10-08
- expires: 2026-12-26 または置換revision（program文書の見直し期限）
- harvest to: 実装済みの通常操作を `tools/Artifacts/README.md`、program状態を `docs/README.md` へ反映し、program完了時に削除する。

## 1. 目的・要件変更・現在地

開発中の成果物を非公開で手軽に保存・受け渡しし、不要になったものは自動で片付ける。人間が毎回ファイルを運んだり、保存・清掃を個別承認したりせずに使えることを成果とする。

[ownerの要件変更](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/108#issuecomment-6049302344)と本チャットの修正指示を採用する。本番運用はなく、過去の開発用Artifact・試験設定はリセット可能。旧証拠の保存継続、旧参照/readerとの互換、PRごとの移行台帳を要求しない。物理的な試験物の不存在を確認した、という意味ではない。

この指示を根拠に、r5のlegacy分岐、旧最低保存義務と日程、参照中/参照不明の無期限保持、keyごとの削除承認を置き換える。**PR #107のE1/E2へのIndefinite追加、reader互換、新保護事実recordの実適用タスクは取りやめる。** 過去の方式確認の成果は否定せず、経緯はPR/Git履歴に残す。撤去する方式を一度完成させてから移行する工程は作らない。

現在の実装機能は [Artifacts README](../../tools/Artifacts/README.md) を正とする。スライスAでEvidence v2、workflow taskの終了/再開配送、純粋保持policy、自動清掃と一回のresetを実装し検証中。H1 bootstrapと固定承認値/CURRENTは初期化済み。新契約の成立は同じ最終headのoffline・限定実観測とC/C′で判定し、実装進捗をR2設定切替・物理清掃完了へ読み替えない。

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

### A. Evidenceの通常利用と期限清掃

**問い:** 任意の明示した非秘密file集合を保存し、別sessionで取得・hash照合・ログ/画像閲覧でき、作業中は残り、終了後は自動で片付くか。

**最低条件:**

1. 固定E1/E2の入力定数を通常のtask/file集合契約へ置換する。publishを内部検証・記録・参照/hash返却まで一操作にし、別sessionで取得・必要な内容を閲覧できる。旧形式対応は作らず非対応を明示する。
2. workflowの終了/再開イベントとStorageの期限を自動接続する。終了の意味と発行元をA3で固定し、Evidence独自の承認やHarness closeの推測接続をしない。イベント再適用は同じ結果になり、再開後は再終了まで削除資格を持たない。
3. inspectと実際に動く清掃、自動起動を同じpolicy判定へ接続する。30日の境界、利用開始/再開と削除の競合、対象外・転送中の非削除、部分失敗の再実行を扱う。利用中保護の状態を判定できなければその対象は削除せず理由を表示する。終了未決taskを年齢だけで消さない。
4. §3の対象限定リセットと、旧経路・schema・不要な互換説明の置換を一回の切替手順にする。原sourceの保護、現ruleの実態確認、設定整理と日常清掃の権限分離を満たす。古い証拠の保存継続を完成条件にしない。
5. Artifacts READMEを実際の新CLIの操作・状態に合わせる。旧保存義務やlegacy分岐を復活させず、切替時に旧経路の説明を除く。新しく設定した範囲内の通常清掃にkeyごとの承認を求めない。

**検証:** 保存→別session取得/hash・ログ/画像閲覧、30日境界、終了イベントの再適用、再開後への古いイベント到着、利用開始/再開と削除の競合、対象外の非削除、部分失敗の再実行、秘密非露出、安全な展開。時刻注入、#106のdummy fixture、隔離した使い捨てデータを使う。実R2の限定往復/清掃は運用経路の確認として行い、旧E1/E2継続取得や30日実待機を要求しない。自動清掃の起動・再実行が人の反復操作なしで成立する経路もA3で具体化する。

判定は関連Artifacts/Harness offline suiteと限定R2検証。Unityを変更しないため全EditMode回帰は適用外。スライスAのschema/API、file配置、清掃間隔、7日の初期値、リセット対象/操作はA3 r5に固定済みで、Git外CURRENTから取得する。実装時に必要な終了イベント接続だけを扱い、Harness全体の改造へ膨らませない。

**停止規則:** 上記の通常利用・期限清掃と一回の切替が成立したら閉じる。Build、Cloud、GUI、常駐broker、別provider、長期保護・互換維持を追加条件にしない。

### B. 開発Buildの保存と件数管理

既存Build出力を普通のpath/metadataとして受け、成功publish、別process fetch/hash、最新N件と利用中の保持、失敗publish時の既存保持を確認する。Nの既定値案10件、系列識別、容量上限をBのA3で固定する。大容量に必要なmultipartは対象サイズに応じて扱い、BuildSystemの作り直しを前提にしない。

### C. 別host / Cloud

必要な環境から個別に、到達性、限定grant、無人保存/取得、ログ/画像閲覧を確認する。未成立は当該環境の未対応として残し、ローカル利用を止めない。親鍵を通常file/promptへ複写せず、platform仕様は着手時に再確認する。

### D. BuildSystem / Harness接続

外部CLIを主入口にUnityを交換可能なbackendとして接続する。H2d/H3・CURRENT連携、同じ実体の所有権整理は必要になった範囲で行う。Storage単独の利用をUnity GUIやEditorに依存させない。Unityテスト/BuildのPhase責任と既知native終了stallの調査停止規則を維持する。

各スライスは担当・責務配置・変更file・発見用/判定必須テストをHANDOFFへ固定する。H1/CURRENT方式は各A3で明示する。ここはprogramであり、実装スライスのA3済みとは扱わない。

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
