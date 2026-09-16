# BuildSystem — AssetDescription 多種別拡張の工程計画

- type: program
- status: 工程方針合意済み。BS2b の Phase A 入力。個別スライスの実装凍結ではない。
- branch: `codex/asset-description-extension-plan`
- planning base: `bd426b7d13567b686b7a8f1304a7c4db3fe00bba`
- risk: high（後続の build 出力、runtime identity、所有者・寿命の設計に関係する）
- owner: BS2b〜BS4および非Scene拡張の各Phase A担当
- created: 2026-09-17
- expires: BS4 Phase D または前提変更時の早い方。残課題は担当する拡張HANDOFFへ移送して本計画を終了する。
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`。実装済みの契約だけを順次反映し、完了時に本計画を削除する。

## 1. 目的と合意した範囲

BS2bに進む前に、将来Prefab、Texture、Mesh等のAssetDescriptionとVariantが追加されても、
新BuildSystemがScene専用に固定されないための境界と検証時期を決める。
この文書は別セッションへの設計入力であり、BS2b全体の凍結済み実装HANDOFFを代替しない。
BS2b担当は本書の条件を自身のPhase Aへ取り込み、build固有の未決事項を解決してからPhase Bへ進む。

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

## 3. 工程と進める最低条件

### BS2b — Content Directory build

答える問い: 成功した選択結果と対応するmaterialization snapshotから、Scene以外も扱えるbuild中核を構成できるか。

- 中核の入力は成功した`BuildPlan`と対応する`BuildMaterializationSnapshot`を基本とする。
  SceneResourceMap走査は呼出側adapterに置き、中核からSceneAssetDescription、Scene固有タグやprovider名を解釈しない。
- 選択済み候補を既存snapshotの依存閉包へ対応付ける。Variantの再選択や依存閉包の再計算はしない。
  planとsnapshotの対応不整合や必要なclosure欠落はbuild前に失敗させる。
- 既存friend test assemblyで、PrefabとTextureをrootとする非Scene snapshot fixtureを作る。
  本番Prefab/Texture Description、汎用factory、source登録機構の追加は不要。
- 同じbuild中核でSceneと非Sceneを処理し、選択外Variant、共有依存、欠損、入力順序変更を検証する。
  異なるrootが依存ファイルを共有するケースと、複数候補が同じphysical keyを持つケースを混同しない。
- build出力にcatalog/address等が必要でも、ファイルGUIDだけで任意のRuntimeロード対象を識別できるとは宣言しない。
  後続Runtimeとの形式互換を固定する必要がある場合は、BS3のidentity設計を前倒ししてからBS2bを凍結する。

追加の最低条件は、非Scene rootが同じbuild中核を通り、Scene固有の前提が消費側へ漏れていないこと。
fixtureによる成功はconsumer境界の実証に限定し、本番source収集、source合成、サブアセットロードの成立とは記録しない。
Content Directoryのpartition、target/subtarget、output、incremental/hash等の詳細はBS2bのPhase Aが所有する。

### BS3 — Runtime directoryとロード対象

Phase Aで必ず次を決め、実装HANDOFFへ固定する。

- ファイルidentityとロード対象identityの関係。サブアセットを表現可能なlocator、永続化形式、互換性の境界。
  具体的なMesh型の実装が後続でも、ファイルGUIDだけを唯一の汎用Object IDにしない。
- logical asset/Variantから選択済みロード対象への解決と、ビルドに含まれる候補との整合性。
  Sceneのfallbackを他種別へ暗黙に流用しない。
- Sceneのload/unload、通常Objectのload/release、Prefab生成インスタンスの寿命の違い。
  `IAssetManagement`と`AssetOwner`を入口とし、型別Descriptionへ新しい寿命管理者を作らない。
- load対象型の検証とカテゴリmetadataの区別。拡張子推定を正しさの根拠にしない。

代表非Scene fixtureを使って解決、ロード、ownerに応じた解放を検証する。
本番の個別Description型を全て揃えることはBS3の追加条件にしない。

### BS4 — Player integration

Sceneに加えて代表非Scene fixtureのロード・解放をPlayerで確認する。
結果には検証した型と選択方式を明記し、Prefabの成功をMeshサブアセットの保証へ拡張しない。
bootstrap、stripping、IL2CPP/AOT等の詳細条件はBS4のPhase Aが所有する。

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
- 各sliceは`osm-workflow`に従う。Phase Bは対象を限定したEditor操作・コンパイル確認まで、テストとBuildの実行・判定はPhase C。
  Unityテストは`pwsh tools/run-tests.ps1`を使い、`unity test` / `unity run`は使わない。
- 新しいasmdef参照、公開API、永続化形式、所有者・寿命の変更は該当sliceのPhase Aで明示する。
- 本計画のarchitectureレビューは別agentが同じ会話・計画を読み実施。同一系列であり、複数モデルの独立レビューやC'完了とは扱わない。
  指摘した「fixtureの実証範囲限定」「build IDをRuntime IDとして固定しない」「BS3 Aでload identityを決める」を採用済み。
- このセッションは計画文書の反映だけを行う。コード、asset、Unity test/buildの実装・実行を含めない。
- 各sliceの最低条件を満たせば終了する。Prefab/Texture/Mesh全種類の完成や、将来のsource合成の完成をBS2bの終了条件へ追加しない。
  現sliceを阻害する問題は違反する受け入れ条件を明記し、その他は上記の所有sliceへ送る。

計画文書の検証は`git diff --check`と`pwsh tools/docs-audit.ps1`。
恒久文書へ未実装機能を実装済みとして移さず、各sliceのPhase Dで成立した範囲だけをharvestする。
