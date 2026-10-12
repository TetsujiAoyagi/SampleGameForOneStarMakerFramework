# BuildSystem Artifact Storage — 系列の退役スライス

## 0. メタデータ

- type: `slice`
- status: `B`（Phase A revision 1 を A3 で凍結。2026-10-12）
- branch: `codex/artifact-storage-series-retirement`
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`（`develop`。PR #123 を含む。A3 凍結時の `origin/develop` と一致を確認済み）
- implementation head commit: 未固定（Phase B で記録する）
- risk: `high`（系列を丸ごと R2 から消す不可逆な操作を新設する。新しい永続記録と、publish・collect・use を拒否する経路を足す。公開 API・asmdef・Unity・transport の C# は変えない）
- owner: OSM maintainers
- created: 2026-10-12
- expires: 2026-11-09 または置換 revision
- harvest to: 操作と契約を `tools/Artifacts/README.md`（新節「系列の退役」と、Build・Content の件数清掃節の既知の限界の更新）、program の状態と後続への移送を `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B へ。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 未保存（revision 1 の本書。凍結 commit は本書を A3 として記録した commit。C′ 用 blind bundle に収録して保存する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- workflow task: `series-retirement`（2026-10-12 に `workflow-task.ps1 start` 済み）
- 引継ぎ票: **次は Phase B（実装）。** 凍結した Phase A revision 1（本書）の §3 責務マップ・§4 順序どおりに実装し、AC1〜AC14 の offline テストを書く。implementation base は上記。必要な権限: ローカルの編集・PowerShell・offline テスト（`tools/Artifacts/tests` の関連 suite。H1 は採らないので B exit の義務は無いが、起点 filter の確認は行ってよい）。実 R2・資格情報・Unity は使わない（AC15 は判定 C）。停止規則: §4 の「Phase A へ差し戻す条件」に当たったら実装を止めて返す。B の出口で §6 と本票を更新して push して終える。判定 C の実行・照合 script・C′ の起動は担わない。引き渡さないもの: C′ 用担当（§5）への所見・会話。

入力の正本は program §2「開発Build」「開発Content」と §4 B（PR #119・PR #123 の「決めたこと」「後続への入力」）、`tools/Artifacts/README.md` の「開発Buildの件数清掃」「開発Contentの件数清掃（content cleanup）」、および現行コード。program と既存スライスで固定済みの契約は §1 に転記し、再議論しない。

## 1. 目的と対象外（A0 planning packet）

### 目的

件数清掃（PR #119・#123）は「最新 N 件を残す」ので、使い終わった系列（期間限定イベントの `contentSet`、検証用系列）は最後の N 件が R2 に残り続ける。owner が「この系列は終わり」と明示したとき、その系列の R2 object を既存の削除手順で消し切り、退役した事実を記録として残し、退役後の操作が一貫した応答を返す手動の運用経路を作る。消し損ねは許し、消し過ぎ（退役していない系列・保護中のもの）は許さない方向を検証で固定する。あわせて、R2 に残る検証用 object 60 件をこの経路で回収し、それを実 R2 の検証とする。

### program・既存スライスで固定済みの契約（再議論しない）

- Build 系列は `project/target/configuration`、Content 系列は `contentSet/target`。系列ごとに独立し、branch や人では分かれない。
- 件数保持は R2 の object にだけ効き、ローカルの記録（receipt・intent・`deletion/`）は削除済みとして残す。削除の正本はローカルの `deletion/` 記録。
- 削除は「削除意図 → DELETE → 送信観測 → 確認 GET → GET 404 `NoSuchKey` で削除済み記録」。DELETE の 2xx だけで削除済みにしない。Content は `transport.json` の GET 404 を確定点とし、確定点の後は残りを消し切る（PR #123 の状態遷移表）。
- 保護: Build は `build use` の有限期限の記録とこの PC の取得中の access lock。Content は保持中 Player（このPCの `builds-v1` の Build と ArtifactsRoot の `players/`）の buildId と revision の末尾 32 hex の照合。保護状態を収集できないときは削除しない。
- 削除の主体は層ごとに別（R2、ArtifactsRoot、DIST cache）。統合 Content フォルダは未実装。
- 通常 writer に bucket 管理権限を与えず、削除拒否（403）を rule 解除で突破しない。key ごとの削除承認・最低保存日数・owner Close 承認は設けない。
- transport は Build DELETE を `BuildRetentionDelete=$true`、Content DELETE を `ContentRetentionDelete=$true` の印付き request だけ通す。
- 公開 reason は許可語だけ。結果 JSON に endpoint・ローカル path・例外本文を出さない。

### 現況（A0 で確認した事実。コードと文書の読取だけ）

**F1. 退役の手段は無い。** `build cleanup`・`content cleanup` は N=10 の定数で、`Get-BuildRetentionDecision` は `KeepCount -lt 1` を拒否する（`BuildRetentionPolicy.psm1` の入口検査）。系列の最後の 10 件は消せない。README の両 cleanup 節の既知の限界に「系列の退役は後続の作業」とある。

**F2. ローカル記録の配置。**
- Build: `builds-v1/<repositoryId>/` 直下は `<32hex>/`、`<32hex>.reserved`、`locks/` だけを既知とし、未知の entry があると `BuildCatalog` は系列を `catalog-incomplete` で止める。系列 lock は `locks/<project>~<target>~<configuration>.lock`。Build directory は系列の階層を持たず、所属は receipt・intent の field で判定する。
- Content: `contents-v1/<repositoryId>/<contentSet>/<target>/` 直下は revision directory と `cleanup.lock` だけを既知とする（`ContentCatalog`）。directory が系列の階層を持つ。
- `ContentProtection` は `builds-v1/<repositoryId>/` 直下の entry 名のうち 32 hex を取り出せる名前だけを使い、他の名前は無視する。

**F3. 削除の部品。** `ArtifactDeletion.psm1`（48 行）は観測の写像・記録の観測検査だけを持つ純粋 module で、Build・Content の件数清掃が使う。削除の試行本体は `BuildCleanup` の `Invoke-BuildDeleteAttempt`（1 object、取下げ可能）と `ContentCleanup` の `Invoke-ContentDeleteAttempt`（複数 object、確定点の後は取下げ不能）に別々にあり、Evidence 清掃・reset は寄せていない（PR #123 の U4）。1 回の実行の DELETE 上限は Build 20、Content 2,048、時間予算はどちらも 600 秒。

**F4. 書込みの入口。** Build の publish は `Invoke-BuildPublish`（`build collect` も最後にここへ渡す）、保護は `Invoke-BuildUse`、Content は `Invoke-ContentCollect`。いずれも系列 lock を取らない（系列 lock を取るのは cleanup だけ）。

**F5. R2 に残る検証用 object（program §4 B と PR #119・#123 の記録）。**
- Build: `retention-probe/windows-x64-player/probe-1`〜`probe-4`（`project=retention-probe`、`target=windows-x64-player`、`configuration=probe-<n>`）。各系列 11 件を publish して最古 1 件を消したので、各 10 件・計 40 object（1 Build = `bundle.zip` 1 object）。PR #123 の A0 時点（2026-10-12）の読取で、このPCの `builds-v1` に検証用の Build directory が 44 件ある（各系列 11 件）。
- Content: `retention-probe-1/windows-x64-player`。11 revision（各 `transport.json` + 1 file）から最古 1 件を消したので、10 revision・20 object。revision の末尾 32 hex は `builds-v1`・ArtifactsRoot に無い乱数で、保持中 Player の保護に当たらない。
- 全件の receipt がこのPCに残っているかは本 A0 で再読取していない（A0/A1 は store を読まない）。Phase C の実行前 dry-run の原記録で確かめる（§5）。

**F6. 次の実 R2 検証への指摘（PR #123 の最終 C′、後続へ移送済み）。** 判定 C の照合 script が (a) 期待 key を削除 intent の `keys` から作っていた（削除経路の出力で削除経路を照合していた）、(b) 削除済み Build の一覧を `*.deleted.json` の存在だけで作り、記録の連鎖・観測の妥当性を再判定していなかった。

**F7. 退役の起点。** イベント終了を機械的に知る情報源（イベント期間の記録、統合フォルダの選択、配布状態）はリポジトリに無い。owner の指示を残す既存の書式として、Workflow の `Decision`（`chat:<UTC>:<taskId>:<kind>` または `github:<owner>/<repo>/pull/<n>/<fragment>`、英数字で始まる 128 字以内の `[a-zA-Z0-9._:/-]`）がある。

**F8. R2 側。** versioning は無効（PR #119 で owner が確認）。Build・Content の key への実 DELETE → GET 404 `NoSuchKey` は PR #119・#123 で成立済み。R2 の一覧（LIST）を使う経路はツールに無い。

### 要求（依頼の要約）

1. 系列を終えるときの片付け。R2 とローカル記録の扱いを決め、統合フォルダ・DIST cache は層が別なので境界だけ決める。何を起点に退役とするか（owner の明示指示か、イベント終了か）。
2. R2 に残る検証用 object（Build 40 件、Content 20 件）の回収。
3. 退役した系列の receipt・削除記録の残し方、退役後の collect／publish／inspect の応答。
4. 次の実 R2 検証の照合器の強化（F6）。
5. 共通の「DELETE → GET 404 → 記録」部品（`ArtifactDeletion.psm1`）をどこまで使うか。

### 対象外（A0 で確定。所有先は §2）

- イベント終了を検知して自動で退役させること（起点は owner の明示指示だけ。U1）
- ArtifactsRoot の `players/`・`content/` の削除、統合 Content フォルダ、DIST cache への操作（境界だけ決める。U6）
- ローカル記録（receipt・intent・`deletion/`）の圧縮・削除
- receipt の無い（未完了の）publish が残した R2 object、別 PC の publish、receipt を失った object の回収（孤児回収）と R2 一覧
- 退役の取消（U4）
- Evidence の清掃・reset の書換え、Evidence への退役の適用
- 件数清掃の自動起動、N の系列ごとの設定、Content 確定記録
- Unity・transport の C#・`build-info.json` の変更

### 常時契約のうち本スライスに効くもの

- テストで `Task.Delay`／`Thread.Sleep` を使わない（並行は signal 同期、時刻は注入）。
- 参照 0 を削除理由にしない（R2 object は消すが、コードの未使用 API は削らない）。
- Artifact Storage の通常操作は Artifacts README の事前承認に従う。退役は「設定した開発領域・契約内の清掃」の新しい種類なので、事前承認の範囲に入れるかを A3 で owner が決める（U11）。実 R2 の DELETE は Phase C で owner の許可を得てから行う。
- Unity の実行・テスト・Build は Phase C の責任（本スライスは Unity を変えない）。
- PR の base は `develop`。

### 未決事項（A1 で推奨案を出し、A3 で owner が決める。推奨案は §3 末尾）

- U1. 退役の起点（owner の明示指示か、イベント終了か）と、指示の記録の形。
- U2. 退役の記録の置き場（既存 store の中か、別の root か）。
- U3. 退役の実行形（宣言と削除を 1 コマンドにするか、宣言だけのコマンドと cleanup に分けるか）と、削除の選別をどこに置くか。
- U4. 退役の取消を許すか。
- U5. 退役した系列の保護中・未完了のものの扱い。
- U6. 層ごとの扱い（R2、ローカル記録、ArtifactsRoot、統合フォルダ、DIST cache）。
- U7. 退役後の publish／collect／use／cleanup／inspect／fetch の応答と、同時に動く publish との競合。
- U8. `ArtifactDeletion.psm1` と削除試行本体をどこまで使い回すか。
- U9. 実 R2 の検証（検証用 60 object の回収）の手順と、照合器の強化（F6）。
- U10. 退役記録を読めないときの publish・collect・use の扱い（fail-closed の範囲）。
- U11. 退役を Artifacts README の事前承認（通常操作）の範囲に入れるか。
- U12. tracked HANDOFF か H1/CURRENT か。

## 2. 意思決定と受け入れ境界

- **このスライスが答える問い:** owner が明示指示した Build 系列または Content 系列 1 つについて、退役を不変の記録として残し、このPCの成功記録にある R2 object を、保護中のものを除いて既存の削除手順で消し切り、退役後の publish・collect・use・cleanup・inspect が一貫した応答を返すか。退役していない系列には何も起きないか。
  - 採否（A0）: 要求 1〜5 は 1 つの問いに収まる。要求 2（検証用 60 object の回収）はこの経路の実 R2 検証（S5）として扱い、要求 4（照合器の強化）はその検証の照合方法（§5）として扱う。要求 5 は責務配置（U8）として扱う。分けて後続へ送るのは、イベント終了の自動検知、ArtifactsRoot・統合フォルダ・DIST cache の削除、孤児回収、ローカル記録の圧縮、退役の取消（§2 の表）。いずれも本問いの合否（退役の宣言・R2 の削除・応答の一貫性）とは別に判定でき、所有するスライスが既にあるか、必要になるまで作らない。

- **進める最低条件**

  **不変条件（起きてはいけないこと）:**
  - I1. 退役記録が確定していない系列の object へ、退役経路から DELETE を送らない。退役経路は指定した 1 系列の、このPCの成功記録から作った key 以外（他系列・別種別・Evidence の key）へ DELETE を送らない。`--dry-run` は退役記録を書かない。
  - I2. 削除試行を始める直前（系列 lock の下で取り直した時点）に保護中（Build: 有効な `build use` 記録・取得中の access lock。Content: 保持中 Player が受け入れうる revision）と判定されたものに、新しい削除試行を始めない。退役は保護を外さない。
  - I3. 退役記録・成功記録・削除記録・保護状態のどれかを確認できないとき、その系列で新しい削除試行を始めない（fail-closed）。退役記録を確認できないとき、その系列への publish・collect・use は送信・書込みの前に止まる。
  - I4. GET 404 `NoSuchKey` を確認して削除済み記録を確定するまで、その Build・revision を削除済みと記録・表示しない。成功記録のある全件が削除済みになり、未解決の試行・保護中・未完了が 0 件のときだけ「退役完了」（`retired`）と表示する。
  - I5. 退役記録の確定後に始まった publish・collect は最初の PUT の前に、use は保護記録を書く前に `series-retired` で止まり、成功を返さない。
  - I6. ローカルの成功記録（receipt・intent）、既存の削除記録・保護記録、ArtifactsRoot、DIST cache を消さない・書き換えない。退役経路が書くのは退役記録、系列 lock、`deletion/` の新しい記録、確認 GET の取得先である新規 private operation だけ。
  - I7. 確定した退役記録を書き換え・削除しない。同じ系列への 2 回目以降の退役指示は既存の記録を保ち、最初の記録の `retiredAt`・`decision` を変えない。

  **成立条件（起きなければならないこと）:**
  - S1. 退役を指示した系列の、保護中でない成功 Build・revision の全 object が、既存の削除手順（Build は 1 object、Content は確定点つき）で DELETE・GET 404 を経て削除済み記録になり、結果が `retired` になる。
  - S2. 保護中・未完了のものが残る間は、それらを消さずに件数と ID を出し、`retired-held` で正常終了する。保護が切れた後の再実行で残りが消え、`retired` になる。
  - S3. 途中で止まった退役は、次の実行で既存の状態遷移表の「再開」から確定し、完了後の同じ入力の再実行では送信しない（DELETE 0・GET 0）。
  - S4. 退役後: その系列への publish・collect・use は `series-retired`、cleanup は退役の選別で動き、削除済みの Build・revision の inspect は `payload-deleted`、fetch は失敗する。
  - S5. 実 R2 で、検証用 Build 系列 4 つ（40 object）と Content 系列 1 つ（20 object）を退役させ、照合器が独立に作った期待 key の全件が削除済みと再判定され、取得が失敗し、再実行の送信が 0 で、実系列の dry-run と実系列のローカル記録が変わらない（高コスト証拠。§5）。

- **受け入れ条件**（各条件の検証例。検証する条件を併記）:

  | # | 条件 | 検証する条件 | 確認 |
  |---|---|---|---|
  | AC1 | `build retire --profile osm --config <build-config> --project <p> --target <t> --configuration <c> --decision <id> [--dry-run]` と `content retire --profile osm --config <content-config> --content-set <s> --target <t> --decision <id> [--artifacts-root <root>] [--dry-run]` は 1 系列だけを対象にする。系列 token は既存の `Get-BuildSeries`／`Get-ContentSeries` の規則で、`--decision` は Workflow の `Decision` と同じ書式（F7）で、何も読む前に検査する。不正なら `invalid-build-identity`／`invalid-content-identity`／`unsupported-series`／`invalid-decision`。Content の ArtifactsRoot は `content cleanup` と同じ規則（無ければ `artifacts-root-not-configured` で何も読まない） | I1・I3 | offline |
  | AC2 | 退役記録（U2）は系列ごとに 1 件の不変記録（CreateNew の temp へ全量書込み → rename。private ACL、reparse 拒否）。field は `schemaVersion`・`kind`（`build`／`content`）・`repositoryId`・`seriesId`・系列 token・`decision`・`retiredAt`・`owner`（SID）だけ。記録の状態は「無い」「退役済み（検査を通った）」「不明（形・field・ACL・reparse・読取の失敗、別系列の内容）」の 3 つ | I3・I7 | offline |
  | AC3 | 実行時の順序: 系列 lock（既存の Build `locks/…lock`、Content `cleanup.lock`）を取る → 退役記録を読む → 無ければ書く（不明なら `retirement-unknown` で blocked、何も書かず送らない）→ catalog・保護を取り直す → 退役の選別（AC4）→ 既存の削除試行を候補ごとに古い順で。既に退役済みなら記録を書かずに続きを行う（I7。2 回目の `--decision` が違っても既存の記録を保ち、結果に既存の `decision` を出す）。lock を取れなければ `cleanup-in-progress` | I1・I3・I7・S3 | offline（別 process を signal で同期） |
  | AC4 | 退役の選別は純粋関数（U3）: 成功かつ保護中でない行を全て候補、成功かつ保護中を `held`（`protected`）、未完了を `held`（`publish-incomplete`）。N を使わない。行の形と全順序（`publishedAt` の降順、同時刻は ID の Ordinal 昇順を新しい側。削除は最古から）は `Get-BuildRetentionDecision` と同じ | I2・S1・S2 | offline（表検証） |
  | AC5 | catalog・保護の読取は既存の `BuildCatalog`／`ContentCatalog`／`ContentProtection`／Build の保護記録をそのまま使い、blocked の理由（`catalog-incomplete`・`delete-record-invalid`・`protection-unknown`・`clock-skew`）も同じ。1 つでも当たれば新しい試行を始めない | I3 | offline |
  | AC6 | 削除試行は既存の `Invoke-BuildDeleteAttempt`／`Invoke-ContentRevisionAttempt`（revision の `publish.lock`、状態遷移表、再開、取下げ、予算 600 秒、DELETE 上限 Build 20・Content 2,048、transport の印）を変えずに使う。各試行の直前に取り直した snapshot でなお退役の候補である場合だけ始める | I1・I2・I4・S1・S3 | offline |
  | AC7 | 結果 JSON は既存の cleanup の固定 field に `mode`（`retention`／`retirement`）と `retirement`（`retiredAt`・`decision`）を足したものだけ。`status`・`reasonCode` は: 全件削除済みで未解決・保護中・未完了が 0 → `passed`／`retired`、保護中か未完了が残る → `passed`／`retired-held`（exit 0）、未解決・残りあり → `partial`（exit 2）、blocked／失敗 → exit 1。`--dry-run` は lock を取らず、退役記録を書かず、記録・network に触れず、退役後に行う選別を返す（`mode=retirement`、退役記録が無ければ `retirement` は null） | I1・I4・S2 | offline |
  | AC8 | 退役済みの系列への `build cleanup`／`content cleanup` は、N の選別ではなく AC4 の選別で動き、結果の `mode` は `retirement`。退役記録が不明なら `retirement-unknown` で blocked（N の選別へ戻らない） | I3・S4 | offline |
  | AC9 | `build publish`（`build collect` 経由を含む）と `content collect` は最初の PUT の前に、`build use` は保護記録を書く前に、対象系列の退役記録を読み、退役済みなら `series-retired`、不明なら `retirement-unknown` で止まり、送信・書込みをしない。既に receipt がある同一入力の `existing` 応答も、退役済みの系列では返さない | I3・I5・S4 | offline |
  | AC10 | `build inspect`・`content inspect`・`fetch` の応答は変えない（削除済みは既存の `payload-deleted`、削除中は `delete-pending`、まだ消えていないものは従来どおり）。退役済みでも、まだ削除されていない保護中の Build・revision は取得できる | S2・S4 | offline |
  | AC11 | 本スライスの経路は `builds-v1`／`contents-v1` の receipt・intent・既存の `deletion/`・`protections/`、ArtifactsRoot 配下の全 file を書き換えない・消さない。fixture の前後で、これらの全 file の一覧と SHA-256 が一致することで確かめる（退役記録・lock・新しい `deletion/` 記録は除く） | I6 | offline |
  | AC12 | 退役していない系列（同じ repositoryId の別系列）を fixture に置き、退役・退役後の cleanup の前後で、その系列への DELETE 送信が 0 回、その系列の dry-run 結果が同じであること。transport へ渡る全 DELETE の key が、指定系列の成功記録から作った key の集合に含まれること | I1 | offline |
  | AC13 | publish と退役の競合: 退役記録の確定より前に始まった publish は止めない（publish は系列 lock を取らない。F4）。その publish が成功すると退役済み系列の成功記録になり、次の退役・cleanup の実行で候補になる。README の既知の限界に書く | I5（境界の明確化） | offline（別 process を signal で同期）・README |
  | AC14 | README に新節「系列の退役」: 起点は owner の明示指示で `--decision` に記録すること、取消は無いこと、退役は保護を外さないこと、R2 の object だけを消しローカル記録・ArtifactsRoot・統合フォルダ・DIST cache は消さないこと、receipt の無い object・別 PC の object は回収しないこと、AC13 の競合 | — | docs-audit と目視 |
  | AC15 | 実 R2（高コスト証拠。§5）: 検証用 Build 系列 4 つと Content 系列 1 つの退役、照合器による独立再判定、再実行の送信 0、実系列の不変 | S5・I1・I4・I6 | 判定 C |

- **ここでは答えない問いと所有する後続スライス:**

  | 問い | 所有 |
  |---|---|
  | イベント終了を検知して退役を起こす（イベント期間の記録、統合フォルダの選択からの導出） | 統合 Content フォルダ（「最新」の解決・選択 UI の所有と一緒に） |
  | 退役した系列の ArtifactsRoot の `players/`・`content/`・`.staging` の削除 | 既存「ArtifactsRoot の件数清掃と `.staging` 回収」（退役記録を入力に使えるかはそこで決める） |
  | 統合フォルダから退役系列を外す、DIST cache の eviction | 統合 Content フォルダ／Content 取り込み（DIST cache は消費側の層で、Storage は触れない） |
  | receipt の無い object・別 PC の object・receipt を失った object の回収、R2 一覧 | 既存「Content の容量上限」の孤児回収と R2 一覧の要否 |
  | ローカル記録（receipt・intent・`deletion/`・退役記録）の圧縮・削除 | 所有スライス未定（program §4 B に入力として記録する） |
  | 退役の取消、退役済み系列名の再利用 | 所有スライス未定（必要になった時。U4） |
  | Content 確定記録に退役を追記するか | 既存「Content 確定記録」 |
  | Evidence 清掃・reset の削除手順を共通部品へ寄せるか | 必要になった時に各 owner が判断（本スライスは寄せない。U8） |
  | 別 PC・Cloud と共有する退役（R2 上の退役記録） | program §4 C |
  | 診断の改善（PR #123 の入力） | 既存「診断の改善」 |

- **判定定義:** GO = 不変条件 I1〜I7 と成立条件 S1〜S5 を判定必須テストと AC15 の生結果（最終 head）で確認し、阻害 findings が無い。NO-GO = いずれかが未達、または誤削除（退役記録の無い系列・保護中・他系列・Evidence の key への DELETE 送信）、削除済みと記録・表示したのに object が残る、退役後の publish・collect・use が成功する、のどれかが 1 件でも観測された。CONDITIONAL ACCEPT は使わない。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし
- **決定記録:** なし
- **本文へ転記した実装制約:**
  - Unity C# の規則（`#nullable enable`・`record` 禁止）は本スライスに効かない（Unity C# を変えない）。transport・packaging の C# も変えない。
  - テストで時間待機を使わない。並行は別 process を signal で同期し、時計・transport・予算は注入する。
  - 公開 reason は許可語だけ。例外本文・path・endpoint を出さない。
  - 資格情報は既存の `Invoke-CredentialTransport` 経由だけ。鍵を引数・ログ・ファイルへ出さない。
  - 通常 writer に bucket 管理権限を足さず、403 を rule 変更で突破しない。
  - 既存の receipt・intent・削除記録・保護記録を書き換えない。追加する記録は不変（CreateNew + rename）。
  - `BuildRetentionPolicy` の既存関数の契約（`KeepCount` ≥ 1）を変えない。
- **未決事項:** なし。U1〜U12 は A3 で推奨案どおりに決定した（§3 末尾、§5 の A3 欄）。

## 3. 責務マップ

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `SeriesRetirementStore.psm1`（新規） | 系列ごとの退役記録の書込み（不変）と読取（無い／退役済み／不明） | 退役記録の形・置き場が変わったとき | Artifact application。記録は永続、取消なし | ArtifactCommands、ArtifactPaths、ArtifactAcl → 状態 | `Read-SeriesRetirement`、`Write-SeriesRetirement` | 注入 root の fixture で各破損・ACL・reparse・競合 | 0 → 約 110 |
| `BuildRetentionPolicy.psm1` | 退役の選別（AC4）を純粋関数として足す。既存 `Get-BuildRetentionDecision` は変えない | 行から候補を選ぶ規則の追加 | 既存。状態を持たない | 行 → 決定 | `Get-SeriesRetirementDecision` を追加 | 表検証 | 148 → 約 185 |
| `BuildCleanup.psm1` | 退役モード（記録の確認・書込み、AC4 の選別、`mode`・`retirement` の結果 field）を既存の run に足す。削除試行は変えない | AC3・AC7・AC8 | 既存 | + SeriesRetirementStore | `Invoke-BuildCleanup` に退役の引数を追加 | 既存 suite が無変更で通る + 新ケース | 191 → 約 230 |
| `ContentCleanup.psm1` | 同上（Content） | 同上 | 既存 | + SeriesRetirementStore | `Invoke-ContentCleanup` に退役の引数を追加 | 同上 | 242 → 約 280 |
| `BuildApplication.psm1` | publish（collect 経由を含む）・use の退役確認（AC9） | AC9 | 既存 | + SeriesRetirementStore | 変更なし（返す reason が増える） | Publish・Collect・Cleanup suite + 新ケース | 337 → 約 355 |
| `ContentApplication.psm1` | collect の退役確認（AC9） | AC9 | 既存 | + SeriesRetirementStore | 同上 | Content suite + 新ケース | 182 → 約 195 |
| `tools/artifacts.ps1` | `build retire`・`content retire` の grammar | 入口 | — | → BuildCleanup／ContentCleanup | CLI | grammar 拒否・必須引数 | 154 → 約 175 |
| `ArtifactDeletion.psm1`・`ArtifactApplication.psm1`・`BuildCatalog.psm1`・`ContentCatalog.psm1`・`ContentProtection.psm1`・`BuildStore.psm1`・`ContentStore.psm1`・`ContentDeletionStore.psm1`・`Transport/*` | 変更なし（既存の部品として使う） | — | — | — | — | 既存 suite | 変更なし |
| `tests/SeriesRetirement.Tests.ps1`（新規）、`tests/SeriesRetirementChild.ps1`（新規） | AC1〜AC13 の offline 検証 | — | テスト専用 | 注入 hook | — | — | 0 → 約 500、約 60 |
| `tools/Artifacts/README.md` | 新節と既知の限界（AC14）、両 cleanup 節の既知の限界の更新 | 公開面 | — | — | — | docs-audit | 330 → 約 370 |

**行数・責務の警報と判断:**
- 退役記録は既存の `BuildStore`・`ContentStore`・`ContentDeletionStore` に足さず、新しい `SeriesRetirementStore` に置く（U2）。Build・Content の両方が使い、既存 store の記録とは変更理由が別。既存の store の配置に置くと、catalog の既知 entry の規則（F2）を変える必要がある。
- 退役の選別は `BuildRetentionPolicy` に別関数として置く。行の形と全順序の知識を 1 箇所に保つため。既存関数の `KeepCount` ≥ 1 の検査は、件数清掃で 0 を渡す誤りを止める安全装置なので変えない（N=0 で退役を表すと、この装置を外すことになる）。
- `BuildCleanup`・`ContentCleanup` の増分はそれぞれ約 20%・約 15%。`ContentCleanup` が 300 行を超える見込みになったら Phase A へ戻す（PR #123 の判断を引き継ぐ）。退役用の別 orchestrator は作らない（U3）。手順（lock・snapshot の取り直し・再開・予算・集計）が件数清掃と同一で、二重に持つと片方だけ直って腐る。
- `BuildApplication` は 337 行で 500 行警報には遠い。publish と use の入口に同じ確認を 2 か所足すだけ。

### 未決事項の推奨案（A3 で owner が全件を推奨案どおりに決定）

- **U1（起点）推奨: owner の明示指示だけを起点にする。CLI の `--decision` に、Workflow の `Decision` と同じ書式の非秘密の指示 ID（例 `chat:20261012T1230Z:series-retirement:retired`、`github:<owner>/<repo>/pull/<n>/<fragment>`）を必須で受け、退役記録に残す。** 理由: イベント終了を機械的に知る情報源が無い（F7）。期日や最終 publish からの経過で推測すると、終わっていない系列を消しうる（I1）。採らない案: (a) イベント終了日を系列に登録して自動退役: 登録の所有者・時計・取消が新しく要り、統合フォルダの選択とも重なる（後続へ）。(b) 指示 ID なし: 誰の指示で消えたかを後から復元できない。
- **U2（記録の置き場）推奨: 別 root `retirements-v1/<repositoryId>/<build|content>/<系列 key>.json`（LocalApplicationData の Artifacts 配下。系列 key は Build `<project>~<target>~<configuration>`、Content `<contentSet>~<target>`）。** 理由: `builds-v1` の repo 直下・Content 系列 directory 直下は未知 entry で catalog が止まる規則（F2）で、そこへ置くと catalog の規則と全既存テストの前提が変わる。別 root なら、読めないときの扱い（`retirement-unknown`）を 1 か所で決められる。token は既存規則で path 要素として安全。採らない案: Content 系列 directory の `retired.json`（Build に系列 directory が無く、置き場が種別で割れる）。
- **U3（実行形と選別）推奨: `build retire`／`content retire` の 1 コマンドで「退役記録の確認・書込み → 削除」を行い、再実行で続きを行う。退役済みの系列への `cleanup` も同じ退役の選別で動く（AC8）。選別は `BuildRetentionPolicy` の新しい純粋関数。** 理由: 宣言だけのコマンドに分けると、宣言して削除しない中間状態の表示・説明が増える。cleanup も退役の選別に切り替えるのは、退役済みの系列で N 件を残す挙動が意味を持たず、どちらのコマンドを打っても同じ結果にするため。
- **U4（取消）推奨: 取消を作らない。退役記録は不変で、同じ系列名には publish できない。** 理由: R2 の削除は不可逆で、取消しても消えた object は戻らない。取消を許すと「退役記録あり・object は一部」の系列に新しい成功が混ざり、件数と退役の状態の組合せが増える。Content は用途ごとに `contentSet` を名付けるので新しい名前で続けられる。Build の実系列（例 `samplegame/windows-x64-player/il2cpp-high`）を退役させると、その系列名へは publish できなくなる（README に明記し、dry-run を先に打つ運用にする）。
- **U5（保護中・未完了）推奨: 退役は保護を外さない。保護中は `held`（`protected`）、receipt の無い未完了は `held`（`publish-incomplete`）として残し、`retired-held`（exit 0）で終える。** 理由: 保護は「使っている」ことの表明で、系列の終了とは独立（I2）。未完了の R2 object は receipt が無く key を確定できないので、孤児回収（後続）の対象。保護が切れた後の再実行で残りが消える。
- **U6（層）推奨: R2 の object だけを消す。ローカル記録は削除済み記録として残し、退役記録を足す。ArtifactsRoot・統合フォルダ・DIST cache には触れない。** 理由: program §2 の「削除する主体は層ごとに別」。ArtifactsRoot は取り込み元で、DIST cache の再取得元でもあり、削除規則は「ArtifactsRoot の件数清掃」が決める。退役記録はそのスライスへの入力（退役済み系列の Player・Content を消してよいか）になる。DIST cache は消費側の known-good・lease・eviction が所有する。
- **U7（応答と競合）推奨: AC9・AC10・AC13。publish・collect・use は止め、inspect・fetch は変えない。退役確定前に始まった publish は止めず、成功すれば次の実行で候補になる。** 理由: inspect・fetch は個々の Build・revision の状態を返すもので、退役中に保護中のものが残る間は取得できる必要がある（S2）。publish に系列 lock を取らせると、件数清掃と publish の関係（publish は cleanup を待たない）を変えることになり、範囲が広がる。
- **U8（共通部品）推奨: `ArtifactDeletion.psm1` と削除試行本体（`Invoke-BuildDeleteAttempt`・`Invoke-ContentRevisionAttempt`）を変えずにそのまま使う。新しい共通 primitive は作らず、Evidence は寄せない。** 理由: 退役は「何を候補にするか」だけが件数清掃と違い、削除の手順・記録・再開は同一でよい。削除試行を別に書くと、PR #119・#123 で検証した状態遷移表を二重に持つ。
- **U9（実 R2 検証）推奨: §5 の AC15。新しい検証用系列を作らず、既存の検証用 60 object の回収そのものを検証にする。照合器は期待 key を削除記録から独立に作り、削除済みの判定を記録の連鎖と観測から再判定する。** 理由: 回収が要求 2 で、新しい系列を作ると回収対象が増える。F6 の指摘は「削除経路の出力で削除経路を照合した」ことなので、期待値の入力を退役前に固定した原記録（receipt、退役前の inspect の reference）に限る。
- **U10（読めない退役記録）推奨: fail-closed。publish・collect・use・cleanup・retire は `retirement-unknown` で止まる。** 理由: 読めない記録を「退役していない」と扱うと、退役済み系列へ publish できる（I5）。`retirements-v1` の root が無いことは「どの系列も退役していない」とする（Build store の repo 無しと同じ扱い）。
- **U11（事前承認）推奨: 退役コマンドの実行は通常操作の事前承認に入れない。owner が系列と `--decision` を明示した指示の範囲でだけ Agent が実行する。退役後の系列への cleanup の再実行（続きの削除）は通常の清掃として事前承認に入れる。** 理由: 退役は系列単位の不可逆な判断で、program の「key ごとの承認を求めない」は件数保持の清掃についての規定。指示 ID を必須にするので、指示の無い退役は CLI の入力として成立しない。
- **U12 推奨: tracked HANDOFF で進める（H1/CURRENT は採らない）。** 理由: 直前の Build・Content の件数清掃スライスと同じ。H1 の固定 step は `artifact-evidence-lifecycle` 用で、新しい suite を足すには承認仕様の改訂が要る。

## 4. 実装計画

- **変更対象:** §3 の表のとおり。
- **順序:**
  1. `SeriesRetirementStore`（記録の書込み・読取・各破損）とテスト。
  2. `BuildRetentionPolicy` の `Get-SeriesRetirementDecision` と表検証。既存の Retention suite が無変更で通ること。
  3. `BuildCleanup` の退役モード、`build retire` の grammar、AC3〜AC8・AC11・AC12 のテスト。既存の BuildCleanup suite が無変更で通ること。
  4. `ContentCleanup` の退役モード、`content retire` の grammar、同じテスト。既存の ContentCleanup suite が無変更で通ること。
  5. `BuildApplication`（publish・use）と `ContentApplication`（collect）の退役確認（AC9）、競合（AC13）のテスト。
  6. README。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- **Phase B から Phase A へ差し戻す条件:** 削除試行本体・状態遷移表・transport の印の変更が必要、`Get-BuildRetentionDecision` の契約変更が必要、既存の catalog の既知 entry の規則変更が必要、退役記録以外の新しい永続記録や lock が必要、publish に系列 lock を取らせる必要が出た、`ContentCleanup` が 300 行を超える、既存の cleanup suite の期待値を変える必要が出た。
- **対象外を維持する方法:** 退役の経路に ArtifactsRoot・`builds-v1`／`contents-v1` の既存記録への書込みを置かない。テストで、退役していない系列・Evidence の key への DELETE が 0 回、ArtifactsRoot と既存記録の fixture が不変であることを assert する（AC11・AC12）。

## 5. テストとレビュー計画

- **単体テスト（offline、`tests/SeriesRetirement.Tests.ps1`）:**
  - 記録: 書込み・再読取、2 回目の指示で既存の `decision`・`retiredAt` が変わらない、形・field・ACL・reparse・別系列の内容で `retirement-unknown`、root 無しで「退役なし」、同時に 2 process が書く競合（AC2・AC3）。
  - 選別: 成功・保護中・未完了・削除中・削除済みの混在、全順序、空の系列（AC4）。
  - 退役: dry-run が何も書かない、`--decision` の書式拒否、退役記録の確定前に何も送らない、全件削除で `retired`、保護中・未完了が残る `retired-held`、保護の期限切れ後の再実行で `retired`、途中停止（予算・上限・403・応答不明）からの再開、完了後の再実行で送信 0、Content の確定点の後の続行（AC3・AC6・AC7）。
  - 退役後の応答: cleanup が `mode=retirement`、publish・collect・use が送信・書込みの前に `series-retired`、記録が不明なら `retirement-unknown`、inspect・fetch が従来どおり（AC8〜AC10）。
  - 境界: 別系列への DELETE 0 回と dry-run 不変、全 DELETE の key が指定系列の成功記録から作った key に含まれる、既存記録・ArtifactsRoot の fixture 不変（AC11・AC12）。
  - 競合: 退役中の系列 lock による `cleanup-in-progress`、退役記録の確定前に始まった publish の成功と次回の候補化（AC13）。
- **差し戻し中の起点 `-Filter`:** `SeriesRetirement.Tests.ps1`、`BuildCleanup.Tests.ps1`、`ContentCleanup.Tests.ps1`、`BuildRetention.Tests.ps1`、`BuildPublish.Tests.ps1`、`BuildCollect.Tests.ps1`、`Content.Publish.Tests.ps1`（いずれも `-Case '*'`）。
- **判定必須テスト（GO 候補 head）:** 3 つの .NET Release build（Probe・Packaging・Transport）、`tools/Artifacts/tests/*.Tests.ps1` の全 suite を `-Case '*'`（`rg` を PATH に通してから 1 回）、Workflow `TaskLifecycle`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、diff 中の `unity/` が 0 件の機械確認、AC15 の実 R2 生結果（最終 head）。
- **全 EditMode 回帰の適用除外:** 適用除外。理由: Unity の source・asmdef・Assets を変更しない。代替証拠: 全 Artifacts offline suite の結果 JSON と、diff に `unity/` が無いことの機械確認。
- **統合・Unity テスト:** なし。
- **操作・実行時・目視条件の検証経路（AC15）:** 担当は判定 C（実行担当が凍結コマンドを順に実行し、判定 C 担当が書いた独立照合 script を原記録の複製に対して実行する）。最終 head で行う。環境は owner の Windows ユーザー、既存の `osm` 資格情報と登録済み build-config・content-config。ArtifactsRoot は PR #123 の AC18 と同じく git 管理外の新規の空 directory（Content の保護源。検証用 revision は保護に当たらない）。操作:
  1. **退役前の固定（照合の入力）:** 実系列（`samplegame/windows-x64-player/il2cpp-high`、`bs4-spring-full/windows-x64-player`、`spring-full/windows-x64-player`）の `cleanup --dry-run`。検証用 5 系列の `retire --dry-run`（退役記録を書かないことも確かめる）。検証用 5 系列の全成功 Build・revision の `inspect`（reference と期待 hash を得る）。検証用 5 系列と実系列の receipt・intent・既存 `deletion/`・`protections/` の複製と一覧・SHA-256。`retirements-v1` が無い（またはこの 5 系列の記録が無い）こと。期待: 検証用 Build 各系列の成功 10・削除中 0・未完了 0・保護 0、Content の成功 10・削除中 0・未完了 0・保護 0（F5 の予想。違えば実行せず、原記録を添えて判定 C へ返す）。
  2. **退役:** 検証用 Build 4 系列を `build retire --decision <owner の指示 ID>` で順に、Content 1 系列を `content retire` で。期待: 各系列が `retired`、`requests` は Build 各系列 DELETE 10・GET 10、Content DELETE 20・GET 30（判定 C の script が `deletion/` の観測件数から再計算した値と照合し、結果 JSON の回数だけで合否にしない）。Build の DELETE 上限 20 は系列ごとに 10 で収まる。
  3. **退役後の応答:** 退役前に得た reference で全 60 件の fetch が失敗、`inspect` が `payload-deleted`。各検証用系列への `cleanup` が `mode=retirement` で送信 0。Build 検証用系列 1 つに `build use` が `series-retired`。Content 検証用系列へ合成出力の `content collect` が `series-retired` で PUT 0（送信回数は結果と `requests` で確認）。
  4. **再実行:** 同じ `retire` をもう一度。期待: DELETE 0・GET 0、退役記録の `decision`・`retiredAt` が 1 回目と同じ。
  5. **退役後の不変:** 実系列の dry-run が手順 1 と同じ。実系列と検証用系列の receipt・intent・既存記録の一覧・SHA-256 が手順 1 と同じ（新しい `deletion/` 記録と退役記録を除く）。
  - **照合器の強化（F6・U9）:** 判定 C の script は次を本スライスのコードを使わずに行う。(a) 期待 key は、手順 1 で固定した receipt の `buildId`・revision と、Content は receipt・intent の file 一覧から、script 自身に書いた key 規則（Build `development/builds/v1/<repositoryId>/<project>/<target>/<configuration>/<buildId>/bundle.zip`、Content `development/content/v1/<repositoryId>/<contentSet>/<target>/<revision>/` + `transport.json`・`content/<path>`）で作る。削除 intent の `keys` は照合される側として扱い、期待 key と一致することを確かめる。(b) 削除済みの判定は `*.deleted.json` の存在ではなく、各試行の記録の連番・`intentSha256`／`sentSha256`／`committedSha256` の hash の結び付き・観測 field（同じ key・generation の GET 404 `NoSuchKey`、`statusObserved`・非 timeout・非 redirect）を script 自身の規則で再判定し、全期待 key の最後の観測が GET 404 であることを確かめる。(c) 記録とは独立に、手順 3 の fetch の失敗を全期待 key に対して確かめる。(d) 期待 key 以外への DELETE が記録上 0 件であること。
  - **保存する証拠（非秘密の原記録そのもの）:** 手順 1〜5 の全コマンドの結果 JSON と exit code、手順 1 の複製と一覧・SHA-256、退役記録、検証用 5 系列の `deletion/` 配下の全記録（今回の run で増えた分を手順 1 の一覧との差で特定できること）、照合 script の source・合成 fixture・出力。endpoint とローカル path は収録前に除く（原本での照合は判定 C の script が行い、その出力を同じ bundle に入れる）。
  - DELETE が 403 なら S5 は未達で、rule は変えない。途中で止まった場合は同じ `retire` の再実行で続き（S3）とし、新しい検証用系列は作らない。
- **未知の操作経路の疎通結果と確認地点:** なし（Build・Content key への実 DELETE → GET 404 は PR #119・#123 で成立済み。退役は同じ削除試行を使う）。検証用系列の receipt が全件このPCに残っていることは未確認で、手順 1 の原記録で確かめる（不足があれば実行せず判定 C へ返し、孤児回収の入力にするかを owner が決める）。
- **人間の判断が必要な条件:** なし（合否は結果 JSON と記録の機械照合）。ただし実 R2 の DELETE（検証用 60 object）と資格情報の使用は、Phase C の実行前に owner の許可を得る。指示 ID（`--decision`）は owner の退役指示から作る。
- **高コスト証拠:** AC15（実 R2 の退役・fetch・再実行）。手順・独立した期待値は上記。期待値の要約: 「手順 1 で固定した receipt から作った 60 key の全てが記録上 GET 404 と再判定され、fetch が失敗し、再実行の送信が 0、期待 key 以外への DELETE が 0、実系列の dry-run と記録が不変」。順序は判定 C（高コスト以外）→ 暫定 C′ → AC15 取得 → 最終証拠ラウンド（判定 C・最終 C′）。
- **判定 C の照合 script の方式:** 判定 C の offline 実行。判定 C 担当が script を書き、資格情報・通信に触れない環境で、AC15 の原記録の複製に対して実行する。原記録へ適用する前に、判定 C が作った合成 fixture（期待値を先に書く。記録の連鎖の破損・期待外の key・GET 404 以外の最終観測を含む不合格例を入れる）で一度通す。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、全 Artifacts PowerShell の parse。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5／Anthropic（Claude Code desktop）。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:** 実施しない（owner の判断。2026-10-12 チャットで A1 初稿 commit `446f823` を確認し、A3 凍結を指示した）。高リスクスライスで独立レビューが 0 件なので、設計の見落としは発見 C・C′ で拾う前提になる（残存リスク）。発見 C は特に、各不変条件を破る順序（AC3・AC9・AC13）と AC15 の照合器を見る。
- **A3 統合担当・モデル・採否:** 主担当 Claude Opus 5.5／Anthropic と owner（2026-10-12 チャット）。U1〜U12 を全て推奨案どおりに採用。A2 の指摘は無いので採否の分類は無い。あわせて owner は AC15 の実 R2 DELETE（検証用 60 object）と `osm` 資格情報の使用を許可した。実行経路（許可ルールの追加か owner 本人の実行か）と `--decision` に使う指示 ID は判定 C の実行前に owner と確認する。
- **C' 用に予約した担当・モデル・ベンダー:** Phase A・B・C に使わないベンダーを 1 つ残す（A0/A1 は Anthropic。B・C の担当を決めるときに、C′ 用に別ベンダーを予約する）。
- **独立性の強化条件を満たせない場合の理由:** 未定（C′ の時点で記録する）。

## 6. Phase B 実装結果

- 実装: 未着手
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

## 7. Phase C

- 種別: 未実施

## 8. Phase C'

- 担当方式: 未実施

## 9. Phase D

- 未実施
