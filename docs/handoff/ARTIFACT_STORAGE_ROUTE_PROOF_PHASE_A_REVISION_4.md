# Artifact Storage Route Proof — Phase A revision 4

## 0. 版と適用範囲

- type: slice の Phase A snapshot。進行台帳: ARTIFACT_STORAGE_ROUTE_PROOF.md。
- revision: route-proof-a-r4 / A3 凍結済み。409 の観測を受けて A を再開し、独立 A2 2件を統合した。
- frozen at: 2026-09-28T17:05:32Z。owner の「GOになるまで」「全部承認なしですすめて」を、新たな409実測に対する限定改訂と新1 runの統合承認として適用する。GO宣言ではない。
- branch: codex/artifact-storage-route-proof / risk: high。
- slice implementation base: 93d2a1c436361ef6ee702096a61087cde55319b4
- 改訂の実装開始点: 31be639a968402e0bdf3f65fd9eda8d0d818dbf3。これは revision 3 の B head。revision 3 C の 409 観測を新 head のGOに遡及転用しない。
- 計画開始時 docs tip: 66bdb5f（revision 3 C の進行記録を含む docs-only commit）。
- 新判定 evidence / C' bundle: 未生成。docs-only commit は実装 head にしない。
- owner: repository owner。局所 B 適応は SOL、新 head の発見・判定 C と C' は独立性条件で選ぶ。
- created: 2026-09-29 JST。expires: Phase D または次の A revision。
- harvest to: tools/Artifacts/README.md、BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md の現況。
- snapshot の生成 UTC / SHA-256 / 固定 commit は進行台帳へ記録する。自己 hash は埋め込まない。公式の参考資料は Cloudflare の [R2 error codes](https://developers.cloudflare.com/r2/api/error-codes/) と [Public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/)（2026-09-28閲覧）。

本文が改訂後の規範入力。凍結済み revision 2 / 3 を書き換えず、必要な契約を以下へ再掲した。変更は locked 上書きで実測した 409 / other / ObjectLockedByBucketPolicy の限定合格。旧 run の C 所見を新 head の判定証拠へ転用しない。

## 1. 問い、最低条件、409 観測の到達点

この slice の問い: 既存 private R2 の osm-artifacts と通常 writer が、所有 Windows ユーザーの別 process 間で synthetic object を同一 byte で往復でき、非認証の同じ存在 key の GET が内容を返さず、限定 prefix の Bucket Lock が同じ writer の上書き・削除を拒否するか。

GO の最低条件は四つ全ての実観測と、同じ implementation head / 実行物への対応である。

1. 既存 DPAPI profile が正常で、writer は対象 bucket の Object Read / Write のみを持ち、private 設定・有限 lock rule の開始条件が成立する。
2. unlocked PUT、別 process の認証 GET、正規 unsigned GET の拒否、上書き・変更 hash GET、DELETE・NoSuchKey 確認が成立する。
3. 同じ Generation の新しい locked key で PUT / GET が成立し、保持内の上書きと DELETE がともに §2.3 の正確な Lock 拒否 tuple、再 GET の原 hash が一致する。実際の前後設定に変化がない。
4. 秘密を出力せず、期限・key 数・清掃制限を守り、source / DLL / 依存 / 生結果の対応と判定必須検証が揃う。

**revision 3 C は正規unsigned GETの400を通過した。** unlocked 7操作と locked PUT/認証GETも成功したが、locked 上書きの `409 / other / ObjectLockedByBucketPolicy` は revision 3 の403条件に合わず停止。locked DELETE拒否・最終原hash GET・C'は未実施。409だけをLockのGOとせず、新しい固定headで12操作と前後設定を検証する。400原因の解明や401/403への変換をGO条件に戻さない。

今回の A は R2 object APIの live run を行わない。owner は既設設定でのオンライン検証を許可済み。新 head のCは§5の1 run予算を新たに固定し、前後設定を自分で取得する。旧runの409結果やA3凍結だけをlive成功と解釈しない。

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
- 同じ Generation の unlocked 上書き・GET・削除の陽性対照が先。locked 操作は新規 PUT からの単調時間が保持内にあるときだけ有効。拒否status単独・権限不足を lock 成功にしない。
- locked 上書きと DELETE の拒否 tuple は同一応答の **403 / forbidden / ObjectLockedByBucketPolicy** または **409 / other / ObjectLockedByBucketPolicy** に限定する。409 は revision 3 C の locked 上書きで実測した値であり、Cloudflare公式エラー表の 403 記載と一致しない。409 全般を合格にせず、正確な S3 Code、EOF確認、timeoutなし、redirectなし、limit未到達、同じ writer Generation、保持内の新しい key と陽性対照を必須とする。上書き・DELETEのそれぞれで許可 tuple が揃い、最後の認証GETが元の byte数・hash と一致した場合だけ Lock 実効性を認める。409の原因やproviderの普遍的なstatus契約は確定しない。
- **起動前に未来の After 観測は存在しない。** -LockRuleJson は Prefix / Enabled / Kind / RetentionSeconds / RuleCount / DateRules / IndefiniteRules / WriterCanConfigure / LifecycleCompatible / BeforeHash の厳密な 10 field。AfterHash を含む入力は拒否する。BeforeHash は事前原記録の SHA-256（小文字 hex 64 桁）。他の型・値域は維持する。
- probe 出力の lockRule.afterHash は null 固定。beforeHash をコピーしない。finalize command、post-state polling、設定取得用権限を増やさない。
- C が実際の run 前後に独立取得された原記録を外側で束ねる。原記録には観察者、取得方法、取得 UTC、bucket/private 設定、全有効 rule、writer scope、lifecycle、残存 key の清掃予定を含める。before UTC <= run 開始 < run 終了 <= after UTC と設定値の同一性を検査する。時刻が異なるファイルの raw hash を同一にする必要はない。追加スクリーンショットの提出を owner の暗黙の作業にしない。
- 取得経路は所有ユーザーの Codex in-app browser に保存された Cloudflare profile の read-only 画面。C が bucket `osm-artifacts` の Settings で Custom Domains、Public Development URL、Object Lifecycle Rules、Bucket Lock Rules の全行を読み、Account API tokens の `R2ObjectReadWrite` 詳細で bucket scope と Item Read/Write のみを読む。各回の画面URL・取得UTC・accessibility DOM の該当生テキスト・画面観察を untracked local bundle に保存する。token値や秘密は取得しない。設定・tokenの編集ボタンを押さない。A3前の疎通は 2026-09-28T16:31:58Z に成功し、非秘密記録を `artifacts/route-proof-phase-a-r3/settings-path-check.txt` に保持した。これは新 run の Before/After 証拠ではない。C 実行時に認証状態が失われて取得不能なら、新 object 作成前に environment-blocked として停止する。
- probe の pass / exit 0 は 12 操作の技術的成功で、C の GO ではない。409の拒否観測だけ、After 証拠不足、設定変更、保持失効・不明なら C は inconclusive、GO 不可。事前 hash の一致で補えない。
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

- tools/Artifacts/Probe/RouteProof.ps1（31be639 head で 548 行、見込み +1〜+8）: policy と 12 操作 orchestration、process/期限/清掃。状態は run 寿命、store と transport に依存し Unity 非依存。`Test-LockRejection` の paired status policyのみ局所変更する。500 行超だが新責務を入れず、判定だけの Helper 分割はしない。
- R2RouteTransport.cs（486 行、既定増分 0）: SDK/HTTP request・cancel・有限 stream 観測、client/stream は操作寿命。秘密保管・GO 判定を持たない。具体的正規 request 欠陥のみ局所修正可。新 transport に置換しない。
- R2RouteTransport.psm1（59 行、増分 0）/ csproj: DLL loading、.NET 8 / AWSSDK.S3 3.7.501.14 の既存境界。package/framework/build 構成を変更しない。
- tools/Artifacts/tests/RouteProof.Tests.ps1（31be639 head で 508 行、見込み +10〜+35）: 実 child 入口、Before/After、限定400と非露出 guard、lock拒否の paired tupleと12操作完走の試験。test 所有の一時 directory/process を case 後に破棄。500 行を超えても同じ runner の case/fixture に留め、汎用 fake R2 や evidence runner を作らない。
- tests/R2RouteTransport.Tests.ps1（187 行、既定増分 0）: 必要な canonical request/read 回帰のみ。本番 factory/reader を使い判定をコピーしない。
- CredentialStore と credentials CLI/tests は変更しない。DLL 対応・原記録は C 手順と小さな manifest の責務。runtime の Git 呼出し/evidence service を追加しない。
- tools/Artifacts/README.md（31be639 head で 73 行、見込み +1〜+3）: 10 field 入力、AfterHash=null、限定400受け入れ、実 After は C の外側で確認することを示す。lockの409限定tupleと最終GET必須を同じ変更で反映し、実能力の成功済みとは書かない。
- 作業文書は現 HANDOFF・本 snapshot・既存汎用 Phase B result を更新する。commit 別 RESULT / FINAL / RERUN を増やさない。旧固定物の訂正は台帳の errata に置く。

行数は目標でない。新責務・状態・依存・公開 API・汎用 module が必要なら A へ返す。小さく見せる分割・機械的改名・参照 0 による削除をしない。

## 4. SOL の局所 B 適応と停止位置

revision 3 の C live は unlocked経路と限定400を通過し、locked PUT/認証GETの後に上書きで `409 / other / ObjectLockedByBucketPolicy` を観測した。source/DLL対応と事前・事後設定の一致を収録したが、status許可集合の違いで停止した。旧runを新条件で再判定したり、409の発生原因を確定したりしない。

SOL は `Test-LockRejection` の同一応答tupleに §2.3 の409/other/Codeを追加する。既存403/forbidden/Codeも維持し、他の409、交差したclass/Code、timeout、EOF不明、redirect、limit到達を拒否する。既存の同一Generation・保持内・陽性対照・最終GETの条件は削らない。RouteProof.Testsに両許可tuple・不許可tupleの回帰を加え、production loopの locked overwrite / DELETE の409観測で12操作完走する試験を行う。READMEの説明も同時に更新する。今回の409対応で R2RouteTransport.cs / SDK / CredentialStore / profile / ruleは変更しない。Cが別の具体的な正規request違反を示した場合だけ、§3のtransport局所修正可否を別途照合する。

限定offline回帰、必要compile、contract/docs audit後に実装headを固定し、既存の汎用 B resultに変更・理由・結果・未実行・hashを追記する。別 result 文書を増やさない。B はlive/GO/C'を行わず終了。C は新 headの発見レビューから開始し、旧 409 run を新headの判定結果へ転用しない。新しい状態・依存・公開API・責務が必要ならAへ戻す。

## 5. 検証・レビュー計画

現在の A はコード変更・build・テスト・R2 object API通信を行わない。31be639のrevision 3 C原記録は現況の根拠として保持し、新条件の成功証拠と区別する。以下は A3 後の局所 B 適応 / 発見 C / 判定 C の作業。

### 限定回帰の起点

- 差し戻し中の起点は lock rejection*、lock rule*、production operation loop*、production loop completes*、unsigned 400 / timeout / 清掃 / 期限 case の変更関連を選ぶ。特に locked overwrite と locked DELETE の409許可tuple、403維持、交差class/Code・別409・timeout・EOF不明の非合格、最終原hash GETが成功条件であることを確認する。-Case string[] は PowerShell 内で渡す。0 件は成功にしない。
- request 修正時は R2RouteTransport.Tests の factory / redirect / cap / cancel 回帰。この runner は filter がないので全 7 case を一度実行する。
- 必要 compile: dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore。restore 必要なら記録し依存版を変えない。build を test/live 成功にしない。
- 局所 B 適応後は pwsh tools/contract-audit.ps1、文書変更後 pwsh tools/docs-audit.ps1。差戻し確定中に最終判定一式を繰り返さない。

### 判定 C の必須一式

新規セッション・B と異なるモデルで実装 blocker のない head を選ぶ。C は slice base 93d2a1c... から最終実装 head の全 repository diff / stat / name-status を非盲検で保管し、31be639からの局所差分も識別する。docs tip を実装 head にしない。C' 用の完全実装 diff は同じ base/head の source/test/config/利用手順を欠落なく含む（現在は tools/Artifacts/** と .gitignore、削除・rename も含む）。全 name-status と照合し、進行文書の除外リストは C 側だけに残す。

1. source と fixed head を照合して同じ Release/net8.0/package 版で一度 build。R2RouteTransport.dll、AWSSDK.Core.dll、AWSSDK.S3.dll、deps.json、関連 source の SHA-256、command/runtime/UTC/base/head を小さな manifest に残す。後の fresh pwsh で loaded assembly location/hash が一致することを確認。
2. Credentials.Tests、RouteProof.Tests（-Case '*'）、R2RouteTransport.Tests 全件。生ログの case 名・件数・exit、build/load、docs-audit、contract-audit が必須。Unity 全 EditMode/PlayMode/build は除外: tools/Artifacts の PS/.NET のみで Unity/asmdef/assets 変更なし、上記全回帰を代替証拠とする。
3. live 前後の source/DLL/依存/Git status を照合し間に build/copy を挟まない。不一致の run を当該 head の判定証拠にしない。JSON 自己申告 base/head だけでは対応の証明にならない。
4. owner は既設設定でのオンライン検証を許可済み。C は新しい最終 head に対し **新runを1回** / 最大2新key / 5分+回復30秒、既設rule、清掃予定を固定する。revision 3のlocked keyを上書き・削除しない。profile/rule再設定をしない。事前・事後の非秘密設定を §2.3 の確認済みUI経路で自分で取得する。認証状態が失われていれば新objectを作らず environment-blocked と記録する。ownerへ同じスクリーンショットの再提出を求めない。
5. §2.2 の限定400 tupleは他のguardが揃う場合に通過する。locked上書き・DELETEのどちらかが§2.3許可tupleでも、最終認証GETの原hash一致までGO候補にしない。正規statusを403に変えるためのrequest探索・同一run反復をしない。lock到達時は事後記録と保持時間を照合。別経路探索・能力否定再現は一次観測と有限追加計画を人間へ返してから扱う。

既知の経路: 31be639 の一次記録で認証PUT/GET、unlocked上書き/DELETE/NoSuchKey、限定400、locked PUT/認証GET、locked上書きの409/Code、前後設定同一は観測済み。`artifacts/route-proof-phase-c-31be639/` のbundle manifest `files.sha256` SHA-256は `74CE1413AB3DA337814082333B8978F037B9180CEFDC4EB21D38E89ABFEC7A18`。旧C収録scriptのsource SHA一覧漏れは台帳§11どおり保持し、新Cでは実行直前・直後のsource/実行物hashを完全収録する。新headでの12操作完走、locked DELETE拒否、最終原hash GET、lock実効性、新前後観測は未確認。未実施を成功にしない。

### C' と記録

GO 候補の必須 evidence が揃うまで C' を開始しない。B/C と異なるモデル・新規セッション、可能なら A 未関与系列を予約。モデル実績は開始後に記録する。

C' 入力は承認・凍結した本 snapshot、所見のない B result、上で定義した判定 C と同じ base/head の完全実装 diff、全必須生結果、非秘密一次原記録、判定 C 前の機械検査のみ。進行台帳、旧 C findings、A2 議論、旧 snapshot 内の C 所見を渡さず、本文にも C 所見を追記しない。docs/handoff 等の履歴差分から所見を逆流させない。進行文書の代わりに列挙済みの clean snapshot を渡す。

artifacts は untracked のまま stage/commit 禁止。既存 tracked artifacts 191 件と過去履歴は削除・書換えしない。旧 bundle 原本は不変、errata は台帳のみ。Phase D で知見を harvest し重複作業文書を通常の前進 commit で整理。

## 6. 分類と停止

- B 適応: 固定条件への具体的違反を、同じ責務・依存・公開面の局所修正で直せる。証拠と回帰を添え、新 head で C をやり直す。
- 原因未特定でも前進: 正規条件への違反がなく限定400 tupleと非露出guardを満たせば、400の因果調査を打ち切って12操作へ進む。lockの409は§2.3の正確なtupleと最終GETまで揃えたときだけ受け入れる。status単独を無条件合格にしない。無期限調査、自動再run、推測修正は禁止。
- A 再開: request / endpoint / 認証 / ここで許可したstatus集合、security boundary、責務・依存・公開APIをさらに変える必要がある。採用前に実装しない。
- 後続: CLI 一般化、Cloud、provider 選定、運用 retention、追加 diagnostics は program の担当 slice へ。将来有益なだけで本 B の blocker にしない。

未決の事実は400と409の因果、実R2のBucket Lock実効性である。両statusの因果解明は本sliceのGO条件から外す。GOは新headの判定Cと独立C'が§1の全条件を確認してからだけ宣言する。本改訂の凍結はその成功を意味しない。
