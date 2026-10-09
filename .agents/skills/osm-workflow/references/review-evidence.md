# レビュー証拠と指摘記録

## 対象を固定する

Phase C と C' は同じ実装を評価する。レビュー開始時に次を固定する。

- implementation base commit SHA
- implementation head commit SHA
- implementation base から head までの完全 diff、stat、name-status
- 実行した機械検査、そのコマンド、結果。未実行ならその旨
- 実行した場合は、その head に対するテストの生ログと XML、コマンド、filter、対象集合
- evidence bundle の生成時刻と識別子または hash

テスト未実行でも、固定した diff と機械検査だけで欠陥発見レビューを開始してよい。その bundle では GO 判定も C' も開始しない。最終判定と C' に使う bundle には、判定必須テストの生ログと XML を含める。

implementation head はレビュー対象の実装差分を固定する値であり、HANDOFF に C/C' の結果だけを追記した review-record commit とは分ける。staged diff や可変な作業ツリーだけを正本にしない。差し戻し等で実装対象の diff が変わった場合は新しい implementation head で bundle を作り直し、古い結果を新 head の合格証拠に混ぜない。

Phase A snapshot、Phase B result snapshot、evidence bundle、C' blind audit bundle は、それぞれ path / id、生成時刻、hash を manifest に記録する。判定 C と C' の入力 bundle は、どちらかのレビューを開始する前に同じ snapshot と判定 evidence から生成する。発見 C の所見や安い XML だけを C' に渡さない。Phase B result へ発見 C の指摘を転載しない。

## 保存先とリモートへの配布

**Evidenceの保存・引渡しは、tracked HANDOFF / H1 CURRENTによる作業指示・run管理の選択とは別である。** 対応済みの同一PC・同一Windowsユーザーでは、通常レビューのEvidenceも既存Artifact Storageの `evidence publish` / `fetch` を使う。H1未移行を旧保存方式の継続理由にしない。操作、明示入力、非秘密確認、事前承認の範囲と保持条件は [Artifacts README](../../../../tools/Artifacts/README.md) を正とする。

A3で `external-current-v1` を採用したtaskでは、Git外の固定recordを正本とし、`run` IDからpath/hash/時刻を機械表示する。Gitへの生成証拠追加と手転記hash台帳は使わない。H1は同一マシンの別worktreeまでを保証し、他マシン配布は成立済みと扱わない。Artifact Storageの利用を理由にCURRENT管理方式を暗黙移行しない。

上記の対応済み環境では、C / C' へ固定入力を引き渡す時点で、通常のtask開始、終了済みtaskなら再開を済ませ、採用する非秘密file集合を明示してpublishする。blind入力と所見は分け、内部台帳や作業directory全体を無条件に送らない。C' 用 bundle に過去の bundle・所見を再帰的に詰め込まず、固定した実装差分と必須の生証拠を収録する。ローカルの生成先は原sourceであり、そこに残しただけでは保存・引渡し完了としない。

publishが返すreferenceと独立した期待packageSha256、対象base / head、bundleの用途、取得手順、保持条件を小さな取得案内にする。内部catalog / receiptを手転記して第二の台帳を作らない。取得案内は次の担当が利用でき、内容の公開可否に合った既存の引渡し先に残し、削除予定worktreeだけに置かない。新しい保存サービスや承認制度を前提にしない。C' には所見を含まない独立した案内を渡し、取得のためにPR本文や可変HANDOFF全文を読ませない。GitHubには公開可能な検証要約を残せばよく、内部台帳や機微なローカルpathの掲載を完了条件にしない。

取得側の別sessionで、案内のreferenceと独立した期待hashを使ってfetchし、検証済みのログ・XML・原画像等の必須内容を閲覧できることを確認する。送信側の読戻し成功だけを次の担当への引渡し完了としない。作業中保持・task終了から30日というStorageの保持条件に従い、継続利用はREADMEのresume / useを使う。古いPRリンクやGETだけを保持延長の根拠にしない。

別host / Cloudや外部のレビュー担当への配布は、到達性・権限・取得と閲覧が成立した範囲だけを確認済みとする。未対応・承認拒否・保存/取得失敗の場合は元証拠を保持し、理由、未完了の引渡し、次の担当を記録する。Git配布、Release asset、CI artifact、別checkoutへの移管へ自動で退避せず、公開範囲の拡大や拒否の迂回をしない。既存証拠の一括移動・削除やGit履歴の書き換えを、この方針だけを根拠に実行しない。

## 操作・目視証拠の受け渡し

判定 C の GO 判定前、C' の開始前に、凍結済み最低条件と証拠を対応付け、必須の操作・目視証拠が収録され、受け渡し先で読めることを確認する。これは証拠の存在・対象版・形式の確認であり、監査所見の先回りではない。発見 C の開始を妨げず、判定 C が必要な証拠を取得する責任も変えない。凍結時に合意した証拠形式は [検証経路の成立](phases-and-handoff.md#検証経路の成立) に従う。

人間の観察も、対象版・場面・観察者・結果を持つ独立した観察記録として保存する。本人の「明白な seam は見えない」等の一次判断を含む原記録を blind bundle に入れ、Phase C の解釈・所見・結論は除く。可変 HANDOFF の「確認済み」だけに依存しない。監査担当は証拠の不足・矛盾を指摘できるが、凍結時に合意していない撮影や全件目視を自動的に追加しない。

証拠不足は、観測そのものの欠落と、既存証拠の収録・転送不備を分ける。後者なら先に元証拠から再梱包し、ゲーム操作や人間の目視をやり直させない。再観測が必要なら、欠けている凍結条件、既存証拠では足りない理由、追加取得する範囲を示す。対象実装の変更による再検証は既存の head 固定規則に従う。

hash 付き bundle を別 worktree / 環境へ渡す場合は、送信元だけでなく受け渡し経路を通したコピーで展開・hash・必須ファイルの閲覧を確認する。Git の改行変換や LFS pointer を内容ファイルと取り違えない。再梱包で変わった bundle の id / hash は更新し、旧 id / hash との対応と payload 同一性の検証結果を残す。implementation head は変わっておらず、payload の同一性が確認できた転送修正は、既存の判定結論を無効にせず、Unity 回帰の再実行理由にしない。内容の同一性を確認できない変更や証拠追加にはこの扱いを適用しない。

## Phase D の保存・引渡し確認

Phase Dでは、判定入力、最終のレビュー所見、削除前HANDOFFの必要部分など、採用したEvidenceに未保存分がないことと、取得案内の引渡しを確認する。同一bundleのpublish・取得・閲覧が確認済みなら、その結果を使い、Phase Dのためだけに再publish・再取得・Unity再テストを要求しない。未保存分や転送不備だけを既存経路で解消し、blind入力と所見の分離は維持する。

保存・引渡し確認後に恒久知見をharvestし、HANDOFFを削除する。task全体の完了/打切りは、人間の決定を処理する既存の通常最終手順で `workflow-task.ps1 end` に記録する。`completed` や配送待ち0は終了イベントの状態であり、Evidence一式の保存済み証明ではない。これは担当者の完了確認であり、Workflow / Storageに証拠網羅性を自動検査する機能があるという意味ではない。

終了済みtaskで未保存Evidenceの保存・利用を再開する場合は、statusを確認して通常のresumeを先に完了する。終了イベントの配送再試行だけなら同じDecision / 内容を使い、再開や新しい終了に読み替えない。詳しい状態と回復手順は [Workflow README](../../../../tools/Workflow/README.md) に従う。

原source、利用者/Harnessのcopy、worktreeはStorageの自動清掃対象ではない。保存・引渡しの確認後も、未保存の変更、他task・他sessionの利用、所有者を確認し、依頼された自分の作業領域だけを後始末する。保存・引渡しが未完了なら元証拠を残し、「実装／マージ済み、Evidence引渡し・後始末は未完了」と区別して報告する。worktreeの保全だけをPhase D・後始末完了に読み替えない。

## Phase C の入力

- 凍結した Phase A snapshot
- Phase B の実装結果と未実行事項
- 固定した evidence bundle

Phase C は構造、進める最低条件とその詳細である受け入れ条件、失敗経路、テスト結果を確認する。スライスの終了可否は進める最低条件で判定する。finding は次の二つに分類して HANDOFF の Phase C 欄へ書く。

- **現在の問いを阻害する欠陥:** 凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を示し、違反する条件または契約を記録できるもの。
- **後続スライスの入力:** 上記の違反根拠を持たない新しい不確実性。重要度にかかわらず現スライスの実装や受け入れ条件を自動拡張しない。

A3 後に後者を現スライスへ取り込むには、人間が例外として明示承認し、置き換える既存条件または期限・検証予算を指定したうえで Phase A を新しい revision として再開する。

発見 C の欠陥は [新しい事実の分類](phases-and-handoff.md#新しい事実の分類) で修正先を決める。B 適応なら Phase A を再開せずに修正へ戻し、その時点では判定 C / C' を起動しない。修正後の GO 候補 head で判定必須検証と C/C' を行う規則は変わらない。診断項目の追加も、凍結済み条件・常時契約への違反根拠がなければ後続へ送り、成功経路が動くことだけで失敗経路の欠陥を先送りしない。

## Phase C' の blind audit bundle

C' へ渡す入力は判定 C の入力から生成するが、次を含めない。

- 発見 C と判定 C の結論、指摘、要約
- root や他モデルが挙げた疑念候補
- facade、旧 identifier、factory test gap のような探索先の例示
- Phase C 後に追加された誘導的な説明
- Phase B result へ転載した発見 C の所見

C' は凍結した Phase A snapshot、所見を含まない Phase B の実装結果、判定 C と同じ evidence bundle だけから独立に findings を出す。同じテストの再実行は必須にしない。監査完了後に人間または Phase D 担当が判定 C と C' を初めて突き合わせる。

差し戻し等でレビュー対象の implementation head または対象 diff が変わった場合は、その対象に対する旧判定 C / C' 結果を無効とする。修正とレビューへ戻り、次の GO 候補が固まってから判定必須テストと C/C' をやり直す。修正 commit ごとに即座に全件実行しない。HANDOFF へのレビュー記録だけを変更した場合は evidence 対象を更新しない。発見 C の差し戻しだけでは C' を起動しない。

## 機械検査と意味レビュー

決定的な検査は1回だけ実行し、モデルごとに再探索させない。ただし単純 grep が意味判定を必要とする場合、その結果は failure ではなく review flag とする。

- 明確な hard gate の例: 編集した Unity `.cs` の `#nullable enable`、Unity 側の `record`、テスト内の `Task.Delay` / `Thread.Sleep`
- 意味判定が必要な flag の例: 破棄されうる `UnityEngine.Object` に対する偽 null パターン、公開 API に露出したログ実装型

GUID、asmdef、保護 YAML などの検査は、変更種類と過去の実害に応じて実行する。全スライスへ無条件に増やさない。

## 発見 C と判定 C

Phase C は欠陥発見と最終判定を分ける。検証範囲の縮小ではなく、重い検証を実施するタイミングをずらす。BS3 の実績は、全 EditMode を毎回走らせた記録でも、毎回 8 filter の記録でもない。関連 filter と重い統合検証を、複数の Unity 起動で繰り返していた。

- **発見 C:** 固定した実装差分の構造・契約・失敗経路を先行し、確認可能な指摘を凍結済み範囲でまとめる。テスト未実行の証拠で開始してよいが、GO 判定は行わない。差し戻し中のテストは、修正と影響範囲の確認に必要なものを `-Filter` 等で選び、対象・理由・結果・未確認範囲を記録する。Phase A の起点 filter を使うが、凍結済み条件または常時契約の確認に必要なら根拠付きで変更してよい。受け入れ条件の追加とは区別する。PlayMode 統合や Player 検証も、当該欠陥の再現・修正確認に必要な場合は理由と範囲を記録して限定実行できる。通常は最終判定時にまとめる。
- **判定 C:** 未解決の blocker がなくなった GO 候補 head で判定必須テストを実行する。実装変更を伴うスライスでは最終の全 EditMode 回帰を標準とする。適用除外は Phase A で理由と代替証拠を明示する。同じ platform / graphics 条件でまとめられるテストは 1 プロセスに集約する。プロセス分離自体が検証条件なら維持する。XML の実行テスト名と件数で、必要な集合が収録されたことを確認する。namespace filter は全件性を保証しない。
- **C':** 最終判定に用いる snapshot、完全 diff、生結果、機械検査を、判定 C と同じ head で評価する。発見 C と判定 C の所見・誘導情報は渡さない。同じテストの再実行は必須にしない。
- **判定後の head 変更:** 実装対象が変わった場合、旧結果を新 head の合格証拠に流用しない。修正とレビューが収束した新 head で判定必須テストと C/C' をやり直す。修正 commit ごとに即座に全件実行しない。レビュー結果の追記だけの commit は再実行条件にしない。

BS4 にも同じ順序を適用する。Player の target、stripping、IL2CPP/AOT の必須範囲は Phase A で固定し、最終候補で bootstrap → 登録 → 論理初回 Scene → 代表 content → 終了を確認する。検証した Player / content と実装 head の対応を記録する。Editor 成功を Player 成功の代用にせず、必須 Player 検証未実行を GO にしない。

使ってはいけない implicit な読み替え:

- 狭い filter の成功を全体成功にする。
- 最後の小修正を古い XML で通す。
- 発見 C の指摘を C' に渡す。Phase B result への転載も含む。
- Player 固有の問題を fake だけで直ったとする。
- 構造レビューを飛ばして先に長い Unity を起動する。
- 差し戻しが決まったあとに最終判定用の検証一式を実行する。

## findings ledger

Unity の serialized enum 値や content 登録方式など、数字・宣言だけでは意味を確定できない指摘は、対象バージョンの定義・実際の参照経路・必要なら限定観測を照合してから blocker とする。未確認の推測は確認待ちとして扱い、ユーザーへ修正作業を返す根拠にしない。

レビュー価値を時間や token だけで評価しない。各指摘に次を記録する。

- severity
- category: machine / obvious / semantic
- unique または他レビューとの duplicate
- accepted / rejected / false positive
- 発見した Phase、担当、モデル
- 根拠となるファイル・行・契約
- bucket: 現在の問いを阻害する欠陥 / 後続スライスの入力。前者は違反する凍結済み条件または常時契約
- 修正 commit、または不採用理由

外部レビューは一つの依頼へ論点を詰め込まず、観点を限定して findings-first で出力させる。モデル可用性や quota は重い本依頼の前に小さい probe で確認し、空振りを同じ重さで反復しない。
