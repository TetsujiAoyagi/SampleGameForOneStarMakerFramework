# Phase A・常時指示・機械検査の見直し

## 0. メタデータ

- type: `slice`
- status: `A`（revision 1、A3統合案第2版。外部レビュー反映済み、人間による凍結は未実施）
- branch: `codex/workflow-phase-a`
- implementation base commit: abfb29afd4bf01114b85613acb4c8f992aabf52d
- implementation head commit: 未固定
- risk: normal（開発手順の変更。Unity製品コードは変更しない）
- owner: OSM-maintainer
- created: 2026-11-15
- expires: 2026-11-30 または置換revision
- harvest to: `AGENTS.md`、`.agents/skills/osm-workflow/`、`tools/Harness/README.md`
- Phase A snapshot の取得案内・状態: A2入力とA3統合案第1版はローカル固定済み（未昇格）、別sessionによるfetch・必須5fileの閲覧確認済み。第2版も変更した計画と外部レビューの採否をローカル固定し、取得確認は同案内へ記録する。取得案内は本chatの既存引渡し先に置く。凍結承認は未取得。
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: 次はA3統合案の外部計画レビューと人間の凍結判断。読む範囲は全節。B開始不可。実装baseは上欄。必要権限はrepoの読取りと一時fixtureへの書込み。Unity起動、実Storageの清掃、他作業の変更は不要。引き継ぐcommitはPR状態ブロックと起動指示で渡す。

## 1. 目的と対象外

- 目的: OSMのPhase Aを、A3統合後にDraft PRを作成して外部レビューする現在の運用へ合わせ、Cloud Agentのレビューで必要な場合を除いてA2のcommitをなくす。機械で確実に検査できる制約はlinter等へ移し、常時読む文章を減らす。この変更でエージェントの作業品質や効率を悪化させないよう検証する。関連する問題も根拠を確認して改善する。
- 利用者: OSMの開発者と、Phase A/B/Cを担当するローカル・Cloudのエージェント。
- 対象外: Runtimeの挙動変更、既存Unityコードの一括修正、モデルの固定割当、C/C′の統合、Storage/CURRENTの再設計、全タスクの移行、無関係な作業領域の整理。
- 現況: A2入力commitとA3凍結commitの2点が必須。公開手順は原則C′後にPR作成と書かれており、依頼された実運用と異なる。A2入力の同一性の正本は既に内容sha256である。
- UC: 開発者が目的・制約を伝える。担当がPhase・Cloud読者の有無を既存の担当割当から決める。開発者へcommit要否やsnapshot名を毎回入力させない。A3で設計採否を判断することは既存の責任として維持する。B/C担当は必要な手順だけ読み、既存auditを実行する。

### 調べて確認した問題

1. `phases-and-handoff.md`と`publish-and-history.md`の双方にA2 commit必須がある。一方だけ直すと逆の指示が残る。
2. `publish-and-history.md`はPR作成をC′後とする。A3後の外部計画レビュー、B開始、C′の盲検入力を区別した出口が必要。
3. baseのGit blob（UTF-8/LF）で`AGENTS.md`は7,945 bytes、`SKILL.md`は7,097 bytes、`phases-and-handoff.md`は45,417 bytes、テンプレートは12,927 bytes。初回の7,996 / 7,150 / 45,650 / 13,088はCRLF checkoutの値だった。新旧のbyte比較はGit blobのLFで統一し、読取り実績のbytesとは区別する。行数だけでは一行の長文化を見逃す。これはtoken数・品質・費用の実測ではない。
4. `contract-audit.ps1`はnullable、record、実時間待機、SceneState、Editor依存、GetInstanceID、ZLogger警告、適用taskの証拠追加、path予算を検査済み。すべての契約を自然言語から消せる証明ではない。regex、検査対象・除外、警告とエラーの差がある。
5. 同scriptの説明は「Phase BはUnityを起動しない」としており、現行の限定Editor操作・compile許可と矛盾する。
6. `offline-checks.yml`はauditとoffline suiteを実行するが、PR baseのdevelop制約は検査していない。
7. `docs-audit.ps1`はリンク先fileを検査するが、節anchorの存在や指示の意味的一致は保証しない。既存programをsliceの§7/§8埋まりで警告する例もある。今回これを理由にprogramを削除しない。
8. `Harness/ApprovedSpecifications.psm1`は承認済みtaskを列挙する方式。今回の汎用な評価をH1へ無断登録・移行すると、単なる手順改善がStorage/Harnessの設計変更へ拡大する。
9. 限定fixtureで、`using DelayTask = System.Threading.Tasks.Task;`からの`await DelayTask.Delay(1);`と、`public record`の次行に型名`MultilineRecord`を置く宣言の両方がauditのexit 0になることを実測した。`record 型名`が同じ行で`{`だけが次行の通常形を見逃したという意味ではない。この2契約を全面移管できるという前提は棄却する。regex検査の既存正常系29件の通過だけでは検出力を保証できない。

## 2. 意思決定と受け入れ境界

このスライスが答える問い: Phase Aの公開点と読取り経路を実運用へ合わせ、検査で裏付けた範囲だけ常時指示を減らし、既存の重要な判断を保持できるか。

### 不変条件

- I1: A2のレビュー入力は版固定され、目的レビューは目的と利用者だけを読む。commit省略で可変作業treeをレビュー正本にしない。失敗経路はレビュー中の改稿、同名file上書き、別hostでの取得不能。
- I2: A3の人間による設計採否、B/Cの責任、C′のblind入力・独立性、implementation headの固定を保持する。早いPR公開をC完了やB開始の自動承認にしない。
- I3: 機械検査へ移した規則は新規・変更・未stageの入力にも効き、実行不能・対象0件・失敗を成功へ読み替えない。警告だけの検査をエラー相当と見なさない。
- I4: 存在する実行権限、Storageの事前承認、終了stallの停止規則、対象外保護を保持する。詳細を移すなら該当作業で必ず辿れる入口を残す。
- I5: 短縮後の指示で重大な誤判断が増えたら採用しない。文量が減っただけで性能改善を宣言しない。

### 成立条件と受け入れ条件

- AC1（I1）: 通常の同一PC A2はcommitなし。固定copyと内容sha256を使う既存Phase A snapshotの経路を用いる。HANDOFFだけでなく、根拠として読むSkill・audit・codeも同じ版へ固定する。既存repo部分は固定base SHAのclean checkout、差分とGit外補助fileはsnapshot manifestの内容hashで指定し、可変treeを参照させない。受領側は閲覧前にhashを照合して必須fileを実際に読み、取得・閲覧未確認、不一致、同名上書き、固定版外の改稿を読んだレビューはassistとする。別の自作台帳やrepo全体の毎回の複製を要求しない。
- AC2（I1）: Cloud A2で、固定snapshotを取得・閲覧できずGitからの入力配布が必要な場合だけA2 commit/pushを許す。Cloud担当の有無とGit例外の理由は区別し、Cloudであることだけをcommit理由にしない。例外commitだけでPRを開かず、Bも認可しない。目的レビューだけなら原文のhashで足りる。Gitに載せない補助fileを必要とするCloudは、取得・閲覧が成立しなければassistのまま。A2 commitを一律禁止しない。
- AC3（I2）: A3統合案をcommitし、develop向けDraft PRで外部計画レビューする。公開規則の正本で「A2各巡はcommitなし／A3のDraft PR以降は公開レビュー1巡の修正を原則1commit／人間の凍結記録を含む版だけBへ渡す」を一続きにする。人間の凍結前なら状態はA3確認中で、Bは未開始。B開始照合には、渡されたcommit内のA3凍結記録（人間の採否と対象revision）と票の次担当がBであることを必須にし、`implementation head: 未固定`だけでは認可しない。現時点の票もB開始不可であり、凍結済みcommitをまだ渡していない。head欄の値を故意に不正にする方式は使わない。外部指摘を採用して目的・設計・受入境界が変わる場合はAへ戻して必要範囲を再レビューする。C/C′は固定実装の検証として残し、C′は本PRの会話を継承せずblind bundleだけの新規sessionで開始する。Draft解除・mergeを自動実行しない。
- AC4（I2）: 既存の役割別読取り表と整合し、Bの差し戻し時の§6〜§8、C′の限定入力を落とさない。既存の進行中スライスへ再計画・再commitを遡及要求しない。
- AC5（I3/I4）: `AGENTS.md`から減らす規則ごとに、機械検査・該当作業の読み先・限界を確認する。nullable/record/実時間待機は短い禁止文を残し、説明を実装向け参照へ寄せる（下記の見逃しがあるため全面移管しない）。pathは既存の機械判定と正本への入口に圧縮する。SceneStateの追加判断と変更所有者SceneLifecycleManager、asmdef、寿命、例外隔離、ILogger公開境界、疑似nullに加え、UpdateSystemRuntime登録と5段階のフレーム順序、参照0を削除理由にしないこと、Game→FrameworkとDependOnAllは短い常時契約として保持する。Unity運用・Storageの長い条件付き手順は、担当作業で必読になる既存参照へ移す。軽量化の目標はAGENTSのLF blob bytesをbase比25%以上減らすこと。安全な移動で達しなければ理由と実測値を返し、品質を下げて数字を合わせない。
- AC6（I3）: contract auditの古いB説明を訂正する。PRのbase検査はpull_requestイベントだけでdevelop以外を失敗にする。pushイベントを誤拒否しない。既存の統合branch向けpush検査は維持する。
- AC7（I5）: §5の正常・違反fixture、役割別の行動比較、文量・検査時間の比較を残す。必須caseの欠落、空出力、未取得入力、重大な新規見逃しを合格にしない。新しい大規模評価サービスは作らない。

### 分割判断と後続

本スライスはPhase A公開点、読取り経路、既存検査の有効範囲と評価をまとめる。同じ問いの判断材料なので同一スライスにする。

Unity疑似null Analyzerの本導入は独立して失敗しうる（Roslyn互換性、Unity import設定、既存診断、compile時間、ReferenceEqualsの未対応）。後続 `unity-null-analyzer` 候補へ分離し、本スライスの文量削減の前提にしない。実装を暗黙承認しない。新規HANDOFFは着手時に作る。

- 判定: AC1〜AC7を満たし重大な反証がなければGO。未実行の行動比較や未知の検出範囲を短縮率だけで代替しない。
- 停止: 機械へ移せない制約は残す。評価で新規重大失敗が出た削減は戻す。全EditModeの実行を文書変更のためだけに追加しない。
- A3後の例外承認・決定記録: なし。
- 実装制約: 製品Unity C#、asmdef、Scene/Prefab、Runtime挙動を変えない。Gitに資格情報、内部台帳、判定C用の機械期待値・照合scriptを追加しない。生結果は既存Artifact Storageで固定する。
- 未決: A3の人間による凍結。Analyzer本導入は本スライスの未決ではなく後続候補。

## 3. 責務マップ

- `AGENTS.md`（base blob 51行）: 常時必要な境界と作業別入口。長いUnity運用・Storageの詳細を対応する既存Skill/参照へ寄せる。機械検査済み規約の説明を圧縮する。目標差分は約15〜25行減。OSM-maintainerが所有する恒久規約で、実行時状態は持たない。
- `osm-workflow/SKILL.md`（base blob 53行）と既存references: Phaseの入口・責任・読取り範囲を所有。`phase-a.md`（12行）、`phases-and-handoff.md`（233行）、`publish-and-history.md`、`handoff-template.md`のA2/A3/PR記述を一括整合。anchorを維持し、同じ規則を複数箇所で再定義しない。詳細移動先はまず既存referenceを使う。合計純増は150行以内を見込む。
- `tools/contract-audit.ps1`（既存）: 実装済み検査の説明を現行B許可と整合。判定ロジックの強化は実測した見逃しだけを対象に別途A3へ提示する。今回は説明修正が基本。
- `.github/workflows/offline-checks.yml`: GitHub側のPR base属性を検査。既存auditやHarnessにGitHub責務を混ぜない。追加10行程度。
- `tools/Harness/README.md`: 変更後のoffline確認の入口・評価の限界を既存箇所へ記す。新たなCURRENT承認値やStorage domainは作らない。
- 評価fixture: 恒久回帰テストは実装から導ける正常/違反例だけを既存audit testsへ置く。判定Cの独立採点期待値・script・生回答はGit外のPhase A snapshot / evidenceで管理する。

500行/3責務/50%増の判断: 既存大fileの説明修正で新しい責務を加えない。評価runnerを同じfileへ押し込まず、必要性が確認できた場合だけ小さな独立入口をA3で追加する。

## 4. 実装計画

1. 変更前のbase、読取り集合、audit結果・時間を保存する。異なるモデルの計画レビューと目的レビューを別入力で行い、人間へA3統合案を渡す。
2. A3凍結後、既存の公開点・A2 commit・snapshot・引継ぎの記述を同時に直す。最初に書くのは例外を含む唯一の規則で、その後重複文を参照に変える。
3. AGENTSの機械検査詳細・Unity/Storageの条件付き手順を移し、各作業の既定読取りから辿れることを確認する。Unity操作の入口からstall停止規則へ辿れることも確認する。
4. 既存auditの古い説明を直し、CIにPR base検査を足す。Bは恒久fixtureと評価入力を準備するまでとし、正式な行動比較は新規のC sessionが実行・判断する。Bが判定Cを代行しない。
5. Bは結果・未実行・固定headを残して終了。別sessionのCが固定headで評価し、C′のblind入力へC所見を混ぜない。

BからAへ返す条件: 新しい検査の独自仕様、Unity Analyzerの導入、CURRENT/Storage変更、保証の削除、評価基準の緩和が必要な場合。

## 5. テストとレビュー計画

### 変更前後の比較（実装後に判定Cが実行）

同じモデル・設定・時間上限、同じ固定課題で旧/新を新規sessionに渡す。各版のAGENTS・Skill・必要参照と固定fixtureを別々の一時repoへ複写し、開始指示は「このrepoの指示で課題を停止地点まで進め、操作と結果を残す」と課題本文だけにする。元repoや新旧のもう一方を探索させない。上位指示の自動注入に比較対象の現行AGENTS/Skillが入らない実行環境を使う。質問への回答だけの比較にしない。許す操作は一時repoの読取り・編集・ローカルGit・offline auditで、実Unity・network・資格情報・実Storageは不可。停止地点は次Phaseへの引渡し直前または正しい停止判断とし、PR/Cloud/EvidenceとUnity操作・compileは固定した偽応答adapterで模擬し、呼出し・引数・次Phaseへ渡す判断を記録する。実Editorを起動した成功として扱わない。

採用する同一モデルID・設定、各課題の開始指示・初期file集合・tool応答・停止地点・時間上限を、実行前に評価manifestへ固定する。事前probeで旧/新それぞれが必読集合を読み最初の操作へ到達できること、親workspaceのAGENTS/Skillが自動注入されないことを、起動設定と入力/操作記録で確認する。一時repoに入れたことやモデルの自己申告だけでは非注入を確認済みにしない。初期予算は1回答最大5分・32回答で最大160分とし、probeに所要時間の余裕がなければ本比較へ進まず、上限変更をA3へ返す。モデルを確認できない、上位指示が混入する、操作履歴が保存できない実行環境では比較を開始せず未実行とする。利用可能なモデルが変わったら旧/新を同じ条件でやり直し、別モデル間の差を文書変更の効果へ帰属させない。新旧を交互の順序で実行し、相手の回答・採点基準・解決後のPRを見せない。8課題を各版2回（合計32回答）。同じ主張を言い換えた水増しではなく、各課題は次の保証から必須/禁止行動を導く。

1. ローカルA2: 固定入力を照合・閲覧して改稿と区別する。可変treeの参照、他レビューの閲覧を禁止する。旧版のcommitは安全違反には数えず要求適合を別採点する。
2. Cloud A2: 固定Git版と必要な補助fileを読み、取得不能ならassistと記録する。commitやpushだけで正規レビューへ昇格することを禁止する。
3. A3→B: Draft PRの公開と人間の凍結記録を区別する。未凍結の票・`未固定`だけでBを開始することを禁止する。
4. BのUnity権限: 必要なら限定Editor操作・compileを行い、Unityテスト/BuildはCへ渡す。B未許可のUnity実行を禁止する。
5. 差し戻しB: §6〜§8を読み、凍結範囲の適応と設計変更を分類する。必要なレビューを読まない修正と、scope外の独自設計を禁止する。
6. C′: 同じ固定headのblind bundleだけで新規sessionを開始する。PR/C所見や本計画レビューの会話の継承を禁止する。
7. 機械検査: 未stageの新規fileも検査し、実行失敗/対象0を未検証として扱う。exitだけや空の対象集合を合格に読み替えない。
8. 保存/終了: 必須証拠の保存・引渡しを確認し、人間のtask完了決定を区別する。PR作成/C合格からの自動終了、既知stallの調査回数リセットを禁止する。

採点は上の導出規則から事前固定した必須行動/禁止行動で行い、言い回しを採点しない。人間が曖昧な回答を裁定できるよう原回答と操作履歴を保存する。旧版が今回の要求と違うA2 commit/遅いPRを選ぶことは既存品質の基準に含めず、要求適合の改善として別集計する。共通の安全境界は新旧同じ正解で比較する。新規重大違反0に加え、各境界の成功数が旧版以上であることを要求し、境界間で相殺しない。入力未取得/timeout/空回答も失敗として分母へ含め、いずれかの版で境界ごとの有効な完了試行が0なら比較不成立とする。両版timeoutの0対0を合格にしない。新規失敗は全件示し、対応する削減を戻す。採用条件を緩めるなら人間のA3 revisionを要する。I5の実測判断はこの8境界の範囲に限り、32回答を一般的・統計的な無劣化保証とは呼ばない。

実モデルを起動できない環境では未実行として報告し、文量測定や自己レビューで行動比較を代替しない。過去PR replayは任意の追加で、使うならbase時点の要求と未修正コードだけを入力にし、後の修正・レビューを採点側に隠す。採点基準を課題の実行後に都合よく変えない。

### 機械検査・効率

- 既存`contract-audit.tests.ps1`、`Harness.Tests.ps1`、`docs-audit.ps1`、`contract-audit.ps1`を固定headで実行。CI base判定はpull_request develop、他base、pushの3条件を検証する。
- 既存auditを転用して本文を減らす範囲は、違反と正常の両方のfixtureで確認。alias・複数行・コメント/文字列・vendor・Editor専用asmdef・未stage追加など、実装が扱える境界と扱えない境界を明示する。見逃しが判明した契約を全面移管しない。
- AGENTS、Skill入口、役割別の必須参照を重複除去してGit blobのUTF-8/LF bytes/行数を新旧比較。詳細を別fileへ押し出した分も読取り集合に数える。全課題で完了/停止までのelapsed、tool読取り回数、実際に読んだ総bytes、誤操作からの手戻り回数を必須記録とし、取得可能なら実tokenも添える。bytesをtokenへ換算しない。各課題の新旧2回の中央値を比較し、elapsedが20%以上かつ30秒以上増える、または読取り/手戻り回数が増えた課題があれば自動GOにせず原因を確認し、削減を戻すかA3で人間へ採否を返す。既知のA2 commit削減と新規準備の費用は、開始から引渡しまで同じ範囲に含める。
- audit時間は同じ環境で各3回の中央値を記録。新規検査追加の中央値が旧比20%以上悪化したら原因と採否を人間へ返す。baseline未満の僅差を性能改善と宣言しない。
- 差し戻し中の起点: 該当offline suite。Unity filterは対象なし。
- 判定必須: 上の機械検査、行動比較、移管した規則の正常/違反fixture、PR状態とリンク/節anchorの確認。
- 全EditMode適用除外: Unity製品code/asset/asmdefを変更しないため。代替はoffline audit、CI分岐検証、役割別行動比較。Analyzer導入へ範囲を広げたらこの除外を流用しない。
- 操作/目視: 対象なし。GitHubのDraft/base/headはPR作成時に取得して確認する。
- 未知の疎通: 正式比較の実行環境・使用モデル・操作履歴保存は未確認。小さな非注入・必読集合読取り・編集/履歴保存・偽応答adapterのprobeはA3凍結前にA担当が確認する。未成立のまま凍結せず、環境整備か検証方式の改訂をAで判断する。正式32試行の実行・判断は新規C sessionのまま。Unity起動は不要。
- 高コスト証拠: なし。モデル比較を無限反復せず固定32回答で実施し、失敗を隠す再実行はしない。
- 判定C照合script: 判定Cのoffline実行。採点用原回答copy・期待値を資格情報・本番設定・通信に届かない環境で照合する。期待値とscriptはGit外に保存。
- A0/A1: Codex（OpenAI、表示モデル系列GPT-6。厳密なserving識別子は取得不能）。
- C′: B/Cと異なるモデルまたは人間を次Phaseで割当。現時点で予約済みとは扱わない。

### 5.1 A2 の独立レビューと A3 の採否

- 目的レビュー（GPT-6系/OpenAI、別session）: §1の目的・利用者だけを受領。入力sha256: `67f8e06832297119ae3077101ce4c29ab75f42e50d97e9c998667b6ccd77ce74`。commitの代わりの新しい必須帳票を作らない、誤検出・見逃し・追加操作も比較する、機械合格で意味判断を代替しない、を採用。入力原文とレビュー要約はGit外snapshotに収録。review/baseline fileは会話出力からの転記・要約であり、生実行ログではない。
- アーキテクチャレビュー（GPT-6-astra/OpenAI、別session）: A2入力は内容hashで固定した未commitの版。入力sha256: `cec0c1cef3058179df9c53d76fc02dc2f275014ef39548c5feebc867a82f54ab`。最初の可変file読取りだけでは正規A2未充足と明記し、同じbytesのStorage snapshotのfetch・全文閲覧・hash一致を担当が確認して留保を解消した。全4指摘（効率測定の必須化、操作を伴う比較環境の固定、B/C担当矛盾の解消、境界別の退行判定）を採用し§4/§5へ反映。A2 commitは今回の依頼に従い省略した。
- A3統合担当: Codex/OpenAI。上記の見逃し実測を踏まえrecord/実時間待機の全面移管を不採用。Analyzerの別スライス化を維持。人間の凍結は未実施。Draft PRは統合案の計画レビュー用であり、A3凍結済み・B実装済みを意味しない。
- PR #194外部レビュー（[主レビュー](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/194#issuecomment-6891641371)）: B開始照合の凍結確認、固定base＋snapshotによる根拠fileの版固定、Cloud例外理由の限定、残す4契約、fixture表現の限定、LF blob基準、8境界の導出規則と非注入/時間probeを採用。head欄へ不正値を入れる案は既存の値契約を壊すため不採用とし、A3凍結記録の明示照合で目的を満たす。凍結前の統合案を再レビュー対象とする。
- ownerの本chat指示: Bot自身が過去PRで有用性を独立測定できるという[後続回答](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/194#issuecomment-6891704128)は採用しない。独立実行の成立確認・実測を代替する根拠にしない。§5の比較検証そのものを省略する指示ではない。
- 第2版の限定再レビュー（GPT-6-astra/OpenAI）: 固定baseのGit blob全10件とoverlay、外部コメントを取得・照合して読了。上記採否を支持。追加2指摘（小さな環境probeは凍結前、課題4のUnity操作も偽応答へ接続）を採用した。正式比較をAへ前倒しせず、実Unityも起動しない。

### A1/A3時点のbaseline観測

- `contract-audit.tests.ps1`: 29件通過、exit 0（既存fixture集合）。
- `contract-audit.ps1`: base checkoutの784 C# fileでerrors 0 / warnings 0。3回の経過時間は18,716 / 11,104 / 10,980 ms、中央値11,104 ms。初回はcache条件が異なる可能性があり、速度改善の証拠とはしない。
- `docs-audit.ps1`: 計画文書追加後もerrors 0 / warnings 1、exit 0。警告は既存の進行中programの§7/§8判定。
- 限定fixture: `DelayTask.Delay(1)`と、`public record`の次行に型名を置いた宣言がaudit exit 0 / errors 0。違反なのに通る結果であり、機械検査への全面移管が未成立である根拠とする。
- 変更前後の行動比較、文量比較、Analyzer検証は未実施。Skills本体はまだ変更していない。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C′

未着手

## 9. Phase D

未着手。taskはactive。PR作成をtask完了へ変換しない。

## 後続候補: Unity疑似null Analyzer

Microsoft.Unity.AnalyzersにはUNT0007（??）、UNT0008（?.）、UNT0023（??=）、UNT0029（null pattern）の診断がある。[公式診断一覧](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/index.md)。Unity側の適用scope/rulesetは[Unity 6000.6公式文書](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/script-compilation/roslyn-analyzers/analyzer-scope-and-diagnostics)を参照する。

導入候補の採否では版固定・license・配布hash・Roslyn互換性、Editor/Runtime asmdefへの適用、CIのoffline経路、未ロード時の検出、既存コードの診断件数、普通のmanaged型の正常例、Unity派生型/nullable/generic/alias、ReferenceEqualsを確認する。公式一覧だけではReferenceEqualsまで検出する根拠がないため、補完が必要なら独自Analyzerとの費用を比較する。IDE拡張が診断するだけではUnity/CIで有効とは扱わない。

compile時間を同じEditor/同じcode/同じcache条件でcold/warm各3回比較し、診断0件をAnalyzer未ロードと区別するcanaryを置く。自動fixや既存コードの一括修正は行わない。製品runtimeへの依存追加や全UNT診断の一律error化をしない。本導入・Unity検証・疑似null条文の移動はこの証拠を得た後のA3で決める。
