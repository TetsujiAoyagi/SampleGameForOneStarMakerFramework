---
name: osm-workflow
description: >-
  Use for planning, HANDOFF creation, implementation, code review, independent
  audit, Unity test execution, documentation changes, branches, or pull requests
  in the OneStarMaker / SampleGame repository.
---

# OSM 開発ワークフロー

`AGENTS.md` の常時契約を前提に、作業の Phase と責任を分離する。使用するモデルやサービスは固定しない。

## 作業開始時

1. 現在のブランチ、差分、対象 HANDOFF を確認する。
2. 自分が担当する Phase と成果物を特定する。
3. 専用ブランチで作業する。`main` / `develop` に直接コミットしない。A3で `external-current-v1` を採用した作業は `pwsh tools/harness.ps1 current -Task <id>` を唯一の作業入口とし、過去RESULTを探して補完しない。未移行作業は従来のHANDOFFを使う。
4. PR の base は `develop` とする。
5. 前の Phase から引き継ぐときは、HANDOFF §0 の引継ぎ票の commit SHA と自分の worktree の HEAD を照合し、作業ツリーが dirty なら引き継がない。PR が存在する間は状態ブロックも照合する。同一 PC ではローカルの SHA で照合し、origin との照合は Cloud・別 host の担当が割り当てられている時だけ行う。食い違えば引き継がずに停止する。C' は PR を読まない。詳細は [Phase 境界の引継ぎ](references/phases-and-handoff.md#phase-境界の引継ぎ)。

Phase A、C、C'、HANDOFF の作成・更新では [Phase と HANDOFF](references/phases-and-handoff.md) を全文読む。ドキュメントを変更する場合は [ドキュメント方針](references/docs-policy.md) も全文読む。

## 公開点とGit履歴

- **push は配布であり保存ではない。** リモートにしか居ない読者が居る時だけ push する。同一 PC の担当同士の引継ぎは、ローカルの commit SHA と、必要なら `evidence stage` の取得案内で行う。
- **公開点**は (a) PR 作成、(b) レビュー依頼（`@cursoragent` のコメントを含む）と、レビュー 1 巡ごとの修正 1 commit、(c) マージ前の最終（harvest と HANDOFF 削除）とする。例外は、Cloud・別 host の担当が割り当てられた Phase 境界と、人間の明示指示。
- 順序は「B ↔ 発見 C → 未公開の試行錯誤 commit の整理 → GO 候補 implementation head の固定 → 判定 C → C' → PR 作成・push」とする。履歴の整理は判定 C の前に済ませる。整理で head が変わったら [判定後の head 変更](references/review-evidence.md#判定後の-head-変更) に従う。
- 判定 evidence を取った head は、push しないローカルの軽量 tag `evidence/<taskId>/<n>` で到達可能に保ち、対象 SHA を引継ぎ票に書く。tag は旧 head の到達可能性だけを担い、整理後の別 head に旧判定を移す根拠にならない。判定 evidence を取っていない中間 head は整理してよい。
- 証拠の保存先と配布は [レビュー証拠](references/review-evidence.md#保存先とリモートへの配布) に従う。証拠の配布方法と、試行錯誤の実装コミットをPR履歴へ積まないことは別の条件として扱う。差し戻し中の中間コミットは判定用headの固定には使えても、そのまま全て公開する理由にはならない。
- PR公開後の修正は、レビュー1ラウンドにつき原則1つの修正コミットにまとめる。独立してレビューすべき変更を分ける場合は、その理由をPRに示す。Git配布が認められない証拠を未pushの中間コミットへ誤って入れた場合は公開前に履歴から除く。公開済みの違反は履歴の書き換えでなかったことにしない。
- 整理の対象は B 以降の実装の試行錯誤コミットに限る。A3 で凍結した Phase A の HANDOFF revision のコミットはレビュー入力の記録として整理しない。
- push前に、前回公開headから増えるコミットを `git log --oneline <前回公開head>..HEAD` で確認する。数がレビュー上必要な単位を超えていれば、未公開分を整理してからpushする。あわせて `pwsh tools/docs-audit.ps1` を通し、push するコミットに機微な path（ローカルの store・transfers・資格情報の場所、reference・packageSha256 の実値）が入っていないことを確認する。公開済み履歴は明示的な合意なく書き換えない。既存履歴の大規模rebaseを対象外とする判断は、未公開の中間コミットを整理しない理由にならない。

## Phase A: 計画と設計レビュー

- A0 で現況、要求、制約、対象外、未決事項に加え、このスライスが答える問い、次スライスへ進む最低条件、後続スライスへ送る問いを同じ planning packet に固定する。
- A1 で主担当が進める最低条件、その詳細である受け入れ条件、責務配置、規模、テスト方針、停止規則を含む初稿を作る。新規 A1 のスライスは最低条件を [不変条件と成立条件](references/phases-and-handoff.md#不変条件と成立条件) に分ける。[高コスト証拠](references/review-evidence.md#高コスト証拠と最終証拠ラウンド) の分類・手順・期待値と、[判定 C の照合 script](references/review-evidence.md#判定-c-の照合-script) の方式も A1 で書き A3 で凍結する。
- 操作・実行時・目視を伴う最低条件は、凍結前に [検証経路の成立](references/phases-and-handoff.md#検証経路の成立) を確認する。AI が取得できない証拠を、暗黙の人間作業として凍結しない。
- A2 で変更規模に応じて複数モデルへ独立レビューを依頼する。各レビューは同じ[入力版](references/phases-and-handoff.md#phase-a-の内部フロー)を使い、互いの指摘を見せない。
- 少なくとも1件は [アーキテクチャゲート](references/architecture-gates.md) に従い、フォルダ境界、クラス責務、依存、所有者、寿命、テスト可能性を専門に見る。
- 高リスク変更では、初稿を見ずに A0 だけから代替構成を出すレビューを追加する。C' の独立性が必要なら、Phase A で利用可能な全モデル系列・ベンダーを使い切らず、未関与の監査担当を予約する。
- A3 で主担当と人間が指摘を採用・不採用・保留に分類し、理由を記録して HANDOFF と受け入れ境界を凍結する。凍結後に設計判断を変える場合は Phase A を新しい revision として再開する。
- A3 後の finding が現スライスを阻害できるのは、凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を [blocker の基準](references/phases-and-handoff.md#blocker-の基準) で示す場合だけとする。凍結条件の意味・合否を変えない人間の裁定は [決定記録](references/phases-and-handoff.md#決定記録) に残し、変える裁定は Phase A の新しい revision にする。それ以外の新しい不確実性は重要度にかかわらず後続スライスへ移送する。現スライスへ例外的に取り込む場合は、人間が置き換える既存条件または期限・検証予算を明示して承認し、Phase A を新しい revision として再開する。
- HANDOFF は [テンプレート](references/handoff-template.md) を基準にする。テスト方針には、差し戻し中の確認に使う起点 filter と、GO 候補 head の判定必須テストを分けて書く。実装変更を伴うスライスの判定必須には、最終の全 EditMode 回帰を標準として含める。適用除外は理由と代替証拠を本文へ書く。

## Phase B: 実装

- HANDOFF がある場合は本文を正とし、書かれていない設計文書を読み込んで独自に再設計しない。
- HANDOFF と他の記述が衝突したら実装を止め、判断を返す。
- 設計判断が新たに必要になった場合も実装を止め、Phase A に戻す。
- 新しい事実は先に [A 再開 / B 適応 / スパイク / 後続スライス](references/phases-and-handoff.md#新しい事実の分類) に分類する。失敗したこと自体を設計判断にせず、B 適応なら Phase A を再開しない。
- 計画外の状態、依存、所有者、寿命、公開 API が必要になった場合、または計画した配置では中核ロジックを単体テストできない場合も実装を止める。Phase B 内で便宜的な Helper / Manager へ押し込まない。
- コメントには、コードだけでは復元しにくい契約、判断理由、変更時の注意を残す。特に寿命・所有者・選択数・失敗境界や、一見省けそうな例外処理の理由を説明する。処理をそのまま言い換えるコメントや行数を満たすためのコメントは増やさない。読者に通じないスライス略号だけで説明せず、対象と理由を通常の言葉で書く。
- Unity Editor、Scene、Prefab、Addressables等を扱う場合は `../osm-unity-editor/SKILL.md` を先に読む。
- 対象を限定した Editor 操作とコンパイル確認は Phase B で行ってよい。Unity バッチテストと Addressables ビルドの実行・判定は Phase C の責任とし、H2未適用のPhase Bでは実行しない。H1適用済みのArtifacts/Harness作業は完了引渡し時に関連offline suiteのB exitを通す。中間commitやWIP救援にB exitを要求しない。B限定のUnityテストは、A3で実行経路と対象集合を凍結したtaskに限る。完了済みH2cの実例（承認profile、初回疎通の扱い、固定記録の入口）は [Harness README](../../../tools/Harness/README.md#h2c-unity-pilot) を正とする。完了時に未実行を明記する。
- 実装を終えたら `pwsh tools/contract-audit.ps1` を実行する。Editor のコンパイル確認を行えなかった場合は、その未確認を明記する。
- B の session は B の出口で commit し、引継ぎ票に SHA と取得案内の所在を書いて終える（PR が存在する間は状態ブロックも更新する）。push は [公開点](#公開点とgit履歴) の規則に従う。判定 C の実行や C' の起動を担わない。判定 C の照合 script も書かない。引継ぎ票の次の担当は、動作確認の対象があれば動作確認、無ければ発見 C とする。

## 動作確認（Phase B の後）

- Phase B の完了 head で、検証経路の操作・実行時・目視の条件（offline のテストで閉じないもの）を各 1 回軽く実行する。テスト suite、Unity バッチテスト、Build、高コスト証拠は含めない。規則は [動作確認](references/phases-and-handoff.md#動作確認phase-b-の後) を正とする。
- 条件ごとの実行担当が手順だけを実行し、生の観測と、検証経路の期待との差の有無を残す。GO / NO-GO と合否は書かない。差があれば、発見 C を待たずに B へ戻す。この往復は発見 C と同じ巡に数える。
- 対象外（テスト suite、Unity バッチテスト、Build、高コスト証拠）以外の条件は、所要時間を理由に省略しない。実施できなかった条件は `未実施`、全て実施して未実施が無ければ `なし`、検証経路に該当条件が無いスライスだけ `対象なし` と記録する。
- 出口の担当が、引継ぎ票（PR が存在する間は PR の状態ブロックも）を次の担当（発見 C、差し戻しなら B）へ更新する。

## Phase C: レビューとテスト

- [レビュー証拠](references/review-evidence.md) に従い、implementation base / head commit を固定した evidence bundle を作る。staged 状態や可変な作業ツリーだけをレビュー対象にしない。
- **固定の対象:** 発見 C の差し戻し入力に限り、完全な変更集合を収録した stage snapshot（reference と packageSha256）で固定できる。判定 C と C' は commit 固定を必須とし、C / C' 用 bundle は同一 commit から生成する。snapshot を使うレビューでは、送信元の可変な作業ツリーをレビュー入力にしない。
- HANDOFF の進める最低条件、その詳細である受け入れ条件、差分を照合し、機能レビューより先に構造レビューを行う。スライスの終了可否は進める最低条件で判定する。
- Phase A の責務マップと実際のメンバー、依存、配置、テスト境界を照合する。行数と増加率は分割命令ではなく、構造判断の説明を要求するトリガーとして扱う。
- 指摘を「凍結済み条件または常時契約への違反により現在の問いを阻害する欠陥」と「後続スライスの入力」に分け、根拠となる凍結済み条件または常時契約を記録する。レビュー担当は欠陥を指摘できるが、後者を理由に現スライスの実装や受け入れ条件を自動拡張しない。
- 変更量、独立した変更理由の混在、単体テスト可能性、Unity の偽 null を確認する。
- 重要な判断を半年後の担当者がコードと公開文書から復元できるか確認する。復元できない箇所は理由のコメントを求め、コメント量そのものは評価しない。
- `pwsh tools/contract-audit.ps1` を実行する。機械で判定できる契約はこれで済ませ、構造レビューは設計判断に集中する。
- 高コスト証拠を分類したスライスは、判定 C → 暫定 C' → 高コスト証拠の取得 → 最終証拠ラウンドの順で進め、判定 C と最終 C' の両方が揃うまで GO にしない。詳細は [高コスト証拠と最終証拠ラウンド](references/review-evidence.md#高コスト証拠と最終証拠ラウンド) を正とする。
- 判断担当が実行できない場合は、判定 C 用の新しい実行担当 session が凍結済みのコマンドだけを実行する（[実行担当と判断担当](references/phases-and-handoff.md#実行担当と判断担当)）。照合 script は実行できない担当に書かせない（[判定 C の照合 script](references/review-evidence.md#判定-c-の照合-script)）。
- Phase C は欠陥発見と最終判定を分ける。詳細は [レビュー証拠](references/review-evidence.md) の「発見 C と判定 C」を正とする。目的は検証範囲の縮小ではなく、重い検証のタイミングをずらすことである。
- 発見 C の欠陥も同じ分類で修正先を決める。B 適応で足りる違反は修正するが、Phase A 再開の理由にはしない。
- B と発見 C の差し戻しは、現在の問いを阻害する指摘が無くなるまで往復する。1 巡は、阻害する指摘を B へ戻し、修正後の head を再びレビューする1往復とし、最初のレビューは数えない。動作確認で B へ戻した往復も 1 巡に数える（最初の動作確認は数えない）。上限は B と発見 C の間で 4 巡、C' の指摘による差し戻しは別に数えて 3 巡とする。上限で収束しなければ、残る指摘と違反根拠を明示して人間に判断を返す。
- **発見 C:** 固定した実装差分の構造・契約・凍結済み失敗経路を先に見る。H1適用taskの正規提出はB exit済みのdiscovery入力を使う。未テストWIP相談はassistで区別する。未移行作業は従来どおりテスト未実行のevidenceでも発見レビューを開始できる。GO 判定はしない。C' も起動しない。明確な差し戻しが決まった段階では、最終判定用の検証一式を実行しない。
- 差し戻し中のテストは、修正箇所と影響する既存経路の確認に必要なものを `-Filter` 等で選ぶ。Phase A の起点 filter を使うが、凍結済み条件または常時契約の確認に必要なら根拠を記録して変更してよい。受け入れ条件の追加とは区別する。PlayMode 往復・Content Directory build・Player は通常は判定時にまとめる。当該欠陥の再現または修正確認に必要な場合は、理由と範囲を記録して限定実行できる。
- **判定 C:** 未解決の blocker がなくなった GO 候補 head で、HANDOFF の判定必須テストを実行する。実装変更を伴うスライスでは最終の全 EditMode 回帰（空 filter）を標準とする。適用除外は Phase A で理由と代替証拠を明示する。同じ platform / graphics 条件でまとめられるテストは 1 プロセスに集約する。プロセス分離自体が検証条件なら維持する。XML の実行テスト名と件数で、必要な集合が収録されたことを確認する。高コスト証拠を除く凍結済みの操作・実行時・目視の条件の証拠が GO 候補 head で揃うまでは、判定必須テストが通っていても判定 C を完了と記録せず C' を起動しない（[操作・目視証拠の受け渡し](references/review-evidence.md#操作目視証拠の受け渡し)）。
- Unity Editor が閉じていることを確認してから `pwsh tools/run-tests.ps1` を実行する。自分で起動した Editor は未保存の変更を確認して正常終了させる。既存の人間所有 Editor を無断で強制終了しない。namespace filter は全件性を保証しない。全件回帰は空 filter、限定検証は XML で対象集合を確認する。
- Windows の Phase C Unity バッチテストは、**最初の実行から sandbox 外の承認済み経路**で `pwsh tools/run-tests.ps1` を起動する。sandbox 内では Unity Licensing Client の named-pipe IPC (`LicenseClient-void`) が成立せず、Unity が再接続を無期限に繰り返した実測がある。実行環境が提供する通常の権限昇格（sandbox外実行）の承認を取得し、承認できない場合はテスト未実行として止める。`run-tests.ps1` 自体は昇格しない。license file の返却・削除・再発行を回避策にしない。
- 起動後にライセンス接続成功とテスト進行をログで確認する。`LicenseClient-void` 不在・`com.unity.editor.headless` 不在の再接続が続き、数分間進行せず XML もない場合は、無期限に待たず、今回起動した Unity PID だけを確認して終了し、生ログと未実行判定を残す。Editor が正常に進行中なら所要時間だけで中断しない。
- Phase C でも `unity test` / `unity run` は使わない。
- exit 0 は1件以上実行かつ failed 0。0件は失敗として扱う。
- `0xC0000005` でも結果 XML が完成していれば、ログ末尾と XML を基に判定する。
- 確認結果と未確認事項を HANDOFF に記録する。

## Phase C': 独立監査

- 人間または AI が担当する。AI は新規セッションで開始する。発見 C と判定 C の結論、指摘、疑念候補、誘導的な説明を含まない blind audit bundle を読み、可変な HANDOFF 全文をそのまま入力にしない。
- 判定 C の判定証拠が揃った head でのみ開始する。高コスト証拠を分類したスライスでは、それ以前の C' を暫定 C' として GO 判定に使わず、高コスト証拠を加えた最終 C' を行う。同じテストを C' が重複実行することは必須にしない。
- blind audit bundle の構成は [レビュー証拠](references/review-evidence.md#phase-c-の-blind-audit-bundle) を正とする。B result や原記録へ発見 C の所見を転載して迂回しない。
- 1 巡目は全体を初見で監査し、2 巡目以降は前巡指摘の解消と修正差分を中心に監査する。受け取る裁定は C' 自身の前巡指摘への決定記録だけとする（[巡ごとの範囲](references/review-evidence.md#巡ごとの範囲)）。
- AI 担当のモデル相違条件、人間担当の確認記録と独立性の扱いは [Phase と HANDOFF](references/phases-and-handoff.md) に従う。人間の回答前に AI が PASS や完了を記録しない。
- 受け入れ条件だけでなく、契約違反、構造劣化、未検証の失敗経路、Phase C 自体の見落としを探す。
- 指摘を「凍結済み条件または常時契約への違反により現在の問いを阻害する欠陥」と「後続スライスの入力」に分け、[blocker の基準](references/phases-and-handoff.md#blocker-の基準) に従って根拠を記録する。後者を理由に現スライスの実装や受け入れ条件を自動拡張しない。
- 指摘、残存リスク、監査できなかった範囲、使用したモデルを HANDOFF の Phase C' 欄へ記録する。

## Phase D: 保存・引渡しと後始末

- [保存・引渡し確認](references/review-evidence.md#phase-d-の保存引渡し確認) に従い、必要なEvidenceの保存と次の担当への取得案内を確認してから、harvest・HANDOFF削除・通常のtask終了・依頼範囲の後始末へ進む。tracked HANDOFF / H1 CURRENTのどちらでもこの確認を省略しない。
- 保存済みの同一bundleは確認結果を再利用する。元証拠をworktreeに保全しただけ、またはtaskがcompletedで配送待ち0というだけでは、保存・引渡しや後始末の完了としない。未完了事項は実装・マージの状態と分けて報告する。

## task の開始・再開・終了

- 通常 task の開始は pwsh tools/workflow-task.ps1 start -Task <id>、活動状態と配送待ちの確認は status を使う。別 worktree でも同じ taskId を使い、終了済み task の保存・利用前には resume -ExpectedVersion <statusの版> -Decision <信頼済み再開record-id> を完了する。
- 人間の通常の task 全体完了／打切り指示を処理する最終手順で、完了 receipt と同じ Decision から end -Reason completed|cancelled -ExpectedVersion <statusの版> -Decision <信頼済み指示record-id> を呼ぶ。Phase D の完了処理もこれを使い、Evidence 専用承認を作らない。
- Agent 停止、pause、timeout、失敗、C/C′ 合格、PR 作成、merge 検知、Harness close を終了に推測変換しない。status の pending-finalization は同じ Decision／内容で再実行して回復する。recorded/delivery-pending は event 記録済みであり、決定 ID を変えて再終了しない。時刻／version を再試行時刻へ置き換えない。
- task の終了意味・store・配送の現況は [Workflow README](../../../tools/Workflow/README.md) に従う。
## 完了

- マージ済みの HANDOFF は、上記の保存・引渡し確認後に、恒久的に残すべき知見だけを公開ドキュメントへ反映して削除する。
- PRを作る場合は差分と検証結果を要約し、base が `develop` であることを確認する。タイトルには Phase を入れず、本文先頭の状態ブロックを各 Phase の出口と implementation head の固定時に更新する（[PR 状態ブロック](references/phases-and-handoff.md#pr-状態ブロック)）。マージ前に状態ブロックと本文の要約が最終 head と一致することを確認する。
