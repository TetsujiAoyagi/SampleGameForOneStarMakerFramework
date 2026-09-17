# BS2b — Content Directory build

- type: slice
- status: Phase C/C' GO、Phase D develop マージ判断待ち
- branch: `codex/bs2b-content-directory-build`
- implementation base commit: `ef3ad21`
- implementation head commit: `c445f49`
- risk: high（生成物形式、Editor build、Runtime 受渡し）
- owner: BS2b 主担当 / Codex
- created: 2026-09-17
- expires: 2026-10-17。Phase D で判断し、残す契約を公開面へ harvest する。
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`、`13-resource-system.md`、`20-variant-checkout-workflow.md`
- Phase A snapshot: `docs/handoff/evidence/bs2b/phase-a-snapshot.md`、2026-09-17 07:58 JST、SHA-256 `d18f4af428c18cb4ac1ee3807d9f8ea051d3f1906972c8685875ac0888f7da41`
- Phase B result snapshot: `docs/handoff/evidence/bs2b/phase-b-result-v2.md`、2026-09-17 09:43 JST、SHA-256 `34408e677b340aeacf2995392b9624f1f30b67dd07c1a2ee2f5052db4999df4c`。`78cd163` 対象の旧 snapshot とレビュー結果は無効化。
- Phase B comment addendum: `docs/handoff/evidence/bs2b/phase-b-comments-v3.md`、2026-09-17 12:35 JST、SHA-256 `12156ca42257ad8a921ac2c984e6821fb7b352505ab5f25200d820cb63d57d29`。
- evidence / C' snapshot: `docs/handoff/evidence/bs2b/manifest-v4.md`、2026-09-17 13:02 JST、SHA-256 `4468fb33cc39b72c38bfd1f0d889fb270e0ba0f45582b746cc0a74ea8bab2836`。`manifest-v2.md` は `c0fff31` の判定用に保持。

## 1. 目的、現在地、対象外

問いは「成功した BuildPlan と対応する BuildMaterializationSnapshot から、Scene と代表非 Scene を含む Content Directory を生成できるか」。BS1 は選択を、BS2a は SceneResource の候補と依存閉包を生成済み。Build 経路と Runtime 経路は未接続。旧 Addressables build は維持する。

対象外は SampleGame の本番選択 policy（BS2c）、Runtime 登録・解決・寿命（BS3）、Player（BS4）、配信と cache（DIST）、旧経路廃止（RET）、非 Scene Description の本番実装、同一ファイル内サブアセットの汎用 locator。`CD0Spike` は復元しない。

## 2. A0: 入力、制約、未決事項

- 入力は成功済み `BuildPlan` と同一候補集合を持つ `BuildMaterializationSnapshot`。選択をやり直さず、`AssetDatabase.GetDependencies` で閉包を再計算しない。snapshot は Scene 専用の tag provider 名を持つが、build consumer はそれを解釈しない。
- 候補は `StableKey`、`LogicalKey`、ファイル GUID の `PhysicalKey` を持つ。閉包は root GUID / path と各依存 GUID / path。異なる root の共有依存は許し、二つの候補が同じ physical key を所有することは許さない。
- 既存 snapshot constructor は internal。非 Scene の Prefab / Texture fixture は既存 friend test assembly で作り、本番 factory や source 登録を増やさない。
- 常時契約: Game → Framework、一方向依存、asmdef edge は設計判断、Editor コードを Runtime に置かない、Unity C# の `#nullable enable`、`record` 禁止、偽 null、SceneState と AssetOwner の維持。
- 未決事項は build root の参照表現、Content BuildReport の保存形式、生成・出力 path の所有、固定 target/subtarget、増分保証、論理対応 metadata の形式。以下 A1 で決め、A2 で検証する。

## 3. A1: 意思決定境界

### 進める最低条件

固定 target/subtarget の一つの Content Directory を、成功した plan と対応 snapshot から Scene・Prefab・Texture fixture について実生成する。build consumer は Scene 固有情報を使わない。選択された logical/representation/load target と成果物および対応する Content BuildReport が同じ build identity に紐づくことを検証する。

### 受け入れ条件

1. Unity I/O のない中核が plan と snapshot の候補対応を stable/physical/logical key で検証し、選択 root と既存閉包だけを canonical 化する。欠損、余分な selected candidate、root/path/GUID 不整合、同一 physical key 衝突を build 前の構造化 issue にする。入力順序で結果を変えない。
2. Scene と非 Scene の選択候補を同じ中核で扱う。非選択 Variant の root は含めず、選択 root が共有する依存ファイルは保持する。closure の共有と候補 physical key の衝突を区別する。
3. Editor adapter が生成 root を作り、選択 root の Scene `LoadableSceneId` と Object `LoadableObjectId` を格納する。root は build identity を持つ。catalog 内のロード用 identity は Unity が作る reference とし、ファイル GUID 単独を任意 Object のロード identity と呼ばない。
4. 同一 logical asset の複数表現を一つの directory に格納できる形式とする。表現は候補の `Representation` tag を snapshot から読み取って metadata に保存するが、tag provider 名と Scene 語彙には依存しない。tag の無い候補は空表現として保存する。BS2b は複数表現の選択 policy を変更しない。
5. runtime 側の単一 `BuildContentRoot : ScriptableObject` に schema version、build identity、target と entry 配列を保持する。entry は logical key、stable key、表現値、種類（Scene/Object）、その index に対応する `LoadableSceneId` / `Loadable<UnityEngine.Object>` を持つ。Object の Unity reference は file ID を含む `LoadableObjectId` から Editor で構築する。BS3 は local directory の登録後に `ContentLoadManager.GetRootAssets<BuildContentRoot>(handle)` が厳密に 1 件であることを確かめ、この root だけで logical→load target を復元する。本番 source 再走査や別 sidecar は要求しない。サブアセット一般化と型検証は BS3。
6. build 前 report は選択、除外理由、issue、root/closure、target、identity を機械可読で示す。失敗時は成功出力を公開しない。成功時は Unity `Content BuildReport` と同じ identity を持つ root、summary、Unity manifest pointer (`BuildManifestHash.txt`) と report/metadata directory location を返す。Unity 内部ファイル一覧を解釈しない。
7. 実 directory の Scene、Prefab、Texture root と非選択 root の不在、共有依存、root metadata の対応を統合検証する。別 logical の共有依存に加え、同じ logical の異なる physical key を持つ 2 候補を `OneOrMore` の非 Scene fixture で選択し、異なる表現、root entry と loadable ID が同じ directory にあることを検証する。directory を新しい local path へ複写し、登録・単一 root discovery・identity 照合を source 再走査なしで確認する。固定 target/subtarget に限定し、incremental は同じ build workspace への再 build 成功と report の対応まで保証する。content hash の安定性や変更 matrix は保証しない。

### 成果物・I/O の凍結案

- partition は選択済み content の単一 directory。固定 target と content set 名に対応する `artifacts/bs2b/work/<target>/<content-set>/` を Unity の再 build workspace として反復利用する。成功後、完全な directory 一式を `artifacts/bs2b/content/<build-id>/` へ staging 経由で複写・原子的に publish する。build-id は UTC timestamp と乱数の衝突しない識別子。content set 名は呼出側が指定する安定した ASCII 識別子で、path segment として検証する。指定できる root は `artifacts/bs2b` 配下へ正規化・制限する。既存成功 directory を上書きしない。
- `Assets/OneStarMakerGenerated/BS2b/<build-id>/` の生成 root は Editor adapter が所有する一時資産。成功・失敗いずれも AssetDatabase で削除し、孤児は次回開始時に ownership marker を確認して限定 cleanup する。非生成 assets は削除しない。
- target は Windows Standalone 64 bit、subtarget は Player。Editor の active target が一致しない場合は build 前に失敗させ、自動切替しない。compression は Uncompressed、BuildContentOptions.None。対象 platform が無い環境では NO-GO と記録する。
- `Content BuildReport` は Unity 返却 object と生成 report/metadata directory の存在を検査し、summary を記録する。Unity 内部形式を OSM 外部 protocol にしない。build identity は root、OSM report と Unity build `name` に一致させる。Player metadata location は BS4 へ渡すが、Player build への注入自体は BS4 の責務。
- report は preflight と outcome を別ファイルへ書く。Unity build は固定 workspace で行い、成功と metadata 検証後に全 content file を staging へ複写して final directory へ移す。失敗した workspace は診断と再試行のため保持するが成功 path として返さない。失敗 staging だけを限定 cleanup する。出力先の containment と既存 directory 保護を行う。

### 判定と停止規則

GO は最低条件と全受け入れ条件が同一固定 target の生テスト、実 build、report/manifest 検査で成立し、現在の問いに致命的反証がない場合。未達は NO-GO。スパイクではないため CONDITIONAL ACCEPT は使わない。条件成立後は後続課題を owner に渡して止める。A3 後の追加は凍結済み条件または常時契約違反に限り、他は後続 slice へ移す。

## 4. 責務マップと配置案

| ファイル / assembly | 責務、変更理由、所有者・寿命、依存、テスト境界 | 現在 / 予想行数 |
| --- | --- | --- |
| `Editor/Build/Content/BuildContentProjection.cs` / 新 Editor asmdef | `OneStarMaker.Editor.Build.Content` の internal pure core。plan/snapshot 照合、closure canonical 化、issue。呼出し中だけの immutable data。Selection と Materialization に依存し Unity I/O 無し。friend tests で全分岐。 | 新規 / 250 |
| `Editor/Build/Content/BuildContentCoordinator.cs` / 同 asmdef | 同 namespace の public build 入口と internal `IContentDirectoryAdapter` port。preflight→Unity build→検査→publish と失敗 staging cleanup。1 run の所有者。projection と port に依存。fake port で失敗経路を検証。 | 新規 / 200 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` / 同 asmdef | 同 namespace の internal adapter。AssetDatabase、root 生成、Unity BuildPipeline、report。Editor I/O と生成 root の限定 cleanup の所有。実 build test。 | 新規 / 250 |
| `Editor/Build/Content/BuildContentReport.cs` / 同 asmdef | 同 namespace の public request/result/report と internal JSON serializer。preflight issue、選択・除外・closure、outcome の immutable snapshot。1 run 寿命。純粋テスト。 | 新規 / 150 |
| `Runtime/BuildContent/BuildContentRoot.cs` / Runtime asmdef | `OneStarMaker.Runtime.BuildContent` の public sealed root と entry 読取 API、Editor adapter が呼ぶ public 初期化口。Unity に serialize する Scene/Object reference と identity。build artifact 寿命。Editor 依存なし。 | 新規 / 150 |
| `Tests/Editor/Build/BuildContentProjectionTests.cs` / 既存 friend asmdef | Scene/Prefab/Texture fixture、欠損・衝突・順序・共有依存。 | 新規 / 250 |
| `Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` / 既存 friend asmdef | 一時 fixture の実 build、root/report 確認、複数表現と移設後の discovery、cleanup。 | 新規 / 220 |

新しい Editor asmdef は `Selection`、`Materialization`、`Runtime` を参照する。`Tests.Editor` から新 asmdef への参照を追加する。いずれもこの Phase A の明示判断とし、他の edge は増やさない。Runtime asmdef の中に Editor API は置かない。root の serialized field は Unity artifact lifetime。`AssetOwner` は Runtime load の BS3 で扱い、build 中の一時資源とは混同しない。500 行警報は現計画では発火しないが、実装時に増えるなら再評価する。

## 5. 実装、テスト、レビュー

順序: projection と純粋テスト → runtime 受渡し型 → Editor adapter / coordinator → 実 build fixture → Phase C。SceneResourceMap の走査や policy を consumer に入れない。中核で Unity API が必要、追加 public API / asmdef edge / owner が必要、root 形式が成立しない場合は Phase A revision へ戻す。

Phase C は実装 base/head の完全 diff と生ログを固定し、構造レビューを機能レビューより先に行う。`pwsh tools/contract-audit.ps1`、Editor を閉じて sandbox 外の `pwsh tools/run-tests.ps1`、必要な実 build を実行する。C' は新規セッションかつ B/C と異なるモデルの blind audit とする。

- A0/A1 主担当: Codex / GPT-6 Astra（OpenAI）
- A2 architecture: GPT-5.6 Sol / OpenAI、A1 を読んだ独立レビュー。root discovery 未固定、複数表現の実証不足、file/API/port map 不足を採用し、§3-4 に反映。
- A2 alternative: GPT-5.6 Terra / OpenAI、A0 と現行 contract のみを読む独立構成案。固定 build workspace の再利用、Unity manifest pointer と report/Player metadata location、active target の検査、成功 directory の全量 publish を採用。Player build 実行と metadata 注入は BS4 のため BS2b に入れない。Unity 内部 manifest の parse は採用しない。
- A3: program §4 の 2026-09-17 不在時委任に基づき主担当 GPT-6 Astra が上記採否を統合して凍結。高リスクのため A2 2件を実施。両レビューは OpenAI 系列なので強化条件の異ベンダー予約は満たさず、C' で可能なら未関与系列を使い、不可なら独立性制約と記録する。
- Phase B: 実装済み。初回 C/C' は `78cd163` を対象に実施。C の条件7指摘を採用し、`c0fff31` へ修正したため初回結果は無効。その後、依頼に従い `c445f49` で日本語コメントを追加し、同じ凍結条件で C/C' を再実施した。

## 6. Phase B 実装結果

- 実装: `340376f`、`78cd163`、`c0fff31`。Unity I/O を使わない projection、構造化 issue、Editor build orchestration / adapter、単一 Runtime root、preflight/outcome report、Prefab/Texture fixture を含むテストを追加した。新 asmdef は Phase A の依存方向に配置した。Selection assembly の internal constructor は既存 Editor test assembly へ限定公開し、無効な plan fixture も検証可能にした。初回 C の差戻し後、projection 専用型を internal に限定し、同一 workspace の連続 build、各 manifest/report、公開 directory の file と移設後 root の検証を統合テストに追加した。
- HANDOFF との差: build report の受渡し位置は Unity content 出力 directory を用いる。Unity 6.6 の `previousBuildReportDirectories` は build 出力 folder も受け付ける。Player 注入は BS4 のまま。
- 未実行: Unity Editor コンパイル確認、バッチテスト、実 Content Directory build。Editor は未接続で、sandbox 内の PowerShell 起動が停止したため、Phase C の batch 経路で確認する。
- 機械検査: `pwsh -NoProfile -File tools/contract-audit.ps1` を sandbox 外で実行、exit 0、変更 Unity `.cs` 9件、違反なし。`git diff --check` exit 0。
- Phase B 担当・モデル: 独立 context の subagent、GPT-5.6 Sol / OpenAI。commit 固定は主担当 GPT-6 Astra。
- 追加依頼: `c445f49` で BS2b の Unity C# 9 ファイルに日本語コメントを 106 行追加し、英語コメント 3 行を置き換えた。責務、入力境界、所有権、build identity、失敗時の片付け、テストの証明範囲を説明した。C# の非コメント行変更は 0。

## 7. Phase C

### 初回レビューと差戻し

`78cd163` 対象の初回 Phase C（GPT-5.6 Terra / OpenAI）は NO-GO とした。P1 / semantic / unique として、条件7の同一 workspace 再 build と実 directory の Scene・Prefab・Texture、除外 root、共有依存の検証が不足していた。P2 / semantic / unique として、internal pure core の補助型 `ProjectedContent` と `BuildContentProjectionResult` が public だった。いずれも採用し、`c0fff31` で補った。初回 C'（GPT-5.5 / OpenAI）は GO だったが、対象 diff を変更したため両方の初回判定を無効化した。

### `c0fff31` の再レビュー

- 入力: `manifest-v2.md` の同一固定 bundle。base `ef3ad21`、head `c0fff31`。Phase A / B snapshot、完全 diff / stat / name-status、Unity 生ログ / XML、2 回分の preflight / outcome を含む。
- 機械検査: `contract-audit.ps1` exit 0、Unity 側変更 `.cs` 9件に機械判定違反なし。`git diff --check` exit 0。`docs-audit.ps1` exit 0、§7/§8 の定型的な警告 2 件。
- Unity 6000.6.0f1 StandaloneWindows64 Player、全 EditMode 761/761 成功・失敗 0・skip 0。Unity log の終了コード 0。BS2b 限定 5/5 成功。統合テストは固定 workspace で 2 回の実 Content Directory build と各 report / manifest、公開 directory のファイル、移設後の単一 root、Scene / Object entry、2 表現、除外 root 不在を確認した。preflight の共有 material と両 build identity が outcome に一致した。
- 新規コンテキスト GPT-5.6 Terra / OpenAI の findings-first 構造→機能レビュー: 現在の問いを阻害する欠陥 0、GO。配置と asmdef edge は凍結 map に一致し、全条件を固定 diff と生証拠で確認した。
- 後続入力: Unity `Library/BuildInstructions/*.buildinst` 7 ファイルがテスト cleanup 警告に残る。Library cache であり、公開 content / source asset ではない。BS2b の凍結条件の違反ではないため修正範囲を拡張しない。

### 日本語コメント追加後の `c445f49` 再レビュー

- 最初の v3 bundle は base→head の Git 差分に以前の HANDOFF review-record と証拠ファイルを混入させた。C' が blind 入力違反として NO-GO と指摘し、採用した。この v3 判定は無効とし、実装 path の完全差分とコメント差分だけを載せた `manifest-v4.md` で C/C' を新規コンテキストからやり直した。
- v4 入力: base `ef3ad21`、head `c445f49`、Phase A/B snapshot と B addendum、`.gitignore` と `unity/Assets/OneStarMaker` の全実装差分 / stat / name-status、`c0fff31` からのコメント差分、生 XML / Unity log、2 回分の preflight / outcome。`manifest-v4.md` は以前の C/C' 所見を含まない。
- 全 EditMode 761/761 成功、失敗 0、skip 0。Unity log の終了コード 0、固定 workspace の実 build 2 回。`contract-audit.ps1` exit 0。C# 非コメント行変更 0。
- Phase C 担当 GPT-5.6 Terra / OpenAI: 構造→機能レビュー、現在の問いを阻害する欠陥 0、GO。日本語コメントが隣接実装の契約と一致し、公開 API / asmdef / Runtime 振る舞いに変更がないことを確認。
- 後続入力: projection の閉包は preflight / report に使い、Unity build は生成 root から依存を解決する。将来の任意 asset 変更まで閉包同一性を保証する要件は BS2b の凍結範囲外。

## 8. Phase C'

新規コンテキスト GPT-5.5 / OpenAI が `manifest-v2.md` の blind bundle のみで独立監査した。初回 C/C' の所見と現在の可変 HANDOFF は渡さなかった。構造・常時契約・全受け入れ条件について現在の問いを阻害する欠陥 0、GO。Unity log で 2 回の `BuildPipeline.BuildContentDirectory` と両 identity を確認し、生 XML の 761/761 成功を確認した。

後続入力は、outcome JSON の選択 / root / closure 欄は空で preflight に詳細があること、実 Object 型検証と汎用サブアセットは BS3、変更 matrix を含む増分保証は本条件外、の 3 件。いずれも凍結条件違反ではない。C と C' の重複した blocking finding は 0。両レビューとも OpenAI 系列で、異ベンダー独立性の強化条件は満たしていないことを記録する。

`c445f49` 対象の v4 blind bundle でも新規コンテキスト GPT-5.5 / OpenAI が独立監査し、現在の問いを阻害する欠陥 0、GO とした。v3 bundle の混入は v4 の実装 path 限定差分で解消した。ハッシュ、761/761 の生 XML、2 回の成功 report、コメント差分の非コメント行 0 と日本語コメントの正確性を確認した。後続入力は B addendum の「v3 manifest」という古い表記で、凍結条件や blind 監査を阻害しないため v4 snapshot は変更しない。

## 9. Phase D

Phase C / C' の GO を記録。base `develop` の PR #60 を更新する。develop マージは人間の判断を待つ。
