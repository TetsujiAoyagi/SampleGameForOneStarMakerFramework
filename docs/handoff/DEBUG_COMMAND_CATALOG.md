# Debug command catalog — A3 frozen r2

## 0. メタデータ

- type: slice; status: A3承認済み（2026-10-05）。旧HANDOFFとは別revisionとして凍結。
- branch: PR #92 `cursor/debug-command-catalog-159b`、作業用branchは `codex/`。baseはdevelop。
- implementation base: `d4ea79b566b259db0d63a8ce1802947f536ccea5`（#87 merge後）。現PR tip `f61be6684662ced6f8b2a5f57753307af0bb920f` は修正前。新implementation head未生成。
- risk: high（新しい公開APIと所有/寿命契約）。owner: root調整、発注者A3、B/C/C'は別セッション。
- created: 2026-10-05; expires: 2026-11-04。
- harvest: `unity/Assets/Docs/Architecture/12-telemetry.md` §10.5 に、Appが明示的に同一instanceを配線した場合の制御コマンド利用契約を加え、公開型のXMLコメントと合わせて残す。default/SampleGameで利用中とは記載しない。wire YAMLや数値payload定義は変えない。
- A snapshot: 本ファイル phase-a-frozen.md。生成時刻とSHA256は phase-a-manifest.json に記録する。B result / evidence / blind bundle: 未生成。H1/H2移行はしない。

## 1. 問い・目的・対象外

問い: アプリが明示的に登録した同期コマンドを、内部呼出しと既存socket adapterで共有でき、内部の名前解決/実行がwire envelopeやVMに依存しないか。

内部からIDebugCommandDispatcherを直に呼ぶ案はwire/UniTaskを持ち込み、アプリごとの辞書は共有する解決規則を繰り返す。小さいcatalogとadapterを採る。実ゲームの新しい業務コマンド、VM、default dispatcher変更、SampleGame配線、外部UI/CLI、実socket往復、並行実行、Scene単位の登録lease、型付きpayload、handler中断は対象外。同一instanceをfixtureで共有できる証拠を、実アプリ配線済みとは呼ばない。

## 2. 凍結済みの契約と最低条件

### 所有・依存・時相

- アプリのcomposition rootがAppスコープのcatalogを作り、初期登録を完了した後、同じinstanceを内部呼出し側とCatalogDebugCommandDispatcherへ渡す。利用中のRegister、並行アクセスは非対応。初期登録限定は呼出し側義務であり、Freeze APIやthread guardを追加しない。
- catalogはhandlerの依存を取得/Disposeしない。handlerのclosureはApp寿命の依存だけを保持し、Scene所有オブジェクトの強参照登録は本slice非対応。App ownerは終了時に新規呼出しを止め、catalog/adapterへの参照を破棄する。Unregister/Disposeを形式的に追加しない。
- TryExecuteは同期で呼出し側thread上にhandlerを呼ぶ。Unity APIを使うhandlerはmain threadから呼ぶ。socket経路の既存MainThreadDebugCommandDispatcherは維持し、内部呼出しは呼出し側が同じ義務を負う。
- catalogはSystemだけに依存、adapterだけがwire DTOとUniTaskを知る。新asmdef edge無し。VMの採否に依存しない。

### 動作と失敗境界

- 名前はOrdinalの完全一致。Registerのnull/empty名はArgumentException、null handlerはArgumentNullException、同名重複はInvalidOperationException。空白文字を自動trimせず、空白だけの名前も非emptyとして現行どおり扱う。任意の新命名文法を追加しない。
- TryExecuteのnull/empty/未登録名はhandlerを呼ばずfalseと固定失敗result。boolは名前解決の成否であり、登録handlerが失敗resultを返してもtrue。業務成否はresult.Success。
- payloadはopaque JSON文字列として透過する。catalog直接呼出しのnull payloadはemptyへ正規化。handlerにJSON parseを強制しない。
- DebugCommandResultのコンストラクタはnull message/payloadをemptyへ正規化する。default(struct)のstring値はnullになり得る。adapterはdefault resultも既存envelopeのemptyへ正規化する。これは公開APIの変更を要しない利用上の説明とする。
- handler例外はcatalogとadapterを通じて呼出し側へ伝播。socket routerを実際に通る経路は既存の例外→失敗envelope変換を使う。adapterを直接呼ぶだけでは例外変換しない。
- adapterはRequestId、Success、Message、PayloadJsonを既存DTOへ写す。null commandは既存の固定missing失敗envelope。adapterがhandler呼出し前にtokenを調べた時点でcancel済みなら、handlerを呼ばず固定canceled失敗envelope。token検査後の競合や実行開始後の同期中断は保証しない。
- catalog直接呼出しにはtoken/キャンセル機構がない。必要な事前判断は呼出し側。新token APIを足さない。
- default CreateDebugCommandDispatcherおよび先行するbuilt-in ping/runtime-diagnosticsは変更しない。全debug命令がcatalogを通るとは主張しない。

### GOに必要な証拠

1. 上記の登録/名前解決/業務失敗/例外/payload境界を純粋テストで観測できる。
2. 同一catalog instanceをfixtureの内部呼出しとadapterに供給し、共有handlerの実行と転写が一致する。adapterのcancel済みtokenでは副作用が0回。handler例外はcatalog/adapterの直接呼出しで伝播することを試験する。既存routerによる例外変換は未変更sourceとの静的照合として説明し、socket実経路の実証には数えない。実socket往復は今回未実証と記録する。
3. ウォームアップ後の登録済み・割り当てないhandlerの2000回実行でcatalog自身の追加allocation 0。handlerやenvelope生成全体のzero allocationは主張しない。
4. 元のdefault dispatcher/組込経路/他Unity機能を含む最終全EditMode回帰、offline suite、contract/docs auditが固定headで合格し、C/C'が同じevidenceを判定する。

上記が揃えばGOとし、実ゲームコマンドや新しい経路を増やさず終了する。未達はNO-GO/未判定。既存native終了stallは調査停止規則を守り、未確認のprocess終了や未実行を成功としない。A3後の追加は凍結条件または常時契約違反の修正だけ。新要求は後続へ送る。

後続の所有問い: 実アプリ配線/業務コマンドは「Debug command consumers」slice、Scene登録lease/並行性/型付きpayloadは同sliceで必要性が出た時だけ新A、script hostは「ScriptSystem再評価」。それらを本sliceの完了条件にしない。

## 3. 責務マップ・変更許可

- Runtime/DebugCommands/DebugCommandCatalog.cs: Ordinal名前解決と同期delegate呼出し、App-owned managed state。約65行→85行以内を目安に契約コメント。公開signature維持。Unity無しで試験できる。
- Runtime/DebugCommands/DebugCommandResult.cs: 経路に依存しない結果値。約31行→45行以内。default/nullとSuccessの説明、signature維持。
- Runtime/DebugSocketServices/CatalogDebugCommandDispatcher.cs: envelope転写と実行前token検査。約65行→85行以内。Appがcatalog参照を渡す。既存DTO/UniTaskだけに依存、例外変換は追加しない。
- OneStarMaker/Tests/DebugCommands/DebugCommandCatalogTests.cs: Unity assembly上のcatalog/adapter境界。約93行→200行以内を目安。上記GOに必要な不足ケースを追加。Unity I/Oや実socketを新たに起動しない。
- tools/DebugCommandOfflineTests/Program.cs: 同じ製品sourceをリンクした純粋契約試験。約153行→220行以内を目安。source grepは構造判定の代用とせず、動作の反証を優先。csproj依存を増やさない。
- docs/handoff/DEBUG_COMMAND_CATALOG.md: この承認revisionとB結果/証拠locatorsの作業台。旧PHASE_A snapshotは履歴で保存し、新凍結snapshotをGit外に固定。Dで2つの完了HANDOFFを削除。
- unity/Assets/Docs/Architecture/12-telemetry.md §10.5: incoming controlの利用契約を30〜50行程度追加。既存のtransport説明に隣接し、別の恒久設計文書を増やさない。大きい既存文書の全面再編は対象外。

配置理由: managed policy / wire adapter / testsの依存と変更理由が異なるため既存の3型を維持。取得/登録/使用の一つの契約を架空のManagerへ分散しない。3型はいずれも500行未満、予想50%以上増加は主に小さいファイルの契約説明で、中核責務追加ではない。規模は分割命令ではなく再確認の警報。

## 4. 実装と検証経路

承認後に専用worktree/branchで現PRへdevelopを履歴保存mergeし、衝突があれば通常解決する。公開済み履歴を書き換えない。製品修正は上記契約説明と不足ケースに限定し、必要な動作差が判明した場合も契約と公開signatureの内側で直す。新API、owner、依存、handlerのasync化が必要ならAへ戻す。

常時制約: Game→Frameworkの一方向と既存asmdef参照を維持。編集するUnity C#は先頭 `#nullable enable`、record禁止、Editor依存は追加しない。SceneState 14値と所有者は不変。破棄されるUnity objectは `== null` / `!= null` で確認し、公開ログはILogger<T>。Update登録・asset取得・Scene/Prefab/Addressables編集は本sliceには追加しない。テストのTask.Delay/Thread.Sleepは禁止し、必要な待機はsignalで制御する。

BはUnityテストを実行しない。contract auditを実施し、compile確認の有無を記録して固定headをCへ渡す。C発見は構造/契約を先に確認し、明確な差し戻し中は最終全件を起動しない。

- 差し戻し起点: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.DebugCommands`（Phase Cのみ）。根拠があれば影響する既存dispatcher/routerのfilterを加える。
- 最終: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj -c Release`、`pwsh tools/run-tests.ps1`（空filter、全EditMode）、`pwsh tools/contract-audit.ps1 -BaseRef d4ea79b566b259db0d63a8ce1802947f536ccea5`、`pwsh tools/docs-audit.ps1`、`git diff --check`。
- 全EditMode適用除外: なし。PlayMode/Player/build/目視/音声/実socket往復は現GO条件にない。
- 実行担当: Phase C。Windowsで最初からrequire_escalated、Unity6000.6.0f1/標準runner、起動前に対象projectと既存Editorを確認する。Editor exe所在は確認済み。このPR headのUnity起動は未確認、初回確認はC。基盤不成立ならrawを残し修復可能範囲を処理、無断のtest除外/人間への実行丸投げはしない。
- 既知native stallはREADMEの停止規則を引き継ぎ、同一症状の再調査予算をtask変更で戻さない。
- 証拠: 最終base/head完全diff、凍結A、所見なしB、raw log/XML/step.json、実行command/time/exit、全実行名と件数。ignored TestResultsへ保存し、zip受領copyのhash/lengthをC/C'前に検証、保持期限最低2026-11-04。台帳をPR本文へ引き継ぐ。
- C': B/Cと異なる新規gpt-5.6-solを予約。C所見/可変HANDOFFを除くblind bundleで判定。Bはgpt-6.1-sol、Cはgpt-6-astraを候補とし新規sessionで開始する。実績は実行後に記録。

## 5. A2 / A3 と現在の状態

旧BはGrok、独立A2/A3未実施だったため本revisionで再開。queue-reassessment-r1の同一入力をgpt-6-astra（architecture）とgpt-6-sol（premise）が独立評価し、方向を支持。architectureのcancel経路限定を採用し、この自己完結候補r1を作成した。両担当が同じr1を独立評価し、architectureは必須指摘0、premiseはrouter証拠境界の明確化を求めた。r2で直接catalog/adapterの例外伝播試験とrouterの静的説明を分け、opt-in文書と後続責任を明記。新APIや実socket試験は増やさず、指摘を採用した。

人間のA3承認: 2026-10-05。提示したr2で進める確認に対し「まあ正直あってもなくても、なんだよな。無駄にはならんからやってもいい。が正直なところ」と回答。必須基盤としての必要性が証明されたという意味にはせず、任意の小さい共通部品として、API追加なし・App所有・起動時登録・同期実行・契約説明/不足テスト・全EditMode・C/C'・マージまでの範囲を承認と記録する。他PRの再設計・追加機能は含まない。B修正、C、C'、D: 未実施。#87の合格を本PRへ流用しない。

## 6. 再開revisionの引渡し台帳

- A3 snapshot: `D:/repositories/unity/SampleGameForOneStarMakerFramework/TestResults/pr92-integration/phase-a-frozen.md`、生成2026-10-04T15:51:12.6113324Z、SHA256 `885298d98af58409ded295bed5400e5fe38e05140667a0660e5c8ee6261c194b`。候補r2は別名で保存済み。
- 専用branch: `codex/pr92-contract-review`。公開tip `f61be6684662ced6f8b2a5f57753307af0bb920f` に develop baseを通常mergeした `e6223adab05b9c342359a91d6cbef599e6b25dfb` から修正。公開履歴の書換えなし。mergeと製品修正は統合履歴と契約補完という別の単位で記録する。
- B担当: fresh gpt-6.1-sol。製品の3型の契約説明、catalog/adapterとofflineテスト、公開§10.5を補完。公開APIと既存の実行動作は変更しない。所見なしB snapshotを同evidence rootの `phase-b-result.md` に固定予定。
- B確認: offline 6/6、contract audit 569ファイル errors0/warnings0、docs audit110ファイル errors0/warnings0。Unity Editor compile/UnityテストはBでは未実行。最終headでCが判定必須を実施する。
- 判定evidence / C / C' / D: 未実施。固定commit、full diff、生結果、bundleと取得案内を確定後に追記。保持期限2026-11-04。同一マシンの受領copyをhash/length検証し、新規担当が読めることを確認する。remote agent向け取得成立は主張しない。
