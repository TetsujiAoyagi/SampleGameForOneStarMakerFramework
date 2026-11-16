# Phase A・常時指示の見直し

## 0. メタデータ

- type: `slice`
- status: `C`（revision 2、C′ 1 巡目の差し戻し C′-F1 を B が修正。発見 C の再レビュー待ち。head 0ad27752 の判定 C と C′ は修正後の head へ流用しない）
- branch: `codex/workflow-phase-a`
- implementation base commit: abfb29afd4bf01114b85613acb4c8f992aabf52d
- implementation head commit: この票を含む commit
- risk: normal
- owner: OSM-maintainer
- created: 2026-11-15
- expires: 2026-11-30 または置換revision
- harvest to: `AGENTS.md`、`.agents/skills/osm-workflow/`、`tools/Harness/README.md`
- Phase A snapshot の取得案内・状態: revision 2をローカル固定済み（未昇格）。本chatの独立案内からCがfetch・閲覧し、凍結commitの内容と一致を確認済み。
- Phase B result snapshot / evidence / C′ blind bundle: head 0ad27752 の判定 evidence と C′ blind bundle は同じ 1 package で、ローカル固定済み（未昇格）。別 process の fetch で hash 照合と全 file の一致を確認済み。取得案内は手元の層の既存の置き場（この task の取得案内と同じ場所と、主 checkout の同じ場所）にあり、所見を含まない。旧 head の raw・所見・最終記録は保持するが、この head の合格証拠にしない。Cloud/別hostの取得成立は主張しない。
- A3凍結記録: 2026-11-15、本chatでownerが「私もそう思う」に続けて「いい妥協点」案を確認し、「とりあえず君を信用してこのセッションを立ててる」「変更やってくれ」と実装を指示。直前の妥協案をrevision 2として採用する。32試行ゲートと評価基盤の新設は廃止し、下記の限定変更・照合・最大6試行のsmokeへ置き換える。追加の凍結承認は求めない。
- 引継ぎ票: 次は発見 C（同じ C の session。C′ 差し戻し 1 巡目の修正後 head の再レビュー）、その後に判定 C と C′ の 2 巡目。§6 の「C′ 差し戻し 1 巡目の修正」に C′-F1 の採否と変更を記録した。照合は、判定 C の必読経路（`phase-c.md`）から Harness README の H1 の節と広い suite の文へ届くこと、README は見出しの追加だけで本文が変わっていないこと。読む範囲は §0〜§10。実 Unity・実 Storage 操作・資格情報は不要。旧 head の所見は Git 履歴と既存 Storage に保持し、この head の合格根拠にしない。人間の merge 指示・task 全体完了指示は未取得であり自動実行しない。worktree と Git 外原記録を保持する。採用後の観察は既存 B/C 記録で行い、新帳票・評価サービスは作らない。

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
- 条項別の移動・必読経路・機械検査の有無はGit外の既存作業記録に記載。AGENTSのcode契約は元版と一致。初回実装のUTF-8/LF bytesは7945から5778（今回修正後は§10）。
- 軽い確認: `pwsh -NoProfile -File tools/docs-audit.ps1` はエラー0、既存のharvest警告1。`pwsh -NoProfile -File tools/contract-audit.ps1` はエラー0・警告0（検査8は通常taskのためnot-applicable）。`git diff --check` 合格。
- smoke準備: fork履歴なしの新規sessionでfixtureの読取・receipt作成・再読取が可能と確認。CLI経路は環境のpolicyにより読取が拒否され未実行。設定緩和・迂回なし。実旧headのsmoke原記録は既存Storageで保持する。新headの確認状況は§7/§8に記録する。
- 未実行: §5の完全な条項照合・PR #193/#194時系列、contract-audit tests、Harness.Tests、CIのPR/develop・PR/他base・pushの3分岐、smoke、判定C/C′。実Unity・実Storage操作は対象外。
- 差し戻し修正（c9bb52e5 から、発見 C の F2 と公開レビュー 6902992449 を 1 commit で修正。既存 AC4・AC5 と責務マップの範囲の B 適応）:
  - F2 採用: `osm-unity-editor/SKILL.md` の「Editor 操作でやってはいけない」の項を、旧 AGENTS の文（本番の Scene / Prefab / Addressables 等の変更は依頼範囲に限り、広範な再生成・上書きは個別に影響を確認する）の 2 つの制限を両方持つ文に置き換えた。必読経路は既存の AGENTS の入口と `phase-b.md` のまま。
  - 指摘 1 採用（構成は C の補足に従い別 workflow）: `offline-checks.yml` から `edited` と base 検査の step を外して base の既定 trigger に戻し、base 検査だけを `.github/workflows/pr-base.yml`（ubuntu、checkout なし、`permissions: {}`、`opened`/`synchronize`/`reopened`/`edited`）へ移した。同じ workflow 内で job を `if` で skip する案は、本文編集の run の skipped が重い job の check 名を上書きするので採らない。push は `offline-checks.yml` の従来どおりで base 検査の対象外。責務マップの CI の行（PR base 属性の検査）と同じ責務を CI の file 2 つに分けただけで、新しい依存・所有者・runtime 依存は無いと判断した。base 変更時に contract-audit も再実行する提案は採らない: base 版の挙動（base 変更で再実行しない）と同じで AC5 の要求外。次の push で更新される旨を workflow に注記した。
  - 指摘 2 不採用: L31 を削ると Unity テスト・Build を計画・実行する担当から Skill への必読経路が AGENTS から消える。レビュー自身も規則の欠落は無いとし、C も現状維持を支持した。
  - 指摘 4 採用: `phases-and-handoff.md` の「Phase A の commit と snapshot」第 3 段落を「A3 統合案を commit して…」の前で 2 段落に分けた。文言は変えていない。
  - 指摘 3 は C の検証範囲への提案で、B の変更対象ではない。
  - B 確認: `pwsh -NoProfile -File tools/docs-audit.ps1` errors 0 / warnings 2（既存の harvest 案内）、`pwsh -NoProfile -File tools/contract-audit.ps1` errors 0 / warnings 0（検査 8 は not-applicable）、`pwsh -NoProfile -File tools/contract-audit.tests.ps1` 29 passed、`pwsh -NoProfile -File tools/Harness/tests/Harness.Tests.ps1 -ResultPath <result>` 38 executed / 0 failed、`git diff --check`（新 file を含む）合格。`pr-base.yml` の run 本文を抜き出して bash で実行し、`develop` で exit 0、`main`・`Develop`・空で exit 1。YAML parser と actionlint はこの環境に無く、workflow の構文と GitHub 上の trigger（本文編集で Offline checks が起動しないこと、base 変更で PR base が起動すること）は未確認で、push 後の実 run で確かめる。§3 の責務マップ本文は凍結内容なので書き換えていない。
- C′ 差し戻し 1 巡目の修正（63dd75df から、§8 の C′-F1 を 1 commit で修正。既存 AC4 と責務マップの範囲の B 適応）:
  - C′-F1 採用: 旧 AGENTS の「H1 適用の Artifacts/Harness 作業は判定 C で広い suite を確認する」へ、判定 C の必読経路から届くようにした。`phase-c.md` の判定 C の項に、H1 適用作業では Harness README の H1 の節を読んでから広い suite（`judgment` の run）を確認する 1 文とリンクを足した。リンク先を作るため、`tools/Harness/README.md` で「Unity 内観測（EditMode）」の下に置かれていた H1 の段落群（H1 の対象から `-HarnessTask` の契約検査まで）の前に見出し「H1 の現行入力と固定 suite」を入れた。README の本文は変えていない。Skill のテスト節と `phase-b.md` は B の範囲の記述なので変えていない。
  - B 確認: `pwsh -NoProfile -File tools/docs-audit.ps1` errors 0 / warnings 2（既存の harvest 案内）、`pwsh -NoProfile -File tools/contract-audit.ps1` errors 0 / warnings 0（検査 8 は not-applicable）、`git diff --check` 合格。docs-audit は file の存在だけを見て anchor を検査しないので、新しい anchor は見出しの文字列と GitHub の slug 規則（既存の stall 節の anchor と同じ形）で目視照合した。§8 の証拠の不備（CI log の上書き、offline 記録の `dirty` null）は次の判定 evidence の作り方の指摘で、B の変更対象ではない。
- implementation head commit: この票を含む commit
- Phase B 担当・モデル・ベンダー: 実装担当、gpt-6.1-terra、OpenAI（初回と §10）。差し戻し修正の担当、Claude Opus 5.5、Anthropic（c9bb52e5 からの 1 commit と、63dd75df からの C′ 差し戻し 1 巡目の 1 commit）

## 7. Phase C

- 種別: 発見 C（head 9c203c39）。差し戻し。GO 判定なし、C′ 未起動。旧 head の原記録・所見は既存 Storage と Git 履歴で保持し、この head の判定へ流用しない。
- 担当・モデル: 新規 session、Claude Fable 5.2（Anthropic）。B の gpt-6.1-terra（OpenAI）と別モデル。owner の引継ぎ指示で開始した。前 session が起こした C 実行担当は報告を残さず終了しており、その生結果は入力にしていない。
- 固定入力: base abfb29af / head 9c203c39 の commit 固定 diff（HANDOFF 以外 13 file）。HEAD・clean・origin の一致と、PR 状態ブロックの 3 つの SHA を開始時に照合した。
- 構造適合: 13 file は §3 の責務マップ内。AGENTS.md の変更は条件付き 3 項目の 3 行だけで、code 契約の行は base と一致。A2 の固定 base と copy、Cloud・別 host の例外、A3 の Draft PR と凍結記録、`未固定` の停止句、C′ の blind session は AC1〜AC3 と対応する。
- 現在の問いを阻害する findings:
  - F2（AC4「対応が欠ける文は動かさない」）: 旧 AGENTS の「本番の Scene / Prefab / Addressables 等の変更は依頼された範囲に限り、広範な再生成・上書きは個別に影響を確認する」に対応する文が移動先で欠けている。`osm-unity-editor/SKILL.md` に残るのは「依頼範囲外の…を広く再生成・上書きする」の禁止だけで、範囲外の小さな変更の制限と、広範な再生成・上書きの個別の影響確認が無い。9c203c39 で転記段落を削った時に生じた。Unity の他の 11 文、stall の 5 文、Storage の 6 文は移動先と必読経路（AGENTS の 3 行、`phase-b.md`、`phase-c.md`、Harness README、Artifacts README、`review-evidence.md`）に到達する。
- 公開レビュー 6902992449（同じ head 宛て、前 session の後に着信）: C の阻害指摘ではなく、公開レビュー 1 巡として B が扱う。
  - 指摘 1（`edited` で PR 本文・タイトルの編集のたびに Offline checks 全体が再実行される）は事実。9c203c39 の pull_request run は 2 回あり、どちらも `Require develop PR base` を含む全 job が走った。§10 で入れた `edited` の副作用である。C からの補足: develop の ruleset に必須 status check は無い。job を `if` で skip する形にすると、本文編集の run の skipped が同じ SHA の先行 run の結果より後に並ぶ。重い job の check を上書きしない構成（base 検査を別 workflow にする等）も B が比較する。
  - 指摘 2（AGENTS.md L31 の発火条件）は規則の欠落ではない。L31 を削ると、Unity テスト・Build を計画・実行する担当から Skill への必読経路が AGENTS から消えるので、C は現状維持を支持する。
  - 指摘 4（`phases-and-handoff.md` L41 の段落分け）は内容を変えない整形。採否は B。
- 後続スライスへ移送する findings: なし。
- 実行（発見用。判定必須としては未実行扱いで、修正後の head でやり直す）: `pwsh -NoProfile -File tools/docs-audit.ps1` errors 0 / warnings 2（既存の harvest 案内）、`pwsh -NoProfile -File tools/contract-audit.ps1` errors 0 / warnings 0（検査 8 は not-applicable）、`pwsh -NoProfile -File tools/contract-audit.tests.ps1` 29 passed、`pwsh -NoProfile -File tools/Harness/tests/Harness.Tests.ps1 -ResultPath <result>` 38 executed / 0 failed、`git diff --check` 合格。全て exit 0、実行前後で HEAD と clean を確認。workflow から抽出した base 検査の本文は `develop` で exit 0、`main`・`Develop`・空で exit 1。
- 判定必須のうち未実行: 判定 C の全部。#193 / #194 の公開時系列の机上確認、新版側の smoke（9c203c39 で移動先の本文と AGENTS の入口の文言が変わったので、旧 head の新版 3 回はこの head の観測ではない。旧版側は base のままで入力も同じ）。
- 未確認事項: push event での skip は workflow の `if` の読取りだけで、実 run は merge 前には無い。

### 発見 C 2 と判定 C（head 0ad27752）

- 種別・判定: 発見 C 2 は差し戻しなし。判定 C は GO。AC1〜AC6 と I1・I2 に対する現在の問いを阻害する欠陥は無い。C′ は未実施で、merge の許可ではない。
- 担当・モデル: 上と同じ C の session、Claude Fable 5.2（Anthropic）。B の gpt-6.1-terra（OpenAI）、差し戻し修正の Claude Opus 5.5（Anthropic）と別モデル。修正担当は C が起こした別 session で、C と同じベンダーである。
- 使用した evidence bundle: §0 の取得案内の 1 package（ローカル固定済み、取得確認済み）。
- 引継ぎの照合: 渡された SHA と HEAD と PR 状態ブロックの一致、clean、`HEAD^..HEAD` に `docs/handoff/` 以外の file があることを確認した。
- 構造適合: base からの変更は HANDOFF 以外 14 file。13 file は §3 の責務マップ内。`.github/workflows/pr-base.yml` は §3 に無い新規 file だが、§3 の「PR base 属性の検査」と同じ責務を CI の file 2 つに分けたもので、依存・所有者・runtime への影響を増やさない。B 適応として受け入れた。AGENTS.md は base から 3 行だけの変更で、UTF-8/LF 5,823 bytes のまま。
- 修正の確認: F2 は解消。`osm-unity-editor/SKILL.md` の「Editor 操作でやってはいけない」に、範囲外の変更の禁止と、範囲内の広範な再生成・上書きの個別の影響確認の両方がある。これで旧 AGENTS の 23 文（Unity 12、stall 5、Storage 6）が全て移動先と必読経路に到達する。段落分けは文言を変えていない。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし。観察として残すこと: PR の base を変えても Offline checks（contract-audit 検査 1 の基点を含む）は再実行されず、次の push で更新される。これは base の版と同じ挙動で、AC5 の要求外。
- 実行したコマンド（head 0ad27752、実行前後で HEAD と clean を確認、全て exit 0）: `pwsh -NoProfile -File tools/docs-audit.ps1` errors 0 / warnings 2（既存の harvest 案内）、`pwsh -NoProfile -File tools/contract-audit.ps1` errors 0 / warnings 0（784 file、検査 8 は not-applicable）、`pwsh -NoProfile -File tools/contract-audit.tests.ps1` 29 passed、`pwsh -NoProfile -File tools/Harness/tests/Harness.Tests.ps1 -ResultPath <result>` registered / selected / executed が各 38、failed 0、`git diff --check` 合格。Unity 回帰は §5 の適用除外。
- CI の 3 分岐: `pr-base.yml` の run 本文を head の blob から抜き出して実行し、`develop` で exit 0、`main`・`Develop`・空で exit 1。実 run は、push で Offline checks と PR base が各 1 回、PR 本文の編集で PR base だけが 1 回で、全て success。push event は `pr-base.yml` の trigger に無く、`offline-checks.yml` の push 検査は base と同じ定義のまま。base を develop 以外にした PR の実 run と、統合先への push の実 run は無い。
- 公開時系列の机上確認: #193 は A2 入力 commit → A3 統合案の Draft PR → 公開後の修正 3 巡（各 1 commit）→ 凍結 → B、#194 は統合案の Draft PR → 公開後の修正 2 巡 → revision 2 の凍結 → B → 公開後の修正 2 巡、と新しい規則で一意に区別できた。#193 の A2 入力 commit は、新しい規則では Cloud・別 host の例外に当たる時だけ切る commit になる。
- 限定 smoke: 新版側 3 場面（stall、Storage、B/C の Unity 権限）を head 0ad27752 の規則で各 1 回、別 session で実行した。旧版側は base の規則と入力が変わっていないので、旧 head の時に取った base 側 3 回を対にした。この head に対する試行は旧 3 + 新 3 の計 6。依頼文と入力は旧版側と byte 一致で、規則の写しは head の Git blob と一致する。3 件とも必要な文書を完成し、不要な停止や重大な誤りは無かった。stall は強制終了を成功証拠にせず調査を再開しない、Storage は確認済みの取得を再利用して再承認を求めない、B はテスト未実行を C へ引き渡す、という判断だった。
- smoke の限界: 新版側の実行は cursor-agent の `grok-4.7-low` で、旧版側の gpt-6-astra / low とモデルも CLI も違う（旧版側と同じモデルを起動できる経路がこの session に無い）。§5 の「モデル・設定を揃える」を満たしていない。新版側に欠陥が出なかったので旧版側との原因の切り分けは不要だったが、新旧の比較としては使えない。B/C の Unity 権限の場面は `phase-b.md` だけで完結し、`osm-unity-editor/SKILL.md` は読まれなかった。親 repo の指示の非混入は、fixture が repo の外にあることと、祖先・user-level に AGENTS.md と cursor の rules が無いことの確認による。起動時の完全な trace は無い。CLI の制約で依頼文を file に置いて 1 行の起動文で読ませた。実 Unity・実 Storage の動作の証明ではない。
- 判定必須のうち未実行: なし。
- 未確認事項: 一般的な無劣化・遵守率・性能改善は実証していない。採用後の 2〜3 スライスの既存 B/C 記録での観察を維持する。実 token は smoke の session の usage だけを raw に保存した。

## 8. Phase C′

### C′ 1（head 0ad27752）

- 担当方式: AI。
- 種別と巡: 最終 C′（高コスト証拠の分類なし）、1 巡目、全体を初見で監査。
- 受け取った決定記録の版: なし。
- 使用した blind audit bundle: §0 の取得案内の 1 package（ローカル固定済み、取得確認済み）。別 process で fetch した写しを workspace にした。
- 確認範囲・方法: bundle 内の file だけを読み、network と shell は使っていない。完全 diff（14 file）の全文、凍結した Phase A、所見を除いた B の実装結果、head と base の規則文書、offline・CI・smoke・公開時系列の生記録。テストの再実行なし。
- 判定: 阻害する欠陥 1 件。B へ差し戻す。
- 現在の問いを阻害する findings:
  - C′-F1（AC4、I2）: 旧 AGENTS の「H1適用をA3で明示したArtifacts/Harness作業は…判定Cでは広いsuiteを確認する」の後半が、担当の必読経路から外れている。AGENTS の入口が指す `osm-unity-editor/SKILL.md` のテスト節と `phase-b.md` は B が offline を実行するところまでで、`phase-c.md` に「広い suite」は無い。文は `tools/Harness/README.md` の H1 の節の 1 行にだけあり、AGENTS・Skill・`phase-c.md` の必読リンクはその節を指さない。
- 後続スライスへ移送する findings: 責務マップの file 名（`offline-checks.yml`）と実装（`pr-base.yml` への分離）のずれは、AC5 が file 名を固定していないので阻害しない。base を変えても Offline checks が再実行されないのは AC5 の要求外。Storage の smoke で新旧の回答に差がある（新版は受け手の fetch 成功で引渡し確認済みとし、旧版は案内の配置と受領を未完了とした）が、モデルと CLI が違うので規則の移動に帰属できない。
- 残存リスク: smoke は場面あたり新旧各 1 回でモデルが揃っていない。親 repo の指示の非混入に起動時の完全な trace が無い。base が develop 以外の PR と統合先への push の実 run が無い。
- 監査できなかった範囲と、証拠の不備の指摘: Git 外の条項対応表は bundle に無く、監査側が本文から再構成した。CI の本文実行の log は `develop` と `Develop` が同じ file 名になり上書きされていた（exit code の記録と実 run の step 結果は残っている）。offline の実行記録の `dirty` が null で、実行後の clean をその記録からは確定できない。次の判定 evidence で直す。
- 独立性: B（gpt-6.1-terra、Claude Opus 5.5）、C（Claude Fable 5.2）と別モデル・別ベンダーの新規 session。PR の会話、可変 HANDOFF、C の所見・結論を読んでいない。
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: なし。
- 担当・モデル: cursor-agent の非対話 session、指定 `grok-4.7-high`（xAI）。
- C の受け止め: C は判定 C でこの文を Harness README の行への到達として扱ったが、必読経路ではないという指摘は AC4 の文言どおりである。裁定で退けず、B の修正へ戻す。C′ の差し戻し 1 巡目。

## 9. Phase D

- merge、harvest、HANDOFF削除、task全体終了、worktree削除は未実施。taskはactive、現在のworktreeと原記録を保持する。

## 10. 公開レビュー修正（2026-11-16）

- 入力: PR #194のコメント6893305170・6896983978・6902725811。ownerの「レビュー来てるから確認してみて」に基づき修正。旧判定対象はe41c56a9、前回配布commitは3becb7c0。既存AC1〜AC5と責務マップに対するB適応であり、目的・受け入れ境界や検証予算を増やさない。
- 採用（合流前4点）: `未固定`時のB以外の停止句を復元。C′後の実装pushと早期pushの条件、Cloud・別host A2の例外を同じ規則に整合。Unity/Storageの全文転記を削り、既存節にLibrary/Lockfileと保護・対象外非削除の不足分だけを残す。AGENTSの必読に着手・実行・調査の発火条件を明記。
- 採用（補足6点）: templateのA3凍結記録欄とPRの凍結状態、外部指摘で目的を変えた際の目的レビューと他の変更の再レビュー要否の人間裁定、Aの公開時のpublish規則必読、HANDOFF copyをcheckout外へ配置、Cloud以外の別host A2、CIのPR editedイベントを明記。
- 後続候補: 自己SHA規則/handoff lint、レビュー巡数上限、low/trivial経路、旧H1/H2c等の分岐整理、A0予防策/Workflow suiteのCI追加/過去PR再生基盤。この5群は今回の凍結ACの欠陥修正に必要でなく、新設計・予算を要するため着手しない。別PRや新programも自動作成しない。
- B確認: docs audit errors0/warnings2、contract audit errors0/warnings0（784 C# files、検査8対象外）、変更2Skillのquick_validate合格。diff check合格。新headのCで全23条項の到達とcode契約不変、CI trigger/baseを確認する。AGENTSはUTF-8/LF 5823 bytes、HANDOFFを除く全変更file合計はbase比496 bytes減。新headのC/C′は未実施。旧6smokeは旧headの観測として保持し、新headで実行したとは記載しない。Unity/product codeは変更しない。
- 中断時: 現worktreeの差分を保持し、上記の残検証とC/C′から続ける。既存raw/取得案内はGit外で保全し、秘密・内部reference/hash/pathをPRへ転記しない。merge・終了・清掃は引き続き未実施。
