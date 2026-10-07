# BuildSystem Artifact Storage Program

## 0. Metadata

- type: `program`
- status: r5 Phase A改訂。2026-10-08のowner指示に基づき、開発用Storageの保存方針と後続順序を見直す。方針の要求は受領済み。個別実装スライスのA3凍結・実装・運用切替は未実施。
- program policy revision: `r5` — 開発Buildは件数、開発Evidenceは終了後30日で整理する。
- branch: `codex/artifact-storage-development-policy`
- planning base commit: `c0f7d6e57ba1fb918863403f6b0077b4ea4c3509` (`develop`)
- implementation head commit: not applicable; 今回は文書改訂のみ。
- risk: `normal`（文書改訂）。削除機能・既存データ移行のリスクと検証は着手時HANDOFFで扱う。
- owner: OSM maintainers
- created: 2026-09-26
- updated: 2026-10-08
- expires: 2026-12-26 または置換revision
- harvest to: 実装済みの操作・保存契約を `tools/Artifacts/README.md`、program状態を `docs/README.md` へ反映し、program完了時に削除する。

## 1. 目的と現在地

開発中のBuild成果物とレビューEvidenceをGit外の非公開領域で手軽に受け渡し、不要になったものは自動で片付ける。日常の保存・取得・清掃に人間のファイル運搬や毎回の承認を要しないことを成果とする。長期監査保管やリリース配布の責任は持たせない。

主な利用者はローカルWindowsの同一ユーザーで動くAgent。既存R2を継続候補として、すでに成立した経路を使う。Cloud対応や複数provider対応は、ローカルの使いやすさを完成させる前提にしない。

現在のCLI・許可範囲・実測の正本は [Artifacts README](../../tools/Artifacts/README.md)。以下は完了済みであり、再証明のために同じ鍵登録・rotation・成功送信を反復しない。

- ローカル資格情報管理（PR #76）: DPAPI CurrentUser、制限ACL、秘密非露出、登録・置換・削除。
- Route proof（PR #81）: ローカルsynthetic往復とhash一致、署名無しGETの限定非露出、Bucket Lockの上書き・削除拒否。unsigned応答400の原因は未確定。
- 最小Artifact CLI（PR #96）: synthetic publish、別session fetch、rotationと失効確認。
- Evidence first use（PR #104）: 固定E1/E2を各1回送信し、独立ledger、別sessionのhash照合とログ・原画像閲覧。実装headは `896eaea8a912248333704c80549d46ecf20c02c6`。C/C′ GOとowner D closeを完了。

任意Evidence、実Build、別host/Cloud、件数保持、期限清掃は未対応。現実装は固定E1/E2、送信時点から30日のserver lock、Close後30日以上かつ参照中の保持、削除の個別承認を前提にしている。r5はこれから変更する方向を定めるもので、現CLIがすでに新方針に対応したとは扱わない。

旧r1〜r4の案・レビュー経緯はGit履歴へ集約する。この本文から切り出す新しいスライスには、以下のr5方針を適用する。完了したスライスの合格範囲や過去の証拠は書き換えない。

## 2. 保存方針

### 開発Build

- 保持する件数Nを設定し、新しいものからN件を残して超過分を古い順に自動削除する。最低保存日数やOwner Close待ちは設けない。
- 件数を数える単位は、取り違えると困るBuild系列（project・target/platform・configuration）とする。branch別の枠を無制限に増やさない。具体的な識別項目とNはBuildスライスのA3で固定する。初稿の既定値案は系列ごとに10件で、owner決定済みの数字とは扱わない。
- 件数対象はpublishが成功したBuild。失敗・未完了uploadやstagingは別の短期清掃対象とし、成功Buildの枠を消費しない。
- 実行・検証中に必要なBuildは明示的な利用中保護で除外する。保護対象もNに数え、削除可能な古いものから減らす。保護だけでNを超えた場合はその間の超過を表示し、勝手に利用中のものを消さない。
- 最新成功Buildの同一性を確定してから件数清掃する。失敗publishによって既存の成功Buildを追い出さない。

### リリースBuild

リリースしたBuildは別ストレージ・別ポリシーで管理する。開発Storageへ永久pinして代用しない。リリース先への明示的なコピーと検証は配布側が所有し、開発Storageの清掃でリリース保存先に触れない。配布先の製品選定や保持期間は本programの対象外。

### 開発Evidence

- 作業中は保持する。作業の完了または打切りをCloseとし、`expiresAt = closedAt + 30日` で自動削除対象にする。30日は終了直後の差し戻し・調査のための猶予であり、最低保存義務の後に無期限保持を積む契約にはしない。
- Closeは既存のtask完了/中止操作に結び付ける。ArtifactごとのOwner署名・別のClose承認儀式を増やさない。既存workflowのPhase D判断そのものは変更しない。
- 過去PR、HANDOFF、台帳にリンクが残っているだけでは保存を延長しない。GitHubや全ドキュメントを走査して「どこからも参照されていない」と証明する仕組みは作らない。
- 再開した作業や必要な調査だけを明示的な利用中保護/延長の対象にする。理由・対象・解除条件または期限を持ち、`inspect`で見えるようにする。Close未設定のまま放置されたtaskも一覧で見つけられるようにし、推測で終了扱いにはしない。
- 恒久的に残すのは設計判断、既知の制約、検証の要約。生ログ・画像・bundle・受信copy・snapshot・witnessを永続保存する必要はない。自ツール所有の派生copyと失敗残骸も期限清掃の対象とし、利用者が選んだ原sourceを巻き添えで消さない。
- 削除後の古い参照は「期限切れ」と分かればよい。小さなID・hash・削除日時の記録で区別し、payloadの復元や再取得を保証しない。台帳の詳細schemaは清掃スライスで決める。

### 通常操作と保護の強さ

- 種別、対象領域、件数/期限、利用中保護を設定した後は、その範囲内のpublish/fetch/清掃を通常の事前承認で運用する。削除1件ごとのowner確認や候補一覧を毎回承認させる運用を完成形にしない。
- 作業中の同一性は固有key、期待hash、利用中保護、清掃対象の限定で守る。上書きで同じ参照の内容を変えず、更新は新しいkeyにする。
- 開発Build/EvidenceにWORM、長期server lock、Close後lock延長、無期限参照追跡を共通必須条件として課さない。必要な用途だけ別policyで選べる。r5の標準経路は通常writerや同一Windowsユーザーに対する物理的な改ざん防止を保証しない。
- 非公開保存、秘密非露出、hash検証、安全な展開、利用中の誤削除防止は維持する。新しいbucket/権限、公開配布、設定範囲外の削除は通常清掃と区別する。実行環境の承認要求は迂回しない。
- 現行E1/E2のlock・保存記録・事前承認は、この文書編集だけでは変更しない。初回移行で対象と影響を固定して現行README/CLIと揃え、その後の定型清掃を包括承認で回せるようにする。

## 3. 次に進めるスライス

### A. 保存期限と清掃を実用化する（次の着手候補）

**問い:** 開発Evidenceを必要な間は取得でき、終了後30日で人手を挟まず片付けられるか。

**進める最低条件:**

1. 固定E1/E2専用の制約を、指定したtask・非秘密の明示file集合に適用できるEvidence経路へ置き換える。通常利用のために毎回コードへhash/file名を埋め込んだり、file集合ごとに別の設計審査を要求しない。入力選択・盲検性の責務はEvidence workflowに残す。
2. taskのCloseと30日期限、再開/延長、利用中保護、`inspect`と実際に削除できる清掃経路を接続する。CLIからの清掃と自動起動は同じpolicy判定を使う。候補表示だけで完了にしない。
3. 期限前、利用中、対象領域外、判定情報不足では削除しない。期限到来した対象は履歴リンクがあっても清掃できる。削除直前の状態再確認と利用開始/Close/延長との競合処理を持ち、部分失敗は再実行できる結果にする。
4. cleanupが扱うremote keyとローカル派生物を所有情報から限定する。未完了upload/stagingの猶予と中断判定も有限値で決め、継続中の転送や原sourceは消さない。正確な値と実行間隔はこのスライスのA3で固定する。
5. 固定private E1に依存するoffline testをdummy fixtureへ置換し、期限後の原Evidence削除が製品テストを壊さないようにする。期限・件数等のpolicyは時刻を注入して検査し、実時間の30日待ちを要求しない。
6. 既存lockと新しい清掃の関係をsyntheticで確認する。旧E1/E2とlocked witnessは移行対象を列挙し、実際のlock満了・旧close記録・新期限を照合する。既存の保護を一括解除せず、新policy経路とlegacy読取を必要最小限で分ける。移行されていない既存objectを新規定の推測で削除しない。
7. 実装済み契約と事前承認をArtifacts READMEへ反映する。元の「参照中は無期限」「毎回個別承認」と新しい期限清掃を、同じ対象の有効な指示として併存させない。

**検証経路:** offlineで境界時刻・再開/延長・利用中競合・不明情報・領域逸脱・部分失敗/再実行を確認する。実R2では隔離したsynthetic taskをCLIで保存・取得・Close・清掃し、取得結果と対象外objectの保持を確認する。期限到来の実機確認方法はA3で固定し、実Evidenceを破壊試験に使わない。既存の秘密非露出・転送・安全な展開の回帰を含める。Unityを変更しないこのスライスの判定はArtifacts/Harnessの関連offline suiteと限定R2検証で行い、Unity EditMode全回帰は適用外とする。

**ここでは答えない問い:** 大容量Buildと件数管理の接続はB、別host/CloudはC、Build実行orchestrationはD。server lock延長や監査用WORMの設計を最低条件へ加えない。

**停止規則:** 上の期限管理・通常清掃が成立したら閉じる。全ての古い残骸の移行、GUI、常駐broker、別providerを完成条件にしない。残るlegacy対象には所有者と扱いを残す。

### B. 開発Buildの保存・件数管理

**問い:** 既存Build出力を保存・取得でき、利用中を保護しつつ系列ごとにN件へ整理できるか。

普通のpathとmetadataを受ける入口から、成功publish、別process fetch/hash、N超過の古い順削除、失敗publish時の既存保持、利用中の除外、リリース保存先の非干渉を確認する。N・系列識別・容量上限・未完了物の猶予をA3で固定する。初回は既存Build出力を入力にし、BuildSystem全体の作り直しを前提にしない。multipartは対象サイズで必要になった場合に実装する。

### C. 別host / Cloudでの受け渡し

必要になった環境から個別に、接続・限定grant・保存/取得・ログ/画像閲覧を確認する。利用者へ鍵やURLを毎回運ばせる方法は無人対応として合格にしない。Cloudの失敗は当該環境の未対応として残し、ローカル利用を止めない。platform固有の秘密供給とnetwork仕様は着手時に再確認する。ローカル親鍵をCloudの通常fileやpromptへ複写しない。

### D. BuildSystem / Harness接続

外部CLIを主入口に、Unityを交換可能なbackendとして既存build routeへ接続する。進捗・取消し・結果を扱い、H2d/H3やCURRENT連携は各taskのPhase Aで切り出す。Storageの保存・取得・清掃をUnity GUIやEditor installationに依存させない。Unityテスト/Buildは既存Phase責任に従い、既知native終了stallの調査は再開しない。

各スライスは着手時に責務配置、公開API、具体的なfile変更、発見用/判定必須テストを自己完結したHANDOFFへ固定する。H1適用とCURRENT方式は各A3で明示する。program改訂だけで実装やlive削除を開始したことにはしない。

## 4. 維持する責務と必要な安全条件

- **Evidence / Buildの呼出し側:** 入力fileとtask/Build系列、base/head、作業の開始・終了を所有する。C/C′の固定されたfindings-free入力と所見分離は既存workflowに従う。Storage側でレビューの中身や合否を再審査しない。
- **Artifact application / CLI:** 梱包・保存・取得の進行、機械可読の結果、種別ごとの保持判定、利用中保護と清掃を所有する。policyは時刻・metadataを入力にしたoffline testで確認できる形にし、通信やUnityを必須にしない。転送とGit/PR更新は別責務だが、通常利用で独立した人間の台帳確定を必須にしない。
- **R2 adapter:** 認証通信、objectの読書き・削除、実際のlock拒否等を扱う。R2 SDK型やbucket policyを上位のBuild/Evidenceへ漏らさない。第二providerやregistryは必要になるまで作らない。
- **Credentials:** 既存DPAPI/ACLとbucket限定鍵を利用し、値をprompt、引数、環境変数、ログ、Gitへ出さない。同一Windowsユーザー内の信頼を前提とし、悪意ある同ユーザーからの隔離を追加要件にしない。
- **包装と取得:** 明示した入力root/file集合を用い、無関係なrepo・profile・資格情報領域を収集しない。照合したsnapshotから送信し、信頼済みの別経路の期待hashで取得bytesを確認する。path逸脱、reparse、衝突、展開量超過を拒否して新規private領域へ展開する。現在の有限上限を出発点に、Build用上限はBで決める。取得やinspectだけでscript/binaryを実行しない。
- **非公開とGit:** `osm-artifacts`は非公開の開発領域として使う。payload・資格情報・署名URLをGitへ置かず、小さな参照/hash/必要な要約だけ残す。stagingはcheckout・同期領域外を既定とする。OneDrive mirror、Unity Library転送、公開配布は含めない。

秘密非露出はdummy sentinelと異常系で、転送はhashと結果で、清掃は対象/時刻/状態で確認する。毎runの設定画像、独立署名台帳、synthetic破壊試験一式を将来の通常操作へ一律継承しない。既存first-useの検証記録と、日常運用の必要条件を分ける。

## 5. 残件と改訂の扱い

残件ownerはOSM maintainers、期限は本programの期限または置換revision。

- 既存のRoute proof object 2件は未清掃として引き継ぐ: `probe/locked/62bd4f1ab6b24a9d93026108ee5dae5c/object.txt`（旧清掃可能時刻2026-09-30 01:53:32 JST）、`probe/locked/b1b6dc2e6516486b94b95825d7fa7475/object.txt`（同05:37:47 JST）。Aのlegacy整理で現況確認し、この文書改訂では削除しない。
- 公開synthetic Release `artifact-probe-20260926` の清掃は未完了。実験終了と対象確認後の別操作とし、開発Storageの自動清掃からGitHub Releasesを操作しない。
- offline private E1依存はA、一般エラー文言と重複publishの扱いは関連入口を直す際の操作性改善とする。任意の改善を全てAのblockerにはしない。
- Cloudは未対応。過去のCursorによる公開GitHub asset取得成功とCodexのCONNECT 403は、現在のprivate R2接続を証明しない。
- 資格情報CLIの対話操作、ACL不整合時の復旧、ロック取得後の失敗処理は関連変更時の入力として残す。完了済みの資格情報スライスを再開する理由にはしない。

**今回の採否:** ownerの「開発Buildは件数」「リリースは別ストレージ/別policy」「Evidenceは終了後30日で十分」を設計入力として採用する。r4からの変更は、無期限の参照保持、最低保存義務の延長、実payload全般へのserver lock必須、定型削除の都度承認を後続の共通条件から外すこと。非公開・秘密保護・整合性・安全な展開・作業中の保持は維持する。Nの具体値やlive移行方法をowner承認済みと捏造しない。

**Phase Aレビュー（2026-10-08）:** Codex主担当と新規sessionの独立A2（同一継承モデル・同系列、モデル多様性は未充足）が要求適合、責務/寿命/依存/テスト境界、現行運用との切替、誤削除条件を確認し、program改訂のblockerなし。A2の指摘「Bの問いがN件に利用中分を加算するようにも読める」を採用し、§2と同じ件数解釈へ表現を統一した。追加の必須条件は増やしていない。r5の詳細A3と各実装スライスのA3は未実施。
