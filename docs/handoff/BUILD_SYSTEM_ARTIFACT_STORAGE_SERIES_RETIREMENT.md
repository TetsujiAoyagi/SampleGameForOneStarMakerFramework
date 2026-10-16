# BuildSystem Artifact Storage — 系列の退役スライス

## 0. メタデータ

- type: `slice`
- status: `C′`（判定 C（高コスト以外）完了。次は暫定 C′（別ベンダー）。AC15 は暫定 C′ の後。2026-10-16）
- branch: `codex/artifact-storage-series-retirement`
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`（`develop`。PR #123 を含む。A3 凍結時の `origin/develop` と一致を確認済み）
- implementation head commit: `84a3a828b32c1364869ef490cfc40c74665c99aa`（R2-1 の修正 commit。レビュー済みの revision 2 head `5621bd15153ebbe9ce5783c44e65555e6b589bae` の上に、`develop` `d911e4b4b662e66fd3fcc54f6b2586c381fabf4a`（PR #125〜#128）の merge commit `2cf0497acdc30f142920d7682c723c2de9489b79` を置き、その上に積んだ。HANDOFF 記録 commit はこの上に積む。revision 1 の head は `61f04438f426e8833b00b37aaeb32538d972afa7`）
- risk: `high`（系列を丸ごと R2 から消す不可逆な操作を新設する。新しい永続記録と、publish・collect・use を拒否する経路を足す。公開 API・asmdef・Unity・transport の C# は変えない）
- owner: OSM maintainers
- created: 2026-10-12
- expires: 2026-11-09 または置換 revision
- harvest to: 操作と契約を `tools/Artifacts/README.md`（新節「系列の退役」と、Build・Content の件数清掃節の既知の限界の更新）、program の状態と後続への移送を `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B へ。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 未保存（revision 1 の本書。凍結 commit は本書を A3 として記録した commit。C′ 用 blind bundle に収録して保存する）
- Phase B result snapshot の取得案内・状態: 本書 §6（implementation head の版を発見 C の evidence bundle に収録済み。保存・引渡しは未了）
- evidence bundle の取得案内・状態: 発見 C 用をローカルに生成済み（git 管理外。revision 1 head 向け `TestResults/series-retirement-discovery-c/` と revision 2 head 向け `TestResults/series-retirement-discovery-c-rev2/` の 2 つ。どちらも base/head 固定の完全 diff・stat・name-status・Phase A snapshot・起点 suite の結果 JSON・manifest。revision 2 向けは revision 1 head からの差分も収録）。ローカル固定済み（未昇格）: 2026-10-16 に owner の指示で旧 scheduler task を削除し、`develop` head `d911e4b4` で `schedule install` をやり直した後（§6 の基盤不備の切替）、task `series-retirement` へ 2 bundle を `evidence stage`（どちらも `passed`、`locality: local`）。取得案内（reference・packageSha256・base/head・用途・保持条件・引渡し先）は `TestResults/series-retirement-stage/retrieval-guide.md`（git 管理外、この PC）。送信側で revision 2 向けを `evidence fetch` し、22 file の取得を確認した。Cloud・別 host へは昇格経路が成立するまで渡せない。判定 C 用（GO 候補 head、高コスト以外）: ローカル固定済み（未昇格）。2026-10-16 に判定 C が implementation head の commit から作り、task `series-retirement` へ `evidence stage`（`passed`、`locality: local`）。取得案内は同じ `retrieval-guide.md` の bundle 3。送信側で fetch し、全 file が原 source と同一 hash であることを確認した。判定 C の結論を含むので C′ には渡さない
- C' blind bundle の取得案内・状態: 暫定 C′ 用をローカル固定済み（未昇格）。2026-10-16 に判定 C が判定 C 用と同じ commit から作り（このスライスの HANDOFF は diff から除き、§1〜§5 と §6 を Phase C への言及を除いて収録。加工は bundle の manifest）、`evidence stage`（`passed`、`locality: local`）。所見を含まない独立した取得案内は `TestResults/series-retirement-stage/cprime-retrieval-guide.md`（git 管理外、この PC）。送信側で fetch し、全 file が原 source と同一 hash であることを確認した。C′ 担当の session での取得・閲覧は未確認。高コスト証拠（AC15）は含まない。最終 C′ 用は AC15 の後に作る
- workflow task: `series-retirement`（2026-10-12 に `workflow-task.ps1 start` 済み）
- 引継ぎ票: **次は暫定 C′（独立監査 1 巡目。GO 判定には使わない）。** 判定 C（高コスト以外）を 2026-10-16 に完了した（§7）。implementation base `3f79639c12eac16da77d06608675564e906f9a49`、implementation head `84a3a828b32c1364869ef490cfc40c74665c99aa`（判定 evidence の head。ローカルの軽量 tag `evidence/series-retirement/1` で到達可能に保つ。その上は本書の記録 commit だけ）。Phase A は revision 2。暫定 C′ の担当は A0/A1・B・発見 C・判定 C（いずれも Anthropic）と別ベンダーの AI か人間で、新規 session で始める。C′ の入力は所見を含まない独立した取得案内 `TestResults/series-retirement-stage/cprime-retrieval-guide.md`（git 管理外、この PC。`locality: local` なので同一 PC の非仮想化 pwsh から sandbox 外で fetch する）だけで、本書・PR #124・§7・判定 C 用 bundle・会話を C′ の入力にしない。C′ 用決定記録: なし（1 巡目）。C′ の結果は §8 に記録し、阻害指摘があれば B へ戻す（C′ による差し戻しは別に 3 巡まで）。暫定 C′ の阻害指摘が無くなった head で、次は AC15（§5 の手順 1〜5、実 R2）: AC15 の実 R2 DELETE（検証用 60 object）と `osm` 資格情報の使用は A3 で owner が許可済み。実行経路と指示 ID は 2026-10-16 のチャットで owner が確認した: 経路は AI 担当の session が非仮想化の pwsh で `osm` 資格情報を使う形、`--decision` は `chat:20261016T0457Z:series-retirement:retired`（検証用 5 系列すべてに同じ ID）。手順 1 の期待値（検証用系列の receipt の不足・未完了 ≠ 0 等）が違えば実行せず判定 C へ返す。原記録の複製は判定 C の照合 script（判定 C 用 bundle の `ac15-checker/`。入力の構成は同じ README）で照合する。その後に最終証拠ラウンド（判定 C の最終判定と、AC15 を加えた最終 C′ 用 blind bundle での最終 C′）。GO は判定 C と最終 C′ が揃うまで記録しない。必要な権限: C′ は読取と fetch（sandbox 外）、AC15 は上記の owner 許可の範囲。引き渡さないもの: C′ への発見 C・判定 C の所見・結論・会話。各 bundle の取得案内の所在は §0 の evidence bundle・C' blind bundle の項

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
  - I4. GET 404 `NoSuchKey` を確認して削除済み記録を確定するまで、その Build・revision を削除済みと記録・表示しない。成功記録のある全件が削除済みになり、その系列の未解決の試行・保護中・未完了が 0 件のときだけ「退役完了」（`retired`）と表示する。Build の系列を特定できない未完了（予約だけで intent も読めない Build）は、どの系列のものか分からず退役経路で消せないので、この判定に数えない。数えない代わりに `held` へ混ぜず、別 field `unattributed`（件数だけ）に出し、`held` の件数と `retired` の表示が食い違わないようにする（revision 2）。
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
  | AC7 | 結果 JSON は既存の cleanup の固定 field に `mode`（`retention`／`retirement`。件数保持の結果にも常に出す）を足し、退役の結果にはさらに `retirement`（`retiredAt`・`decision`）と、Build だけ `unattributed`（`count`。系列不明の未完了の件数。判定には数えず `held` にも入れない）を足したものだけ。`status`・`reasonCode` は: 全件削除済みで未解決・保護中・未完了が 0 → `passed`／`retired`、保護中か未完了が残る → `passed`／`retired-held`（exit 0）、未解決・残りあり → `partial`（exit 2）、blocked／失敗 → exit 1。`--dry-run` は lock を取らず、退役記録を書かず、記録・network に触れず、退役後に行う選別を返す（`mode=retirement`、退役記録が無ければ `retirement` は null） | I1・I4・S2 | offline |
  | AC8 | 退役済みの系列への `build cleanup`／`content cleanup` は、N の選別ではなく AC4 の選別で動き、結果の `mode` は `retirement`（退役していない系列の cleanup は `retention`）。退役記録が不明なら `retirement-unknown` で blocked（N の選別へ戻らない） | I3・S4 | offline |
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

### Phase A revision 2（2026-10-12、owner 裁定。発見 C の要・裁定 2 件を決定記録にせず修正）

- 変更は I4・AC7・AC8 の 3 条件だけ（上の本文に反映済み）: (1) 件数保持の結果にも `mode=retention` を出す。`BuildCleanup`／`ContentCleanup` の既存 suite の結果 field 集合の期待値は `mode` を足して更新してよい（§4 の差し戻し条件「既存 suite の期待値を変える」の、この点に限る例外。ほかの期待値は変えない）。(2) Build の系列不明の未完了は判定に数えず、`held` に混ぜず、退役の結果の `unattributed`（`count`）に出す。Content に系列不明の未完了は無いので field を足さない。
- §5 の AC15 手順 1 の期待値は変えない（検証用系列の未完了 0）。違えば実行せず判定 C へ返す。`unattributed.count` は手順 1 の dry-run で記録し、手順 2 の結果の `retired` と矛盾しないことを確認する。A2 の独立レビューは revision 1 と同じく実施しない（owner の裁定。範囲が 2 field の追加に限られるため）。他の条件・責務マップ・順序は変えない。

## 6. Phase B 実装結果

- 実装: §4 の順で実装した（commit `feat(artifacts): add series retirement (build retire / content retire)`）。§4 の「Phase A へ差し戻す条件」には当たらなかった（削除試行本体・状態遷移表・transport の印・catalog の既知 entry・`Get-BuildRetentionDecision` の契約・publish の lock は変えず、退役記録以外の永続記録と lock は足していない。`ContentCleanup.psm1` は 263 行）。
  1. `SeriesRetirementStore.psm1`（新規 105 行）: `Read-SeriesRetirement`（`none`／`retired`、それ以外は全て `retirement-unknown` の例外）、`Write-SeriesRetirement`（同じ directory の temp へ CreateNew・flush → 上書きしない move。move に負けたら自分の temp を消して既存を読み直す。返すのは書込み後に読み直した状態）、`Assert-SeriesRetirementDecision`（`\A[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,127}\z`）。置き場は U2 どおり `retirements-v1/<repositoryId>/<build|content>/<token を ~ で結んだ名前>.json`、各階層と file に専用 ACL。record の field は AC2 の 10 個（Build）／9 個（Content）だけで、`owner` は現在のユーザー SID と一致しなければ不明。
  2. `BuildRetentionPolicy.psm1`（148 → 194）: 行の検査と全順序を private の `Read-BuildRetentionRows` に移し、`Get-BuildRetentionDecision`（入口の `KeepCount` 検査と理由の順序は同じ）と新しい `Get-SeriesRetirementDecision` が共有する。後者は保護中でない成功を全て候補（古い順）、保護中を `protected`、失敗・未完了を従来の理由で保留し、理由は `candidates-found`／`retired-held`／`retired`。BuildRetention suite は無変更で 9/9。
  3. `BuildCleanup.psm1`（191 → 217）: `Invoke-BuildCleanup` に `$Retire`・`$DecisionId` を足し、`Set-BuildCleanupMode`（退役記録の読取・無ければ書込み・選別の切替）を系列 lock の直後・catalog の前（dry-run は書かずに読むだけ）に置いた。`Get-BuildCleanupDecision` が退役中なら退役の選別を使う。削除試行・取り直し・再開・集計は既存のまま。
  4. `ContentCleanup.psm1`（242 → 263）: 同じ形（`Set-ContentCleanupMode`）。`Invoke-ContentRevisionAttempt` の publish.lock 後の取り直しも同じ選別を通る。
  5. `BuildApplication.psm1`（337 → 350）: `Assert-BuildSeriesActive` を publish（系列検査の直後、予約・operation 作成の前）、collect（系列検査の直後、退避と既存成功の応答の前）、use（系列 lock の下、保護記録の前）に置いた。`ContentApplication.psm1`（182 → 187）: collect の同じ確認。`tools/artifacts.ps1`（154 → 160）: `build retire`・`content retire`（`--decision` 必須、`--dry-run` は cleanup と retire だけ）。
  6. `tools/Artifacts/README.md`: 新節「系列の退役（build retire / content retire）」（AC14 の全項目、U11 の事前承認の扱い、結果 JSON、known limits）、事前承認節に退役を通常操作に含めない一文、件数保持 policy 節に `Get-SeriesRetirementDecision`、両 cleanup 節の既知の限界から「系列の退役は後続」を外して新節へ案内。
  - テスト: `tests/SeriesRetirement.Tests.ps1`（新規 574 行、17 ケース）・`tests/SeriesRetirementChild.ps1`（新規 68 行。実 CLI、記録の書込み競合）。AC と case の対応: AC1 `cli-grammar`、AC2 `record-store`・`record-race`、AC3 `build-retire-complete`・`record-before-delete`・`build-lock`・`record-race`、AC4 `policy-table`、AC5・AC6 `build-resume`・`content-resume`・`content-retire`、AC7 `dry-run-no-writes`・`build-retire-complete`・`content-retire`・`cli-grammar`、AC8 `record-before-delete`・`build-after-retire`・`content-after-retire`、AC9 `build-after-retire`・`build-collect-retired`・`content-after-retire`、AC10 `build-after-retire`・`content-after-retire`、AC11・AC12 `build-boundary`・`content-boundary`、AC13 `build-publish-race`。並行は別 process を named event で同期し、時刻は注入した。publish の退役確認を外す変異と「不明な記録を none と読む」変異を一度ずつ入れ、7 ケースが落ちることを確かめてから戻した（コードには残していない）。
- HANDOFF との差（いずれも B 適応。凍結条件の意味は変えていないと判断したが、発見 C で確認してほしい）:
  - **結果の `mode`:** AC7 は `mode`（`retention`／`retirement`）を足すとするが、退役モード（retire、退役済み系列への cleanup）の結果にだけ `mode='retirement'` と `retirement` を足し、件数保持の結果には field を足さない（`mode` が無いことが件数保持を表す）。既存の BuildCleanup・ContentCleanup suite が結果の field 集合を完全一致で検査しており、`mode=retention` を常に出すと「既存の cleanup suite の期待値を変える」差し戻し条件に当たるため。退役モードでは N を使わないので `keepCount` を null にした。
  - **Build の系列不明の未完了:** catalog が `held` に入れる系列不明の未完了（予約だけ・intent も読めない Build。ID は null）は、件数清掃と同じく結果の `held` に件数を出すが、`retired`／`retired-held` の判定（I4 の「保護中・未完了が 0」）には数えない。判定は退役の選別が返す系列所属の保留だけで行う。系列不明のものを数えると、同じ repository に予約だけの Build が 1 件あるだけでどの系列も `retired` にならず、しかもそれらは退役経路で消せない（key を作れない）ため。消す側には働かない（候補を増やさない）。README に明記した。
  - **content collect の確認位置:** repositoryId を得るのが content-config の読取（新規 private operation への複写の後）なので、確認はその直後・退避（ArtifactsRoot）・store・送信の前に置いた。拒否された collect も空の private operation（config の複写だけ）を 1 つ残す（既存の collect の失敗時と同じ残置）。Build の publish・collect は operation 作成の前に確認する。
  - **retire で store が無い場合:** Build の repo directory・Content の系列 directory が無く系列 lock を取れない場合も、退役記録は書く（消すものが無いことは既存どおり snapshot で確かめる）。lock なしの書込みは上書きしない move で、2 process の同時書込みは `record-race` で検証した。
  - **依存:** `SeriesRetirementStore` は ArtifactPaths ではなく `../Workflow/WindowsStorePaths.psm1` を直接 import する（BuildStore・ContentStore と同じく root の native path を解決するため）。reparse 検査は ArtifactAcl のもの。
  - **指示 ID の書式:** Workflow の `^…$` と同じ文字種・長さを `\A…\z` で照合する（末尾改行を拒否するため、Workflow より狭い）。
  - **取得中の Build:** 退役中にこの PC の取得中（access lock）の Build は既存どおり `in-transfer` で飛ばして `partial`（`held` には入れない）。README に明記した。
  - **既存 suite と実 store:** 既存の suite は `SeriesRetirementStore` の注入 root を設定していないので、publish・collect・use・cleanup が実 `%LOCALAPPDATA%\OneStarMaker\Artifacts\retirements-v1` を読む（読取だけ。現状は無いので「退役なし」）。既存 suite を無変更に保つ条件のため直していない。この PC で `samplegame/windows-x64-player/il2cpp-high` や `bs4-spring-full/windows-x64-player` を実際に退役させると、それらの suite が `series-retired` で落ちる（AC15 の検証用系列名は既存 suite で使っていない）。後続の入力として program §4 B へ送る。
  - 行数: `SeriesRetirement.Tests.ps1` は見積り約 500 に対し 574 行。`BuildRetentionPolicy` は約 185 に対し 194 行（共有関数への移動を含む）。
- 実行した検証と結果（implementation head `61f04438f426e8833b00b37aaeb32538d972afa7` の作業ツリー、2026-10-12。結果 JSON は git 管理外）:
  - 3 つの .NET Release build（Probe・Packaging・Transport）: いずれも exit 0・エラー 0。
  - `tools/Artifacts/tests/*.Tests.ps1` 全 20 suite を `-Case '*' -ResultPath <git 管理外>`（rg を PATH に通して各 1 回）: 全 suite failed 0。ArtifactEvidence 19、ArtifactPackage 9、ArtifactRotation 11、ArtifactTransfer 20、BuildCleanup 23、BuildCollect 9、BuildPublish 9、BuildRetention 9、Content.Contract 9、Content.Fetch 5、Content.Publish 12、ContentCleanup 23、Credentials 22、EvidenceCleanup 35、EvidenceReset 31、EvidenceRetention 14、EvidenceSchedule 78、R2RouteTransport 7、RouteProof 26、SeriesRetirement 17。Workflow `TaskLifecycle` 23/23。
  - 全 Artifacts PowerShell（72 file）の parse: エラー 0。`pwsh tools/contract-audit.ps1`: errors 0・warnings 0。`pwsh tools/docs-audit.ps1`: errors 0・warnings 0。implementation base からの diff に `unity/` は 0 件。
- 未実行: AC15（実 R2。判定 C の高コスト証拠）、判定 C の照合 script（B は書かない）。上の全 suite は B の確認で、判定 C の判定必須テストの代わりにはしない。Unity のテスト・Build は適用除外（Unity を変更しない）。
- implementation head commit（revision 1 の Phase B）: `61f04438f426e8833b00b37aaeb32538d972afa7`
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5／Anthropic（Claude Code desktop、A0/A1 と同じ担当の別 session）。C′ 用には Anthropic 以外のベンダーを予約する（§5）。

### Phase B（revision 2 の実装）

- 実装: `BuildCleanup`／`ContentCleanup` の結果に `mode`（件数保持は `retention`、退役は `retirement`）を常に出す。Build の退役の結果は系列不明の未完了を `held` に混ぜず `unattributed.count` に出す（件数保持では従来どおり `held`）。判定（`retired`／`retired-held`）は revision 1 と同じく系列所属の保留だけで行う。README の該当 2 項目を更新。
- 既存 suite の期待値変更は、revision 2 が例外承認した `mode` の追加だけ（`BuildCleanup.Tests.ps1`・`ContentCleanup.Tests.ps1` の結果 field 集合の 1 語）。`SeriesRetirement.Tests.ps1` は field 集合と、`retention` の `mode`、系列不明の予約を置いた Build の `unattributed`（`retired` かつ `held.count=0`、件数保持の cleanup でも同じ）の case を足した。
- 実行と結果（head `5621bd15153ebbe9ce5783c44e65555e6b589bae`）: Artifacts 全 suite の `-Case '*'`（rg を PATH に通して各 1 回）が全て failed 0（SeriesRetirement 17、BuildCleanup 23、ContentCleanup 23、EvidenceCleanup 35 ほか）、contract-audit・docs-audit は errors 0・warnings 0。.NET build・TaskLifecycle・Unity は C# と Workflow を変えていないので再実行しない（revision 1 の B 結果が有効）。AC15 は未実行（判定 C）。

### Phase B（R2-1 の修正。発見 C（revision 2 後）の差し戻し）

- `develop` の merge: レビュー済み head `5621bd1` は `develop` から 89 commit 遅れ（PR #125〜#128）で PR が CONFLICTING だったため、修正の前に `develop` `d911e4b4` を merge した（`2cf0497a`）。競合は `tools/Artifacts/README.md` の 2 hunk（冒頭の概要文と「エージェント操作の事前承認」）だけで、本スライスの退役の文と PR #128 の `evidence stage` の文を両方残した。`BuildCleanup.psm1`・`ContentCleanup.psm1`・`tools/artifacts.ps1`・suite は自動 merge で、手で直した箇所は無い。
- R2-1 の修正（`84a3a828`）: README の「開発Buildの件数清掃」「開発Contentの件数清掃（content cleanup）」の結果 JSON の列挙に `mode`（件数保持は `retention`、退役済みの系列では `retirement`。詳細は「系列の退役」節へのリンク）を足した。変更は 2 行。コード・suite の変更なし（B 適応）。
- 実行と結果（head `84a3a828` の作業ツリー、2026-10-16。結果 JSON は git 管理外）: Artifacts 全 22 suite と Workflow `StoreContext`・`TaskLifecycle` を `-Case '*' -ResultPath`（rg を PATH に通して各 1 回）: 24 suite 計 500 case、全て selected＝executed、failed 0（ArtifactEvidence 26、ArtifactPackage 9、ArtifactRotation 11、ArtifactTransfer 20、BuildCleanup 23、BuildCollect 9、BuildPublish 9、BuildRetention 9、Content.Contract 9、Content.Fetch 5、Content.Publish 12、ContentCleanup 23、Credentials 22、EvidenceCleanup 45、EvidenceReset 31、EvidenceRetention 20、EvidenceSchedule 78、R2RouteTransport 7、RouteProof 26、SeriesRetirement 17、StoreConsolidation 20、StoreConsolidationPlan 24、StoreContext 22、TaskLifecycle 23）。`pwsh tools/contract-audit.ps1`: errors 0・warnings 0。`pwsh tools/docs-audit.ps1`: errors 0・warnings 0。.NET Release build 3 本は再実行していない（3 project の source は 2026-10-12 13:58 以降変わらず、手元の DLL は同日 22:44 の build）。`unity/` の変更は 0 件。AC15 は未実行（判定 C）。
- 基盤不備（本スライスの対象外。後続への入力）: Evidence の `evidence stage` の前提である README の導入手順（`schedule status` → 新 head で `schedule install`）が、この PC で `scheduler-identity-mismatch` で止まる。原因は Store 版 pwsh の更新（7.6.6 → 7.6.7）で、登録済み task の `execute` が消えた旧 path を指し、日次 job も 2026-10-16 03:00 から `0x80070002` で失敗している。`install` は既存 task を修復しない設計のため、owner が旧 task（`\OneStarMaker\OSM-Evidence-Cleanup-…`）を削除してから `install` する必要があり、この切替手順は README に無い。失敗した `install` は runtime copy（`develop` head 分）だけを残した。Storage CLI は理由を表示せず `Artifact operation failed.` だけを出す。この 2 点（切替手順の文書化、失敗理由の表示）は program §4 への移送候補。同日、owner の指示で旧 task を `Unregister-ScheduledTask` で削除し、`develop` `d911e4b4` の detached worktree（DLL は手元の build を複写）から `schedule install` をやり直して `passed`（新 pwsh の path、次回 2026-10-17 03:00）。その後に 2 bundle を stage した（§0）。
- Phase B 担当・モデル・ベンダー: Claude Fable 5.1／Anthropic（Claude Code desktop、新 session）。次の発見 C はこれと別モデルにする。

## 7. Phase C

- 種別: **発見 C**（GO 判定ではない。C′ は起動していない）
- 担当・モデル・ベンダー: Claude Sonnet 5.5／Anthropic（Claude Code desktop、新規 session。Phase B の Opus 5.5 とは別モデル。A0/A1・B と同ベンダーなので、C′ の強化条件は別ベンダーで満たす）。
- 引継ぎ照合: ブランチ・PR #124 の状態ブロック・§0 の引継ぎ票・リモートの head（`3302acb`。implementation head の上に HANDOFF 記録 commit 1 つ）が一致し、引継ぎ票は「次は発見 C」だった。implementation base は head の祖先。
- 使用した evidence の取得案内・状態: 発見 C 用の bundle をローカルに生成（`TestResults/series-retirement-discovery-c/`、git 管理外）。base/head 固定の完全 diff・stat・name-status、implementation head 時点の HANDOFF（Phase A snapshot `076d071` と同一 hash）、起点 suite の結果 JSON、manifest（生成時刻 2026-10-12T14:14Z と各 file の SHA-256）。publish は未実施（資格情報に触れない指示。引渡し未完了）。
- 構造適合（§3 の責務マップとの照合）:
  - 変更ファイルは §3 の表と一致する。未記載の新ファイル・asmdef・Unity・transport の C# の変更は無い（`unity/` の diff 0 件）。`ArtifactDeletion`・`BuildCatalog`・`ContentCatalog`・`ContentProtection`・各 store・Transport は無変更。`Invoke-BuildDeleteAttempt`／`Invoke-ContentRevisionAttempt` の本体と状態遷移表、`Get-BuildRetentionDecision` の入口検査と理由の順序も変えていない（共有関数への抽出のみ。BuildRetention 9/9）。
  - 責務: 退役記録は `SeriesRetirementStore` に閉じる（読取は none／retired／例外 `retirement-unknown` の 3 値、書込みは CreateNew の temp → 上書きしない move）。選別は `BuildRetentionPolicy` の純粋関数、順序（lock → 記録 → catalog → 選別 → 既存の削除試行）は既存 orchestrator に `Set-*CleanupMode` を 1 つ足しただけで、別 orchestrator は無い。
  - 行数: `BuildCleanup` 217（予想 約230）、`ContentCleanup` 263（300 行警報の内）、`BuildApplication` 350（予想 約355）、`ContentApplication` 187、`BuildRetentionPolicy` 194、新規 `SeriesRetirementStore` 105。`SeriesRetirement.Tests.ps1` は 574 行で 500 行の説明トリガーに当たるが、変更理由は系列の退役の検証 1 つで、case ごとに独立（`SeriesRetirementChild.ps1` 68 行に並行の子 process を分離）。分割を要する混在は無い。
  - 依存: `SeriesRetirementStore` の import は ArtifactCommands・ArtifactAcl・`../Workflow/WindowsStorePaths`（§3 の表の ArtifactPaths は使っていない）。`BuildStore`・`ContentStore` が同じ import を持つので方向は新規ではない。循環・asmdef 参照の追加は無い。
  - 小さな重複（後続の入力）: 退役確認が Build は `Assert-BuildSeriesActive`、Content は `ContentApplication` の inline で、同じ「none 以外は止める」規則が 2 か所にある。今は同じ規則で、片方だけ直す事故の予防として後続の整理候補。
- 不変条件を破る順序の検討（AC3・AC9・AC13）: すべて破れなかった。根拠はコードと既存 case。
  - I1/I3/AC3: 退役記録の確認・書込みは系列 lock の直後・catalog の前（`Set-BuildCleanupMode`／`Set-ContentCleanupMode`）。書込み失敗・不明記録は例外で `blocked` になり、DELETE・`deletion/` に到達しない（`record-before-delete` が FaultHook で書込み失敗、`kind` 改変で retire／cleanup／dry-run の 4 経路が `retirement-unknown`・DELETE 0 を確認）。不明なとき件数保持の選別へ戻る分岐は無い。書込みの競合は move に負けた側が既存を読み直す（I7、`record-race`）。
  - I2: 各試行の直前に取り直した snapshot が退役の選別を通る（Build は系列 lock の下の while で毎回、Content は `Invoke-ContentRevisionAttempt` が publish.lock 後に `Get-ContentCleanupDecision` で再確認し、候補でなければ `not-candidate`）。保護中は `protected` として候補から外れる。
  - I5/AC9: publish は予約・operation 作成の前、collect は退避と既存成功の応答の前、use は系列 lock の下で保護記録の前、content collect は ArtifactsRoot への退避・store・送信の前。use は retire と同じ系列 lock を取るので、retire 中の use は `cleanup-in-progress`、確定後は `series-retired`（取り違えて保護を延ばす順序は無い）。`existing` も返さない。
  - AC13: publish は系列 lock を取らず確認も 1 回なので、記録の確定前に始まった publish は成功し、次の retire で候補になる。これは §2 と README が許容を明記した境界で、`build-publish-race` が成立を確認している（未完了の間は同じ系列の `publish-incomplete` として `held` に数えられ `retired-held`、完了後の再実行で `retired`）。
  - I4/I6/I7/AC11・AC12: 削除済み記録は既存の GET 404 `NoSuchKey` だけ。退役経路の追加書込みは退役記録・系列 lock・既存の `deletion/`・operation だけで、fixture 前後の file 一覧と SHA-256 の一致を `build-boundary`／`content-boundary` が確認する。2 回目の `--decision` は既存を保つ（`build-after-retire`・`content-retire`）。
- 「HANDOFF との差」4 つの解釈（§6）の判定:
  1. **`mode` の扱い（要・裁定）:** AC7 の字面は件数保持の結果にも `mode=retention` を足すが、§4 は「既存の cleanup suite の期待値を変える必要が出た」を Phase A への差し戻し条件にしている。B は退役の結果にだけ `mode`／`retirement` を足し、件数保持の field は変えなかった。AC7 の字面と §4 の差し戻し条件が衝突しており、B は止めて判断を返すべきだったが、採った読みは I1〜I7・S1〜S5・AC1〜AC15 のどの合否も変えない（AC7 の検証例は retire／dry-run／退役後の cleanup の結果で、件数保持の結果の field は検証対象に無い。AC15 の照合も `mode=retirement` だけを見る）。したがって決定記録にできる読みだと判断する。owner が合否が変わる読みだと判断する場合は Phase A revision 2。
  2. **系列不明の未完了を判定に数えない（要・裁定）:** `BuildCatalog` が `buildId=null` で `held` に入れる予約だけの Build は、系列を特定できず消せない。B はこれを `held` に出すが `retired`／`retired-held` の判定には数えない。I4 の「未完了が 0 件」を字面どおり読むと、repository に孤立した予約が 1 件あるだけでどの系列も `retired` にならない（かつそれは退役経路で消せない）。件数保持も同じく系列不明を判定に使わない。系列の特定できる未完了（intent が読める等）は数える（`build-publish-race`）。I4 と S1/S2 の合否を、特定できない未完了の不在を前提に読む点で、決定記録にできる読みと判断する。観測上は `held.count>0` なのに `reasonCode=retired` が出うる不整合があり、AC15 の手順 1（「未完了 0」を実行前に確かめ、違えば止まる）がこの条件を実機で検出する。owner の裁定が要る。
  3. **content collect の確認位置:** AC9「最初の PUT の前」を満たす。拒否された collect が空の private operation を残すのは既存の失敗時の残置と同じで、I6（退役経路の書込み）の対象外。凍結条件に反しない。
  4. **既存 suite が実 `%LOCALAPPDATA%` の `retirements-v1` を読む:** 凍結条件（I1〜I7）に反しない。読むだけで、現状は存在しない（確認済み。本発見 C の実行でも作られていない）。「既存 suite を無変更に保つ」ことを優先した B の判断は §4 に沿う。後続の入力として下に記す。
  - その他の差（依存元、指示 ID の `\z`、取得中 Build の `in-transfer`→`partial`）は凍結条件の意味を変えない。
- 現在の問いを阻害する欠陥（凍結済み条件または常時契約への違反）: **0 件。**
- 後続スライスの入力（違反根拠は無く、現スライスを拡張しない）:
  - 既存 suite が注入 root を持たず、publish・collect・use・cleanup が実 store の `retirements-v1` を読む。実系列を退役させると、その系列名を使う suite が `series-retired` で落ちる。suite の hermetic 化（既定で `SeriesRetirementStore` の TestRoot を注入）は別の整理（program §4 B へ B の記録のとおり送る）。
  - 退役確認の規則の 2 か所（Build の `Assert-BuildSeriesActive`、Content の inline）を 1 つにまとめる候補。
  - `Set-ContentCleanupClassification` の `held` には退役の `protected` も入り、`protected` 欄と二重に出る（`content-retire` が `held=1`・`protected=1` と固定している）。表示上の重複で、判定には影響しない。
- 実行したテストコマンドと結果（作業ツリー = implementation head と同じ実装、`-Case '*' -ResultPath <git 管理外>`）: `SeriesRetirement` 17、`BuildCleanup` 23、`ContentCleanup` 23、`BuildRetention` 9、`BuildPublish` 9、`BuildCollect` 9、`Content.Publish` 12。いずれも failed 0。`pwsh tools/contract-audit.ps1`: errors 0・warnings 0。`pwsh tools/docs-audit.ps1`: errors 0・warnings 0。
- 判定必須のうち未実行: .NET Release build 3 本、Artifacts 全 suite（上記 7 以外）、Workflow `TaskLifecycle`、`unity/` 0 件の機械確認、AC15（実 R2）、判定 C の照合 script。いずれも判定 C が最終 head で行う（B の結果 §6 は判定の代わりにしない）。
- 未確認事項: 発見 C は差分とコードの静的な読みと起点 suite の実行に基づく。実 R2・資格情報・Unity・実 store には触れていない。変異（publish の確認を外す等）の再実行はしていない（B の記録を信頼せず、確認が必要なら判定 C で行う）。
- owner 裁定（2026-10-12 チャット）: 要・裁定 1・2 は決定記録にせず修正する。2 は「数えず、表示を整合させる」。実装は Phase A revision 2 の凍結後に B が行う（発見 C 担当は実装しない）。
- 指摘の ledger: 阻害する指摘 0 件。要・裁定 2 件（上記 1・2。category: semantic。他レビューとの重複なし。A2 が 0 件のため独立の突合は無い）。後続の入力 3 件。

### 発見 C（revision 2 後、2026-10-13）

- 種別: **発見 C**（GO 判定ではない。判定 C・C′ は起動していない）
- 担当・モデル・ベンダー: Claude Opus 5.5／Anthropic（Claude Code desktop、新規 session）。revision 2 の Phase B（Sonnet 5.5）とは別モデル。revision 1 の Phase B は同じ Opus 5.5 だが、今回の主な範囲は revision 2 の差分で、revision 1 部分は影響経路の再確認に限る。C′ の強化条件は別ベンダーで満たす。
- 引継ぎ照合: ブランチ `codex/artifact-storage-series-retirement`、PR #124 の状態ブロック（Phase B 完了・revision 2・次は発見 C）、§0 の引継ぎ票（次は発見 C（revision 2 後））、リモートの head（`6d2f36d`。implementation head `5621bd1` の上に HANDOFF 記録 commit 1 つで、差分は本書だけ）が一致。implementation base と revision 1 head はいずれも implementation head の祖先。
- 使用した evidence の取得案内・状態: 発見 C（revision 2 後）用の bundle をローカルに生成（git 管理外）。base/head 固定の完全 diff・stat・name-status、revision 1 head → implementation head の diff と stat、implementation head 時点の本書と Phase A revision 2 を収録した `6d2f36d` 時点の本書、起点 suite の結果 JSON、manifest（生成時刻と各 file の SHA-256）。publish は未実施（資格情報に触れない指示。引渡し未完了）。
- 構造適合（revision 1 head からの差分）: 変更は `BuildCleanup.psm1`（+9/−6、結果の組立てとコメントだけ）、`ContentCleanup.psm1`（+3/−3、`mode` の初期値とコメント）、README の退役節 2 項目、3 suite の期待値、本書。§3 の責務マップの範囲内で、選別（`Get-SeriesRetirementDecision`／`Get-BuildRetentionDecision`）・catalog・削除試行・`SeriesRetirementStore`・`BuildApplication`／`ContentApplication`・`tools/artifacts.ps1` は revision 1 head から無変更（diff 0）。`unattributed` を数える元は `BuildCatalog` の `held`（系列不明の未完了だけを入れる。`buildId=null`。系列の特定できる未完了は `rows` の `incomplete` として選別へ入る）で、新しい依存・状態は無い。行数は `BuildCleanup` 217 → 220、`ContentCleanup` 263 のまま（300 行警報の内）。
- revision 2 の 3 条件の照合:
  - **既存 suite の期待値（revision 2 の例外の範囲）:** `BuildCleanup.Tests.ps1`・`ContentCleanup.Tests.ps1` の変更はそれぞれ 1 行で、結果 field 集合の文字列に `mode` を 1 語足しただけ。他の期待値は無変更。`SeriesRetirement.Tests.ps1` の変更は field 集合の定数（`mode` を基本集合へ移し、退役は `retirement`、Build はさらに `unattributed`）、`retention` の `mode` の assert、`unattributed` の case の追加で、既存 case の削除・緩和は無い。
  - **I4・AC7（`unattributed`）:** 退役モードでは `held` を選別の `heldBuilds`（系列所属の保護中・未完了）だけにし、catalog の系列不明の未完了は `unattributed.count` にだけ出す。件数保持では従来どおり `heldBuilds`＋catalog の `held`。判定（`retired`／`retired-held`）は選別の reasonCode のままで、系列不明の未完了を数えない。`storeAbsent` の確認（catalog の `held` があれば `unconfirmed`）も変えていない。`build-retire-complete` が「系列不明の予約 1 件で `retired`・`held.count=0`・`unattributed.count=1`、系列の特定できる未完了は `unattributed` に入らない、退役後の cleanup も同じ」を確認する。Content は `unattributed` を持たない（`Set-ContentCleanupMode` と初期結果のどちらにも無く、`ContentCatalog` に系列不明の保留は無い）。
  - **結果の field 集合:** Build retire は初期結果で `mode=retirement`・`retirement`・`unattributed` を持つので、入力検査の失敗・blocked・dry-run・成功のどの経路でも同じ集合。退役済み系列への Build cleanup は記録の読取（`Set-BuildCleanupMode`）で `unattributed` と `retirement` を足すので、読取後の経路（dry-run を含む）は retire と同じ集合。Content は Build から `unattributed` を除いた同じ規則。記録を読む前に止まる cleanup（入力・config の不正、`cleanup-in-progress`）と、記録が不明な `retirement-unknown` は件数保持の集合（`mode=retention`）で返る。後者は選別を行わず配列は空なので AC8（N の選別へ戻らない）には反しないが、表示の読み方は後続の入力 R2-a とする。
  - **AC8:** 退役済み系列の cleanup の `mode=retirement`、不明な記録の `retirement-unknown`（blocked、送信 0）は revision 1 から変わらず、`record-before-delete`・`build-after-retire`・`content-after-retire` が通る。
- revision 1 で確認した不変条件への影響: 差分は結果の組立て（`Set-BuildCleanupClassification`・`Set-*CleanupMode`・初期結果）とコメント・README・テストに限られ、lock・退役記録の読取／書込みの順序、snapshot の取り直し、選別、削除試行、publish・collect・use の退役確認に触れない。したがって I1〜I7 と AC3・AC9・AC13 を破る順序の検討（上の revision 1 の結果）は revision 2 の head でもそのまま成り立つ。`Set-BuildCleanupClassification` が `held` を変えるのは結果の表示だけで、候補・判定・exit code には効かない（`held` と `unattributed` の他の読み手はコードに無い）。
- 現在の問いを阻害する欠陥（凍結済み条件または常時契約への違反）: **1 件。**
  - **R2-1（docs、B 適応）:** `tools/Artifacts/README.md` の「開発Buildの件数清掃」と「開発Contentの件数清掃（content cleanup）」の結果 JSON の項目は、field を列挙して「…だけで」と網羅を述べているが、`mode` が無い。revision 2 の AC7 で件数保持の結果にも `mode=retention` を常に出すようになり、この 2 項目は implementation head で偽になった（退役節は「件数清掃の固定fieldに `mode` を足したもの」と書くので、同じ README の中で主張が食い違う）。違反根拠: ドキュメント方針（`docs/README.md`）の公開面のルール 1「今この瞬間に真であること。同じ主張の新旧を併存させない」と、AC7 が凍結した結果 field。修正: 2 項目の列挙に `mode`（件数保持は `retention`、退役済みの系列では `retirement`。退役の詳細は「系列の退役」節）を足す。コード・suite の変更は要らない。docs-audit は検出しない（文の意味の検査は対象外）。
- 後続スライスの入力（違反根拠は無く、現スライスを拡張しない）:
  - **R2-a:** 退役記録を読む前に止まる cleanup と `retirement-unknown` の blocked は `mode=retention` を返す。`mode` は「どちらの選別で動いたか」で、選別をしていない結果では既定値にすぎない。AC7・AC8 は blocked の `mode` を定めておらず、配列は空で exit 1 なので誤削除・誤表示（I4）には当たらない。読み手が `retirement-unknown` で `mode=retention` を「件数保持へ戻った」と読まないよう、未確定時の `mode`（null 等）か README の一文を、次に結果の形を触るときに決める。
  - revision 1 の後続の入力 3 件（suite の hermetic 化、退役確認の 2 か所、Content の `held`／`protected` の二重表示）は変わらず有効。
- 実行したテストコマンドと結果（作業ツリー `6d2f36d` = implementation head と同じ実装、`pwsh tools/Artifacts/tests/<suite>.Tests.ps1 -Case '*' -ResultPath <git 管理外>`）: `SeriesRetirement` 17、`BuildCleanup` 23、`ContentCleanup` 23、`BuildRetention` 9、`BuildPublish` 9、`BuildCollect` 9、`Content.Publish` 12。いずれも selected＝executed、failed 0。`pwsh tools/contract-audit.ps1`: 違反なし。`pwsh tools/docs-audit.ps1`: errors 0・warnings 0。実行後も実 store の `retirements-v1` は存在しない。
- 判定必須のうち未実行: .NET Release build 3 本、Artifacts 全 suite（上記 7 以外。EvidenceCleanup を含む）、Workflow `TaskLifecycle`、`unity/` 0 件の機械確認、AC15（実 R2）、判定 C の照合 script。いずれも判定 C が最終 head で行う。
- 未確認事項: 静的な読みと起点 suite の実行に基づく。実 R2・資格情報・Unity・実 store には触れていない。変異の再実行はしていない。
- 指摘の ledger: R2-1（severity: low、category: semantic、unique、accepted、bucket: 阻害、根拠: ドキュメント方針の公開面ルール 1 と AC7、修正 commit: `84a3a828`）。R2-a（severity: low、category: semantic、unique、bucket: 後続の入力）。

### 発見 C（差し戻し 1 巡目、2026-10-16）

- 種別: **発見 C**（差し戻し 1 巡目。GO 判定ではない。判定 C・C′ は起動していない）
- 担当・モデル・ベンダー: Claude Opus 5.5／Anthropic（Claude Code desktop、新規 session）。修正した Phase B（Claude Fable 5.1）とは別モデル。C′ の強化条件は別ベンダーで満たす（§5 の予約のまま）。
- 引継ぎ照合: ブランチ `codex/artifact-storage-series-retirement`、ローカルの HEAD `00a9b50b`（= PR #124 の head）、PR #124 の状態ブロック（次は発見 C（差し戻し 1 巡目））、§0 の引継ぎ票（同じ）が一致。`84a3a828..00a9b50b` の差分は本書だけ。本巡の途中で、修正した B の session が本書の記録 commit `0feab443`（発見 C 用 2 bundle の `evidence stage` と scheduler の切替の記録。§0 の evidence bundle の項・引継ぎ票の末尾・§6 の基盤不備の 3 行）を積んで push した。本書だけの変更で implementation head は `84a3a828` のままなので、本巡の入力は変わらない。implementation base `3f79639c` とレビュー済み head `5621bd15` はいずれも implementation head の祖先。作業ツリーには本スライスと無関係の未追跡の空 file（`docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md`、0 byte、2026-10-14 作成。別の作業の置き場）が 1 つあった。レビュー入力は commit 固定の差分で、この file は差分にも commit にも含めず、触れていない。
- 使用した evidence の取得案内・状態: 本巡用の bundle をローカルに生成（`TestResults/series-retirement-discovery-c-r1/`、git 管理外）。base→head と レビュー済み head→head の完全 diff・stat・name-status、merge commit の develop 側との diff、競合 2 file の branch 側との diff、R2-1 の修正 patch、implementation head と `00a9b50b` 時点の本書、起点 suite の結果 JSON、manifest（各 file の SHA-256）。本巡の bundle は stage していない（実 store に触れない指示。前 2 回の bundle は `0feab443` のとおりローカル固定済みだが、本巡の入力は commit 固定の差分）。
- 範囲: レビュー済み head `5621bd15` → implementation head `84a3a828` の 2 commit（merge `2cf0497a` と R2-1 の修正 `84a3a828`）。それ以前の差分は前 2 回の発見 C の結果を引き継ぎ、影響経路の再確認に限る。
- merge `2cf0497a` の照合（親は `2e4c2675`（= `5621bd15` + 本書の記録 commit 2 つ）と `d911e4b4`、merge base は `3f79639c`）:
  - **本スライスだけが変えた 11 file**（`BuildCleanup`・`ContentCleanup`・`SeriesRetirementStore`・`BuildApplication`・`ContentApplication`・`BuildRetentionPolicy`・4 suite・本書）: `d911e4b4→2cf0497a` の patch が `3f79639c→2e4c2675` の patch と一致（index 行を除いて同一）。退役経路のコードは merge で 1 行も変わっていない。
  - **develop だけが変えた 27 file**: merge 後の内容が `d911e4b4` と同一。
  - **両方が変えた 2 file:** `tools/artifacts.ps1` は hunk が重ならず、本スライスの hunk（`build retire`／`content retire` の grammar・`--dry-run` の許可・import の分岐）は行番号の移動を除いて同一。`tools/Artifacts/README.md` の競合 2 hunk（冒頭の概要文、「エージェント操作の事前承認」）は両側の文の和で、どちらの側の文も欠けていない。
  - **退役経路の依存:** `SeriesRetirementStore`・`BuildCleanup`・`ContentCleanup`・`BuildApplication`・`ContentApplication` が import する module（`ArtifactCommands`・`ArtifactAcl`・`../Workflow/WindowsStorePaths`・各 store・catalog・`ContentProtection`・`ContentDeletionStore`・`ArtifactDeletion`・`ArtifactApplication`・Transport）は develop で無変更。suite が dot-source する `EvidenceTestSupport.ps1` は develop で追加だけ（資格情報読取の計数と helper 4 つ）で、既存の関数・assert の意味を変えない。
  - **PR #126（仮想化判定）:** `tools/artifacts.ps1` の先頭、引数の解釈と store module の import より前に `Get-OsmStoreContext` の判定が入った。`build retire`／`content retire`・publish・collect・use・cleanup はすべてこの入口を通るので、仮想化された host では `virtualized-store`／`store-context-unknown`（exit 1）で `retirements-v1` を読む前・書く前に止まる。退役記録が仮想化の別実体へ分裂して書かれる経路を塞ぐ側に働き、I3（読めない記録で進まない）・I7（記録の一意性）を弱めない。判定は store を読まないので、AC1 の「何も読む前に `--decision` を検査する」の意味（store・記録・network より前）も変わらない。`StoreConsolidation` の棚卸しは `Artifacts\` 直下の未登録名を `excluded`（`unrecognized`）として列挙するだけで開かず、`retirements-v1` を移動・書換え・削除しない（I6・I7 に触れない）。
  - **PR #127・#128（欠落 staging の回復、`evidence stage`）:** 変更は Evidence の module・suite と Workflow に閉じる。Evidence と `StoreConsolidation` の module は `builds-v1`・`contents-v1`・`retirements-v1`・Build／Content の cleanup・`BuildRetentionDelete`／`ContentRetentionDelete` の印を参照しない（`StoreConsolidation` の名前表を除く。そこは列挙だけ）。退役経路から Evidence の key へ DELETE が届く経路（I1）も、Evidence の操作が退役の記録・選別に触れる経路も無い。
  - 以上から、merge は §3 の責務マップ（変更 file・依存・行数）と I1〜I7・S1〜S4 の検討（前 2 回の発見 C）を変えない。
- R2-1 の修正 `84a3a828` の照合: README の 2 節の結果 JSON の列挙が、suite の固定 field 集合（`BuildCleanup.Tests.ps1`・`SeriesRetirement.Tests.ps1` の Build 15 field、`ContentCleanup.Tests.ps1`・同 Content 15 field）と一致する。`mode` の値（件数保持 `retention`、退役済みの系列 `retirement`）は `BuildCleanup`／`ContentCleanup` の初期結果と `Set-*CleanupMode` の挙動どおり。「系列の退役」節の結果 JSON の記述とも食い違わない。R2-1 は解消した。
- 現在の問いを阻害する欠陥（凍結済み条件または常時契約への違反）: **0 件。**
- 後続スライスの入力（違反根拠は無く、現スライスを拡張しない）:
  - **R1-a:** `StoreConsolidation` の名前表は `builds-v1`・`contents-v1` を `out-of-scope-role` に登録しているが、`retirements-v1` は未登録で `unrecognized` として `excluded` に載る。README の合流節は対象外の名前に `builds-v1`・`contents-v1` を挙げ、残りを「toolsが所有しないdirectory」とまとめる。挙動は安全側（開かず・合流しない）で、仮想化判定の後は分裂側に退役記録ができないが、表示は tools が所有する記録を所有外と読ませる。次に合流の名前表を触るときに `retirements-v1` を `out-of-scope-role` に足す（program §4 B の入力）。
  - **R1-b:** 「系列の退役」節の「結果JSONは件数清掃の固定fieldに `mode` を足したもの」は、R2-1 の修正で件数清掃の列挙に `mode` が入ったため冗長になった（集合としては真）。harvest で README を整えるときに「件数清掃と同じ固定field」へ直す。
  - **R1-c（判定 C への注記）:** implementation base `3f79639c` からの完全 diff は develop の merge を含むので 40 file で、うち 27 file は develop（PR #125〜#128）のもの。本スライスの差分は `d911e4b4..84a3a828` の 13 file。判定 C は最終 head の bundle を作るとき、どちらを完全 diff とするかと、develop 分をレビュー対象外とする根拠を bundle の manifest に明記する（凍結条件は変えない）。
  - 前回までの後続の入力（R2-a、suite の hermetic 化、退役確認の 2 か所、Content の `held`／`protected` の二重表示）は変わらず有効。
- 実行したテストコマンドと結果（作業ツリー `0feab443` = implementation head と同じ実装、`pwsh -NoProfile -File <suite> -Case '*' -ResultPath <git 管理外>`）: §5 の起点 filter の 7 suite（`SeriesRetirement` 17、`BuildCleanup` 23、`ContentCleanup` 23、`BuildRetention` 9、`BuildPublish` 9、`BuildCollect` 9、`Content.Publish` 12）と、Workflow `StoreContext` 22。いずれも selected＝executed、failed 0。`StoreContext` は起点 filter に足した: merge で入った仮想化判定が `retire` を含む CLI の入口に置かれ、退役記録を書く前に止まることの確認に要るため（受け入れ条件の追加ではない）。`pwsh tools/contract-audit.ps1`: errors 0・warnings 0。`pwsh tools/docs-audit.ps1`: errors 0・warnings 0。implementation base からの diff に `unity/` は 0 件。
- 判定必須のうち未実行: .NET Release build 3 本、Artifacts 全 suite（上記以外。EvidenceCleanup を含む）、Workflow `TaskLifecycle`、AC15（実 R2）、判定 C の照合 script。いずれも判定 C が最終 head で行う（B の §6 の全 suite 結果は判定の代わりにしない）。
- 未確認事項: 差分とコードの静的な読み、patch の機械比較、起点 suite の実行に基づく。実 R2・資格情報・Unity・実 store には触れていない（実行の前後で実 store の `retirements-v1` は存在しない）。変異の再実行はしていない。仮想化された host での `retire` の拒否は develop の `StoreContext` suite の範囲で、退役のコマンドを仮想化 host で実行してはいない。
- 指摘の ledger: 阻害する指摘 0 件。R2-1 は解消を確認（修正 commit `84a3a828`）。R1-a（severity: low、category: semantic、unique、bucket: 後続の入力、根拠: `StoreConsolidation.psm1` の `ArtifactsLevel` と README の合流節）、R1-b（severity: low、category: obvious、unique、bucket: 後続の入力、根拠: README「系列の退役」節）、R1-c（severity: info、category: semantic、unique、bucket: 判定 C への注記、根拠: §0 の implementation base と merge の親）。A2 が 0 件のため独立の突合は無い。

### 判定 C（高コスト以外、2026-10-16）

- 種別: **判定 C**（GO 候補 head の判定のうち、高コスト証拠 AC15 を除く部分。§5 の順序により AC15 は暫定 C′ の後に取得し、最終証拠ラウンドで最終判定する。GO は記録しない）
- 担当・モデル・ベンダー: Claude Opus 5.5／Anthropic（Claude Code desktop、新規 session）。独立性: 直近の Phase B（revision 2 は Sonnet 5.5、develop の merge と R2-1 の修正は Fable 5.1）とは別モデルだが、revision 1 の Phase B（と A0/A1）とは同じモデルの別 session。判定 C は凍結コマンドの実行と機械照合が中心で、全体の独立監査は別ベンダーの C′ が担う。
- 引継ぎ照合: ブランチ `codex/artifact-storage-series-retirement`、ローカルの HEAD `8cd06b49`（= PR #124 の head）、PR #124 の状態ブロック（次は判定 C）、§0 の引継ぎ票（次は判定 C）が一致。`84a3a828..8cd06b49` の差分は本書だけ。作業ツリーには本スライスと無関係の未追跡の空 file（`docs/handoff/ARTIFACT_STORE_VIRTUALIZATION.md`、発見 C 差し戻し 1 巡目の記録と同じ）が 1 つあり、触れていない。判定は implementation head の detached worktree（作成直後の dirty 0、実行前後も dirty 0）で行い、可変な作業ツリーを入力にしていない。owner の AC15 の確認（経路と指示 ID）は §0 に記録済み（`8cd06b49`）。
- 使用した evidence: 判定 C 用 bundle（implementation head の commit から作成。§0 の evidence bundle の項。ローカル固定済み（未昇格））。完全 diff の範囲と develop 分の扱い（R1-c）は bundle の manifest に明記した: base→head の完全 diff は 40 file（develop の merge を含む）、本スライスの変更集合は `d911e4b4..84a3a828` の 13 file（§3 の責務マップの 12 file と本書）で、判定 C の対象はこちら。残る 27 file は head の内容が `d911e4b4` と同一（`d911e4b4..84a3a828` に現れない）で、develop の PR #125〜#128 が各自のレビューを経た内容。merge の正しさは発見 C 差し戻し 1 巡目の patch の機械比較による。
- 判定必須（§5）の実行と結果（implementation head の detached worktree、2026-10-16）:
  - 3 つの .NET Release build（Probe・Packaging・Transport。新しい worktree なので最初に build した）: いずれも exit 0・警告 0・エラー 0。
  - 全 suite（`rg` を PATH に通してから、`pwsh -NoProfile -File <suite> -Case '*' -ResultPath <git 管理外>` を各 1 回）: Artifacts 22 suite と Workflow `StoreContext`・`TaskLifecycle` の 24 suite、計 500 case。全 suite で registered＝selected＝executed、failed 0、exit 0（ArtifactEvidence 26、ArtifactPackage 9、ArtifactRotation 11、ArtifactTransfer 20、BuildCleanup 23、BuildCollect 9、BuildPublish 9、BuildRetention 9、Content.Contract 9、Content.Fetch 5、Content.Publish 12、ContentCleanup 23、Credentials 22、EvidenceCleanup 45、EvidenceReset 31、EvidenceRetention 20、EvidenceSchedule 78、R2RouteTransport 7、RouteProof 26、SeriesRetirement 17、StoreConsolidation 20、StoreConsolidationPlan 24、StoreContext 22、TaskLifecycle 23）。実 store の `retirements-v1` は実行前後とも存在しない。
  - `pwsh tools/contract-audit.ps1`: errors 0・warnings 0。`pwsh tools/docs-audit.ps1`: errors 0・warnings 0。
  - `unity/` の機械確認: `3f79639c..84a3a828` と `d911e4b4..84a3a828` のどちらも `unity/` の file は 0 件（全 EditMode 回帰の適用除外の代替証拠。§5）。
  - 全 Artifacts・Workflow の PowerShell（tracked の 86 file）の parse: エラー 0。
- AC15 の照合 script（§5 の方式「判定 C の offline 実行」）: 判定 C が書いた（判定 C 用 bundle の `ac15-checker/`。git 管理外）。本スライスの module を import せず、期待 key を receipt・intent と凍結した key 規則から作り（(a)）、削除記録の連番・`intentSha256`／`sentSha256`／`committedSha256` の結び付き・観測（同じ key・generation の清浄な GET 404 `NoSuchKey`）を自分の規則で再判定し（(b)）、手順 1 の reference による fetch の失敗を全期待 ID で確かめ（(c)）、期待外の Build・revision の新しい削除記録と期待外 key への DELETE が 0 であること（(d)）、requests を記録の観測件数から再計算した値との一致、手順 1 と手順 5 の store の全 file の SHA-256 の不変（新しい file は期待 ID の `deletion/`・`access.lock`、検証用系列の lock、退役記録だけ）、実系列の dry-run の不変を確かめる（19 check）。合成 fixture 23 case（合格 1、不合格 21: 連鎖の hash 2 種、最後の GET が 200・timeout・generation 変化、Content の key の観測欠落、期待外 key・期待外 Build への DELETE、fetch の成功・別 reference、再実行の送信、requests の不一致、実系列の記録・dry-run の変化、事前の保護・未完了・退役記録、退役記録の変化、削除記録の欠落、前の試行の未取下げ、use の成功。判定不能 1）を、期待（verdict と fail する check の集合）を先に固定してから実行し、23 case すべてが期待どおり（不一致 0）。途中で fixture 生成側の欠陥 3 つ（strict mode の hashtable 読取、配列の `,` の優先順位、結果編集時の時刻の再直列化）を直した。期待の表と照合 script の判定規則は変えていない（経緯は `ac15-checker/README.md`）。原記録への適用は AC15 の後。
- 高コスト証拠を除く操作・実行時・目視の条件: §5 の検証経路の操作・実行時条件は AC15 だけで、高コスト証拠に分類済み。ほかに凍結済みの操作・目視条件は無い（動作確認の対象もこれだけ）ので、判定 C（高コスト以外）の完了を妨げる未取得の証拠は無い。
- 構造適合・不変条件: implementation head は発見 C 差し戻し 1 巡目と同じ `84a3a828` で、本スライスの 13 file はその巡の確認から変わっていない（同じ commit）。行数は `BuildCleanup` 220、`ContentCleanup` 263（300 行警報の内）。前 3 回の発見 C の構造適合と I1〜I7 を破る順序の検討は、この head でそのまま成り立つ。
- 現在の問いを阻害する欠陥（凍結済み条件または常時契約への違反）: **0 件。**
- 後続スライスの入力（違反根拠は無く、現スライスを拡張しない）:
  - **J-a:** 判定必須の全 suite の結果 JSON を `-ResultPath <dir>/<suite>.json` で置くと、Credentials suite の結果が `Credentials.json` になり、`evidence stage` が保護名規則（`PackagePolicy`）で `unconfirmed` を返す（理由は表示されない）。レビュー証拠（`review-evidence.md`）の「保存先とリモートへの配布」の改名規則（非秘密と確認して改名し、対応表を同じ bundle に入れる）で対処した（`results/Credentials-suite-result.json`、`_renamed-files.txt`）。判定 evidence の命名を suite 名と別にするか、stage が保護名の理由を返すかは、Evidence の診断の改善（program §4）の入力。
  - 前回までの後続の入力（R1-a、R1-b、R2-a、suite の hermetic 化、退役確認の 2 か所、Content の `held`／`protected` の二重表示）は変わらず有効。
- 判定必須のうち未実行: AC15（実 R2。高コスト証拠。暫定 C′ の後に取得し、最終証拠ラウンドで照合 script を原記録の複製へ適用する）。
- 未確認事項: 実 R2・資格情報・Unity には触れていない。実 store に対しては、`retirements-v1` の有無の読取と、task `series-retirement` への `evidence stage`・送信側の fetch だけを行った。C′ 用 bundle の、C′ 担当の session での取得・閲覧は未確認（C′ の開始時に確かめる）。変異の再実行はしていない。
- 判定 C（高コスト以外）の結論: §5 の判定必須のうち高コスト証拠以外が全て通り、阻害する指摘は 0 件。**判定 C（高コスト以外）完了。** C′ 用 blind bundle を同じ commit から作り、ローカル固定した（§0）。GO 判定は AC15 の後の最終証拠ラウンドで行う。
- 指摘の ledger: 阻害する指摘 0 件。J-a（severity: low、category: obvious、unique、bucket: 後続の入力、根拠: `PackagePolicy` の保護名と `review-evidence.md`「保存先とリモートへの配布」の改名規則）。

## 8. Phase C'

- 担当方式: 未実施

## 9. Phase D

- 未実施
