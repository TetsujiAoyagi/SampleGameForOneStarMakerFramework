# Artifact Storage Route Proof — Phase A revision 3

## 0. 版と適用範囲

- type: slice の Phase A snapshot。進行台帳: ARTIFACT_STORAGE_ROUTE_PROOF.md。
- revision: route-proof-a-r3 / A3 凍結済み。A2 2件の独立レビュー指摘を統合した規範本文。
- frozen at: 2026-09-28T16:34:42Z。owner の「Aをそのように直してA3凍結して、400受け入れます」を統合承認とする。C / C' の完了や GO の宣言ではない。
- branch: codex/artifact-storage-route-proof / risk: high。
- slice implementation base: 93d2a1c436361ef6ee702096a61087cde55319b4
- 改訂の実装開始点: f256252f31bd90c308b63f97a301539ad7e1cfc8。これは A3 より前に作られた source-first 候補であり、旧 B head 8c1793ec507205d5134da5a9fd22b6d12c33cb56 の live 結果を遡及して合格にしない。
- 計画開始時 docs tip: f256252f31bd90c308b63f97a301539ad7e1cfc8
- 新判定 evidence / C' bundle: 未生成。docs-only commit は実装 head にしない。
- owner: repository owner。旧 B は SOL が完了。新 head の発見・判定 C と C' は独立性条件で選ぶ。
- created: 2026-09-29 JST。expires: Phase D または次の A revision。
- harvest to: tools/Artifacts/README.md、BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の現況。
- snapshot の生成 UTC / SHA-256 / 固定 commit は進行台帳へ記録する。自己 hash は埋め込まない。公式の参考資料は Cloudflare の [R2 error codes](https://developers.cloudflare.com/r2/api/error-codes/) と [Public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/)（2026-09-28閲覧）。

本文が改訂後の規範入力。凍結済み revision 2 を書き換えず、必要な契約を以下へ再掲した。変更は観測済み 400 の限定合格と source-first 実装の扱い。旧 run の C 所見を新 head の判定証拠へ転用しない。

## 1. 問い、最低条件、source-first 候補の到達点

この slice の問い: 既存 private R2 の osm-artifacts と通常 writer が、所有 Windows ユーザーの別 process 間で synthetic object を同一 byte で往復でき、非認証の同じ存在 key の GET が内容を返さず、限定 prefix の Bucket Lock が同じ writer の上書き・削除を拒否するか。

GO の最低条件は四つ全ての実観測と、同じ implementation head / 実行物への対応である。

1. 既存 DPAPI profile が正常で、writer は対象 bucket の Object Read / Write のみを持ち、private 設定・有限 lock rule の開始条件が成立する。
2. unlocked PUT、別 process の認証 GET、正規 unsigned GET の拒否、上書き・変更 hash GET、DELETE・NoSuchKey 確認が成立する。
3. 同じ Generation の新しい locked key で PUT / GET が成立し、保持内の上書きと DELETE がともに 403 / ObjectLockedByBucketPolicy、再 GET の原 hash が一致する。実際の前後設定に変化がない。
4. 秘密を出力せず、期限・key 数・清掃制限を守り、source / DLL / 依存 / 生結果の対応と判定必須検証が揃う。

**source-first 修正 commit f256252 は新A3より先行する候補である。** 旧 B 完了と旧 C の400観測は記録済みだが、新条件の判定 C / C' は未実施。400 の原因解明や 401/403 への変換を GO 条件にせず、§2.2 の限定条件を満たしたとき先へ進む。

今回の A は R2 通信をしない。owner は既設設定でのオンライン検証を許可済みだが、許可の対象・予算・前後観測は §5 に従って新しい固定 head の C 実行時に確認する。A3 の凍結だけを live 成功と解釈しない。

対象外: 汎用 Artifact CLI / publish / fetch / safe extraction、実 Evidence / Build upload、Cloud、provider 変更、Worker / proxy / public domain、鍵再登録・rotation、credential schema、Unity / asmdef、SDK 更新、汎用 evidence framework。これらと代替 request 探索の所有者は BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の最小 Artifact CLI / provider 再選定 Phase A。未証明の R2 を前提に後続実装を始めない。

## 2. 受け入れ契約

### 2.1 資格情報・宛先・fixture

- profile は既存 osm、bucket は osm-artifacts、credential v1 の Endpoint は null、Generation は小文字 hex 32 桁。正常 active を再利用し、set / replace / ACL 自動修復・再入力をしない。
- endpoint は \Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z のみ。path / query / userinfo / port / 別 jurisdiction host は拒否。実 account ID は既存の非秘密記録から読み、文書に複製しない。
- 秘密は既存 store の同一 process callback 内だけ。chat / 引数 / env / Git / ログへ載せない。store は秘密の寿命・安全な戻り値の検査を所有する。
- remote key は probe/unlocked/<run-id>/object.txt と probe/locked/<run-id>/object.txt の最大 2 個/run。run-id は小文字 hex 32 桁。fixture は ASCII OSM-ROUTE-PROOF:<run-id>:original と :changed、各 1〜1024 byte。送信前に hash を算出。既存 key / 実 payload を使わない。

### 2.2 正規 unsigned GET と 400

- URI は account endpoint + /osm-artifacts/ + key の各 segment の escape、method GET。Authorization / 署名 query / 対象変更 query なし。redirect 無効。同じ run/key の直前の認証 GET で byte 数・hash・Generation を照合する。
- 合格 tuple は 401 / unauthorized / Unauthorized、403 / forbidden / AccessDenied、または **400 / other / InvalidArgument**。同一応答の HTTP status / status class / S3 Code を組で照合する。EOF 到達、全体 hash が object と異なること、取得済みの期待長 prefix hash も異なることを要する。直前の認証 GET と source / DLL の対応も必須。
- 400 合格の意味は「この正規 unsigned GET は存在する同一 object の byte を返さなかった」という限定された実観測である。認証・認可処理が 400 を出したという因果、Cloudflare の全アクセス経路の非公開性、400 原因の特定を主張しない。公式エラー表は欠落認証を 401 / Unauthorized、権限不足を 403 / AccessDenied と説明し、今回の 400 / InvalidArgument を認証拒否として定義していない。private の別経路は §2.3 の設定原記録で確認する。
- object と同じ先頭 N byte の hash は後続 byte・EOF・status にかかわらず内容露出の一次観測。N 未満の prefix hash を作らず、部分応答を拒否証拠にしない。
- 本文は 8192 byte + 超過検出 1 byte まで。8193 byte 目、EOF 未確認、timeout、redirect、429 / 5xx、DNS / TLS / proxy、404 / NoSuchKey、SignatureDoesNotMatch、認証 GET 失敗、許可外 Code は非合格。本文・任意 header・SDK 例外は保存しない。
- 判定 C で上の正規条件・timeout 非合格・同一応答照合に具体的違反を示せた場合だけ、同じ責務内で局所修正して新 head で検証し直す。代替 host / path / query / method / 認証や、401/403 を得るための header 探索は含めない。

### 2.3 Bucket Lock と前後証拠

- 既設 rule を再設定しない。全有効 rule は probe/locked/ の Age がちょうど 1 件、900〜86400 秒。空 / 他 / unlocked prefix、Date / Indefinite、保持不明を拒否する。writer に設定権限がなく lifecycle が妨げないことを C の非秘密観測で確認する。
- private 設定は Public Development URL（r2.dev）が無効、Custom Domains が空の両方。C が試験前後の非秘密原記録を取得・照合する。提出済み画像2件は旧 run の事前状態を示し、別 head の事後状態には流用しない。前に未成立・未確認なら environment-blocked で開始せず、後で変化・不明なら inconclusive、どちらも GO 不可。S3 GET の非露出で別公開経路の確認を代用しない。
- 同じ Generation の unlocked 上書き・GET・削除の陽性対照が先。locked 操作は新規 PUT からの単調時間が保持内にあるときだけ有効。403 単独・権限不足を lock 成功にしない。
- **起動前に未来の After 観測は存在しない。** -LockRuleJson は Prefix / Enabled / Kind / RetentionSeconds / RuleCount / DateRules / IndefiniteRules / WriterCanConfigure / LifecycleCompatible / BeforeHash の厳密な 10 field。AfterHash を含む入力は拒否する。BeforeHash は事前原記録の SHA-256（小文字 hex 64 桁）。他の型・値域は維持する。
- probe 出力の lockRule.afterHash は null 固定。beforeHash をコピーしない。finalize command、post-state polling、設定取得用権限を増やさない。
- C が実際の run 前後に独立取得された原記録を外側で束ねる。原記録には観察者、取得方法、取得 UTC、bucket/private 設定、全有効 rule、writer scope、lifecycle、残存 key の清掃予定を含める。before UTC <= run 開始 < run 終了 <= after UTC と設定値の同一性を検査する。時刻が異なるファイルの raw hash を同一にする必要はない。追加スクリーンショットの提出を owner の暗黙の作業にしない。
- 取得経路は所有ユーザーの Codex in-app browser に保存された Cloudflare profile の read-only 画面。C が bucket `osm-artifacts` の Settings で Custom Domains、Public Development URL、Object Lifecycle Rules、Bucket Lock Rules の全行を読み、Account API tokens の `R2ObjectReadWrite` 詳細で bucket scope と Item Read/Write のみを読む。各回の画面URL・取得UTC・accessibility DOM の該当生テキスト・画面観察を untracked local bundle に保存する。token値や秘密は取得しない。設定・tokenの編集ボタンを押さない。A3前の疎通は 2026-09-28T16:31:58Z に成功し、非秘密記録を `artifacts/route-proof-phase-a-r3/settings-path-check.txt` に保持した。これは新 run の Before/After 証拠ではない。C 実行時に認証状態が失われて取得不能なら、新 object 作成前に environment-blocked として停止する。
- probe の pass / exit 0 は 12 操作の技術的成功で、C の GO ではない。After 証拠不足、設定変更、保持失効・不明なら C は inconclusive、GO 不可。事前 hash の一致で補えない。
- owner の既提出原画像を受理し、C' に画像再評価までは要求しない。C は新 run に必要な前後記録の取得と整合検査を担当する。既提出画像を撮影時刻不明のまま新 run の直前・直後証拠に転用しない。

### 2.4 実行・清掃・判定

- 12 操作: unlocked PUT → 認証 GET → unsigned GET → 変更 PUT → 変更 GET → DELETE → NoSuchKey GET、locked 新規 PUT → 認証 GET → 変更 PUT 拒否 → DELETE 拒否 → 原 hash GET。1 child = 1 操作、本番ループは一つ。
- operation は子起動・import・credential 読取・header/body EOF を含め 30 秒以内。parent の終了・stdout/stderr 回収は合計 45 秒以内、残時間共有。process tree 終了と両 pipe EOF を確認。run は 5 分。SDK 隠れ retry / redirect を禁止。
- 失敗後 unlocked PUT が送られた可能性があれば、child 終了確認後だけ別枠合計 30 秒で DELETE + NoSuchKey GET。起動・待機・pipe を含む。204 単独、別 object hash だけでは removed にしない。終了未確認 child と清掃を競合させない。清掃成功でも run を合格に変えない。
- locked key の保持前追加清掃をせず、key / rule / 期限 / 清掃予定を記録。保持未証明の残存物は unconfirmed。自動清掃機構を作らない。
- 外部 JSON は既存 allowlist（result / phase / operation / 検証済み key / 期待・実測 hash・byte / prefix hash / EOF・上限 / status・Code / Generation / lockRule / UTC / cleanup / base・head）を維持。未知 Code は [A-Za-z][A-Za-z0-9]{0,63} の全体一致名のみ。raw message / body / header / 任意 property を拒否。
- exit は pass 0、provider-capability-failure 2、environment-blocked 3、inconclusive 4。新 status enum は作らない。開始条件不足は environment-blocked、結果・清掃不明は inconclusive。「実経路未証明」は報告文であって新 runtime 値ではない。
- unlocked 上書きの 429 / TooManyRequests は 1 秒以上後に一度だけ再試行し、再び429なら inconclusive。locked 上書きの成功後は保持有効を再確認し、1 秒以上後に一度だけ再試行して、原 hash からの変化を再 GET で確認するまで能力否定にしない。全体期限は伸ばさず、400 retry や新たな自動再現は足さない。DELETE の success 単独も能力否定にしない。
- provider 能力否定の CONDITIONAL ACCEPT は、有効な開始条件・陽性対照の下で、内容露出なら別 run / 新 key で再現、lock なら保持内の禁止操作の再現と変更 hash / 削除を確認した実証に限る。現在は未到達。追加 live 再現は別途許可が必要。C が生結果から判断し、自動再現機構は増やさない。
- GO は §1 と判定 C / C' が揃った場合のみ。§2.2 の400合格なら unsigned GET を通過して12操作とBucket Lock実効性まで判定する。400の原因未特定それ自体はGOを妨げない。CONDITIONAL ACCEPT は R2 採用成功でなく program 再選定への否定材料。inconclusive / environment-blocked / 時間切れは slice 完了ではない。

## 3. 責務・規模・配置

- tools/Artifacts/Probe/RouteProof.ps1（source-first head で 548 行）: policy と 12 操作 orchestration、process/期限/清掃。状態は run 寿命、store と transport に依存し Unity 非依存。f256252 に限定400 tupleの判定が追加された。500 行超だが新責務を入れず、判定だけの Helper 分割はしない。C では timeout・EOF・同一応答・hash guards の実装適合を確認する。
- R2RouteTransport.cs（486 行、既定増分 0）: SDK/HTTP request・cancel・有限 stream 観測、client/stream は操作寿命。秘密保管・GO 判定を持たない。具体的正規 request 欠陥のみ局所修正可。新 transport に置換しない。
- R2RouteTransport.psm1（59 行、増分 0）/ csproj: DLL loading、.NET 8 / AWSSDK.S3 3.7.501.14 の既存境界。package/framework/build 構成を変更しない。
- tools/Artifacts/tests/RouteProof.Tests.ps1（source-first head で 502 行）: 実 child 入口、Before/After、限定400と非露出 guard の試験。test 所有の一時 directory/process を case 後に破棄。500 行を超えても同じ runner の case/fixture に留め、汎用 fake R2 や evidence runner を作らない。
- tests/R2RouteTransport.Tests.ps1（187 行、既定増分 0）: 必要な canonical request/read 回帰のみ。本番 factory/reader を使い判定をコピーしない。
- CredentialStore と credentials CLI/tests は変更しない。DLL 対応・原記録は C 手順と小さな manifest の責務。runtime の Git 呼出し/evidence service を追加しない。
- tools/Artifacts/README.md（source-first head で 73 行）: 10 field 入力、AfterHash=null、限定400受け入れ、実 After は C の外側で確認することを示す。README の記述は live 成功証拠ではない。
- 作業文書は現 HANDOFF・本 snapshot・既存汎用 Phase B result を更新する。commit 別 RESULT / FINAL / RERUN を増やさない。旧固定物の訂正は台帳の errata に置く。

行数は目標でない。新責務・状態・依存・公開 API・汎用 module が必要なら A へ返す。小さく見せる分割・機械的改名・参照 0 による削除をしない。

## 4. source-first 修正と今後の停止位置

旧 B は 8c1793e で終了し、旧 C は正規 GET の 400 / InvalidArgument で inconclusive と記録した。その後 f256252 で RouteProof.ps1、RouteProof.Tests.ps1、README.md を先に修正した。これは新 A3 より前の候補であり、新 A3 の追認だけで判定 C 合格や旧 run の再分類はしない。

新 A3 の実装対象は、§2.2 の限定400 tupleと非露出 guardを source-first 候補で満たすことである。**f256252 の Test-UnsignedPrivacy は TimedOut=true / EofConfirmed=true の矛盾 observation を明示拒否しない既知違反がある。** 発見 C でその実装箇所を確認し、`-not $Observation.TimedOut` の局所 B 適応と誤合格防止回帰を必須として、新 head を固定し直す。redirect、本文上限、EOF不明、object全体または期待長prefix一致も具体的に照合し、違反があれば同じ責務内で修正と回帰を行う。400原因の再調査・代替 request 探索はせず、blockerを解消した head で判定 C へ進む。新しい状態・依存・公開 API・責務が必要なら A に戻す。

既存の Before/After 分離、実 child 入口の byte-identical copy とテスト専用 module、source/DLL/依存対応は旧 B 結果を出発点に固定 head で再検証する。新しい production 用テスト API、汎用 fake R2、汎用 CLI / Helper / evidence framework は作らない。旧 B result は所見のない実装記録として保持し、必要な局所修正だけ同じ汎用 result に追記する。commit 別 result を増やさない。

## 5. 検証・レビュー計画

現在の A はコード変更・build・テスト・R2 通信を行わない。f256252 の source-first 修正は既存候補として固定し、文書の read-only audit と後の C 検証を区別する。以下は A3 後の発見 C / 必要なら局所 B 適応 / 判定 C の作業。

### 限定回帰の起点

- 差し戻し中の起点は child entry offline*、lock rule before evidence*、credential callback*、production operation loop*、production loop completes*、lock rule*、unsigned 400 / 内容露出 / EOF / timeout / 清掃 / 期限 case の変更関連を選ぶ。特に `TimedOut=true / EofConfirmed=true / 400-other-InvalidArgument` が非合格になる回帰を追加・実行する。-Case string[] は PowerShell 内で渡す。0 件は成功にしない。
- request 修正時は R2RouteTransport.Tests の factory / redirect / cap / cancel 回帰。この runner は filter がないので全 7 case を一度実行する。
- 必要 compile: dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore。restore 必要なら記録し依存版を変えない。build を test/live 成功にしない。
- 局所 B 適応時は pwsh tools/contract-audit.ps1、文書変更後 pwsh tools/docs-audit.ps1。差戻し確定中に最終判定一式を繰り返さない。

### 判定 C の必須一式

新規セッション・B と異なるモデルで実装 blocker のない head を選ぶ。C は slice base 93d2a1c... から最終実装 head の全 repository diff / stat / name-status を非盲検で保管し、source-first f256252 の差分も識別する。docs tip を実装 head にしない。C' 用の完全実装 diff は同じ base/head の source/test/config/利用手順を欠落なく含む（現在は tools/Artifacts/** と .gitignore、削除・rename も含む）。全 name-status と照合し、進行文書の除外リストは C 側だけに残す。

1. source と fixed head を照合して同じ Release/net8.0/package 版で一度 build。R2RouteTransport.dll、AWSSDK.Core.dll、AWSSDK.S3.dll、deps.json、関連 source の SHA-256、command/runtime/UTC/base/head を小さな manifest に残す。後の fresh pwsh で loaded assembly location/hash が一致することを確認。
2. Credentials.Tests、RouteProof.Tests（-Case '*'）、R2RouteTransport.Tests 全件。生ログの case 名・件数・exit、build/load、docs-audit、contract-audit が必須。Unity 全 EditMode/PlayMode/build は除外: tools/Artifacts の PS/.NET のみで Unity/asmdef/assets 変更なし、上記全回帰を代替証拠とする。
3. live 前後の source/DLL/依存/Git status を照合し間に build/copy を挟まない。不一致の run を当該 head の判定証拠にしない。JSON 自己申告 base/head だけでは対応の証明にならない。
4. owner は既設設定でのオンライン検証を許可済み。C は最終 head、1 run/最大 2 新 key/5 分+回復 30 秒、既設 rule、清掃予定を固定する。profile/rule 再設定をしない。事前・事後の非秘密設定を §2.3 の確認済み UI 経路で自分で取得する。認証状態が失われていれば新 object を作らず environment-blocked と記録する。owner へ同じスクリーンショットの再提出を求めない。
5. 一度の正規 run で §2.2 の限定400 tuple が出たら、他の guard が揃う場合は残り loop へ進む。400の因果を推測せず、401/403に変えるための再申請・反復をしない。lock 到達時は事後記録を照合。別経路探索・能力否定再現は一次観測と有限追加計画を人間へ返してから扱う。

既知の経路: 所有 Windows ユーザー/既存 profile の認証 PUT/GET、unlocked 回復 DELETE/NoSuchKey、400 / InvalidArgument / EOF と object hash不一致は 8c1793e の一次原記録で観測済み。新 head での12操作、lock 実効性、新前後観測は未確認。既提出画像は旧時点の開始状態の確認経路であり、新 run の時刻付き前後記録ではない。未実施を成功にしない。

### C' と記録

GO 候補の必須 evidence が揃うまで C' を開始しない。B/C と異なるモデル・新規セッション、可能なら A 未関与系列を予約。モデル実績は開始後に記録する。

C' 入力は承認・凍結した本 snapshot、所見のない B result、上で定義した判定 C と同じ base/head の完全実装 diff、全必須生結果、非秘密一次原記録、判定 C 前の機械検査のみ。進行台帳、旧 C findings、A2 議論、旧 snapshot 内の C 所見を渡さず、本文にも C 所見を追記しない。docs/handoff 等の履歴差分から所見を逆流させない。進行文書の代わりに列挙済みの clean snapshot を渡す。

artifacts は untracked のまま stage/commit 禁止。既存 tracked artifacts 191 件と過去履歴は削除・書換えしない。旧 bundle 原本は不変、errata は台帳のみ。Phase D で知見を harvest し重複作業文書を通常の前進 commit で整理。

## 6. 分類と停止

- B 適応: 固定条件への具体的違反を、同じ責務・依存・公開面の局所修正で直せる。証拠と回帰を添え、新 head で C をやり直す。
- 原因未特定でも前進: 正規条件への違反がなく限定400 tupleと非露出 guard を満たせば、400の因果調査を打ち切って12操作へ進む。400自体を無条件合格にしない。無期限調査、自動再 run、推測修正は禁止。
- A 再開: request / endpoint / 認証 / status 許可集合、security boundary、責務・依存・公開 API をさらに変える必要がある。採用前に実装しない。
- 後続: CLI 一般化、Cloud、provider 選定、運用 retention、追加 diagnostics は program の担当 slice へ。将来有益なだけで本 B の blocker にしない。

未決の事実は 400 の原因と実 R2 の Bucket Lock 実効性である。400原因は本 slice の GO 条件から外す。GO は新 head の判定 C と独立 C' が §1 の全条件を確認してからだけ宣言する。本改訂の凍結はその成功を意味しない。
