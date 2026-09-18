# BS3 Runtime directory とロード対象

- type: slice
- status: B（Phase A revision 2 凍結済み、実装待ち）
- branch: `codex/bs3-runtime-directory`
- implementation base commit: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head commit: 未生成
- risk: high（公開 API、寿命、Unity Content Loading、取消）
- owner: BuildSystem 主担当
- created: 2026-09-18
- expires: BS3 Phase D
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md`、`18-asset-description.md`、必要なら起動文書
- Phase A snapshot path / id: `artifacts/bs3-phase-a/phase-a-r2.md`（旧 `phase-a.md` は revision 1 記録）
- Phase A snapshot generated at: 2026-09-18 JST
- Phase A snapshot hash: revision 2 snapshot 生成後に記録
- Phase B result snapshot path / id: 未生成
- evidence bundle path / id: 未生成
- C' blind bundle path / id: 未生成

## 1. A0 入力、目的、境界

### このスライスが答える問い

ビルド済みの local Content Directory を source 再走査なしに解決・ロードし、処理中・利用中・cache 内の資源を残さずに unregister できるか。

### 現況

- BS2b の `BuildContentRoot` schema v1 は build identity、target、logical/stable key、representation、Scene/Object 種別、`LoadableSceneId` または `Loadable<Object>` を保存する。Editor integration は移設後の単一 root discovery まで実証し、型付きロードと寿命は未実証。
- BS2c は本番 Scene graph の候補と Full/Whitebox 同梱を選択し、Content Directory build へ接続済み。
- `AssetManagement` は Addressables backend と `AssetOwner` 台帳、resident cache を持つ。既存 Scene lifecycle は `SceneResource` と `SceneAssetDescription` を渡す。`AssetKey` の拡張子分類は型検証ではない。
- 起動時 `AbstractApplicationInitializer` は Config 自体を Addressables から読み、AssetManagement を先に作る。Player bootstrap は BS4 が所有する。
- `artifacts/bs2c-*` 等の既存検証ログと未追跡 PRE は変更・追加しない。

### 常時制約と対象外

- Game → Framework の一方向、asmdef edge 無断追加なし、SceneState 14 値の順序保持、`IAssetManagement` と `AssetOwner` がロード入口、`ILogger<T>` 公開面、Unity 偽 null、`#nullable enable`、Unity C# の `record` 禁止。
- Editor/Runtime 分離。Scene/Prefab/Addressables の変更は限定し、Editor 操作は `osm-unity-editor` に従う。
- 物理削除と remote install は DIST、Player bootstrap と code 保持は BS4、本番非 Scene Description と Mesh subasset の build source は需要別後続 slice、旧 Addressables 経路廃止は RET。
- source 欠損の全経路を通す end-to-end 実証は DIST。ただし BS3 の登録・解決そのものに local `SceneResourceMap` materialization を要求しない。

### A0 未決事項

1. `Loadable<Object>` に保持された Unity load ID と、外部に説明できる file/object locator、schema 互換性の関係。
2. `SceneAssetDescription` を要求する既存 Scene API と directory の logical 解決を、Scene lifecycle を壊さずに接続する位置。
3. sync bootstrap、Prefab instance、cache、in-flight operation が unregister を阻む範囲。
4. revision 削除許可を同一 process 内でどう原子的に管理するか。process 外保護は DIST に渡すか。

## 2. A1 設計初稿（A2 レビュー対象）

### 進める最低条件と受け入れ条件

1. **解決と identity**: root の schema / identity / target / entry 整合性を登録時に検証し、`logical key + representation + kind` が一意の選択済み対象だけを返す。未知 key、曖昧選択、欠損、型不一致は reason code 付き失敗。Scene 専用の default fallback を Object に適用しない。file GUID は build 入力単位、Object load ID は Unity の local file ID を含む別単位とし、同一ファイルの複数 Object を表現できる locator を schema に確保する。既存 v1 root の扱いを A3 で固定する。
2. **ロードと寿命**: `IAssetManagement` と `AssetOwner` を通し、Scene は load→unload→scene-owned release、Object は load→owner release、Prefab instance は GameObject 破棄→instance release とする。代表 Prefab/Texture fixture と Scene を実 directory から取得する。要求型を実取得 Object に照合し、`AssetType` のカテゴリは cache budget 用 metadata とする。
3. **単一 directory owner**: backend/session owner が register、root discovery、load、in-flight 終端、全依存解放、unregister を順序付ける。登録失敗を rollback して同一/別 path で再試行可能。caller 取消は native 操作の終了とみなさず、遅延成功資源を owner が回収する。resident cache の entry も revision 利用中とみなす。明示 close は新規受付を止め、発行済み処理を drain する。caller が所有する live instance/handle があれば `ResourcesInUse` で登録を維持し、caller の解放後に再試行する。live Scene と cache を解放した後にだけ unregister する。同期 `Application.quitting` は best effort とし、完全 drain を GO と偽らない。
4. **削除排他**: 同一 process 内で revision identity ごとに active 利用 lease と排他的 delete lease を原子的に切り替える。delete lease 保持中は登録・新規利用不可。利用中・登録中・処理中・cache 内は delete lease を拒否する。delete lease は削除成功/失敗の確定まで保持し、失敗でも必ず解除する。物理 I/O は DIST 側。別 process の同時利用保護はこの slice で保証しない旨を API に明示する。
5. **起動時選択**: Full/Whitebox 同時同梱時に一度選んだ表現を Scene lifecycle へ渡し、高水準の Scene 遷移順序を維持する。Editor Play は local directory を明示選択したときだけ新 backend を使い、旧 Addressables 起動を移行期間の既定として残す。実行中切替 UI は設けない。
6. **失敗検証**: 欠損 directory/root/entry、登録失敗→再試行、取消後完了、owner 解放、cache eviction、明示 close の cleanup（live instance 拒否→owner 解放→retry を含む）、削除競合を fake と Unity integration で確認する。source 再走査を暗黙 fallback にしない。

### 判定・停止規則

- GO: 上記六条件が同一固定 base/head の証拠で成立し、常時契約違反がない。
- NO-GO: いずれか未達。最低条件を増やして延期しない。
- この問いを阻害しない新規課題は BS4、DIST、RET または需要別非 Scene slice へ送る。例外的な scope 変更はユーザーが置き換える条件または期限・検証予算を指定して Phase A revision を再開する。

### 責務配置の初稿

| 配置 | 責務、所有者・寿命、テスト境界 | 見積 |
|---|---|---|
| `Runtime/BuildContent/BuildContentRoot.cs`（現78行） | Serialized な load 対象 locator と明示 metadata。永続化契約のみ。Editor I/O やロード状態を持たない。schema 変更なら +40〜80行。 | 120〜160行 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs`（新規） | root 検証と logical/representation/kind 解決。immutable、directory session 寿命。Unity I/O なしの単体テスト。 | 180〜250行 |
| `Runtime/BuildContent/ContentDirectorySession.cs`（新規） | register/root discovery/unregister、処理中・live・cache の drain。process 内 revision 利用保護。Unity adapter を port に分離して fake テスト。 | 250〜400行 |
| `Runtime/BuildContent/UnityContentDirectoryBackend.cs`（新規） | Unity.Loading 呼出と native handle を専有。I/O と依存解放順の integration テスト。 | 200〜350行 |
| `Runtime/AssetManagement/AssetManagement.cs`（現376行） | 既存 owner 台帳と cache の再利用、新 backend への明示 route。+80〜130行を予想。500行に近づくため route 抽出または二つの backend mode で責務を保つ。 | 456〜506行 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs`（現838行）と SampleGame `DependOnAll` | Editor Play の起動時設定と既存 lifecycle への一度だけの選択を配線。config の Addressables 先行読みは維持。+20〜50行に抑え、新規 directory 管理ロジックを置かない。 | 858〜888行 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` | locator/schema v2 を出力する場合だけ変更。AssetDatabase 依存は Editor 内。 | 要精査 |

上記は配置候補であり、API の署名・schema version・ファイル数・行数は A2 の所見と Unity API 調査後 A3 で固定する。新規 asmdef は作らず既存 Runtime/Editor 境界に置く方針。公開 API を追加する場合も `IAssetManagement` に directory の登録や物理削除は置かず、backend/session 側の口に限定する。既存呼出側は Addressables mode のまま機能する。

### Phase B/C/C' の予定

- B: pure index と lease 状態機械→Unity adapter→AssetManagement/Scene bridge→Editor Play 接続の順。Unity Editor の対象限定操作・compile 確認まで。`pwsh tools/contract-audit.ps1`。テスト/Build は未実行と記録。
- C: B と異なるモデルの新規セッション。固定 commit の完全 diff で構造レビュー後、Editor を閉じて承認済み sandbox 外で `pwsh tools/run-tests.ps1 -Filter ...`、必要な Content Directory build/integration、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。結果 XML の1件以上・failed 0を確認。
- C': B/C と異なるモデルの新規セッションで blind bundle を監査。Phase C の結論は含めない。未確保なら独立監査済みと記録しない。
- A0/A1 主担当: Codex GPT-6 Astra（OpenAI）。A2/A3 と C' の実績は後記。

## 3. A2 独立レビュー

同じ A1 入力版を別々に渡し、相互の所見を見せずに実施した。テストはこのレビューでは未実行。

- A0 のみからの代替構成: GPT-5.6 Sol（OpenAI）。pure index、Unity adapter、directory session、既存 owner 台帳の分離を提案。採用。同期 teardown と source metadata の境界を A3 で明示する。
- アーキテクチャゲート: GPT-5.6 Sol（OpenAI）。scene bridge、typed port と revision key、二重寿命台帳、同期 shutdown、locator/schema、process 全域の delete lease の六件が A3 blocker。全件採用。責務マップの per-file 依存/公開面/規模も追記する。
- Unity/動作レビュー: GPT-5.6 Terra（OpenAI）。CD0 で実証した Unity API を再利用し、caller 取消から native 完了を切り離すこと、source を消した移設 directory の実ロード、fake deletion race を要求。採用。sync bootstrap は旧 Addressables のままにする。
- 不採用: なし。後続へ保留: cross-process delete exclusion、Mesh subasset の build source 一般化、Player 起動と code stripping（それぞれ DIST、需要別 slice、BS4）。
- A3 最終照合: GPT-5.6 Sol が六件の closure を再確認。通常停止・Scene metadata・instance ownership・delete path の追補を採用し、最低条件の shutdown 文言を明示 close の `ResourcesInUse`/retry 契約へ揃えた。
- revision 2: Phase B 担当 GPT-5.6 Terra が実装前に起動設定と公開失敗型の未決を発見して停止。主担当が §4.7 を追加。GPT-5.6 Sol の architecture 再照合は blocker なし。GPT-5.6 Terra の behavior レビューが AppConfig の大小文字、AfterSceneLoad 登録時点、Editor guard、削除拒否 code、診断 field を指摘し、全件採用した。追加範囲は既存の六最低条件の起動・失敗・排他の具体化であり、program の目的や予算を拡張しない。

## 4. A3 凍結対象の設計判断

Program に記録された不在時委任を適用し、A2 の通常判断は主担当が統合した。以下が A1 の曖昧な候補記述に優先する凍結済み実装契約である。

### 4.1 identity と永続化

- file identity は canonical GUID。load target identity は `LoadableSceneId` または `LoadableObjectId` であり、後者は Unity の GUID と local file ID を含む。Runtime は Unity ID を opaque として扱い、GUID のみから Object をロードしない。schema v1 の `Loadable<Object>` は既にこの ID を保持するので、v1 は既存 Scene と main Object のロードを許可する。v1 Object のカテゴリは `Other`、診断用 file/local ID は不明として扱い、型は実取得 Object で検査する。
- schema v2 は既存 field を残し、Object entry に file GUID、local file ID、`AssetType` とは独立した明示カテゴリを追加する。Editor adapter は `TryGetGUIDAndLocalFileIdentifier` と `CreateLoadableObjectId` を同じ Object から取得して格納する。main asset だけを出す現在の materialization の選択制約は変えない。将来 subasset が候補になっても、同じ file GUID の異なる local ID を表せる保存形式である。v1/v2 以外を `UnsupportedSchema` で拒否する。v2 の空・不正 locator、Scene に Object locator がある等の kind 不整合は登録時に拒否する。
- root は一件だけ。登録 API の expected build identity/target を root と照合し、path の basename は identity に使わない。現在の target は `StandaloneWindows64` のみ。`(logical key, representation, kind)` 重複、stable key 重複、空 key、無効 Unity ID は構造化 issue として session を rollback する。build report/metadata directory は照合できる場合に読むが、Runtime 起動の必須入力は build済み local directory と expected identity/target で、source `SceneResourceMap` の materialization は行わない。

### 4.2 解決と公開入口

- `ContentDirectoryIndex` は immutable な pure policy。選択済み root entries だけを読み、`(logical key, representation, kind)` を引く。Scene で要求 representation が欠ける場合だけ `Full` に fallback する。両方なければ `EntryMissing`。Object は完全一致のみ。同一 logical key の二表現同梱を許し、曖昧な key だけの要求は受け付けない。カテゴリは cache budget 用で、`typeof(T).IsInstanceOfType(actual)` と Unity の偽 null でロード型を検証する。
- `IAssetManagement` に directory 専用の `LoadContentAssetAsync<T>(logicalKey, representation, owner, ct)`、`LoadContentSceneAsync(sceneIdentity, representation, options, ct)`、`InstantiateContentAsync(logicalKey, representation, parent, worldSpace, ct)` を追加する。登録/削除 API は追加しない。既存メソッド・呼出側・Addressables key はそのまま。directory mode 未選択時の専用メソッドは `DirectoryNotRegistered`、旧メソッドは従来経路を使う。`LoadAppAssetSync` は directory に流さず既存 Addressables bootstrap 専用とする。
- `SceneDirector` は起動時に固定した directory mode のときだけ `LoadContentSceneAsync(sceneIdentity, selectedRepresentation, ...)` を呼び、`SceneResource` は graph と lifecycle metadata のみに使う。payload 0 の論理 Scene node は従来どおり Unity scene をロードしない。既に `SceneManager` に載る Scene は従来の扱い。directory entry 欠損時は Addressables に fallback しない。`AddressablesLoaded` 等の内部名は backend がロードしたかを表す名前へ変更する。UI/config/SceneResourceMap の bootstrap 同期取得は移行期間 Addressables を維持する。
- 現 slice の高水準 Scene lifecycle には Runtime の `SceneResourceMap` と payload の有無が必要で、これは移行期間 Addressables から取得する。directory mode の Scene 対象を payload の参照先から解決してはならず、payload 有りの node は必ず root index の logical identity から解決する。payload 0 は Scene 実体なしの論理 node として従来どおり扱う。source の AssetDatabase/materialization は一切起動しない。取得済み content だけで graph metadata も含めて起動する経路は DIST/BS4 に渡す。

### 4.3 owner、取消、close

- session は Unity directory handle、root、native 発行済み操作、revision 利用 lease と unregister を所有する。`AssetManagement` は `AssetOwner` の registry と resident cache を唯一の正本として所有する。session は owner の複製台帳を持たない。代わりに directory origin の backend token が session lease を保持し、registry/cache/in-flight から解放または eviction された時に返す。cache key と registry key は `directory + build identity + target + logical key + representation + kind + locator` を含み、Addressables key と別 namespace にする。異なる revision の token は共有しない。
- `IAssetBackend` の address string port は旧 Addressables 用に残す。directory 用に typed entry と revision token を受け取る internal port を別に置く。Object は root に埋め込まれた同じ `Loadable<Object>` を native 完了まで保持し、成功後の実型を検査する。複数 owner は一つの backend token を registry で共有し、最後の owner 解放まで `Release()` しない。Prefab は `GameObject` の元 asset token を instance の生存中も保持し、GameObject 破棄時に instance を解放してから元 token を解放する。Scene は `LoadableSceneId` で `SceneManager.LoadSceneAsync`、正常 unload は `SceneManager.UnloadSceneAsync` の終端を待って token を返す。
- caller の ct は waiter の待ちだけを取り消す。発行済み native operation 自体へ渡さない。operation は成功/失敗の terminal まで session が追跡する。全 waiter が去った成功 Object は直ちに `Release()`、成功 Scene は unload terminal まで追跡する。失敗と取消の後も session は残存 token を掃除し、retry を許す。既存 Addressables `_inFlight` の第一 caller ct 問題も directory route に流用しない。
- `CloseAsync` は新規受付停止→native operation terminal→abandoned result cleanup→live Scene unload→owner が解放済みか確認→resident cache の当該 revision entry eviction→root を含む全 token release→unregister の順。正常 close は idempotent。Prefab instance とその GameObject は Scene/呼出側が所有し、session が勝手に破棄しない。live instance/owner handle が残る場合は `ResourcesInUse` を返し、session を登録状態に維持して owner が破棄/解放した後の retry を許す。別 origin の Addressables entry は eviction しない。登録失敗は register handle が有効なら unregister まで rollback し、同一 path/別 path の retry を可能にする。
- `IAssetManagement.ReleaseAll` と `Application.quitting` の同期契約は維持する。同期 shutdown は新規受付停止と同期解放を開始し、既に発行した native operation が残れば terminal callback で cleanup/unregister する。callback 前には delete lease を許さない。process 終了で callback が実行できない場合は OS の process 終了に任せ、正常 `CloseAsync` 完了と偽らない。Unity は Play 停止を同期で進めるため、通常の Editor 停止で awaitable close 完了を保証しない。GO の shutdown cleanup 証拠は Play 中に明示 `CloseAsync` を呼んだテストで terminal と unregister を観測する。起動済み Scene が Unity によって先に解体された場合は無効 Scene への unload を呼ばない。通常 Play 停止の完全 drain は後続 RET の Editor workflow 置換条件として記録する。

### 4.4 revision 利用と物理削除の排他

- process-global `ContentRevisionGate` は `(expected build identity, target)` を identity とし、登録予約に正規化 absolute path を結び付ける。同 identity に別 path が同時登録されたら拒否する。同 path の重複登録も拒否し、v1 は process 内で単一 active directory session とする。path は移設後に変わり得るため恒久 revision ID にしない。
- 状態は `Free → Registering → Active → Closing → Free` と `Free → Deleting → Free`。全遷移を同一 lock で原子的に行う。`TryAcquireDelete(identity,target,path)` は待たずに拒否理由を返し、成功時に `IDisposable` delete lease を返す。`Deleting` 中は register/new use を拒否。delete lease は DIST が物理削除の成功/失敗を確定するまで保持し、`finally` で dispose する。使用中、処理中、cache 内、登録中は `Free` に移らない。delete lease 自体は削除を実行しない。
- gate は identity と canonical path の双方を索引にし、いずれかが登録・利用・削除中なら別 identity を名乗っても同じ path の削除許可を出さない。process 全体で active session は一つだけ。DIST は install metadata の identity/target と削除する実 path の一致を検証してからこの port を呼ぶ契約とし、未登録 revision も指定できる。BS3 fake delete はこの不一致と競合を検証する。gate は disk 上の metadata 自体の真正性を判定せず、DIST の物理削除責務を奪わない。
- この gate は同一 process 内だけの排他である。別 process の file 使用、OS file lock、remote install と disk budget は DIST が所有する。DIST は process 間の delete 安全性を必要とする場合、同じ port の外側に lock を加える。

### 4.5 変更規模と停止条件

- `AssetManagement`（376行）は directory 専用 port を注入/呼出する薄い route のみ追加し、native handle や revision gate を持たせない。500行を超えるなら partial/専用 coordinator を分ける。`AbstractApplicationInitializer`（838行）には選択済み directory session の生成/close 呼出のみを追加し、登録状態機械を置かない。既存の長さを増やす理由は起動順序の所有者がここだからであり、処理詳細を移す。
- Phase B 中に v1 の Unity API が計画どおり利用できない、必要な asmdef edge が増える、予定外の owner/状態/API が要る、または async close と同期終了をこの契約で安全に実装できない場合は Phase A revision に戻す。

### 4.6 確定した責務マップとテスト境界

| ファイル / namespace / asmdef | 変更理由・責務・依存・所有者・公開面 | 現在→予想行数 / 検証 |
|---|---|---|
| `Runtime/BuildContent/BuildContentRoot.cs` / `OneStarMaker.Runtime.BuildContent` / 既存 Runtime | schema v2 の serialized locator/category。Unity.Loading の値と primitive だけに依存。root は directory content が所有し session 終了まで。公開 DTO の追加 field のみ。 | 78→150。v1/v2 roundtrip、locator の Editor 出力。 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` / 同 namespace / 既存 Runtime | pure policy。root entry を immutable map に投影し、検証と Scene fallback/厳密 Object 解決。Unity I/O なし。session 寿命、internal。 | 0→250。mock entry による単体テスト。 |
| `Runtime/BuildContent/ContentRevisionGate.cs` / 同 namespace / 既存 Runtime | process-global 登録予約・利用/削除 lease 状態機械。native API なし、process 寿命。DIST 接続に必要な最小公開 facade と internal state。 | 0→220。fake delete race、例外解除。 |
| `Runtime/BuildContent/ContentDirectorySession.cs` / 同 namespace / 既存 Runtime | index、gate、native adapter、owner 台帳の協調。directory handle と pending 操作を所有。登録から close まで。`IAssetManagement` には公開しない。 | 0→350。fake native terminal 制御。3責務に見えるため policy/index、gate、Unity adapter と分離して orchestration に限定。 |
| `Runtime/BuildContent/UnityContentDirectoryBackend.cs` / 同 namespace / 既存 Runtime | `ContentLoadManager`、`Loadable<Object>`、SceneManager の Unity I/O と native token。session にのみ返す internal port。Unity Editor API へ依存しない。 | 0→300。移設後 PlayMode integration。 |
| `Runtime/AssetManagement/AssetManagement.cs` と `IAssetManagement.cs` / 既存 Runtime | 既存 registry/cache の唯一の owner。directory mode route と三つの公開 overload、revision-qualified key。session へ使用 token を返す。 | 376→480、interface 79→110。owner/cache/取消の単体テスト。500行を超えれば route を internal partial に抽出。 |
| `Runtime/AssetManagement/Cache/AssetResidentCache.cs` / 既存 Runtime | origin/session ごとの eviction を可能にし、cache 保持中は token lease を維持する。既存 Addressables entry も同じ API で保持。 | 218→260。eviction/clear と lease callback。 |
| `Runtime/SceneSystem/SceneDirector.Loading.cs` / 既存 Runtime | directory mode の Scene API 呼分けと backend-owned 名称の修正。既存 lifecycle と空 payload node の責務を維持。 | 685→705〜725。Scene load/unload 順。 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` と SampleGame `DependOnAll` / 既存 Runtime と Game | 起動時の mode/path/identity/target/representation を選び、一つの session の開始・終了を配線。Game 固有設定は SampleGame、Framework に季節名を持ち込まない。 | 838→880、Game 側 310→340〜370。Editor Play で旧/新を切替確認。 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` / 既存 Editor asmdef | main Object から v2 file/local locator と category を作り、root に保存。Editor I/O はここだけ。 | 152→182〜212。Content Directory を実 build。 |

全ファイルの新規または編集 Unity C# は `#nullable enable` で始める。runtime から Editor asmdef を参照せず、asmdef edge は増やさない。新規中核ロジックが fake/単体テスト不能になった場合、別の Helper へ逃がさず配置を Phase A で再検討する。

### 4.7 Phase A revision 2: 起動入力と失敗契約

Phase B は実装前に停止した。旧 snapshot は設計として保持し、以下を追加した新 snapshot を Phase B 入力にする。

- 設定元は既存 `AppConfig` の JSON→環境変数→コマンドライン優先順。新キーは `content:runtimeMode`（未指定/`addressables` または `directory`）、`content:directoryPath`、`content:buildIdentity`、`content:representation`。キー名は既存 AppConfig と同じ大小文字非区別、mode 値と representation 値は ordinal 完全一致とし、未知 mode は起動失敗。`addressables` は既定で他の三キーを読まず、現行呼出側の動作を維持する。
- `directory` mode は Editor Play だけで有効にする。`Application.isEditor && Application.isPlaying` で guard し、Player 等では `InvalidConfiguration` で拒否する。三キーすべて非空を要求し、target は現 slice 固定 `StandaloneWindows64`。directoryPath は rooted absolute path だけを受け付け、`Path.GetFullPath` で正規化する。相対 path、URL、存在しない path、identity/target 不一致は起動失敗とし、Addressables へ暗黙 fallback しない。実行中変更は読まない。Player 入力の生成と path 基準は BS4 が所有する。
- `content:representation` は root に保存された文字列をそのまま要求する。`Full` と `Whitebox` の選択は呼出側の明示入力であり、Framework は季節や `assets:sceneVariant` の空文字を変換しない。`assets:sceneVariant` は旧 Addressables scene route にだけ使う。Scene fallback は明示した representation が root にない場合の `Full` entry に限る。SampleGame の `AppInitializer` と既存 `app-config.json` は既定 Addressables のまま維持し、Editor Play の明示選択は既存 AppConfig の環境変数/コマンドラインで行う。tracked config を一時書換・復元する実装は追加しない。
- `AbstractApplicationInitializer` は `BuildConfig` の直後（BeforeSceneLoad）に mode と値を一回読み、構文を検証する。ContentLoadManager の登録と root 検証は AfterSceneLoad の `resolve-scene-variant` の直後、`create-scene-director` の直前の `register-content-directory` stage で行う。Config/UI/SceneResourceMap の Addressables bootstrap はその前に終える。SceneDirector の最初の directory load より前に session を install する。起動失敗時は session の rollback/close を試み、その失敗も記録して元の例外を隠さない。派生 `AppInitializer` の新しい override は不要。単一 session は initializer が所有し、明示 close と同期終了で回収する。
- 失敗は公開 `ContentDirectoryException : InvalidOperationException` と公開 `ContentDirectoryFailureCode` で表す。最低限の code は `InvalidConfiguration`, `DirectoryNotRegistered`, `RegistrationFailed`, `InvalidRoot`, `UnsupportedSchema`, `IdentityMismatch`, `TargetMismatch`, `EntryMissing`, `EntryAmbiguous`, `TypeMismatch`, `OperationFailed`, `ResourcesInUse`, `RevisionBusy`, `PathInUse`, `DeletionInProgress`。exception は `Code`、`BuildIdentity`、`Target`、`LogicalKey`、`Representation` を nullable string で持ち、内部原因は InnerException。取消は通常の `OperationCanceledException` を保ち、失敗 code に畳まない。root index は pure result/issue を使い、公開境界でこの例外へ写す。`IAssetManagement` の既存メソッドが投げる例外は変更しない。
- 公開削除口は `ContentRevisionGate.TryAcquireDelete(buildIdentity,target,absolutePath,out ContentDeletionLease? lease,out ContentDirectoryFailureCode rejection)` とし、成功時の lease を DIST が `finally`/`using` で解放する。`ContentRevisionGate` が唯一の process-global 状態を持つ public static 型で、session 用の登録予約/解放メソッドは internal とする。拒否時の lease は null、同じ path が別 identity で使用中なら `PathInUse`、同 identity の使用中なら `RevisionBusy`、削除中なら `DeletionInProgress`、入力不正なら `InvalidConfiguration` を返す。`ContentDeletionLease` は物理削除を行わない。identity/target/path の検証責任と process 内排他は §4.4 に従う。未登録 revision に対する外部 install metadata の検証は DIST の責務。
- これらを新規公開 API として Phase A で承認する。`IAssetManagement` の三つの directory load 追加以外に既存 API の署名は変更しない。`AssetManagement` の実装を直接 bootstrap が構成する既存経路に session install 用 internal method を追加し、Game 側に新しい asmdef edge を要求しない。

## 5. Phase B 実装結果

未実施。

## 6. Phase C

未実施。

## 7. Phase C' 独立監査

未実施。

## 8. Phase D

人間のマージ判断待ち。Phase D の委任は受けていない。
