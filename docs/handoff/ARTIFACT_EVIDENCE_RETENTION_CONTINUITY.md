# Artifact Evidence server保護の継続 — 方式確認と移行契約

## 0. 現在の範囲と版

- type: `slice`、status: `公開headの判定C/C′完了・Phase D未着手`。方式確認と移行契約の限定スライス。Phase Dは未着手。
- branch: `codex/artifact-retention-continuity`。入口は本HANDOFF。H1 / external-current-v1未採用。
- A0 / implementation base: `26d9a9cdb224e5f1797518e99ee21407cc037bd9`。PR公開用の文書・契約判定headは `d05277dc02d4cb159e1c4c0068fb947cff22e39b`、固定manifestは§7。production sourceはA0と同一。以後のレビュー結果だけの記録commitは判定headへ読み替えない。未公開の試行commitは元branchで保持し、公開履歴は方式・契約と判定結果記録に整理する。原取得head `185734da8a2b9fb74204d17812a99a2428b8c47a` のraw metadataを変更せず、source不変性で公開headへ結ぶ。
- risk: high。owner: OSM maintainer/storage owner。owner判断期限: **2026-10-30 JST**。
- created: 2026-10-07 JST。harvest先: `tools/Artifacts/README.md` と [program](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md)。Phase Dでharvest/delete。現在は保持する。
- 人間はr2のA3と「phaseC/c'まで」の進行を明示承認し、続いて一回の具体pilot案を明示承認した。今回のexact 2rule/3dummyと保存先・停止規則の許可。E1/E2本番適用、reader実装、新token、実Evidence送信、実Buildの許可ではない。
- 実行承認recordはowner管理のGit外証拠内のfile ID `closure-pilot-r3/approval.json`、SHA-256 `108d9fc9e320fff9782f153a49c77ea388846eec5cd6715551d86e974aa45559`。§7の固定bundleに収録。ローカル限定の取得条件は§6に記す。
- 承認したtargets SHA-256 `91b1bb5fd7607112d9e7d59c70c6e4ee83e53ddce2abc4be44f8617332c8a5fd`、提案SHA-256 `e7f46e966863218f3842677e20f8f2e291caf72c9355e5d7269203b622091fcd`。未承認と記載された提案原文を後から書き換えず、承認は別recordで結合した。

## 1. A0 — 固定した事実と保持義務

remote developは2026-10-07に取得しA0と照合。共有checkoutはdevelop / `c0f7d6e57ba1fb918863403f6b0077b4ea4c3509`、tracked差分なし。ignored `artifacts/route-proof-phase-c-8c1793e/live-run/`は列挙拒否で完全検査できない。他Agentのcheckoutは変更せず専用worktreeを使用し、既存証拠worktreeを清掃しない。

AGENTS、osm-workflowと該当参照、docs/README、Artifacts README、program r4、policy/codec/ledger/reader/関連テスト、PR #104保持台帳とPR #106 Phase D台帳を固定した。PR #96/#104/#105/#106はマージ済み。Evidence first useの判定implementation headは **`896eaea8a912248333704c80549d46ecf20c02c6`**、offline可搬性は **`19720ed7c6065ffcc55ee2643f811462025a940d`**。文書、merge、今回のreview headへ置き換えない。削除済みHANDOFFは復活させない。

三つの時計:

1. 最大24hの原設定観察/config期限は接続/configの鮮度。切れたら同identityの新観察/configと新immutable案内・期待hashを既存経路で固定する。保存義務やserver lockを延長しない。
2. 有限server lockの保護下限はPUT-intent前時刻+30日。E1 **2026-11-06 07:13:42.5023602 JST**、E2 07:16:35.3325043 JST。過去referenceのretainUntilを更新しない。
3. owner closeからの最低保存義務はE1/E2 **2026-11-06 08:17:25.6469389 JST**、以後も参照中保持。参照unknownも保持。期限は削除許可ではない。

E1/E2正本 `LocalApplicationData/OneStarMaker/Artifacts/evidence-retention/8fe65074daf5379c4a311ec5166b21ff/retention.json` は期待SHA-256 **`70032a5208f153d88f89f0470d2dcefd5ce9508c645871a2d0f456f89f0eba40`** とpilot前後一致。元ledgerのclose=nullを変更しない。

#106 offline証拠は別義務。D close **2026-10-07 12:58:05 JST**、最低 **2026-11-06 12:58:05 JST**、以後参照中保持。E1/E2正本へ混ぜない。#106 manifest `d0d0f1161bf020d709e6444272a012bb413dc924ae5bda89207d385f103dbb02`。

E1 ledger ID `8dd797d6e2f14258978185eef15dade2`、SHA-256 `3519e864aba787b9fb370fbf6a748adb410be5549ebd294d604bfcd5d2a20357`、package `49f64d158a7ac32a49a609260cc66e6a644d42d1398c272a0d62ce68ef67a5cd` / 316952 bytes / 15 entries。exact key:

`evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/caed8bae55c033673b75532dc650f0a3a3cedc48/64c42fc4d5d24fa580af7cc12d3a3c92/bundle.zip`

E2 ledger ID `767d073dec92416281d9b03f7e58f527`、SHA-256 `5d96b17b3cb0deddd0f59fa7c09ef8aa27c247f38d20ffe6e8d4e4f87a759977`、package `4d7810cfbd0415bde093b16f3e2ce556c86a20ef65da9a4571290ab919b30b06` / 112718 bytes / 4 entries。exact key:

`evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/896eaea8a912248333704c80549d46ecf20c02c6/1495786d921949fca6e92edbef21010e/bundle.zip`

全E1/E2、ledger、案内、原画像、失敗記録、intent/receipt、旧証拠/受取copy、locked witnessを保持。実Evidence PUT/上書き/DELETE/prune/再送は禁止。stable osm鍵は既存CLI内部だけで使い、会話/引数/env/log/Gitへ出さない。TokenDelete、新token、再入力は要求しない。

## 2. A1/A3 — 問い・最低条件・停止規則

**問い:** 既存key/bytes・信頼台帳を変えず、保存義務と参照中期間にserver保護を継続する具体方式と、安全に適用する移行契約を確定できるか。

凍結した最低条件M1–M4:

- **M1:** 個別正本/ledger/hash/key/server下限/必要期間を照合し、#106を別管理。unknown参照は保持する。
- **M2:** provider一次仕様と所有bucketの全適用rule/lifecycle/writer原観察、必要な限定synthetic実測により、既存object適用・切替中保護・具体方式を判断する。旧rule下で新規作成した非秘密同一witnessの変更前後key/hashを確認する。歴史的witnessは操作しない。
- **M3:** 選択方式に対する旧reference/policy identity/readerの可否と必要最小差分・失敗契約を確定する。現readerの期待拒否を観測できれば互換性不成立の判定は完了。reader実装成功と本番実保護は後続「保持保護の実適用・reader互換」。
- **M4:** exact対象/rules/期間/actor/権限/前後比較/失敗停止/非破壊復帰/owner/期限を、一つの実行判断可能な契約として渡す。新依存/API/schemaの実装設計は後続A2/A3へ渡す。

M1–M4の原観察、版、hash、時刻、tuple、権限、影響の一致が受け入れ条件。全て閉じ致命的反証がなければ方式・契約GO。実保護成立と読み替えない。未達/timeout/environment-blocked/inconclusiveはNO-GO。旧ruleの除去/置換/短縮、実object再PUT/DELETE、新秘密/API/依存/範囲拡大が必要なら停止。追加条件は凍結条件/常時契約違反を示すものだけで、その他は後続へ移送する。

対象外: reader実装/本番rule Save/任意Evidence、新file集合/保存先拡大、資格情報変更、Cloud、実Build、Build接続、操作性、#106テスト強化3点、Unity native終了stall再調査、悪意ある管理者による解除からの物理WORM保証。

## 3. 責務・依存・寿命・変更範囲

tracked変更は本HANDOFFとprogramの現在スライス/後続契約。production code、tests、Unity、asmdef、公開APIは増分0。文書は一つの問い・所有者・寿命を持つ自己完結契約として非分割。500行/3責務/50%増加は形式分割理由にしない。

storage ownerは方式/操作/参照終了を判断。通常writerに管理権限を加えない。AIは承認exact範囲で原証拠を取得し、判定担当は固定原証拠で独立判定する。設定主体は認証済み管理UI、通信主体は既存CLI。管理APIを新設しない。

policyは現configのfail-closed検査とidentity、codecはopaque reference、ledgerはimmutable事実と案内/close、transportはbytes通信、credential storeは秘密寿命、Packagingはpackage同一性を所有。依存はCLI→application/policy→transport/credentials/Packaging。管理設定や参照運用をこれらへ便宜的に混ぜない。

Git外single-operation runnerは承認済みexact16stepの有限process起動/非秘密原応答記録だけ。既存RouteProof -Childを呼び、独自signer/credential取得/APIを作らない。ownerは試験取得担当、寿命は参照中の証拠保持。production再利用APIではない。

## 4. 一回pilotと原実績

pilot ID `8b4830f6345d462f9db4ce07b47e5240`。実行は2026-10-08 00:09:48～00:14:17 JST、16操作を一回完走。操作<=30秒、子process+pipe EOF+終了確認<=45秒、active work<=60分。失敗時同key再PUTせず止める。

exact新規対象:

- W: `probe/unlocked/8b4830f6345d462f9db4ce07b47e5240/existing.txt`。追加rule `retention_continuity_w_8b4830f6345d462f9db4ce07b47e5240`。
- L: `probe/locked/8b4830f6345d462f9db4ce07b47e5240/existing.txt`。追加rule `retention_continuity_l_8b4830f6345d462f9db4ce07b47e5240`。
- control: `probe/unlocked/8b4830f6345d462f9db4ce07b47e5240/control.txt`。追加rule対象外。

W/L ruleはそれぞれkey全文prefix、enabled Indefinite。prefixなのでsuffix keyにも適用されexact ACLではない。試験前のrun directory検索は各空一覧で、既存matching objectなしを原DOM/JPEGで固定した。

originalは57 bytes、SHA-256 `22bccc18a20eda5b02dac4aa390763c7eebb4b7d0e1b9284bbe29e44cf0203cb`。changedは56 bytes、`fe6b5af3f8ef3a738de863e729d781e59c19f273f1476d3bce9bdee3b3338943`。非秘密markerだけ。各CLI起動前にCreateNew/flush intentを固定した。

旧設定でW/L各一回PUT200、別子processのGET200/original hash一致。その後に管理UIのAdd→Save changesで2ruleを一度ずつ追加した。旧enabled `evidence_first_use` / `evidence/first-use/` / Age30日、`probe_locked` / `probe/locked/` / Age1日は編集/解除/短縮なし。lifecycleはbucket-wide multipart abort7日一件だけ。公開development URL disabled、custom domain0を維持。

同writer controlはPUT200/GET原hash→異bytes PUT200/GETchanged hash→DELETE204/GET404 not-found NoSuchKey。W/L各異bytesPUTとDELETEはすべて **409 / other / ObjectLockedByBucketPolicy**、最終GETは200/original57bytes/hash。全16操作generation `3068547bc9524f59b33c24ae9e3c4e9e`、期限超過/redirect/limit到達なし、子process/pipe EOF確認。実Evidence操作0、資格情報変更0。

Wは旧Age非対象なので追加Indefiniteの既存objectへの効力を区別できる。Lは旧Ageで保護中の既存objectへの追加。L拒否単独を延長証明とせず、[provider Bucket Locks](https://developers.cloudflare.com/r2/buckets/bucket-locks/)の既存object適用・最長rule優先と追加only契約を合わせる。前後画像だけで原子性を証明しない。旧ruleを一度も外さないことが切替中の保護根拠。

writerの管理権限なしは原token metadataのObject Read & Write/bucket限定と[provider権限定義](https://developers.cloudflare.com/r2/api/tokens/)からの推論。管理拒否実試験は行っていない。新token/秘密表示なし。W/L・2rule・rawを参照中保持し、自動清掃/解除しない。DELETE成功したのは承認controlのみ。

## 5. M3/M4 — 選択方式と後続移行契約

選ぶ方式は**旧Age30日を維持し、E1/E2それぞれのbundle key全文prefixへIndefiniteを追加**。有限延長の反復を不要にし、参照中/unknownは保持する。管理者が解除できることを無期限WORM保証にしない。本番未適用。

既存E1/E2の `protection-witness.txt` は各run内でbundleの兄弟keyであり、bundle key全文prefixへの追加2ruleの対象外に意図的に残す。既存Age30日の保護下限以後のserver保護継続は、この方式では成立したと扱わない。witness自体と原記録の保持義務・操作禁止は維持し、run全体を覆うruleへ広げず、必要な追加保護は後続のowner判断に分ける。

現readerはexact Evidence keyへの追加Indefiniteをconfig検査で拒否する。固定旧reference+明示synthetic settings/timeによる22観測は受理5/拒否17、テスト17失敗ではない。readerは同policy検査を呼ぶ。選択方式の期待拒否は互換性不成立の判定で、読取成功ではない。

後続「保持保護の実適用・reader互換」の最小契約:

1. 現在の実policyを正しく検査し、旧origin identity/key/hash/ledgerと新保護の対応を検証するread契約を先に実装・offline検証する。同identity再観察/config/immutable案内/hash取得は既存README経路を再利用する。
2. 旧reference/ledger/retainUntil/close正本を変更せず、新保護事実は別recordにする。作成主体はownerの委任を受けた適用AI、確認主体は後続C/C′。unknown参照は保持、参照終了判断はowner。
3. 旧configや期限の偽装、validator/publish guardの無条件緩和は禁止。新schema/API/配置は後続Aで独立A2/A3する。移行readを用意できなければ本番ruleを先に保存して読取を壊さない。
4. 本番操作案は本書E1/E2 exact keyを各prefixとするenabled Indefiniteの追加2ruleのみ。rule名は `retention_continuity_e1_8b4830f6345d462f9db4ce07b47e5240` と `retention_continuity_e2_8b4830f6345d462f9db4ce07b47e5240`。現時点は未作成・本番承認なし。
5. 適用担当は承認範囲のlocal AI、設定判断ownerはOSM maintainer/storage owner。保存前に同prefix/suffixの対象一覧と全適用ruleを固定し、想定外object/lock/lifecycleがあれば停止。旧Age30日/試験rule/公開設定/通常writer権限を維持する。replace全collection APIは使わない。
6. 保存後に原rule/時刻/対象を固定し、新保護recordと旧originを結ぶ新immutable案内・期待hashで別process GET、全entry/hash/原画像閲覧を確認。実bundle/歴史的locked witnessへのPUT/DELETEはしない。必要なsynthetic実証は今回rawを再利用できるか後続Aで照合する。
7. 失敗/不明は止め、原記録を保持。旧Ageも追加済み保護も安易に解除しない。非破壊復帰は正当なreader契約/案内の訂正。data再送/上書き/DELETE、rule解除をrollbackの既定にしない。保持義務を緩めない。
8. owner判断2026-10-30 JST、後続実保護raw **2026-11-04 18:00 JST**、後続C/C′/owner受容 **2026-11-05 18:00 JST**を提案する。E1旧下限前に成立させる。間に合わない/未成立はownerへNO-GOを返す。CLI禁止やlocal copyだけでserver保護成立としない。参照終了と解除/清掃は別のowner exact判断。

Build接続/Cloud限定アクセス/操作性/#106強化3点はprogramへ問い・owner・着手条件だけを維持し、詳細設計は追加しない。

## 6. A2/A3実績と検証受け渡し

A2は同じr1の18固定入力を照合した独立2session、architecture gpt-6-astra / feasibility gpt-6-sol。少なくともarchitectureがフォルダ/責務/依存/owner/寿命/テスト可能性を確認。互いの所見なし、fork none。次は当時の指摘と採否理由を元の固定記録から戻した台帳で、r1の保留を現在の実行開始条件にしない。今回pilotと移行契約は同じr2 M1–M4を実行可能に具体化したもの。主担当自己レビューをA2/C′に数えない。

- **AR1（採用）:** 方式確認の出口に後続reader実装成功を要求すると、方式選定と実装の循環になる。M3を現readerの可否判定と必要最小差分・後続契約へ修正し、実readerの成立は後続「保持保護の実適用・reader互換」が所有する。
- **FR1（採用、AR1と一部重複）:** 実証対象・権限・時間予算・経路が未確定で、そのまま実行仕様を凍結できない。具体化前は実行を保留し、その後、§0の明示承認でexact 2rule/3dummy、管理UIと既存CLI、§4の一回16操作/期限/停止規則を固定した。採用しただけで解消済みとせず、承認と原操作を分けて記録した。
- **FR2（採用）:** rule設定後に作る新規objectだけでは既存objectへの効力を示せない。旧設定下で作成・GETした同一synthetic objectへ追加し、前後key/hashと旧rule維持を確認する方式へ修正。§4のW実測と一次仕様を合わせて判断し、L拒否だけや前後画像だけで延長・原子性を断定しない。実Evidence/歴史的witnessを操作しない。
- **FR3（採用）:** 有限server下限直前の判断では後続実適用の時間が足りない。owner判断10月30日、実保護raw11月4日18時、後続C/C′/owner受容11月5日18時という§5の日程案と未達時NO-GO返却を追加した。提案日程を実適用承認や成立済みへ読み替えない。
- **FR4（採用）:** raw取得と判定開始の順序が曖昧。所見を書く前に取得し、固定headのevidence/blind bundleと別受取の全hash/必要file閲覧を確認してから新規C/C′を開始する。§7/§8の固定受取に反映し、C所見をB resultへ転記しない。
- **AR2（採用して後続へ移送）:** 新保護事実recordの作成・確認主体、参照unknown時の更新/失敗引渡しを明確にする必要がある。実適用担当・後続C/C′・参照終了を判断するownerの役割は§5へ渡し、旧ledger/retention正本を変更しない。新schema/CLI/APIの実装設計は後続A2/A3で確定する。

不採用なし。r1時点で保留したFR1の具体操作は上記の別承認と原実証で具体化した。本番適用/reader実装の承認は現在も未取得。Build/Cloud/操作性/#106強化を採用所見の追加実装として同梱しない。

A3承認snapshot r2はGit外approved-a-r2.md、SHA-256 `eb9d044a592cd836a6006359525b49104ec8d7ace745cf568c58bdbb5d99dc1c`。具体pilotの承認は§0の別record。旧提案/旧raw/失敗を保持。盲検入力にはC/A2所見を含めず、凍結条件と承認操作を投影したA snapshotを作り、原承認hashとの対応をmanifestへ残す。

Bは新規gpt-6-sol session。Git外offline v2 scriptとpilot runnerを作り、production変更0。runner SHA-256 `9698956cabf50688b5a2b5e07f5b51ac3ea19352bcecb28808c78584111678c0`、parse0errors。既存route transport build0errors/0warnings、DLL `7ebfe416e44dc3d0751d645a08c1a996dfd253fe9b03c31c3ca897de2b53ab82`。B自身live実行0。B result-pilot SHA-256 `4f9a110e4d76e36ee308597a7734f07526afd4fe4fc01998155374cf5bc8c91c`。原証拠取得はrootが実行し、独立判定は後から行う。

取得先はowner管理の同一Windowsユーザー/同一PCのGit外証拠領域。bundle ID `retention-publication-r1` の `manifest.json` を§7の期待hashで照合し、掲載fileのbytes/SHA-256を確認して読む。cloneには原証拠を含まず、別環境/Cloudの取得経路は未成立。pilot observations SHA-256 `513606d6fe6210afebef67f0f8d932de7aafa18900cad590d85ff06c201726af`。原intent/stdout/result/DOM/JPEG・provider記録はfile ID `closure-pilot-r3` 配下で保持し、正本や保存先の移動を意味しない。

差し戻し中は文書/source照合とpolicy/reader限定filterを使う。最終必須はM1原保持hash、M2上記原16操作と全設定/一次仕様、M3選択方式の期待拒否と最小契約、M4上記対象/actor/期限/承認、最終headのdocs/contract/diff検査と関連5offline case。production source不変でも最終headで5caseを実行する。旧22観測のsource hash一致を固定し、旧run metadataを最終head実行へ偽装しない。

全EditMode/PlayMode/Unity BuildはUnity/source/serialized asset/asmdef変更なし・依存なしのためA3で適用除外。代替はstrict policy/reader確認と限定live実証。後続実装の3build/全7suite/reader不在probeを今回実行済みにしない。

C/C′は同じbase/head完全diff、承認A投影、所見なしB result、必須rawと機械検査の入力を判定開始前に作り、別受取copyの全hash/bytesと閲覧成立を確認する。CはBと別modelの新規session。C′は予約した未関与gpt-5.6-sol新規session、B/Cと異なるmodel、blind入力。C/A2所見、可変HANDOFF、私的所見を渡さない。共通手続指示はGit外で依頼に明示添付する。

同OpenAI/GPT系列、runtime variant未実測という強化独立性の制約を記録する。C′は同suite再実行不要。M1–M4と現在問いに阻害がなければここで終了し、後続実適用へ自動進行しない。

## 7. Phase C

**判定C: GO（保持方式と実適用前の最小移行契約のみ）**。2026-10-08 JST、新規未関与session、起動指定 `gpt-6-astra / high / fork_turns=none`。担当 `/root/c_publication_retention`、Bは `gpt-6-sol`。現在のblocker0、M1–M4充足。既契約の後続「保持保護の実適用・reader互換」へ渡し、新しい完了条件なし。本番E1/E2適用・reader実装・Phase Dの判定に拡大しない。

base `26d9a9cdb224e5f1797518e99ee21407cc037bd9`、公開判定head `d05277dc02d4cb159e1c4c0068fb947cff22e39b`。bundle ID `retention-publication-r1`、生成2026-10-08 01:01:12.4005775 JST、211file、manifest SHA-256 `9388d622dc839171061b67476b64e25b456ccdceef6c50519f3b41cd1c6bbdfb`。別受取 `c-publication-received-r1` receipt SHA-256 `7b12846f84ee2015d9f837887221baf30e11cd2832116a20134b621a663db7b4`。C自身が全211件bytes/hashとUTF-8/JSON解析を確認した。

完全diff/stat/name-statusを先に照合。2文書 +137/-1、計画責務/依存/owner/寿命に適合し非分割妥当。承認A投影・承認records・B結果・M1正本/ledger/config/固定PR、22観測/script、policy/codec/reader/source、runner/transport、全16step raw、全DOM/JSON/provider原記録、公開head必須rawとsource-runtime-indexを閲覧。8原画像を実表示（W/L空一覧・保存form、前後settings、proof、writer metadata）。after-W/L画像はhash検査、対応DOMは閲覧。全fileの逐語意味レビューを主張しない。

必須検査: `pwsh -NoProfile -File tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case policy-valid,policy-overlap,policy-expired,reader-hash,handoff-identity-substitution`（非秘密ResultPath指定と全commandは固定runへ保持）、5選択/5実行/5成功/失敗0。`pwsh -NoProfile -File tools/contract-audit.ps1` は608files/errors0/warnings0、`pwsh -NoProfile -File tools/docs-audit.ps1` は111docs/errors0/warnings1、diff空白検査合格。判定時docs警告は括弧付き未着手欄をharvest候補としたheuristic。判定必須未実行0、Unityテスト/実Buildは§6のA3適用除外。

原rawは取得head/時刻のまま保持し、43sourceのGit hash/blob、base/headの41同一blob・2文書変更、runtimeの改行差、B source/DLL hash/provenanceで公開subjectへ対応付けた。旧runを公開headの再実行にしない。22観測は受理5/期待拒否17。L拒否単独を延長証明とせずW実測・原一次仕様・旧rule不変追加onlyを合わせて判断。

Cの詳細判定回答はGit外の証拠root配下 `publication-r1/results/C_RESULT.md`、8081bytes、SHA-256 `0631c36aa90f9fe0eed4c7772f3aec4112a256146e055acc5a4812354a866455`。agent自身がCreateNewで固定、tool回答転記で原stdout fileではない。

限界: 管理権限なしはmetadata/一次仕様からの推論、管理拒否実測なし。物理WORM/旧期限経過後実測/本番実適用/reader成功/別host/Cloudは未確認。B build/parseはtool回答転記で原stdoutなし。同GPT系列/runtime variant未実測。新R2/UI/資格情報/Build/テスト再実行なし。他C/C′所見・可変HANDOFF・指定外private memoは未閲覧。

## 8. Phase C′

**独立C′: GO（保持方式と実適用前の最小移行契約のみ）**。2026-10-08 JST、A/B/C未関与の新規blind session、担当 `/root/cprime_publication_retention`。起動指定は `gpt-5.6-sol / high / fork_turns=none`、本人が参照できた識別は `GPT-5`、正確なruntime variant未実測。B/Cとは起動指定が異なる。同OpenAI/GPT系列で異vendor/runtime実装の独立性を主張しない。

対象base/headと211file manifestは§7と同一。別受取 `cprime-publication-received-r1` receipt SHA-256 `32a9be7e561c32faa97d14b1126d7574c4c4ef1d4c9724e255d4a785acc846a8`。全211件bytes/hashを独自再計算しmissing/mismatch0、所見なし固定入力変更0。現在blocker0、M1–M4充足。後続reader/protection fact recordの設計・実装と本番適用を新しい現スライス条件にしない。

構造から承認A/B、完全diff、M1正本/ledger/保持台帳、runner/probe source、22観測、全16step raw/集約、全設定DOM/JSON/provider、provenance/source-runtime-index、公開head必須検査を照合。原画像9枚（writer metadata・settings before/after-W/after-L/final、W/L空一覧と保存form）を実表示。43source hash一致・41blob不変/2文書変更を確認。5必須case/contract/docsは固定rawから確認、同suite再実行なし。

C′の詳細判定回答はGit外の証拠root配下 `publication-r1/results/CPRIME_RESULT.md`、9424bytes、SHA-256 `77c498093ce64176ce30345c66909fa04f6ee106f006028e5a9c2935035baa32`。agent自身がCreateNewで固定、tool回答転記で原stdout fileではない。

可変checkout/HANDOFF、過去/現在の本スライスC/C′所見とC結果は未閲覧。新R2通信/UI操作/資格情報アクセス/Build/テスト再実行なし。既存設定/試験証拠を再利用し、原metadataと不変sourceの対応を確認。未確認は本番適用・変更policyでのreader実取得・Phase D・別host/Cloud/実Build・物理WORMで、明示後続/対象外。証拠不足を理由とする追加実操作要求なし。

この結果追記だけのcommitは公開判定head/bundleを変更しない。C/C′所見の人間による突合・残存リスク受容・harvest/mergeはPhase Dに残す。リモート/Cloudへの原証拠配布は未成立で、GitHub cloneから原証拠を取得できるとは主張しない。ローカル固定コピーを参照中保持し、期限を削除許可にしない。

## 9. Phase D

未着手。人間の判断前にclose/merge/harvest/deleteを完了としない。

## 10. 公開文書の指摘対応

PR #107の公開差分レビューを受け、A2採否理由を元記録から本文へ復元し、cloneに存在しない手元層の参照をbundle/file IDへ置き換え、既存witnessが追加2ruleの対象外であることを明記した。本文の対象key、rule、方式、M1–M4、承認範囲、保持義務・期限、実行結果、後続責務は変更していない。採否/取得案内/対象範囲の記録訂正であり、公開判定headと固定rawを更新したことにはしない。外部レビューはprivate bundle/必須検査を未取得で、判定C/C′の代替には数えない。Phase Dは未着手。
