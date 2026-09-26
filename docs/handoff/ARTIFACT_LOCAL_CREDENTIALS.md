# ローカル資格情報管理 — 段1（スライス0）HANDOFF

## 0. メタデータ

- type: slice
- status: D — B完了、最終版のC/C'はGO。人間のマージ判断待ち。マージ未実施。
- branch: codex/artifact-local-credentials
- implementation base commit: 0c46a81ae6f28627a6b84bd4232ad2845452f4d6（PR #76の第2回レビュー対応の起点。初回実装の起点はae4b6e9b87c6690b16b83ca7d37af53def46f652）。
- implementation head commit: 9d2cc5744d943e76ad879fde272daddec70ffbf0。以後の本HANDOFFへの結果追記だけのコミットとは区別する。現在のC/C'結果は§11、第1回レビュー対応は§10、初回実装は§7/8に記録する。
- risk: high — 資格情報、ファイル権限、失敗時の拒否と置換を扱う。検証にはダミー値だけを使う。
- owner: OSM保守担当
- created: 2026-09-26
- expires: 2026-12-26
- harvest to: 現在の利用案内は docs/README.md、運用契約は tools/Artifacts/README.md。Phase Dのマージ時に必要な知見を反映し、このHANDOFFを削除する。
- A2レビュー入力: A1初稿のSHA-256 D01C6AD1B5D628A3227AA60C0A0522847B3D52E6C3AE8A013F2A15CFC518BCB4
- Phase A snapshot: 凍結コミット bef89538a30c9fbabefd6b8315cd9cd63bcf8341 の本ファイル。保存先 C:/Users/void/AppData/Local/Temp/osm-credentials-20260926/phase-a.md、生成時刻2026-09-26 11:36 JST、SHA-256 C9242D610D6D6716D0297123371CA161A1CD78AEB2F8BFE5B76C95285457DC84。
- 初回Phase B result snapshot: C:/Users/void/AppData/Local/Temp/osm-credentials-20260926/phase-b.md。最終版生成2026-09-26 12:42 JST、SHA-256 1AB8A8339838BC2963B159CED58B9E3CFA3F6769CD89832C627498CD54876ABA。同一の固定コピーを初回ZIPに収録。
- 初回evidence bundle / C' blind bundle: C:/Users/void/AppData/Local/Temp/osm-credentials-20260926/head-3a5126a2480d76ebbbe4c5c0ada538cea9172698/blind-bundle.zip。生成2026-09-26T12:47:10.9511680+09:00、SHA-256 3E121DA9714817068A7D9BF862B3808A33431DF6D57A2A6BFFACA88A68D4BC81。
- 取得・保持: 上記ローカルZIPをコピーし、SHA-256を照合して展開する。blind-manifest.txtの全16項目を照合する。保持期限は2026-12-26、保持担当はOSM保守担当。現時点ではローカル一時領域だけの保存であり、永続・遠隔保管は未実施。期限前の削除はしない。R2への転送やGitへのpayload追加は行っていない。
- 初回Phase C判定原記録: 初回head別ディレクトリの c-private/c-decision-ja.txt、SHA-256 D864E0BF5B54A678538B4CE2A3B97F5FAB191CA9376B7F93BF3485041D5D912F。blind ZIPの外に保存。
- 初回Phase C'最終結果: C:/Users/void/AppData/Local/Temp/osm-credentials-20260926/cprime-final-ja.md。生成2026-09-26 12:51 JST、SHA-256 DACB03C7B7525751F448D48F2281ECDA5058E95FAF7D80772FFD8A69E4A108DE。
- 第1回レビュー対応のPhase B result snapshot: C:/Users/void/AppData/Local/Temp/osm-pr76-noninteractive/phase-b.md。生成2026-09-26 13:32 JST、SHA-256 01FDD00916AFB3471A32A9BF6602770134013BD7ACC5286F9F071C7D8F92BC47。
- 第1回レビュー対応のevidence bundle / C' blind bundle: C:/Users/void/AppData/Local/Temp/osm-pr76-noninteractive/head-aa3f7ad915a75bd6c8ca23d9da4e7cf3c6512bb3/bundle/blind-audit-aa3f7ad915a75bd6c8ca23d9da4e7cf3c6512bb3.zip。生成2026-09-26T04:47:57.9943471Z、SHA-256 0DA9E00B72963FEA680ED1878DDD5B3EC3E0563A4ACB4DAF878DB4385BAAD0CC。
- 第1回レビュー対応の取得・保持: 上記ZIPをコピーし、SHA-256照合後に展開する。manifest.sha256のSHA-256は381A5FF27CA7E090324CA1DCB364928AE1A64D6F485AD28A890441FBBB1AED71、収録26ファイルを照合する。保持期限・担当・ローカル保存の制約は初回と同じ。
- 第1回レビュー対応のPhase C原記録: 第1回のhead別ディレクトリの c-private/phase-c-result.md、SHA-256 3D646458E01458DF2F400C771D8CBDF4B8B16E1809EE15564FC44D439704F31D。blind ZIPの外に保存。
- 第1回レビュー対応のPhase C'結果: C:/Users/void/AppData/Local/Temp/osm-pr76-noninteractive/cprime-aa3f7ad-result-ja.md、SHA-256 F18D55166D110DB0FF736AD08D2EDA663F7CB6E5C401B8FA7ED44B160F77CE30。
- 現在のPhase B result snapshot: C:/Users/void/AppData/Local/Temp/osm-pr76-double-dash/phase-b.md。生成2026-09-26 14:11 JST、SHA-256 38ED458F530D2E20C18DFDDFF12BD591720094AE164DADF88B2ABDAE9CFF9C32。
- 現在のevidence bundle / C' blind bundle: C:/Users/void/AppData/Local/Temp/osm-pr76-double-dash/head-9d2cc5744d943e76ad879fde272daddec70ffbf0/blind-audit-bundle.zip。生成2026-09-26T05:24:51.8189195Z、SHA-256 20BCE444D360A123EE9022D6A9177CB0AABA50CB203A1DFDFB78DC421DDBA4BC。
- 現在の取得・保持: ZIPをコピーしSHA-256照合後に展開する。manifestのSHA-256はB1E9FFFE1C53EA1798C6D8F4ACDB97DCBB8BF12C3849E403963C88597A39390C、payload 28件を照合する。保持期限2026-12-26、保持担当OSM保守担当、ローカル一時保存のみという制約は変わらない。凍結Aは同一コピー、Bは中立結果、完全revision差分と最終関連sourceを収録し、可変HANDOFF・PR所見・過去所見・C所見は除外した。
- 現在のPhase C原記録: 現在のhead別ディレクトリの c-private/phase-c-result.md、SHA-256 11BD84ECD3C10C57965138660A7C8FD7E474ABDC7ED239818866CDA957D764C9。blind ZIPの外に保存。
- 現在のPhase C'結果: C:/Users/void/AppData/Local/Temp/osm-pr76-double-dash/cprime-9d2cc57-result-ja.md、SHA-256 97132F448DFAFDFC1655B7A13AA4D218D1B1F278D406D86196FD169745460E21。

ユーザーの指示により、本HANDOFF、README、コードコメントを日本語にする。以下は凍結済み条件の日本語訳であり、条件の追加・緩和ではない。英語で保存した凍結snapshotは、過去の入力を検証できるよう変更しない。

## 1. 目的・開始時点の状況・対象外（A0）

ユーザーの「ローカル段1」はprogramの「スライス0」、「段2」は「スライス1」を指す。番号の違いで対象範囲を変えない。

docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md のr1は、Windowsローカルの親資格情報について保存先・責務・セキュリティ境界を凍結したもので、個別実装の凍結ではなかった。A0時点では資格情報ストアとartifactコマンドは未実装。リポジトリは公開、R2のosm-artifactsバケットは非公開だが、オブジェクト資格情報とR2接続は未検証だった。本スライスは、**ダミーのS3鍵ペアだけ**でWindowsローカルの登録・保管・置換・削除を成立させる。実R2鍵の有効性は主張しない。

対象外は、実トークンの発行・入力、Cloudflare設定変更、R2呼出し、実Evidence・実ビルドのアップロード、クラウド向けgrant、梱包・転送・署名コマンド、Unity/BuildSystem連携、OneDriveへの保存、公開配布。スライス1が実R2認証とread/write/read-back、クラウド側grantと外向き接続を担当する。スライス2がサーバー側の確定オブジェクト保護とartifact CLI契約を担当し、その保護が成立するまで実payloadをアップロードしない。未受領のprogramレビュー第4コメントはprogramの改訂で扱う。

引き継ぐ制約は次のとおり。

- DPAPIはCurrentUserのみ。暗号化ファイルはWindowsのローカルユーザー領域 OneStarMaker/Artifacts/credentials/ に置き、checkoutと D:/OneDrive/OSM-Artifacts には置かない。
- 管理CLIは所有ユーザーが利用する。平文保存への代替、秘密を含む子プロセス引数、引数・永続環境変数からの鍵入力、Gitへのpayload追加、通常ログへの秘密出力、秘密のexportコマンドを禁止する。
- 同じWindowsユーザーのプロセスはDPAPIを復号できる。同ユーザーのAgentからの隔離とは主張しない。
- バケット単位のObject Read & Write権限には上書き・削除能力がある。CLI規則だけで追記専用にはできない。
- トークン発行・失効・事故対応はCloudflare所有者の責務。ローカル削除はサーバー側失効を行わないと表示する。

## 2. 凍結済みの意思決定・受け入れ境界（A3 r1）

**このスライスが答える問い:** Unityやネットワークなしで、Windows所有者がダミーR2鍵ペアの登録、安全な状態表示、置換、ローカル削除を行え、失敗時の保全と平文の保存・出力防止を確認できるか。

**進める最低条件:** 独立したWindows検証で、ダミー値の登録、ローカル状態表示、置換の成功・失敗、改ざん・復号失敗の拒否、削除を確認する。生成ファイルと捕捉した出力にダミー鍵がなく、既存のリポジトリ変更を壊していないことを示す。テストのファイル操作観測点で隔離fixture外への書込みがないことを記録し、実操作でも許可した保存先に限定されることを観察する。別ユーザー検証を実行できなければ未確認と記録する。OneDrive全体の走査は要求せず、全体を走査済みとも主張しない。

受け入れ条件は、上記最低条件を構成する次の6項目とする。

1. **入力:** pwsh tools/artifacts.ps1 credentials set --profile osm はAccess Key IDとSecret Access Keyを非表示の対話入力で受け取る。--replaceで既存profileを置換し、通常のsetは既存profileを拒否する。リダイレクト・非対話入力はプロンプト前に有限時間で拒否し、保存状態を変更しない。鍵の引数・パイプ・環境変数・ファイル入力を受け付けない。profileはパスではなく制限したASCII識別子とし、本スライスの利用対象はosmのみ。テストだけが内部で隔離設定を注入できる。
2. **レコードと状態表示:** 2つのS3鍵と、profile・bucket・endpoint・世代・作成/置換日時・任意のtoken ID/失効参照を単一のDPAPI暗号化activeレコードへ保存する。token IDは今回は収集せずnull。別のメタデータファイルやCloudflare bearer tokenは追加しない。credentials status --profile osm は短時間復号し、レコード全体を検証して安全な項目だけを表示し、平文を破棄する。local-only; R2 connectivity unverified を明示し、破損を拒否する。接続確認日時を捏造しない。
3. **保存先と権限:** Windows Known FolderのLocalApplicationDataから固定保存先を解決し、環境変数の差替えを保存先指定として信頼しない。checkout・既知の同期先配下を拒否し、経路上のすべての祖先・対象のreparse pointを拒否する。資格情報用ディレクトリ・ファイルは現在ユーザー、SYSTEM、AdministratorsだけのACLとし継承を無効化する。既存の危険なACLは拒否する。DPAPIはCurrentUser。未対応OS、Known Folder不明、ACL設定失敗、破損・別ユーザーの暗号文、不正レコード、危険なパスは一般化した診断と非0終了で拒否し、平文へ代替しない。テスト用保存先の内部注入をCLI・環境変数へ公開しない。
4. **置換と失敗境界:** ローカル構造検証とテスト専用fake callbackを確定前に実行し、同一ディレクトリへ候補を暗号化して復号・解析を確認する。fake成功をリモート有効性と扱わず、CLI・環境変数からfakeを指定できないようにする。排他ロックで変更を直列化し、最大5秒で無変更のタイムアウトとする。読む対象はactiveだけ。初回は既存時に失敗する作成、置換は同一volume内の原子的交換とし、この交換を確定点とする。確定前の失敗では旧activeを保全する。temp/backupを自動昇格しない。activeなし・backupありは読取りを拒否する。確定後の清掃失敗は「確定済み・清掃保留」と区別し、activeを正とする。所有temp/backupの限定清掃と中断時の回復をこの境界で検証する。
5. **削除:** credentials remove --profile osm は、そのprofileのactiveと安全に識別できる所有temp/backupだけを削除し、無関係なファイルを触らない。Cloudflare失効を行わないと表示する。再削除の冪等な結果を文書化する。ディレクトリツリーや他profileを削除せず、置換・削除をサーバー失効と混同しない。
6. **非露出:** 通常・verbose・エラー・改ざん・注入失敗時の出力、例外、プロセス引数、Git差分にダミー鍵がない。状態表示は非秘密かつローカル情報に限定する。入力・暗号化中のプロセスメモリには平文が存在する。破棄可能なバッファは可能な範囲で消去するが、メモリの完全消去は主張しない。

**判定と停止:** 最低条件の証拠がすべて揃い、反証がないときだけGO。それ以外は失敗・未確認の条件を示してNO-GOとする。実装スライスなので条件付き合格は設けない。問いに答えたら終了し、実R2接続や実トークン登録へ広げない。

**人間のA3承認:** 2026-09-26に上記のローカル置換境界を凍結した。fakeで失敗時の旧値保全を検証し、候補検証と原子的交換でローカル成功を定義する。実鍵のrotationにはスライス1の切替え前read/write/read-backと、所有者による旧トークン失効確認が必要。本スライスでは将来の復号読取りAPIを追加しない。実鍵フローのために状態・所有者・寿命・公開API・拒否契約を変える場合は、B内で追加せずPhase Aへ戻す。

**後続へ送る問い:** スライス1ローカルが実トークン登録、endpoint、認証probe、サーバー検証付きrotation、接続日時を担当。スライス1クラウドが各Agentのgrantと外向き接続を担当。スライス2がartifactコマンド、manifest、サーバーロック、保持期間、安全な展開を担当。programが未受領コメントと全体判断を担当する。これらをダミー限定の本スライスの完了条件に追加しない。

**凍結後の例外:** 最低条件・受け入れ条件・AGENTS.md違反を示す場合だけ現スライスの阻害要因にする。それ以外は担当する後続へ送る。範囲拡大は承認されていない。後述の外部ツール検証によるUnity回帰の代替は承認済み。

## 3. 責務マップと規模見積り

A0時点で各ファイルは新規（0行）。すべてUnity外に置き、asmdef・Unity assembly・Scene・engine依存を追加しない。永続状態の所有者はWindowsユーザー、復号した平文の寿命は各操作中だけとする。

| ファイル | 責務・変更理由 | 依存・所有・テスト境界 | 予想増分 |
| --- | --- | --- | ---: |
| tools/artifacts.ps1 | コマンド解析、非対話拒否、安全な終了コード。CLIの使い方が変わるときに変更する。 | Commandsを呼ぶ。永続秘密を所有せず、S3・Unityに依存しない。 | 100–150行 |
| tools/Artifacts/Credentials/CredentialStore.psm1 | レコードの版・直列化・DPAPI・候補検証・原子的確定・回復。保存形式と更新契約を所有する。 | PathAclに依存。ネットワーク・梱包・将来の公開読取りAPIなし。平文は操作内だけ。 | 300–450行 |
| tools/Artifacts/Credentials/CredentialPathAcl.psm1 | Known Folder解決、ACLとreparse安全性。ファイルシステムの安全条件が変わるときに変更する。 | Windows APIに依存。検証済みパスを扱い、鍵バイトを受け取らない。 | 180–280行 |
| tools/Artifacts/Credentials/CredentialCommands.psm1 | 対話入力、ローカル検証、安全な表示の調整。 | Storeに依存。実S3 adapterなし。fake・保存先注入はテスト内部だけ。 | 180–260行 |
| tools/Artifacts/tests/Credentials.Tests.ps1 | ダミー鍵・故障注入・生成物/出力走査。PTYはCの実CLI観察と組み合わせる。 | 隔離保存先とfake callback。実ネットワーク不要。 | 250–400行 |
| tools/Artifacts/README.md | 実装済みCLI、ローカル状態、回復、所有者の失効責務を説明する。 | 文書だけ。鍵値を記載しない。 | 50–90行 |

合計見積りはテスト込み約1,060–1,630行。依存はlauncher→Commands→Store→PathAclの一方向で、逆importを作らない。公開CLIはset/status/removeのみ。モジュールexportは次の層に必要な呼出しだけに絞り、生の秘密読取りとテスト注入点を公開しない。Storeが500行を超える場合は、単一のレコード寿命責務として凝集している理由、または別責務をPhase Aへ返す理由を記録する。行数だけで分割しない。既存ファイルの50%以上の増加は計画しない。

## 4. 実装順序とBの停止条件

1. 固定パス・profile・ACL拒否を実装する。テスト用保存先は内部注入とし、既存リポジトリ差分を保全する。
2. 版付きの単一暗号化レコードと作成・置換・削除を実装する。候補を確定前に検証し、temp/backupを自動昇格しない。activeなし・backupありは拒否。確定後清掃失敗を別結果にし、ロック待機は5秒以内とする。
3. 非表示入力CLIと安全な状態・エラー表示を実装する。ダミーだけで非対話拒否と実PTY入力を確認し、入力が表示されないことと終了結果を記録する。
4. 故障注入と平文走査を加える。実トークンの所有者操作は後続作業と明示し、登録済みと書かない。

計画した責務配置でパス・ACL・原子性を守れない場合、新しい状態所有者・秘密公開API・依存・入力経路・拒否契約が必要な場合はBを止めてPhase Aへ返す。既存境界内の不具合はB適応で直し記録する。LocalMachine、弱いACL、平文ファイル、未レビューのpackageを回避策にしない。

テストの待機はシグナルまたは注入時刻で扱い、Task.Delay / Thread.Sleepを使わない。endpointはnull/未設定のままとし、実accountのendpointを捏造・要求しない。bucketはosm-artifacts固定。終了結果は成功・確定済み清掃保留・失敗を区別し、どれもR2認証の成功とは扱わない。

## 5. テスト・レビュー・証拠計画

- **単体/ローカル統合:** pwsh tools/Artifacts/tests/Credentials.Tests.ps1 をダミーで実行する。暗号化往復、statusの復号・検証・非露出、重複登録拒否、置換成功、暗号化前fake失敗、確定前保全、確定後清掃保留、activeなし・backupあり拒否、5秒ロックタイムアウト、改ざん・復号エラー、危険なACL・祖先reparse、非対話拒否、PTY、削除と再削除、保存物・出力・Git差分の平文不在を検証する。別Windowsアカウントを利用できる場合は別ユーザー暗号文も確認する。同ユーザー内の暗号化は別ユーザー検証の代用にしない。
- **発見Cの起点filter:** ハーネスのケース選択機能で修正対象を限定確認する。凍結条件の確認に必要ならCが根拠を記録して範囲を広げられる。Unityのfilterは適用しない。
- **判定Cの必須項目:** 全資格情報テスト、pwsh tools/contract-audit.ps1、pwsh tools/docs-audit.ps1、資格情報操作前後のGit状態・差分比較。全EditMode回帰はA3承認済みの適用除外。Unity/Runtime、asmdef、package、build挙動を変更せず外部ツールだけを対象とするため、全外部テストと契約/文書監査を代替証拠とする。Unity側変更が入ったらこの除外は無効となり、Phase Aで検証計画を見直す。
- **実行経路:** C担当がローカルWindows/PowerShell 7で固定headを検証する。DPAPI/ACLは承認済みsandbox外経路を使う。PTYで実CLIへダミー文字だけを入力し、入力非表示、終了コード、安全な出力を観測する。実鍵をAgentツールへ送らない。テスト名・件数、暗号文/ACLの検査、許可先への書込み、Git前後状態を生結果として保存する。
- **証拠:** 平文走査後のstdout/stderr、ケース結果、ファイル/ACL観測、入力値を除いたPTY記録、base/headを保存する。鍵や暗号化資格情報blob自体は保存しない。C'には凍結A、所見を含まないB結果、固定完全diff、生結果だけを渡し、C所見を別に置く。
- **A3時点の疎通:** PowerShell 7.6.5、.NET 8/10 SDK、通常ユーザーでのDPAPI CurrentUserダミー往復、PTYのConsole.ReadKey(true)による1文字非表示入力と正常終了を確認した。実装後の複数文字入力、取消、ACL、回復、別ユーザーはこの疎通だけでは確認しておらずB/Cの対象とする。sandboxでDPAPI失敗を観測済みのためCは通常ユーザー経路を使う。必須の実CLI観測ができない場合は成功に読み替えず、最小限の観測支援または明示した未達として扱う。別ユーザーの未確認はA3で許容済み。
- **人間の判断:** A3凍結とPhase Dのマージ判断。ゲーム画面の主観評価は不要。実トークン入力は対象外。
- **A0/A1:** GPT-6 Sol / OpenAI。モデル名は起動指定を記録し、自己申告で推定しない。
- **A2:** アーキテクチャ担当GPT-5.6 Sol、A0だけを見る代替案担当GPT-5.6 Terra。共通A0境界を渡し、前者は上記A1ハッシュをレビューした。互いの所見を渡していない。
- **A2採否:** 暗号化メタデータとstatusの復号、確定/回復規則、export/依存/テスト注入の制限、Known Folder/祖先reparse、DPAPI/PTY証拠経路、OneDrive主張の限定、ローカルfake検証、高リスク分類を採用した。CLI・寿命・DPAPI・ファイル保存の責務分離も採用。「原子的確定後の再読込み失敗でも旧activeを正とする」案は確定点と矛盾するため不採用とし、候補を事前検証して確定後復号不能なら拒否する。token IDはnull、将来の読取りAPIはスライス1へ送る。人間がA3で採否を承認済み。
- **A3:** 2026-09-26にこのチャットで凍結とB→C→C'を承認。ローカル置換、外部テスト代替、別ユーザー未確認の扱いを含む。実装固有のACL/回復/入力の検証はB/Cで行う。凍結時のコミットとsnapshotを保持する。
- **C'の独立性:** B・Cと異なるモデル、新規セッション、所見を含まない入力を使う。実施モデルと強化条件の制約は最終結果に記録する。

## 6. 初回Phase B — 実装結果

tools/artifacts.ps1 と tools/Artifacts/Credentials/ のPathAcl・Store・Commandsを実装した。単一activeレコードをCurrentUser DPAPIと制限ACLで保管し、排他ロック、候補の復号・解析検証、初回の重複拒否、同一ディレクトリ内の原子的置換、確定後清掃保留を扱う。CLIはcredentials set/status/remove --profile osmだけを受け付け、非表示入力、リダイレクト拒否、ローカル状態表示を行う。隔離したダミーテストとREADMEを追加。Unity・R2・実トークン・クラウド操作は追加していない。

既存.gitignoreのtools/**/artifacts/がWindowsでソース用tools/Artifacts/も除外するため、対象ソース・テスト・READMEだけを明示的にforce-addした。ignore規則は変更していない。

PathAcl/Storeは変更直前に予定した書込み先を内部テスト台帳へ記録し、保存先外を拒否する。各ケースはfixtureの清掃前に生成ファイルと出力・例外を走査し、引数とGit差分も調べる。初回にはKnown Folder下の固定OneStarMaker/Artifacts/credentials階層の初期化だけを許可し、秘密レコード・ロック・候補・backup・清掃はcredentials配下に限定する。

既存の共有OneStarMakerはreparse検査だけを行い、継承ACLやRevisionLocks等の兄弟データを変更しない。資格情報専用のArtifacts/credentialsには厳格なACLを要求する。全階層未作成、既存共有親と兄弟ACLの保全、危険な専用階層ACLの拒否をダミーfixtureで限定確認した。

Bは構文検査、限定したダミー検証、契約/文書監査を実施した。全件テストと実PTYの最終判定はCが担当し、Bの限定成功を代用にしない。別ユーザーDPAPIは第二のWindowsアカウントが必要で、未確認を合格に読み替えない。Unity Editorのコンパイル・Unityバッチテストは未実施で、外部PowerShell/Markdownのみが差分対象。担当モデルはGPT-6 Sol / OpenAI（起動指定）。コード実装の最終コミットは450a72296f440955854bfa50a1388708c0afa5c5、主担当の文書日本語化を含む判定headは3a5126a2480d76ebbbe4c5c0ada538cea9172698。snapshotは§0参照。

保存レコードはJSONの項目集合、重複しない項目名、各値の単一型、完全なUTC日時を検証する。復号可能でも形式が不正なら拒否し、置換を拒否したときは現行暗号文のバイトを保持する。重要な境界判断を説明する日本語コメントをコードへ追加し、READMEと本HANDOFFを日本語にした。これらは既存の受け入れ条件を実装・説明する変更であり、実トークンやネットワーク操作を追加していない。

## 7. 初回Phase C — 一次レビュー

**初回判定: GO。** 対象headは3a5126a2480d76ebbbe4c5c0ada538cea9172698。担当はGPT-5.6 Sol / OpenAI。Bと異なるモデルの新規セッションで、固定A/B入力と完全差分から構造確認を先に実施した。launcher→Commands→Store→PathAclの依存、状態所有者、内部テスト注入、秘密をexportしない境界は凍結マップに一致し、Unity側変更はない。後日の外部レビューで未確認経路の欠陥が判明し、§10で修正・再判定した。

発見Cで扱った指摘と処置は次のとおり。すべて凍結条件内のB適応で、受け入れ範囲を拡大していない。

- 条件6・最低条件: 書込み先の観測がfixture内列挙に限られ、清掃後の平文走査が空になる欠陥。内部の書込み台帳とケースごとの清掃前走査へ修正（0331d60）。
- 条件1/3: 初回の固定三階層作成まで書込み境界で拒否する欠陥。固定ディレクトリ初期化だけを許可し、未作成三階層のテストを追加（3ea4547）。
- 条件1/3: 既存の共有OneStarMakerにも専用ACLを要求し、実CLIの初回登録を拒否する欠陥。共有親を変更せず、専用Artifacts/credentialsへACL境界を限定（75d19e3）。当初の「親も今回作成した」という推定は、通常ユーザーでの再観察により撤回した。既存RevisionLocksを削除・変更する回避は行っていない。
- 条件2/3に関するC'の指摘は§8の修正後、全判定を新headで再実行した。旧headの成功を流用していない。

判定コマンドは、通常ユーザーの承認済み経路による pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1（全件・filterなし）、tools/contract-audit.ps1、tools/docs-audit.ps1。資格情報テストは**17件実行・17件成功・失敗0**、監査はともにexit 0。ケース名と生結果はZIP内のcredentials-tests.raw.txtにある。不正日時、日時/鍵配列、余分・重複JSON項目など、復号可能な不正レコードを含む。

同headの実PTYで初回set、replace 2回、status、remove、再removeを確認した。両入力プロンプトの非表示、成功exit 0、世代変更と作成日時維持、削除後statusの拒否を観測。専用ディレクトリ・active・lockのACLは所有ユーザー/SYSTEM/Administratorsのみで継承無効。保存ファイルと収録テキストに実行時ダミー値の平文はなく、削除後のactive・所有候補/backupはなし。実機でRevisionLocksの残存、fixtureで共有親/兄弟ACLの不変、Git前後のcleanを確認した。

未解決blockerは0。全EditModeは凍結済みの外部ツール限定例外で未実行。別WindowsユーザーでのDPAPI拒否はA3許容どおり未確認。実R2、実トークン、サーバー失効、実payload転送は今回の証拠に含めない。

## 8. 初回Phase C' — 盲検の独立監査

**初回最終判定: GO（強化独立性に制約あり）。** 対象headは3a5126a2480d76ebbbe4c5c0ada538cea9172698。担当はGPT-6 Astra / OpenAI。BのGPT-6 Sol、CのGPT-5.6 Solとは異なるモデル。新規セッションへ§0の初回blind ZIPだけを渡し、C所見・結論・探索候補、可変HANDOFF、過去の監査結果を渡していない。完全diff内の固定日本語HANDOFFと英語の凍結Aを照合し、翻訳による条件変更がないことも確認した。今回の修正後の独立監査は§10を参照する。

最初の監査は旧head 75d19e3 に対してNO-GOだった。CPrime-01（P2、条件2/3）として、日時の接頭辞検査とPowerShellの配列比較により、不正日時・日時配列・鍵配列がレコード検証を通る反例を確認。既存の型・形式検証責務内で厳密なJSON/単一型/完全UTC日時検証と拒否時の旧暗号文保全テストを追加した（450a722）。旧NO-GO記録は cprime-result-head-75d19e3.md（§0の作業ディレクトリ内、SHA-256 64E3EF7EDB67E636ADD175518CE3A5D017215F89E6F0030D5BED07ABDCA98A97）に保持する。

最終監査はさらに新しいセッションで開始し、旧監査の所見も渡していない。受信先の新規ディレクトリへZIPをコピー・展開し、ZIPとmanifest全16ファイルのハッシュ一致、必須ファイルの実閲覧を確認した。凍結条件1〜6、完全差分、全17件の生結果、PTY/ACL/削除の一次観察を監査し、阻害する欠陥なし。全テストの重複実行はしていない。監査用一時領域だけでダングリングリンクのWindows API挙動を限定確認し、製品コード・実資格情報ストアは変更していない。

モデル相違・新規セッション・盲検入力の最低条件は充足。別ベンダーClaude Codeは組織設定による403で利用不可だったため、全担当がOpenAI系列である。同系列に共通する見落としの可能性を、強化独立性の制約として人間のPhase D判断に残す。

残存事項は、別ユーザーDPAPIの未実測、同Windowsユーザーからは復号可能であること、完全メモリ消去やOSクラッシュ全時点の保証をしないこと。実R2認証・rotation・失効・クラウドgrantは段2（スライス1）、確定オブジェクトのサーバー保護はスライス2へ送る。OneDrive全域の走査も主張しない。

## 9. Phase D — マージ判断

§11の修正後headでC/C'はいずれもGO、未解決の凍結条件違反はない。結果の突合は今回もC'完了後に行った。[PR #76](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/76)はdevelop向けに作成済みで、ユーザー承認に基づき今回の修正を同PRへ追加する。マージは行わない。人間のマージ判断を待ち、マージ時に恒久的な知見を公開文書へ反映して本HANDOFFを削除する。未マージなので、完了HANDOFFの保持によるdocs-auditのharvest警告は予定どおり。

## 10. PR #76 第1回レビュー対応 — 非対話起動の拒否

以下はacb4bea..aa3f7adでの記録。後日の第2回レビューで二重ダッシュの未確認経路が判明したため、この時点のGOを現在のheadへ流用しない。修正と新しい判定は§11に記録する。

[外部レビュー](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/76#issuecomment-5842966658)の指摘を採用した。コンソール付きpwshの非対話起動はリダイレクト判定だけでは検出できず、入力待ちになる。severity high / category semantic / unique / accepted、凍結条件1への違反であり、現在の問いを阻害する欠陥と分類した。既存Commandsの入力境界内で修正できるB適応なので、Phase Aの条件は変更していない。

**B（gpt-6-sol）:** 入力前に起動引数の非対話指定を確認する。完全形、短縮形、大文字小文字、Windowsのslash、PowerShellが受け付けるUnicodeダッシュ3種に対応し、固定CLI文法のもとで引数全体を安全側に拒否する。日本語の理由コメント、README、private判定の回帰テストを追加した。保存形式・Storeの責務・公開CLIは変更していない。修正コミットは6ee194cとaa3f7ad。

**発見C（新規gpt-5.6-sol）:** 6ee194cの全件検証は18件中5件成功・13件失敗。追加したCommandsの強制importによりテストのStore参照と公開関数が不整合になる欠陥を確認した。また、非ASCIIダッシュによるPowerShell非対話指定の受理を確認した。前者は判定必須テスト未達（semantic / unique / accepted）、後者は条件1の同じ拒否経路の欠陥（semantic / unique / accepted）としてBへ一括差戻し。aa3f7adで同一Store参照を使うimport順とダッシュ対応を修正した。旧headの失敗記録は旧head別領域に保持し、合格証拠へ混ぜていない。

**判定C（同じC担当）: GO。** acb4bea..aa3f7adの完全差分を固定し、構造を再確認した。3ファイル・55行追加/7行削除で既存責務内に収まり、依存・所有者・寿命・Unity側への変更なし。通常ユーザーの承認済み経路で次を確認した。

- pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1: 全18件成功、失敗0、exit 0。
- tools/contract-audit.ps1、tools/docs-audit.ps1: ともにexit 0。文書監査は未マージHANDOFFのharvest警告1件のみ。
- 実ConPTYの-NonInteractiveと-noniによる置換: 約1.134秒と1.123秒でexit 1、一般診断のみ、入力プロンプトなし。active/lockのハッシュとACLは前後不変。
- 通常対話のダミーset/status/replace/removeと再remove: 成功、入力echoなし、旧新ダミー値の平文残留なし。作成日時維持・世代更新・ローカル削除の表示を確認。最後はlockのみでactive/candidate/backupなし。
- Gitは同じheadで前後clean。Unity全EditModeは凍結済み適用除外。別ユーザーDPAPIはA3で許容した未確認のまま。

**C'（新規gpt-6-astra）: GO、blocker 0件。** Cと同じbase/headのblind ZIPだけを入力とし、受領コピーのZIP/manifestおよび26ファイル全件のハッシュを確認した。凍結A、所見を含まないB結果、完全差分、最終ソース、生結果と一次観測を実閲覧し、18件のケース名と条件1〜6を照合した。C所見・疑念候補・PRコメント・可変HANDOFFは渡していない。全テストの重複実行はしていない。モデル名は起動指定を記録した。B/C/C'のモデル相違・新規セッション・盲検性は充足するが、同じOpenAI系列であり強化独立性には制約がある。

**後続への引継ぎ:** 外部レビューが後続とした以下4点は、今回の非対話拒否修正に含めていない。段2（スライス1ローカル）の資格情報運用を担当するOSM保守担当へ引き継ぎ、着手時に条件と責務を具体化する。

- 未作成の共有OneStarMakerにも専用ACLが付く点: 共有親の新規作成時のACL方針を整理する。
- ロック取得後のACL設定失敗でハンドルが解放されない点: 例外時の解放責務を確認する。
- 矢印キー等で入力が中止される点: 対話入力の操作仕様を整理する。
- 危険なACLのactiveは削除も拒否され暗号文が残る点: 所有者による復旧・清掃手順を整理する。

実鍵、実R2、Cloudflare変更、実payload転送は引き続き未実施。同一WindowsユーザーのAgentからの隔離を主張しない。今回の独立監査結果との突合後も未解決blockerはなく、マージ判断だけを人間に残す。

## 11. PR #76 第2回レビュー対応 — ホスト解析規則との照合

[第2回レビュー](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/76#issuecomment-5843337258)を採用した。前回の実装・18件テスト・C/C'では、pwshが非対話指定として受理する二重ダッシュを見落としていた。severity high / category semantic / unique / accepted、凍結条件1への違反として現在の問いを阻害する欠陥に分類した。前回のGOと修正済み報告はこの経路に対して不十分だった。

**B（新規gpt-6-astra）:** PowerShell v7.6.5の公式GetSwitchKey、MatchSwitch、IsDashを直接照合した。入力引数をTrimし、slashまたは4種類のdashを1個除き、dashの場合のみ同一文字の2個目を除く。noniから完全形までをOrdinalIgnoreCaseで比較する。二重ダッシュだけでなく前後空白も同じ条件1のB適応として修正した。公式ソースへのリンクと日本語の理由説明をコード/READMEに追加した。Store、保存形式、所有者、依存、公開CLIは変更せず、Phase Aは再開していない。修正コミットは9d2cc5744d943e76ad879fde272daddec70ffbf0。

回帰ケースは9種類の有効prefix×11種類の略語長×3種類の大小表記を確認し、BMPの25種類の空白を各prefixの前後で検査する。全二重prefix組合せ、三重prefix、短すぎる名前・余分な末尾なども検査する。製品判定だけを期待値にせず、実pwshのRead-Hostが非対話例外を返すことを識別する独立の実行経路で449通りを照合した。標準入力を閉じたこのhost検証は、実ConPTYの代用にはしていない。

**C（新規gpt-5.6-sol）: GO、blocker 0件。** 0c46a81..9d2cc57の固定差分は3ファイル・40行追加/1行削除で、既存の入力責務とテスト境界内に収まる。構造確認後、同headの判定証拠を新たに取得した。

- pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1: 全18件成功、失敗0、exit 0。今回拡充した入力判定ケースを含む。
- probe-host.ps1による実ホスト照合: 449件一致、不一致0、timeout 0、exit 0。有効表記297件、空白50件、二重prefix25件、三重prefix5件、名前境界72件。
- contract-audit / docs-audit: exit 0。文書監査は未マージHANDOFFのharvest警告1件のみ。
- 実ConPTY: --noni、--NonInteractive、同一em dashの二重、前後空白付き--noniを、activeなしのsetとactiveありのreplaceで直接確認。すべてプロンプト前に一般診断のみでexit 1。既存active/lockのSHA-256とACLが不変で、candidate/backupは0件。
- 通常対話: ダミーset→非対話4表記の拒否→別ダミーでのmasked replace→safe status→removeを確認。入力echoなし、置換でactiveのハッシュが変化、削除後active/candidate/backupなし。再removeの成功も確認した。
- 実使用したダミー6値の走査はcheckout、証拠、本番保存先、ZIP展開先で0一致。補助検証出力に含まれたダミー値のラベルは、入力値を除外する方針に従って項目名に置換した観測記録とし、CLIの出力と区別した。共有親とRevisionLocksは変更せず、Gitは同headで前後clean。

**C'（新規gpt-6-sol）: GO、blocker 0件。** Cと同じbase/headのblind ZIPを受領コピーで検証し、ZIP/manifestと28 payload全件のハッシュ一致を確認した。凍結A、中立B、完全差分、最終source、生結果と一次観測を照合。C所見、PR本文/コメント、可変HANDOFF、過去所見は渡していない。全件テストの重複実行はしていない。B/C/C'のモデル相違・新規セッション・盲検性は充足するが、別系列・別ベンダーによる強化独立性は満たしていない。

C'-01（低 / machine / unique / accepted）は、BとCの別実行が同じhost-matrix.jsonという名前を使ったための証拠識別の指摘で、凍結条件違反ではない。今回この台帳で対応を明確にした。B snapshotとbundleの内容は改変せず、それぞれの結果を保持する。

- Bの限定実行: C:/Users/void/AppData/Local/Temp/osm-pr76-double-dash/host-matrix.json、SHA-256 DFE33FF68764F21BC57A156F6D7E6525D08707A2E81DA65DD4588E77069F7C4D。B snapshotの記載はこちらを指す。
- Cの固定head実行・最終bundle収録: C:/Users/void/AppData/Local/Temp/osm-pr76-double-dash/head-9d2cc5744d943e76ad879fde272daddec70ffbf0/host-matrix.json、SHA-256 7E476CAD063FE1354CDF651686442E9E1AFDD4572046B190621A808705148D76。今回の最終判定はこちらを用いる。

別WindowsユーザーDPAPIは凍結A3どおり未確認。Unity全EditModeは承認済みの外部ツール限定例外。§10の後続4点は段2のまま。実鍵・R2・Cloudflare・実payloadは扱わず、同一ユーザーのAgentからの隔離も主張しない。現在の結果の突合はC'完了後に実施し、マージ判断を人間に残す。
