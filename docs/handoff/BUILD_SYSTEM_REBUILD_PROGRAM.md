# BuildSystem刷新 — 全体計画と継続開発の引継ぎ

- type: program
- status: 継続中。次はBS2b Phase A。個別スライスの実装凍結ではない。
- branch: `codex/build-system-program`（本program文書の整備用。各実装は専用ブランチ）
- planning base: `65d1b91`（2026-09-17のdevelop）
- risk: high（後続の build 出力、runtime identity、所有者・寿命の設計に関係する）
- owner: BS刷新の主担当。各slice開始時に実行担当と現在地を更新する。
- created: 2026-09-17
- expires: RET Phase D。各slice Phase Dで進捗と後続課題を見直し、2026-10-17までに継続要否・前提を再確認する。
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`、`13-resource-system.md`、`20-variant-checkout-workflow.md`、必要に応じて`04-app-startup.md`。実装済みの契約だけを反映し、RET Phase Dで本計画を削除する。

## 1. 目的・現在地・全体の順序

Unity 6.6 Content Directoriesを用いて、content選択、build、Runtimeロード、Player起動、配信とcacheを分離し、
代替経路が成立してから旧Addressables BuildSystemを廃止する。Scene以外のVariant拡張も妨げない。
本書をBS刷新のprogram正本とし、各sliceの順序、最低条件、未決事項の所有者、作業継続条件を引き継ぐ。
各sliceは本書を入力に自己完結したHANDOFFを作り、Phase Aで詳細を凍結してからPhase Bへ進む。

復帰したPre-Phase A v3を歴史的入力として読み、現行実装と完了済みHANDOFFの履歴で更新した。
古いEditor版、人間によるEditor起動待ち、未実装interface案は引き継がない。PRE自体は編集・追跡追加しない。
本書だけで後続を開始でき、未追跡PREや過去の会話を必須入力にしない。
先のAssetDescription拡張programは本書へ統合し、別正本として残さない。

現在地（2026-09-17）:

- U66: 完了。`ProjectVersion.txt`は6000.6.0f1。バージョン移行を再実行しない。
- CD0: 完了、限定実証によるCONDITIONAL採用。Mono/High strippingでroot・Object・Sceneの往復と移設を実証。
  IL2CPP/AOT、HTTP、処理中native loadの取消、全incremental matrixの保証ではない。
  `unity/Assets/CD0Spike/`はPhase D（`bda2ed7`）で削除済み。本番adapterとして復元せず、履歴の実証と公開文書を設計入力にする。
- BS1: 完了。Unity非依存のselection coreを実装済み。
- BS2a: 完了。SceneResource production materializationを実装済み。既存build/Runtime経路へは未接続。
- BS2b以降: 未着手。旧Addressables経路は残存し、Player build可を現行の前提にしない。

実行順序:

```text
U66 → CD0 → BS1 → BS2a                 完了
                       ↓
                     BS2b              Content Directory build
                       ↓
                     BS2c              SampleGameの選択policyと入力配線
                       ↓
                     BS3               Runtime backendとEditor Play
                       ↓
                     BS4               Player build / bootstrap
                       ↓
                     DIST              配信・local cache・開発workflow
                       ↓
                     RET               旧経路の廃止
```

BS2cは今回明示した接続工程の名前。既存の完了sliceや実装済み機能ではない。
独立して進められる調査は並行可能だが、未確定の上流形式へ依存する実装は先行させない。
非Scene本番拡張は実需要時に担当sliceを挿入し、全型の実装を本線の必須条件にはしない。

ユーザーが選択した方針:

- 対象は個別にロードする論理アセットの差し替え。Scene→Prefab→Material→Texture等の直参照を書き換える仕組みは対象外。
- BS2b前は境界を設計し、BS2bで非Scene入力を検証する。個別Description型の本番実装は実需要時に行う。
- 将来のMeshには、FBX等の同一ファイル内にある複数Meshの個別指定も含める。ただしBS2bの完了条件にはしない。
- BS2aの完了判定は維持する。非Scene source、optional content、alias/shared physical assetをBS2aへ遡及追加しない。

## 2. 現況と設計境界

現況の根拠は[AssetDescription設計文書](../../unity/Assets/Docs/Architecture/18-asset-description.md)と実装。

- `AssetDescription` / `IAssetPayloadProvider`および旧`IAssetDescriptionSource`には種別追加の拡張口がある。
  ただし旧Collector/Whitelistの契約が、新しいmaterializationへ自動的に接続されるわけではない。
- BS1 SelectionはUnityやアセット種別を知らない。BS2aの`SceneResourceContentMaterializer`がScene専用の入力を作る。
- `ExactlyOne`、空Variant→`Representation=Full`、SceneResourceのidentityからのキー生成はScene adapterの規則。
  全種別に適用する既定値にはしない。他種別の必須性・選択数・タグ規則はそのsourceの設計で決める。
- `BuildMaterializationSnapshot`のconstructorはinternal、tag provider識別はScene固有であり、汎用source合成APIは未実装。
- 現行materializationはファイルGUIDをphysical key/依存閉包のlookup keyに使う。
  同一physical keyの候補はmaterializationとSelection双方で衝突になる。サブアセット対応済みとは扱わない。
- `AssetKey`にはScene型判定とアドレス末尾によるカテゴリ推定がある。これは汎用ロード対象の型検証やサブアセット識別の契約ではない。

今固定する境界は次の4つ。

1. **論理アセット:** 利用側が要求する対象。同じ対象のVariant候補をまとめる単位。
2. **選択候補:** ビルドで選択する表現。sourceがタグ、必須性、由来を与える。
3. **ファイル:** ビルド入力と依存閉包を取得する単位。現在はGUID/pathで識別する。
4. **ロード対象:** 実際に取得するSceneまたはObject。将来は同一ファイル内の複数Objectを区別する必要がある。

3と4を同一概念に固定しない。具体的なlocatorやserialized schemaをこのprogramで先取りしない。
既存AssetDescriptionの埋め込み構造、serialized field、AssetOwner、SceneStateはこの計画の反映では変更しない。

program全体の責務境界:

- Framework selectionはproject語彙を知らない。SeasonやScene graph由来のタグはSampleGame側policyが供給する。
  Lighting/Environment/VFX等のScene roleを自動的にBuildTagへ変換しない。
- 生成root/configはbuild成果物。SceneResource graphやpayloadをauthoringの正本とし、生成物を手編集しない。
- build backendはUnity Editor I/O、Runtime backendはlocal directoryの登録とload、Distributionは取得・検証・installを担当する。
  HTTP、VCS、Scene lifecycleを同じ層へ混ぜない。FrameworkからGameへの依存を作らない。
- partitionは配信・更新・登録寿命の境界。BuildTagごとにdirectoryを作らない。最初の既定は選択済みcontentの単一directory。
- 常時契約の`IAssetManagement`、`AssetOwner`、SceneLifecycleManager所有のSceneState、ILogger<T>、Update順序を維持する。
  公開APIやasmdef edgeの変更が必要なら該当slice Phase Aに明示する。後続実装への白紙委任ではない。

## 3. 工程と進める最低条件

### BS2b — Content Directory build

答える問い: 成功したplanと対応するsnapshotから、Sceneと代表非Sceneを含むContent Directoryを生成できるか。

- 中核の入力は成功した`BuildPlan`と対応する`BuildMaterializationSnapshot`を基本とする。
  SceneResourceMap走査は呼出側adapterに置き、中核からSceneAssetDescription、Scene固有タグやprovider名を解釈しない。
- 候補とclosureの対応付け・検証はUnity Content Directories API / AssetDatabaseを呼ばない中核に置き、fixtureで単体検証する。
  root生成、AssetDatabase操作、Content Directory build、生成物cleanupはEditor adapterへ隔離し、実buildで統合検証する。
  build orchestrationとEditor adapterはOSMのEditor/Build配下へ新設する。Playerがdeserializeするroot/受渡しmetadata型は
  必要最小のRuntime側assemblyへ置き、UnityEditor依存を持たせない。具体配置・asmdef依存・owner・test境界はBS2b Phase Aで固定する。
  `CD0Spike`は復元しない。責務の違いはclass/adapterで分離し、実directory生成までを一つのsliceで証明する。
- 選択済み候補を既存snapshotの依存閉包へ対応付ける。Variantの再選択や依存閉包の再計算はしない。
  planとsnapshotの対応不整合や必要なclosure欠落はbuild前に失敗させる。
- 既存friend test assemblyで、PrefabとTextureをrootとする非Scene snapshot fixtureを作る。
  本番Prefab/Texture Description、汎用factory、source登録機構の追加は不要。
- 同じbuild中核でSceneと非Sceneを処理し、選択外Variant、共有依存、欠損、入力順序変更を検証する。
  異なるrootが依存ファイルを共有するケースと、複数候補が同じphysical keyを持つケースを混同しない。
- root生成・一時生成物のowner/path/cleanup、固定target/subtarget、出力先、build identity、incremental/hash、reportの形式をPhase Aで決める。
  選択・除外と理由、issueをbuild前に確認できるreportを持ち、失敗した成果物を成功出力として公開しない。
  成功buildと対応するContent BuildReportを後続へ渡す。全incremental matrixを無条件に追加せず、保証する範囲を固定する。
- build出力にcatalog/address等が必要でも、ファイルGUIDだけで任意のRuntimeロード対象を識別できるとは宣言しない。
- **成果物形式はBS2bが所有する。** Phase Aで、同一logical assetの複数表現を同一directoryへ格納するか別directoryに分けるか、
  起動時に表現を区別する情報の格納場所とRuntimeへの受渡し口、logical/表現/ロード先の対応を必須決定事項として凍結する。
  初期の単一directory方針との整合、build identityとBuildReportの対応、成果物へ保持するmetadataの範囲も固定する。
  この最小の受渡し契約をBS2cへ先送りしない。将来の全種別schemaやサブアセットlocator全体の実装を要求するものではない。
- 取得済み成果物からの起動は、ローカルのproduction materialization成功を要求しない。
  BS2bが成果物または保持するローカルmetadataへ必要な論理対応情報を渡す契約を決め、BS3がその入力だけで解決・起動する。
  BS2aのsource/依存欠損時にsnapshotを返さない契約は緩めない。source欠損での一連の実証はDISTが所有する。

最低条件は、固定targetと凍結したincremental保証範囲でSceneと代表非Sceneの実directoryが生成され、
Scene固有の前提が消費側へ漏れず、成果物・論理対応情報・BuildReportの対応を確認できること。
fixtureによる成功はconsumer境界の実証に限定し、本番source収集、source合成、サブアセットロードの成立とは記録しない。
Content Directoryのpartition、target/subtarget、output、incremental/hash等の詳細はBS2bのPhase Aが所有する。

### BS2c — SampleGameの選択policyと入力配線

答える問い: 本番Scene graphからproject固有の選択を導出し、意図した論理contentだけをBS2bへ渡せるか。

- Season/Representation schema、Season membershipの導出、共通content、bootstrapに必要なcontent、例外metadataの入口を決める。
  全Sceneへ手入力タグを複製せず、project側providerで導出する。FrameworkへSeason名を埋め込まない。
- 季節を絞る際は、要求されるlogical groupの範囲も定義する。全季節のExactlyOneを残したまま他季節を除外して欠落errorにしない。
- 現BS2aのExactlyOneと、PREのFull+Whitebox同時同梱例はそのまま両立しない。
  BS2cはBS2bの格納・受渡し契約に従い、単一表現buildと複数表現dev artifactの選択policy・cardinalityを凍結する。
  BS2aの既存契約を黙って緩めず、変更するpolicy/adapterと回帰条件を明示する。
- 最低条件は全季節Full、Spring Full、Spring Whiteboxの候補・必須group・除外理由が説明でき、実buildへ接続できること。
  Full+Whitebox同時同梱の開発用途を維持する設計と検証範囲もここで確定し、Runtimeの起動時選択はBS3へ渡す。
  本番graph規模の欠落・重複は機械検査、動作は代表箇所を検証し、標本確認を全件実行済みとしない。

BS2cはSampleGameのタグ導出、必須group、除外理由と選択入力を所有し、成果物schemaは所有しない。
BS2bの受渡し契約では表せない要件が判明した場合は、BS2bの契約を変更するPhase Aへ戻し、BS2c内で別形式を作らない。
新しいsource登録基盤全体や全アセット種別の導入は、この接続を証明するためだけに追加しない。

### BS3 — Runtime directoryとロード対象

答える問い: build済みlocal contentをsource再走査なしに解決・ロードし、利用資源と登録を一貫した寿命で解放できるか。

Phase Aで必ず次を決め、実装HANDOFFへ固定する。

- ファイルidentityとロード対象identityの関係。サブアセットを表現可能なlocator、永続化形式、互換性の境界。
  具体的なMesh型の実装が後続でも、ファイルGUIDだけを唯一の汎用Object IDにしない。
- logical asset/Variantから選択済みロード対象への解決と、ビルドに含まれる候補との整合性。
  Sceneのfallbackを他種別へ暗黙に流用しない。
  BS2bが渡す成果物または保持済みmetadataを読み、ローカルSceneResourceMapのmaterializationを起動条件にしない。
  metadata欠損・不整合時は構造化した失敗として扱い、source再走査を暗黙の必須fallbackにしない。
- Sceneのload/unload、通常Objectのload/release、Prefab生成インスタンスの寿命の違い。
  `IAssetManagement`と`AssetOwner`を入口とし、型別Descriptionへ新しい寿命管理者を作らない。
- load対象型の検証とカテゴリmetadataの区別。拡張子推定を正しさの根拠にしない。
- directory登録からroot discovery、load、全依存解放、unregisterまでを所有する単一ownerと、失敗・再試行・shutdownの順序。
  callerの取消をnative abortと同一視せず、発行済み処理の終端をownerが受け取りdrainしてから解放する。
  directory ownerはbackend/session側の責務とし、`IAssetManagement`へ登録・物理削除APIを当然には追加しない。
  公開面を変える必要があれば、BS3 Phase Aで変更内容と既存呼出側への影響を明示する。
- resident cacheにあるbackend資源もdirectory依存として扱い、使用中・cache内・処理中の資源を残してunregisterしない。
- revisionの利用開始と物理削除の排他契約はBS3 Phase Aが所有する。directory ownerが登録・処理中・resident cacheを含む
  利用保護を管理し、DISTが削除する間は新規利用・再登録を許さない。単なる「未使用か照会してから削除」にはしない。
  削除側は排他的な削除許可を取得し、完了または失敗の確定まで保持する。具体的なport、状態遷移、例外時の解除、
  対象process範囲はBS3 Phase Aで固定し、競合をfake削除側で検証する。物理ファイル削除そのものはDISTが担当する。
- BS2cの表現方針に従う起動時選択とEditor Playの接続。実行中切替UIは追加しない。
  高水準Scene lifecycleを維持し、Addressables backendとの移行期間の配線・切替を明示する。

代表非Scene fixtureを使って解決、ロード、ownerに応じた解放を検証する。
本番の個別Description型を全て揃えることはBS3の追加条件にしない。
成功だけでなく、欠損、登録失敗後の再試行、取消後完了、owner解放、cache eviction、終了時cleanupを検証して次へ進む。

### BS4 — Player integration

答える問い: 必要なcodeと設定を持つPlayerがcontentを二重同梱せず起動し、論理初回Sceneと代表contentを利用できるか。

Sceneに加えて代表非Scene fixtureのロード・解放をPlayerで確認する。
結果には検証した型と選択方式を明記し、Prefabの成功をMeshサブアセットの保証へ拡張しない。
- PlayerのScene一覧はbootstrapを基本とし、contentをPlayer本体へ二重同梱しない。
- build要求から生成したruntime configでlocal directory、logical first scene、表現選択を渡す。
  shared `app-config.json`の一時書換・復元を新経路の前提にしない。
- 対応するContent BuildReportとPlayer buildの関係、stripping、IL2CPP/AOT、対象platformをPhase Aで固定する。
  CD0のMono成功やreport受理だけでcode保持の因果・AOT成功を主張しない。
- 最低条件は固定targetでbootstrap→登録→論理初回Scene→代表content→終了が成立し、必要なcodeが保持されること。
  環境不足による必須Player検証未実行はGOに置き換えず、具体的な不足を残す。

### DIST — 配信・local cache・開発workflow

答える問い: remote/LANの成果物を検証済みlocal directoryとして導入し、既存の利用中revisionを壊さずロードできるか。

- UnityへURLを渡さず、OSMが取得・検証・install完了したlocal directoryだけをBS3へ登録する。
- OSM transport manifestのversion、content set/revision、target、相対path、size/hashと互換性をPhase Aで決める。
  Unityの内部manifestをそのまま外部protocolにせず、Unity Objectの依存解決をdownloaderへ再実装しない。
- v1は選択したdirectory revisionの必要file一式を取得する。部分取得状態をactiveとして登録しない。
  stagingで検証後にinstall完了へ切り替え、active revisionはimmutableとする。
- interrupted download、hash不一致、欠損、target/revision不一致からの再試行を検証する。
- DISTはknown-good保持、disk budget、削除候補の選定と物理削除を所有する。利用状態・削除可否を独自の台帳で再判定しない。
  BS3の排他契約で削除許可を得たrevisionだけを削除し、削除中の再登録を防ぐ。許可取得不可なら削除を延期し、
  容量不足時の結果を報告する。Runtime資源を保持したrevisionをbudget達成のために強制削除しない。
  新規利用との競合、利用中・resident cache保持中の拒否、物理削除失敗後の許可解除を接続して検証する。
- 開発者のlocal/remote選択、鮮度の扱い、必要なsource pathの案内とEditor Playを新経路へ接続する。
  依存閉包のローカル完結性を判定し、source欠損時にも取得済みcontentから実行できる経路を検証する。
  source path案内のための検査と成果物からの起動は分け、前者の欠損で後者を止めない。BS2b/BS3の入力契約を使う。
  未checkout Sceneの直接編集を可能とはしない。Git/SVN checkoutの自動操作は対象外。
- 最低条件はremote/LAN→install→register→代表Scene/Object loadが成立し、中断更新で既存playable revisionを壊さないこと。
  必要source pathの案内、local/remote選択、鮮度、Editor Playの置換範囲も検証する。
  旧workflowに残す機能があれば理由・移行先・ownerを明記してRETへ渡し、旧経路不要とは扱わない。
  配信先の実運用公開は別の明示指示に従う。検証用local endpointを本番配信済みとは扱わない。

asset単位のHTTP遅延fetch、delta patch、CDN最適化は後続の専用拡張。DIST v1の最低条件を増やさない。

### RET — 旧Addressables BuildSystemの廃止

答える問い: 新経路の利用者を維持したまま、置換済みの旧build・開発workflowを取り残しなく廃止できるか。

開始条件はBS2b〜BS4とDISTの必要経路が成立し、置換対象の各利用者に移行先があること。

- Phase Aで旧profile、whitelist、group一時mutation、hybrid play、remote build/catalog injector、checkout report、
  Player overlay、関連tool/docs/testsの利用者と置換先を列挙し、置換済み・意図的廃止・残存を区別する。参照0だけでは削除しない。
- serialized参照や設定の移行、廃止するmenu/CLIの案内、残す互換口、rollbackを凍結する。
- Addressables packageはRuntime、AssetPayload/AssetReference、third-partyまでusage scanし、不要化を確認した場合だけ削除する。
  package依存が残るならその理由とownerを明記し、「旧BuildSystem廃止」と「package全廃」を区別する。
- 最低条件は通常のbuild/Editor Play/Player/配信が新経路を使い、廃止対象の旧経路に隠れた利用者がなく、回帰検証と文書更新が完了すること。
- RET Phase Dでprogramの残課題を実需要別の担当へ移送し、公開文書に現況をharvestして本書を削除する。

### 非Scene拡張 — 最初の本番需要が発生した時点

実需要が出た種別だけについて専用slice HANDOFFを作る。BS2bより前に強制挿入しない。

- Descriptionの保持場所・型、収集元、論理キーの衝突範囲、必須性、選択数、タグ、対象型検証を定義する。
- 新materializationの構築・合成口とprovider identityを設計する。旧Collectorがあることだけで新経路を完成扱いしない。
- Meshサブアセットを扱うsliceでは、ファイルとObjectの対応、同一ファイル共有、aliasの意味を決める。
  materializationとSelection双方の衝突規則を確認し、単に重複チェックを外して対応しない。
- 同一ファイル内の異なるMeshが混同されないこと、ファイル依存を共有できることを検証する。
  BS3/BS4完了後の追加でも、その種別に必要なRuntime/Player回帰検証はこのsliceで行う。

## 4. 別セッションへの引継ぎ・レビュー・停止規則

- BS2b開始時は本書と現行実装を読み、§3のBS2b条件をslice HANDOFFの受け入れ条件へ転記する。
  本書に書かれていないbuild詳細を実装中に決めず、Phase Aで解決する。
- 各sliceは`osm-workflow`に従い、1 slice / 1 branch / 1 HANDOFF、PR baseはdevelopとする。
  実装base/headとPhase A snapshotを固定し、Phase境界では新規セッションへ規定の入力を渡す。
- 新しいasmdef参照、公開API、永続化形式、所有者・寿命の変更は該当sliceのPhase Aで明示する。
- 本計画のarchitectureレビューは別agentが同じ会話・計画を読み実施。同一系列であり、複数モデルの独立レビューやC'完了とは扱わない。
  指摘した「fixtureの実証範囲限定」「build IDをRuntime IDとして固定しない」「BS3 Aでload identityを決める」を採用済み。
  全体統合時にも別agentがPRE・公開文書・統合案をレビューし、BS2cでのpolicy接続、cacheを含むdirectory寿命、
  DISTの開発workflow検証、RETの機能別移行確認を採用した。旧13のSO基底図は現行のSerializable埋め込み構造へ訂正した。
- PR #59のレビュー（head `3da2bc0`）を受け、成果物契約のBS2b所有、pure対応付けとEditor I/Oの分離、
  source未取得時の起動入力、BS3の利用保護とDISTの削除処理の排他を追記した。
  slice追加分割や削除I/OのBS3集約は採用せず、一つのslice内の責務分離と単一の利用保護契約で指摘の問題を解消する。
- この文書整備セッションは計画だけを反映する。以下の継続開発委任は別の開発セッションへ適用する。
- 各sliceの最低条件を満たせば終了する。Prefab/Texture/Mesh全種類の完成や、将来のsource合成の完成をBS2bの終了条件へ追加しない。
  現sliceを阻害する問題は違反する受け入れ条件を明記し、その他は上記の所有sliceへ送る。

### 不在中の継続開発に関するユーザー委任（2026-09-17）

ユーザーは別セッションでBS開発を続け、人間の在席を前提にせず、必要な権限承認だけリモートで行う運用を希望している。
本program内の通常の設計判断とA2 findingsの採否・A3凍結は主担当へ委任されたものとして扱い、判断理由を記録する。
これは今回のユーザーの自律進行指示を本program内のA3採否へ適用するもので、共通Skillの人間確認を全作業から削除する変更ではない。
この委任を共通Skillへharvestしない。各高リスクsliceはA2の担当・モデル・独立性と、制約があればその内容を実績欄へ記録する。
各sliceのA→B→C/C'、失敗修正、機械監査、証拠作成、ローカルcommit、push、develop宛てPR作成まで、
単なる継続確認のために止まらない。高リスクのA2独立レビューやPhase分離を省略する許可ではない。

- 技術的に決められる未決事項は調査・比較してPhase Aで閉じる。新しいpublic APIやasmdef参照もPhase Aで設計判断を明記してから実装する。
  Phase Bで新たな設計判断が必要ならPhase Aへ戻すが、委任範囲内なら主担当が再設計・レビューを継続する。
- Unity Editorが閉じていても必要なら正しいprojectを確認して起動し、限定したEditor操作・コンパイル確認を行う。
  本番assetは凍結済みslice範囲内だけ。未保存の人間所有Editorを無断で強制終了しない。
- テストとBuildの実行・判定はPhase C。標準`pwsh tools/run-tests.ps1`を使い、Windowsでは初回からsandbox外の承認済み経路を使う。
  `unity test` / `unity run`は禁止。licensing接続と進行を確認し、接続失敗を無期限に待たず、license fileの削除・返却を回避策にしない。
- CはBと異なるモデル、AIのC'はB/Cと異なるモデル・新規セッション・blind bundleを使う。
  各Phaseは新しいcontextのsubagent等へ規定のsnapshotを渡して実行できる。ユーザー所有の新規タスクを無断作成しない。
  担当を確保できない場合は独立監査済みと記録せず、可能な証拠準備まで進めて不足を報告する。
- 通常の確認は繰り返さず、進捗と判断はHANDOFFへ残す。承認待ちで止まるときは、具体的な操作・必要理由・再開条件を一度で提示する。
  権限承認はアプリ側の機構に従い、この文書で回避しない。

人間へ戻す条件:

- programの目的・範囲変更、常時契約の例外、凍結後に新しい要求を取り込むための受け入れ条件置換または検証予算追加。
- ユーザーの未保存作業を失う操作、未承認の広範な本番asset上書き、外部配信の実公開、必要な秘密情報・課金等の本人判断。
- 必須検証を実行できずGOの根拠が不足する場合、必要な独立監査担当が確保できない場合。
- Phase Dの最終マージ判断。`osm-workflow`の人間ゲートを維持し、本委任だけで無断マージしない。
  C/C'まで終わったらPRと根拠・残存リスクを提示し、明示されたマージ指示の範囲で後始末とharvestを行う。

実行中セッションの自律継続方針であり、未起動の次セッションや定期実行を自動作成したことを意味しない。
merge待ちの間は独立した次sliceの読取調査や計画準備を進められるが、未マージの前提で本線実装を無制限に積まない。

### 各slice終了時に引き継ぐ記録

- 現在のslice/Phase、branch、PR、固定base/head、完了条件の判定、次の一手。
- C/C'証拠と独立性、未実行の検証、後続へ移した問いとowner、権限待ちがあればその操作。
- 使用中のEditor/project、生成成果物の場所とcleanup状況、人間の未保存作業との区別。
- merge後は完了sliceの恒久知見を公開文書へ移してslice HANDOFFを削除し、本programの現在地を更新する。
  全体の順序や未着手課題まで完了sliceと一緒に消さない。

計画文書の検証は`git diff --check`と`pwsh tools/docs-audit.ps1`。
恒久文書へ未実装機能を実装済みとして移さず、各sliceのPhase Dで成立した範囲だけをharvestする。
