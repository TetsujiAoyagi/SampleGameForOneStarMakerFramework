# 差し戻し運用の改善 — Astra 向け入力

- type: research
- status: 人間が方針を承認済み。Astra が Skill へ harvest する実装入力。未凍結の作業台。
- branch: `cursor/workflow-sendback-guidance-df27`
- risk: normal（契約の適用範囲を明示する。Phase 骨格は変えない）
- owner: ワークフロー改善の実装担当（Astra）
- created: 2026-09-19
- expires: Skill へ harvest した Phase D。2026-10-19 までに継続要否を再確認する。
- harvest to: `.agents/skills/osm-workflow/SKILL.md`、`references/phases-and-handoff.md`、`references/architecture-gates.md`、`references/review-evidence.md`
- 根拠スライス: PR #66（BS4 Player integration、`develop` へマージ済み、`b23ccc4`）
- 関連するが別件: `REVIEW_WORKFLOW_EFFICIENCY.md` は呼び出し経路と token の話。本書は **A 再開の適用範囲** の話。混ぜない。

会話ログは正本にしない。Astra は本書と現行 Skill だけを入力にする。

## 0. Astra への依頼

現行の Phase 骨格（A0〜A3 / B / 発見 C / 判定 C / C' / D）は維持する。捨てない。厚くもしない。

やることは一つ。**「Player や実装が一度失敗したら設計をやり直す」をデフォルトにしないための分類を、Skill 本文へ書く。** 書いたらこの HANDOFF は harvest して削除する。恒久の哲学文書を増やさない。

対象外:

- BS4 実装、PR #66 の再開、公開 Architecture の再記述
- 新しいレビュー哲学、新しい Phase、モデル固定、検査スクリプトの追加
- `REVIEW_WORKFLOW_EFFICIENCY.md` の残課題（`stream-json` 実測）への合流
- DIST / RET のスライス分割そのもの（方針だけ Skill に残し、切るのは各 Phase A）

## 1. このスライスが答える問い

高リスクの初回実機スライスで差し戻しが膨らむのは、ゲート不足か、すでに書いた停止規則の適用が広すぎるか。後者なら、A 再開 / B 適応 / スパイク / 後続スライスの判別を Skill のどこへ、どの文で固定すれば、次の DIST / RET で同じ 9 回を繰り返さないか。

## 2. 診断（2026-09-19、PR #66 と先行スライス）

GitHub 上の PR #66 は差し戻されていない。C / C' は GO、レビューも「merge 差し戻し不要」。多かったのは、PR 前に同じブランチで Phase A を revision 1〜9 までやり直したこと。

同じ型は BS3（PR #63、Phase C が r11、コミット 37）でも起きている。BS2b は 8 コミットで終わっている。差は「初めて実機に当たる高リスク」か「純関数に近い」か。

| スライス | PR | コミット | 再開の規模 |
|---|---|---|---|
| BS2b Content Directory | 60 | 8 | 小さい |
| BS2a materialization | 57 | 16 | 中 |
| BS1 selection | 55 | 21 | 中（レビュー埋めが主） |
| BS3 Runtime | 63 | 37 | 大きい（C が r11） |
| BS4 Player | 66 | 33 | 大きい（A が r9） |

PR #54 はスコープ停止、PR #65 は発見 C と判定 C の分離を入れた。規則は既にある。BS4 はその翌日の適用第一号で、発見 C は backup root 混入を正しく拾った。残った癖は、**拾ったものをまた Phase A 全セット（独立レビュー 2 件 + 凍結）に載せること**。

結論: **ワークフローの骨格は妥当。過剰なのは儀式の適用範囲。** 領域（IL2CPP / High / Content Directories の初回 Player）への慎重さ自体は正しい。rev 2 / 3 / 4 / 8 と rev 9 の backup 混入は、このゲートが防ぐべき種類。

### BS4 revision の判定

A3 凍結後にスライスを止められるのは、凍結済み最低条件・受け入れ条件・常時契約への違反だけ。Phase B は新しい所有者・寿命・公開 API が必要なら A に戻す。最低条件が「実 Player で bootstrap → 登録 → Title → fixture → 終了」だと、落ちるたびに阻害になり、直しに設計が少しでも要ると A 再開になる。

| Rev | きっかけ | 種類 | A 再開は妥当か |
|---|---|---|---|
| 2 | SampleGame から directory close できない | 所有者 / API | **妥当。** A1 が BS3 の internal close を見落とした |
| 3 | snapshot / plan の ctor が internal | 所有者 / friend | **妥当。** fixture 合成の可視性を A が未確認 |
| 4 | `UIScene` の runtime 名が `UICommon` ではない | Unity の事実 | **経験的には妥当。** 初稿の「本番 UIScene を直接 Scene 0」が外れた |
| 5 | Title の UXML が null → define 不一致と仮説 | 誤った仮説を凍結 | **過剰。** ログ 1 本で棄却できた。rev 6 で仮説自体が死んだ |
| 6 | 本体は UI Toolkit の `UxmlSerializedData` 欠落 | 既に A3 が許可済み | **過剰。** A3 は「実 Player で欠けた型だけ `link.xml`」と書いてある |
| 7 | 同じ entry への load + `InstantiateContentAsync` が二回目 await を拒否 | 既知 Runtime 制約 | **ほぼ過剰。** A1 は「可能なら instantiate」。`Object.Instantiate` は煙の適応 |
| 8 | `PanelRenderer` 初期化前に Title を足した | 新しい ready 契約 | **境界。** 実バグ。Framework に wait を足すなら A は正しい |
| 9 | shipping に Unity backup root、receipt が最終状態だけ、A1 の単体証拠不足 | 発見 C | **半分妥当。** backup 混入は凍結条件違反。10 段階 schema v2 は証拠契約の拡張 |

A2 は初回から「全て採用」。Terra / Sol / Astra の指摘を落とさず、分割と後の 10 段階 receipt まで一度に載せた。各 revision でも独立レビュー 2 件を回した。回数の主因はクラス分割ではなく、**「失敗 → 仮説を設計として凍結 → また落とす」**。

## 3. 進める最低条件

1. Skill 本文に、実装中・発見 C の finding を次の 4 つへ分類する規則がある。
   - **A 再開:** 新しい所有者、公開 API、asmdef、寿命、fail-closed の意味が変わる。凍結済み最低条件を、計画した配置のまま満たせない。
   - **B 適応:** 凍結済み A3 が既に許可している実装（欠けた型の `link.xml`、「可能なら」の範囲の煙適応、既存の名前契約を満たす Scene 複写など）。所有者・公開面・寿命・fail-closed を変えない。
   - **スパイク:** 実機失敗の原因が未確認。仮説を A3 に書かない。安い観測（Player 1 本、ログ、既存 filter）で因果を取ってから、上の 3 つのどれかへ分類する。
   - **後続スライス:** 今の成功経路を否定しない。 「証拠が完全になる」「将来必要」は理由にしない。
2. A2 は全件採用を既定にしない。所有者・API の blocker は取る。証拠スキーマの厚化は、今の問いが死ぬときだけ取る。
3. 初回実機接触で独立した未知が複数あるなら、A0 / A1 で問いを割る。分割自体を後続の実装条件にはしない。
4. 発見 C の finding が B 適応なら A を再開しない。判定 C / C' を起動しない規則は PR #65 のまま。
5. 完了して harvest 済みのスライス（BS4 を含む）を、後続入力のために再開しない。

受け入れ条件は上記の観測可能な詳細であり、別の完了バーを作らない。GO は Skill 3 ファイル以上に分類規則が本文で読め、BS4 実装差分が 0、新しい公開哲学文書が 0 のとき。Unity テストは対象外。`pwsh tools/docs-audit.ps1` と `git diff --check` を判定必須とする。

## 4. ここでは答えない問い

- DIST の配信・cache・別 process 排他、RET の旧経路廃止。切るなら各 Phase A。
- PR #66 レビューが後続へ送った 4 件（`runtimeMode=addressables` の fail-closed、receipt の backend / stripping がリテラル、Title 選択存在の preflight、Quit 前の 10 段階完全一致）。BS4 は成立済み。必要なら DIST 以降の Phase A が拾う。
- レビュー CLI の mode / 出力形式 / token。`REVIEW_WORKFLOW_EFFICIENCY.md` が所有する。
- 人間確認を Skill から消すこと。今回の委任は「この改善案を Skill へ落とす」だけ。

停止規則: 最低条件を満たし、Phase 骨格を壊していなければ終了する。例示を増やすために検査や Phase を足さない。

## 5. Skill へ入れる文面案

Astra は意味を保ったまま短くしてよい。新しい用語を増やさない。既存の「凍結済み条件または常時契約への違反」と「後続スライスの入力」の二分類を捨てず、**B 適応** と **スパイク** をその前段の仕分けとして足す。

### 5.1 `phases-and-handoff.md` — 意思決定境界の直後

次を、A3 凍結後の blocker 規則の直後へ入れる。

```text
実装中または発見 C で新しい事実が出たとき、先に次のどれかを選ぶ。選ぶ前に Phase A を再開しない。

- A 再開: 新しい所有者、公開 API、asmdef、寿命、fail-closed の意味が必要。または凍結済み最低条件を、計画した配置のまま満たせない。再開後は新しい revision として A2 / A3 を行う。
- B 適応: 凍結済み A3 が既に許可している実装詳細。所有者・公開面・寿命・fail-closed を変えない。例は、実 Player で欠けた型だけの link.xml、「可能なら」と書いた煙の API 適応、既存の名前契約を満たす一時 Scene 複写。HANDOFF に一行残し、設計判断としては扱わない。
- スパイク: 失敗の原因が未確認。仮説を A3 に書かない。Player 1 本、ログ、既存の起点 filter など安い観測で因果を取ってから、A 再開 / B 適応 / 後続のどれかへ分類する。
- 後続スライス: 今の成功経路を否定しない新しい不確実性。重要度にかかわらず送る。

「失敗した」「知らなかった」「証拠が薄い」だけでは A 再開しない。A2 は全件採用を既定にしない。所有者・API・寿命の blocker は取る。証拠スキーマや診断項目の厚化は、今の問いが満たせないときだけ取る。
```

初回実機接触については、A0 の「このスライスが答える問い」の近くへ次を足す。

```text
高リスクの初回実機スライスで、独立して失敗しうる未知が複数あるなら、A0 / A1 で問いを割る。一つの最低条件に、起動・stripping・fixture 合成・配布形診断を同時に載せない。分割は後続スライス名を本文に書くことで足り、この場で全スライスを実装しない。
```

### 5.2 `architecture-gates.md` — Phase B 停止条件

既存の「HANDOFF にない状態、依存、所有者、寿命、公開 API が必要」のあとに、停止しない側を書く。

```text
次では Phase A に返さない。凍結済み文面の実装として進め、HANDOFF の実装結果へ一行残す。

- 凍結済み A3 が明示許可した保持・検査・複写
- 「可能なら」と書いた煙・ハーネスの API 選択
- 既存の名前・path・GUID 契約を満たすための一時 asset 操作で、所有者と公開面が変わらないもの

原因未確認の実機失敗は、仮説を設計として返さず、先に観測する。観測後に所有者・公開面・寿命が本当に要るときだけ Phase A へ返す。
```

### 5.3 `review-evidence.md` — 発見 C

発見 C の finding 分類の直後へ入れる。

```text
発見 C が凍結済み条件の違反を見ても、直し方が凍結済み A3 の許可範囲に収まるなら B 適応であり、Phase A 再開でも判定 C / C' 起動でもない。直し方が新しい所有者・公開面・寿命・fail-closed を要する場合だけ A 再開する。成功経路を否定しない診断項目は後続スライスへ送り、現スライスの受け入れ条件に足さない。
```

### 5.4 `SKILL.md` — Phase B と Phase C の短い参照

Phase B の「設計判断が新たに必要になった場合も実装を止め、Phase A に戻す」の直後に、一文足す。

```text
設計判断か B 適応か未確認の失敗かは、phases-and-handoff の A 再開 / B 適応 / スパイク / 後続の分類で決める。失敗したこと自体を設計判断にしない。
```

Phase C の「指摘を欠陥と後続入力に分ける」の近くに、一文足す。

```text
B 適応で足りる違反は欠陥として直すが、A 再開理由にしない。
```

## 6. 実装計画

1. 現行 Skill 4 ファイルを読み、既存文と重複する箇所は置換または直後追記にする。同じ規則を 3 箇所以上に全文コピーしない。
2. `phases-and-handoff.md` を正本にする。他ファイルは参照または 1〜2 文。
3. `docs/README.md` の作業台表は、この HANDOFF が残っている間だけ行を維持し、harvest 時に消す。
4. Unity C#、Scene、BS4 コード、公開 Architecture は触らない。
5. `pwsh tools/docs-audit.ps1` と `git diff --check`。Editor 起動なし。

Phase B から A へ返す条件: 新しい Phase を足したくなる、BS4 を再開したくなる、検査スクリプトを足したくなる。その場合は実装を止め、この HANDOFF の対象外を確認する。

## 採否の既定（人間承認済み）

| 提案 | 採否 | 理由 |
|---|---|---|
| A 再開 / B 適応 / スパイク / 後続の 4 分類を Skill へ書く | 採用 | 回数の本体。既存の二分類を前段で仕分ける |
| A2 全件採用を既定にしない | 採用 | 証拠スキーマまで一度に載せると revision が増える |
| 初回実機の問いを A0 で割る | 採用 | 分割の実装は各 Phase A。Skill は原則だけ |
| 発見 C の B 適応は A を再開しない | 採用 | PR #65 のタイミング分離を壊さない |
| 完了スライスを再開しない | 採用 | BS4 は `develop` 済み |
| Phase 骨格の改廃、新哲学文書の恒久化 | 不採用 | 足りないのは到達経路ではなく適用範囲 |
| `--mode ask`、子 agent 禁止、60〜80% 削減 | 不採用 | 別調査の不採用項目。再導入しない |

A3 の例外承認はなし。人間判断を要する範囲変更はない。

## 使ってはいけない implicit な読み替え

- PR #66 の後続 4 件を、この改善の受け入れ条件にする。
- rev 2 / 3 / 4 / 8 まで B 適応だったことにしてゲートを緩める。
- 差し戻し回数の上限や、A 再開禁止を書く。
- この文書を公開 Architecture や `AGENTS.md` の常時契約へ移す。常時契約は増やさない。Skill の手順だけを直す。
