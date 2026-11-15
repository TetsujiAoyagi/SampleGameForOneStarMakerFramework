# Phase A・常時指示の見直し

## 0. メタデータ

- type: `slice`
- status: `B`（revision 2、凍結済み・実装完了）
- branch: `codex/workflow-phase-a`
- implementation base commit: abfb29afd4bf01114b85613acb4c8f992aabf52d
- implementation head commit: この票を含む commit
- risk: normal
- owner: OSM-maintainer
- created: 2026-11-15
- expires: 2026-11-30 または置換revision
- harvest to: `AGENTS.md`、`.agents/skills/osm-workflow/`、`tools/Harness/README.md`
- Phase A snapshot の取得案内・状態: 旧案とレビュー記録はローカル固定済み（未昇格）、別sessionの取得確認済み。revision 2は本chatの案内で渡す。
- Phase B result snapshot / evidence / C′ blind bundle: 未生成
- A3凍結記録: 2026-11-15、本chatでownerが「私もそう思う」に続けて「いい妥協点」案を確認し、「とりあえず君を信用してこのセッションを立ててる」「変更やってくれ」と実装を指示。直前の妥協案をrevision 2として採用する。32試行ゲートと評価基盤の新設は廃止し、下記の限定変更・照合・最大6試行のsmokeへ置き換える。追加の凍結承認は求めない。
- 引継ぎ票: 次は発見 C。§0〜§8 と固定差分を読み、§5 の条項・読取経路・#193/#194 の時系列照合、offline 検査、PR base の3分岐、別sessionの限定smokeを実施する。移動表とsmoke準備記録は既存の引渡し先にある。成立しないsmokeは未実行とし、対応する移動を見送る。Unity製品code、実Unity、実Storage清掃、別taskは対象外。C′は所定のblind入力のみ。

## 1. 目的と対象外

- 目的: OSMのPhase Aを、A3統合後にDraft PRを作成して外部レビューする現在の運用へ合わせ、Cloud Agentのレビューで必要な場合を除いてA2のcommitをなくす。機械で確実に検査できる制約はlinter等へ移し、常時読む文章を減らす。この変更でエージェントの作業品質や効率を悪化させないよう検証する。関連する問題も根拠を確認して改善する。
- 利用者: OSMの開発者と、Phase A/B/Cを担当するローカル・Cloudのエージェント。
- 対象外: Runtime/Unity C#・asset・asmdefの変更、既存code一括修正、Analyzer本導入、CURRENT/Storage domain再設計、汎用評価サービス、merge。
- UC: 利用者は目的・制約とA3の採否を伝える。担当が既存の割当からCloudの有無と固定入力の配布方法を決める。commit要否・hash等の手入力や新しい帳票を利用者へ求めない。
- 現況: 通常A2 commitとC′後PR作成という規則が実運用とずれている。AGENTSの長い条件付き3項目は移動可能だが、code契約の全面機械移管は未成立。

## 2. 受け入れ境界

- I1: 入力版の同一性、A3の人間判断、B/Cの責任、C′のblind入力、implementation head固定を保持する。
- I2: 移した契約を担当が辿れず、禁止を避けるだけで必要な仕事が終わらない状態を合格にしない。新たな保存/承認負担でA2 commit省略を相殺しない。
- AC1: 同一PCの通常A2はcommit不要。既存code/規則は固定baseのclean detached checkout、HANDOFFは同時点の固定copyとsha256で渡す。読む前に版を照合し、可変treeを正本にしない。Git外の補助fileが必要な場合だけ既存snapshot/取得経路を使い、全A2への新manifest・stage/fetch・取得receiptを追加しない。目的レビューは目的・利用者原文だけのまま。
- AC2: Cloud A2のGit配布が必要なときだけ例外commit/pushを許す。Cloudであることだけを理由にせず、Git外必須証拠が取得不能ならassist。例外commitだけでPR作成/B開始を認可しない。「Cloudの担当」と公開規則を同時に直す。
- AC3: A3統合案をDraft PRで外部計画レビューする。公開後は1巡の修正を原則1commitにまとめる。B開始には渡されたcommitの人間の凍結記録・対象revision・票の次担当Bを照合し、`未固定`だけでは開始しない。head値は既存3種を保つ。C′はPR会話を継承せず固定blind入力だけの新規sessionとする。
- AC4: AGENTSの長いUnity運用、native終了stall、Storage運用の3項目を短い入口へ置き換える。code契約は全文保持する。移動元の各文を、移動先file/節・担当作業の必読経路・機械検査の有無へ対応付け、対応が欠ける文は動かさない。stallはUnity Editor操作だけでなくPhase Cのrun-tests前からも必読にする。既存進行taskの再計画は要求しない。
- AC5: contract-auditの古い「BはUnityを起動しない」という説明を直す。CIでpull_requestのbaseがdevelopであることを検査し、push検査は維持する。新しいcode linterやAnalyzerは今回導入しない。alias Task.Delayと型名が次行のrecordを既存regexが見逃すため、短いcode契約を消さない。
- AC6: §5の照合と限定smokeを行い、未実行は未実行と記録する。AGENTS削減率・tool回数・少数の新旧成功数を採否ゲートにしない。smoke環境が小さく用意できなければ大きな基盤を作らず、確認できない規則の移動だけを見送る。禁止/許可の意味を変えるならAへ返す。
- 分割: 上記を同じ文書/運用変更として扱う。Unity疑似null Analyzer本導入はRoslyn互換性・ReferenceEquals等の検出・Unity/CI適用・compile負荷が独立の未知なので、後続候補のまま。別PRや新評価programは作らない。
- 判定: AC1〜AC6を満たし、未実行を成功とせず、残すべき契約を保持できればGO。一般的な無劣化保証・性能改善の実証とは呼ばない。

## 3. 責務マップ

- `AGENTS.md`: 地図とcode契約を保持し、条件付き3項目のみ短縮。baseのUTF-8/LF blobは7,945 bytes。数字目標のために契約を消さない。
- `osm-workflow/SKILL.md`と既存references: A2/A3/PR/Cloud/引継ぎの正本と担当別の入口を整合する。説明を重複追加せず旧規則を置換する。新規の大きな参照文書は作らない。
- `osm-unity-editor/SKILL.md`、`phase-b.md`、`phase-c.md`、Artifacts/Harness README: 条件付き運用の既存正本。移した文の実行前の読取り経路を所有する。
- `tools/contract-audit.ps1`: 説明だけの修正。`.github/workflows/offline-checks.yml`: PR base属性の検査。これらは製品runtimeへ依存しない。
- 500行/3責務/50%増: 既存大fileへ新評価責務を追加しない。固定試験のためだけの汎用runner/adapterは作らない。

## 4. 実装順序

1. 本凍結記録をcommitし、別B sessionへSHAを渡す。
2. 公開点、A2固定、A3凍結、Cloud例外、HANDOFF templateとレビュー証拠の記述を整合する。
3. 条件付き3項目を移し、移動表をGit外の検証記録に残す。対応不能の文は残す。
4. audit説明とPR base検査を直す。B結果・未実行・固定headを記録してCへ渡す。
5. Cで照合・offline検査・限定smokeを確認し、PRの差分と検証結果を更新する。mergeはしない。

## 5. 検証

- 決定的な確認: 移動元全文の条項ごとの対応、code契約不変、必読経路、リンク/anchor、A2/A3のGit配布例外・未凍結B拒否・C′blindを照合する。#193と#194の公開時系列に新規則を当て、A2とA3 Draft、公開修正、凍結、Bを一意に区別できるか机上確認する。
- offline: `docs-audit.ps1`、`contract-audit.ps1`、既存`contract-audit.tests.ps1`、`Harness.Tests.ps1`。PR base分岐はPR/develop・PR/他base・pushを確認する。説明変更や文言一致だけを写す新testは作らない。
- 小さなsmoke: stall停止、Storage通常操作の事前承認、B/CのUnity権限の3場面を、新旧各1回・計6試行まで別sessionで確認する。モデル/設定/入力を揃え、同じ課題を使う。実Unity・network・資格情報・実Storage操作は許さず、非秘密の小さなfixture上で必要な成果物・判断・次担当への引渡しまでを見る。正解・旧新版ラベル・他回答は実行者に見せない。
- smoke前に起動設定・入力記録で親repoのAGENTS/Skillが混入しないこと、読取り/小さな編集と操作履歴保存を確認する。モデルの自己申告やtoken差だけで非注入を証明しない。成立しなければ未実行とし、対応する移動を見送る。新しい評価基盤や偽の本番サービスを作らない。
- smokeは具体的な欠陥の発見であり、成功率比較や統計的な採否ゲートではない。失敗は残し、移動が原因なら入口を直すかその文を戻す。必要なら失敗した場面だけ1回再確認し、初回失敗を消さない。
- 効率: LF blob bytesと役割別読取り集合、取得可能な実token/elapsed/手戻り/成果物品質を併記する。回数単独で改善/劣化を宣言せず、一度の評価準備と通常の準備費用を区別する。
- 全EditMode除外: Unity code/asset/asmdefを変えないため。代替は上記offlineと契約/経路照合。Unityを起動しない。
- 採用後: 次の2〜3スライスの既存B/C記録で移動由来の見落としを観察し、確認された文だけ戻す。新帳票や必須評価サービスは追加しない。これは本PRの性能実証済みという意味ではない。
- 判定C照合script: 必要ならCのoffline実行。採点期待値・生回答・照合scriptはGit外、既存Artifact Storageへ保存する。

### 5.1 レビューと採否

- revision 1の目的レビュー・独立設計レビュー・PR #194外部レビューの入力/採否はGit履歴と既存固定packageに保存済み。目的と利用者の原文は変更していない。
- 32試行維持案は撤回。新コメント6892322141の統計計算を再現し、p=.9で約74.18%、p=.95で約52.24%の偶然不合格（独立試行/境界の仮定）を確認した。6試行smokeへ置換する。
- 文の移動/到達確認/採用後観察を採用。3回で遵守率を保証するという表現、token差だけで非注入を証明する主張、merge済み差分への一致を正解とする方式は採用しない。
- 人間のrevision 2承認は§0の本chat指示。条件付き3項目以外のcode契約を短縮する必要があれば範囲を増やさず残す。

## 6. Phase B 実装結果

- 実装: A2の固定baseとHANDOFF copy、Cloud例外、A3 Draft PR・人間の凍結確認、C′のblind session、関連参照を更新。AGENTSの条件付き3項目を既存正本の入口に移し、Phase Cのstall必読を追加。audit説明を現行のB権限に合わせ、CIでPR baseをdevelopに制限した。
- 条項別の移動・必読経路・機械検査の有無はGit外の既存作業記録に記載。AGENTSの他の全行は元版と一致。UTF-8/LF bytesは7945から5778。
- 軽い確認: `pwsh -NoProfile -File tools/docs-audit.ps1` はエラー0、既存のharvest警告1。`pwsh -NoProfile -File tools/contract-audit.ps1` はエラー0・警告0（検査8は通常taskのためnot-applicable）。`git diff --check` 合格。
- smoke準備: fork履歴なしの新規sessionでfixtureの読取・receipt作成・再読取が可能と確認。CLI経路は環境のpolicyにより読取が拒否され未実行。設定緩和・迂回なし。実smokeと親指示非混入の成立確認はCで行う。3項目の移動はその結果まで暫定。
- 未実行: §5の完全な条項照合・PR #193/#194時系列、contract-audit tests、Harness.Tests、CIのPR/develop・PR/他base・pushの3分岐、smoke、判定C/C′。実Unity・実Storage操作は対象外。
- implementation head commit: この票を含む commit
- Phase B 担当・モデル・ベンダー: 実装担当、gpt-6.1-terra、OpenAI

## 7. Phase C

未着手

## 8. Phase C′

未着手

## 9. Phase D

未着手。taskはactive。mergeとtask全体完了は今回の指示に含まれない。
