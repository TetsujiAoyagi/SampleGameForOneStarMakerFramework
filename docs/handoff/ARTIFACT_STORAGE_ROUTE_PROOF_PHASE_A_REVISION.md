# Artifact Storage Route Proof — Phase A revision 2

## 0. 版と適用範囲

- type: slice の Phase A snapshot。進行台帳: ARTIFACT_STORAGE_ROUTE_PROOF.md。
- revision: route-proof-a-r2 / A3 凍結済み。A2 指摘の統合と人間の承認が完了し、別セッションの B 入力とする。
- frozen at: 2026-09-28T15:26:42Z。owner が「A3凍結しよ」と承認。承認後の変更は凍結状態の記録だけで、規範本文の受け入れ境界は承認候補と同一。
- branch: codex/artifact-storage-route-proof / risk: high。
- slice implementation base: 93d2a1c436361ef6ee702096a61087cde55319b4
- 改訂の実装開始点: da4e405a0c5019a2f0edd857e1a2c11b61432c34
- 計画開始時 docs tip: dfbed2fdd48027ccbdcaecbbd8124da30019a362
- 新 implementation head / B result / 判定 evidence / C' bundle: 未生成。docs-only commit は実装 head にしない。
- owner: repository owner。B は別セッションの SOL。C / C' は開始時に独立性条件で選ぶ。
- created: 2026-09-29 JST。expires: Phase D または次の A revision。
- harvest to: tools/Artifacts/README.md、BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の現況。
- snapshot の生成 UTC / SHA-256 / 固定 commit は進行台帳へ記録する。自己 hash は埋め込まない。

本文が B の規範入力。旧 A3 の要件は以下へ再掲した。変更点は Before / After 証拠の分離と有限な B 作業であり、R2 合格条件の緩和ではない。旧 snapshot の議論・発見 C 所見を追加指示として取り込まない。

## 1. 問い、最低条件、B の到達点

この slice の問い: 既存 private R2 の osm-artifacts と通常 writer が、所有 Windows ユーザーの別 process 間で synthetic object を同一 byte で往復でき、非認証の同じ存在 key の GET が内容を返さず、限定 prefix の Bucket Lock が同じ writer の上書き・削除を拒否するか。

GO の最低条件は四つ全ての実観測と、同じ implementation head / 実行物への対応である。

1. 既存 DPAPI profile が正常で、writer は対象 bucket の Object Read / Write のみを持ち、private 設定・有限 lock rule の開始条件が成立する。
2. unlocked PUT、別 process の認証 GET、正規 unsigned GET の拒否、上書き・変更 hash GET、DELETE・NoSuchKey 確認が成立する。
3. 同じ Generation の新しい locked key で PUT / GET が成立し、保持内の上書きと DELETE がともに 403 / ObjectLockedByBucketPolicy、再 GET の原 hash が一致する。実際の前後設定に変化がない。
4. 秘密を出力せず、期限・key 数・清掃制限を守り、source / DLL / 依存 / 生結果の対応と判定必須検証が揃う。

**B の引渡し条件は §4 の実装・回帰と §5 のオフライン確認。B 完了は slice の GO / 完了ではない。** 正規 request に欠陥を確認できなければコードを変えず、400 の原因未特定・実経路未証明として B を引き渡す。400 が消えるまで実装を続けない。

今回の A / B は R2 通信禁止。read-only GET、設定参照、curl、SDK、管理 API も含む。owner が後で明示許可するまで C の live 部分は未実施で止める。A3 承認を通信許可と解釈しない。

対象外: 汎用 Artifact CLI / publish / fetch / safe extraction、実 Evidence / Build upload、Cloud、provider 変更、Worker / proxy / public domain、鍵再登録・rotation、credential schema、Unity / asmdef、SDK 更新、汎用 evidence framework。これらと代替 request 探索の所有者は BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の最小 Artifact CLI / provider 再選定 Phase A。未証明の R2 を前提に後続実装を始めない。

## 2. 受け入れ契約

### 2.1 資格情報・宛先・fixture

- profile は既存 osm、bucket は osm-artifacts、credential v1 の Endpoint は null、Generation は小文字 hex 32 桁。正常 active を再利用し、set / replace / ACL 自動修復・再入力をしない。
- endpoint は \Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z のみ。path / query / userinfo / port / 別 jurisdiction host は拒否。実 account ID は既存の非秘密記録から読み、文書に複製しない。
- 秘密は既存 store の同一 process callback 内だけ。chat / 引数 / env / Git / ログへ載せない。store は秘密の寿命・安全な戻り値の検査を所有する。
- remote key は probe/unlocked/<run-id>/object.txt と probe/locked/<run-id>/object.txt の最大 2 個/run。run-id は小文字 hex 32 桁。fixture は ASCII OSM-ROUTE-PROOF:<run-id>:original と :changed、各 1〜1024 byte。送信前に hash を算出。既存 key / 実 payload を使わない。

### 2.2 正規 unsigned GET と 400

- URI は account endpoint + /osm-artifacts/ + key の各 segment の escape、method GET。Authorization / 署名 query / 対象変更 query なし。redirect 無効。同じ run/key の直前の認証 GET で byte 数・hash・Generation を照合する。
- 合格 tuple は 401 / unauthorized / Unauthorized または 403 / forbidden / AccessDenied のみ。同一応答の HTTP status / status class / S3 Code を組で照合する。EOF 到達、全体 hash が object と異なること、取得済みの期待長 prefix hash も異なることを要する。
- 400 / InvalidArgument は inconclusive。非公開性の成功、provider 能力否定、実装不具合の確定のどれにも読み替えない。
- object と同じ先頭 N byte の hash は後続 byte・EOF・status にかかわらず内容露出の一次観測。N 未満の prefix hash を作らず、部分応答を拒否証拠にしない。
- 本文は 8192 byte + 超過検出 1 byte まで。8193 byte 目、EOF 未確認、timeout、redirect、429 / 5xx、DNS / TLS / proxy、404 / NoSuchKey、SignatureDoesNotMatch、認証 GET 失敗、許可外 Code は非合格。本文・任意 header・SDK 例外は保存しない。
- B 適応は上の正規条件への具体的違反をオフラインで示せた場合だけ。代替 host / path / query / method / 認証や、403 を得るための header 探索は含めない。

### 2.3 Bucket Lock と前後証拠

- 既設 rule を再設定しない。全有効 rule は probe/locked/ の Age がちょうど 1 件、900〜86400 秒。空 / 他 / unlocked prefix、Date / Indefinite、保持不明を拒否する。writer に設定権限がなく lifecycle が妨げないことを owner の非秘密観測で確認する。
- private 設定は Public Development URL（r2.dev）が無効、Custom Domains が空の両方。owner が試験前後に各項目を記録する。前に未成立・未確認なら environment-blocked で開始せず、後で変化・不明なら inconclusive、どちらも GO 不可。S3 GET の拒否で別公開経路の確認を代用しない。
- 同じ Generation の unlocked 上書き・GET・削除の陽性対照が先。locked 操作は新規 PUT からの単調時間が保持内にあるときだけ有効。403 単独・権限不足を lock 成功にしない。
- **起動前に未来の After 観測は存在しない。** -LockRuleJson は Prefix / Enabled / Kind / RetentionSeconds / RuleCount / DateRules / IndefiniteRules / WriterCanConfigure / LifecycleCompatible / BeforeHash の厳密な 10 field。AfterHash を含む入力は拒否する。BeforeHash は事前原記録の SHA-256（小文字 hex 64 桁）。他の型・値域は維持する。
- probe 出力の lockRule.afterHash は null 固定。beforeHash をコピーしない。finalize command、post-state polling、設定取得用権限を増やさない。
- C が実際の run 前後に独立取得された原記録を外側で束ねる。原記録には観察者、取得方法、取得 UTC、bucket/private 設定、全有効 rule、writer scope、lifecycle、残存 key の清掃予定を含める。before UTC <= run 開始 < run 終了 <= after UTC と設定値の同一性を検査する。時刻が異なるファイルの raw hash を同一にする必要はない。
- probe の pass / exit 0 は 12 操作の技術的成功で、C の GO ではない。After 証拠不足、設定変更、保持失効・不明なら C は inconclusive、GO 不可。事前 hash の一致で補えない。
- owner の観測原記録を受理し、C' に画像再評価までは要求しない。AI は非秘密記録の準備・整合検査を担当する。owner が前後観測を引き受ける確認は将来の live 再開時に一度行う。現時点で実施・合意済みとしない。

### 2.4 実行・清掃・判定

- 12 操作: unlocked PUT → 認証 GET → unsigned GET → 変更 PUT → 変更 GET → DELETE → NoSuchKey GET、locked 新規 PUT → 認証 GET → 変更 PUT 拒否 → DELETE 拒否 → 原 hash GET。1 child = 1 操作、本番ループは一つ。
- operation は子起動・import・credential 読取・header/body EOF を含め 30 秒以内。parent の終了・stdout/stderr 回収は合計 45 秒以内、残時間共有。process tree 終了と両 pipe EOF を確認。run は 5 分。SDK 隠れ retry / redirect を禁止。
- 失敗後 unlocked PUT が送られた可能性があれば、child 終了確認後だけ別枠合計 30 秒で DELETE + NoSuchKey GET。起動・待機・pipe を含む。204 単独、別 object hash だけでは removed にしない。終了未確認 child と清掃を競合させない。清掃成功でも run を合格に変えない。
- locked key の保持前追加清掃をせず、key / rule / 期限 / 清掃予定を記録。保持未証明の残存物は unconfirmed。自動清掃機構を作らない。
- 外部 JSON は既存 allowlist（result / phase / operation / 検証済み key / 期待・実測 hash・byte / prefix hash / EOF・上限 / status・Code / Generation / lockRule / UTC / cleanup / base・head）を維持。未知 Code は [A-Za-z][A-Za-z0-9]{0,63} の全体一致名のみ。raw message / body / header / 任意 property を拒否。
- exit は pass 0、provider-capability-failure 2、environment-blocked 3、inconclusive 4。新 status enum は作らない。開始条件不足は environment-blocked、結果・清掃不明は inconclusive。「実経路未証明」は報告文であって新 runtime 値ではない。
- unlocked 上書きの 429 / TooManyRequests は 1 秒以上後に一度だけ再試行し、再び429なら inconclusive。locked 上書きの成功後は保持有効を再確認し、1 秒以上後に一度だけ再試行して、原 hash からの変化を再 GET で確認するまで能力否定にしない。全体期限は伸ばさず、400 retry や新たな自動再現は足さない。DELETE の success 単独も能力否定にしない。
- provider 能力否定の CONDITIONAL ACCEPT は、有効な開始条件・陽性対照の下で、内容露出なら別 run / 新 key で再現、lock なら保持内の禁止操作の再現と変更 hash / 削除を確認した実証に限る。現在は未到達。追加 live 再現は別途許可が必要。C が生結果から判断し、自動再現機構は増やさない。
- GO は §1 と判定 C / C' が揃った場合のみ。CONDITIONAL ACCEPT は R2 採用成功でなく program 再選定への否定材料。inconclusive / environment-blocked / 時間切れは slice 完了ではない。

## 3. 責務・規模・配置

- tools/Artifacts/Probe/RouteProof.ps1（546 行、見込み -5〜+20）: policy と 12 操作 orchestration、process/期限/清掃。状態は run 寿命、store と transport に依存し Unity 非依存。変更は Before/After 分離と立証済み局所修正。既存 -Library / clock / child seam で試験する。500 行超だが新責務を入れず、判定だけの Helper 分割はしない。
- R2RouteTransport.cs（486 行、既定増分 0）: SDK/HTTP request・cancel・有限 stream 観測、client/stream は操作寿命。秘密保管・GO 判定を持たない。具体的正規 request 欠陥のみ局所修正可。新 transport に置換しない。
- R2RouteTransport.psm1（59 行、増分 0）/ csproj: DLL loading、.NET 8 / AWSSDK.S3 3.7.501.14 の既存境界。package/framework/build 構成を変更しない。
- tools/Artifacts/tests/RouteProof.Tests.ps1（404 行、見込み +80〜140）: 実 child 入口と Before/After 試験。test 所有の一時 directory/process を case 後に破棄。500 行を超えても同じ runner の case/fixture に留め、汎用 fake R2 や evidence runner を作らない。
- tests/R2RouteTransport.Tests.ps1（187 行、既定増分 0）: 必要な canonical request/read 回帰のみ。本番 factory/reader を使い判定をコピーしない。
- CredentialStore と credentials CLI/tests は変更しない。DLL 対応・原記録は C 手順と小さな manifest の責務。runtime の Git 呼出し/evidence service を追加しない。
- tools/Artifacts/README.md（開始時 73 行、見込み -5〜+10）: B2 の入力変更と同時に該当説明・実行例だけを更新し、AfterHash 事前入力を除く。実 After は C の外側で確認すること、既存 run は inconclusive / GO なしという現況を反映する。
- 作業文書は現 HANDOFF・本 snapshot・既存汎用 Phase B result を更新する。commit 別 RESULT / FINAL / RERUN を増やさない。旧固定物の訂正は台帳の errata に置く。

行数は目標でない。新責務・状態・依存・公開 API・汎用 module が必要なら A へ返す。小さく見せる分割・機械的改名・参照 0 による削除をしない。

## 4. SOL の B 作業と停止位置

### B1: 有限な原因照合

1. branch、tracked 差分、開始点の祖先関係を確認。既存成果を reset / checkout / rewrite で捨てず docs commit と実装を分ける。
2. 台帳の source/DLL/bundle/run の path・hash と既存 build log を一度照合する。確定できない旧対応を新 build の合格証拠にせず、後から build して旧 run を遡及証明しない。
3. 既存 source/fixture の request 生成 → SendAsync → child JSON → 判定を追う。method、同一 key、escaped URI、auth/query 不在、redirect 無効、loaded DLL 対応に限定し、最大 3 仮説のオフライン確認まで。公式公開文書は読めるが bucket/API へ接続しない。
4. 条件違反を再現できた場合だけ修正＋回帰。なければ「正規条件への違反未発見、400 原因未特定」を記録して B2 へ。推測の header/endpoint 変更、診断だけの production field 追加、同じ調査の反復をしない。

### B2: Before / After 分離

Read-LockRule の入力を §2.3 の 10 field、出力 afterHash は null にする。既存 fixture を合わせる。正常 Before、AfterHash 混入、BeforeHash 欠落/型不正、未知 field、不正 rule を試験する。正常ループでも afterHash が null のままを確認。設定の自動取得・finalize subcommand は足さない。

同じ変更で tools/Artifacts/README.md の入力説明と実行例を 10 field に合わせ、afterHash=null / 実 After は C の原記録で照合する責任を明記する。古いコマンド例を Phase D まで残さない。

### B3: 実 child 入口のオフライン回帰

既存 RouteProof.Tests.ps1 の一時 fixture に **本番 RouteProof.ps1 の byte-identical copy** と、相対 import 先の test 専用 Credentials/CredentialStore.psm1、Probe/R2RouteTransport.psm1 を置く。copy と source の hash 一致を確認。production CLI option / env seam / 関数コピーは増やさない。

- 別 pwsh の -File <copy> -Child を実際に起動。dummy endpoint/run-id/payload/hash/commit、起動 timestamp、余裕のある既存 operation budget を渡す。
- dummy store は正規 Profile=osm / Bucket=osm-artifacts / Endpoint=null / Generation を返し、実 callback に dummy id/secret/Generation/request を渡す。dummy transport は operation と有効残時間を検査し固定 allowlist 観測だけ返す。実 store/DPAPI active/HTTP/実 DLL を fixture から呼べない配置にする。
- 正常 child が exit 0 / stdout 1 JSON / stderr 空、operation/Generation 正しく、dummy secret が全出力にないこと。Endpoint=null を endpoint 引数と比較する退行と、closure の helper 未束縛の退行で失敗する構成にする。
- 同 fixture の不正 profile は exit 3・transport 未呼出し、期限切れは inconclusive・transport 未呼出し。Task.Delay / Thread.Sleep は使わず timestamp/既存 clock 注入。process 待機は有限、timeout 時は当該 test child だけ終了・回収。
- 12 操作を fake server で再実装しない。共有本番ループ＋実 JSON 境界の既存試験を再利用。store 本体の DPAPI/漏洩条件は Credentials.Tests が担う。

### B4: 一度の引渡し

§5 の限定回帰・必要 compile・監査後に実装 head を固定する。B result は変更・理由・オフライン結果・未実行・400 の違反確認有無・版/hash のみ、C 所見を転載しない。新 build の DLL hash を正しく記録し、過去 hash に合わせる build 反復をしない。

ここで SOL の B は終了。live、GO、C' を始めない。未解決 400 だけを理由に B を再開しない。発見 C が凍結契約への具体的違反をまとめて返した場合だけ必要な B 適応をする。

## 5. 検証・レビュー計画

現在の A はコード変更・build・テスト・R2 通信禁止。文書の read-only audit は実装テストと区別する。以下は A3 承認後の B/C 作業。

### 限定回帰の起点

- 新 case 名: child entry offline*、lock rule before evidence*。既存 credential callback*、production operation loop*、production loop completes*、lock rule*、unsigned/清掃/期限 case の変更関連を選ぶ。-Case string[] は PowerShell 内で渡す。0 件は成功にしない。
- request 修正時は R2RouteTransport.Tests の factory / redirect / cap / cancel 回帰。この runner は filter がないので全 7 case を一度実行する。
- 必要 compile: dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore。restore 必要なら記録し依存版を変えない。build を test/live 成功にしない。
- B 完了時 pwsh tools/contract-audit.ps1、文書変更後 pwsh tools/docs-audit.ps1。差戻し確定中に最終判定一式を繰り返さない。

### 判定 C の必須一式

新規セッション・B と異なるモデルで実装 blocker のない head を選ぶ。C は slice base 93d2a1c... から最終実装 head の全 repository diff / stat / name-status を非盲検で保管し、改訂差分を da4e405... からも添える。docs tip を実装 head にしない。C' 用の完全実装 diff は同じ base/head の source/test/config/利用手順を欠落なく含む（現在は tools/Artifacts/** と .gitignore、削除・rename も含む）。全 name-status と照合し、進行文書の除外リストは C 側だけに残す。

1. source と fixed head を照合して同じ Release/net8.0/package 版で一度 build。R2RouteTransport.dll、AWSSDK.Core.dll、AWSSDK.S3.dll、deps.json、関連 source の SHA-256、command/runtime/UTC/base/head を小さな manifest に残す。後の fresh pwsh で loaded assembly location/hash が一致することを確認。
2. Credentials.Tests、RouteProof.Tests（-Case '*'）、R2RouteTransport.Tests 全件。生ログの case 名・件数・exit、build/load、docs-audit、contract-audit が必須。Unity 全 EditMode/PlayMode/build は除外: tools/Artifacts の PS/.NET のみで Unity/asmdef/assets 変更なし、上記全回帰を代替証拠とする。
3. live 前後の source/DLL/依存/Git status を照合し間に build/copy を挟まない。不一致の run を当該 head の判定証拠にしない。JSON 自己申告 base/head だけでは対応の証明にならない。
4. live は **別途明示許可と owner の前後観測引受けが揃うときだけ**。許可時に最終 head、1 run/最大 2 新 key/5 分+回復 30 秒、既設 rule、清掃予定を固定。profile/rule 再設定依頼ではない。AI の管理画面取得経路は未確認。初回確認は C live 開始前、取得不能なら開始せず担当合意へ戻す。
5. 一度の正規 run が 400 なら回復清掃後 inconclusive で停止。同じ実行を再申請・反復し因果を推測しない。許可 tuple なら残り loop、lock 到達時は事後記録を照合。別経路探索・能力否定再現は一次観測と有限追加計画を人間へ返してから扱う。

既知の経路: 所有 Windows ユーザー/既存 profile の認証 PUT/GET、unlocked 回復 DELETE/NoSuchKey は既存原記録で観測済み。新 head の成功、unsigned 拒否、lock 実効性、新前後観測は未確認。未実施を成功にしない。

### C' と記録

GO 候補の必須 evidence が揃うまで C' を開始しない。B/C と異なるモデル・新規セッション、可能なら A 未関与系列を予約。モデル実績は開始後に記録する。

C' 入力は承認・凍結した本 snapshot、所見のない B result、上で定義した判定 C と同じ base/head の完全実装 diff、全必須生結果、非秘密一次原記録、判定 C 前の機械検査のみ。進行台帳、旧 C findings、A2 議論、旧 snapshot 内の C 所見を渡さず、本文にも C 所見を追記しない。docs/handoff 等の履歴差分から所見を逆流させない。進行文書の代わりに列挙済みの clean snapshot を渡す。

artifacts は untracked のまま stage/commit 禁止。既存 tracked artifacts 191 件と過去履歴は削除・書換えしない。旧 bundle 原本は不変、errata は台帳のみ。Phase D で知見を harvest し重複作業文書を通常の前進 commit で整理。

## 6. 分類と停止

- B 適応: 固定条件への具体的違反を、同じ責務・依存・公開面の局所修正で直せる。証拠と回帰を添えて進む。
- 有限な調査で停止: 正規違反を示せず 400 因果不明。B1 上限で止めて未証明を記録。合格化、無期限調査、自動再 run、推測修正は禁止。
- A 再開: request / endpoint / 認証 / status 許可集合、security boundary、責務・依存・公開 API を変える必要がある。候補と同じ存在 object の非公開性を判定できる根拠を人間へ返し、採用前に実装しない。
- 後続: CLI 一般化、Cloud、provider 選定、運用 retention、追加 diagnostics は program の担当 slice へ。将来有益なだけで本 B の blocker にしない。

未決の事実は 400 の原因と実 R2 の能力であり、B が独自設計で埋める欄ではない。本改訂の承認は GO / C' 成功、live 再開許可を意味しない。
