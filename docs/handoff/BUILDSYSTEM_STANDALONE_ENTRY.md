# BuildSystem 単独の入口: build の入力と生成物の読み口（`BUILDSYSTEM_STANDALONE_ENTRY`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（**延期。A1 改稿までで止めた。A2 未実施、A3 未凍結。実装キューではない**）。owner が 2026-11-27 に延期を決めた。再開の条件は、`PLAY_CONTENT_RESOLUTION_PROGRAM.md` の S4-4（要求の時点の install と登録）の Phase D の後。理由と、再開時にやり直すことは下の「延期と再開」
- 位置付け: program [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §4.3 の X6 と、§6 D4 の順序 (2)。X7（出力先と target の固定）は本書 §2 の分割の判断で別の小スライスへ分けた
- 版の経緯:
  - 2026-11-26 A1 初稿。新しい public は入力の入口 1 型だけで、実行時の読み口は「今の public で辿れる」として外していた。
  - 2026-11-26 U1（経路テスト用の asmdef）を owner が裁定。
  - 2026-11-27 延期（owner）。改稿への外部計画レビュー（F-04・F-05。Play Content 解決の S4-3b 以降との依存と統合のゲート）を受けて、owner が S4 の後続との衝突を避けるために決めた。F-04・F-05 は本書に反映せず、再開時の入力として「延期と再開」に記録した。
  - 2026-11-27 A1 改稿。Draft PR の外部計画レビュー（判定「A1 要改訂」、F-01〜F-03）と owner の指摘「前提から考えていない」を受けて、公開面を「最小の数」からではなく §1 の「公開面の導き方」から導き直した。読み口を本スライスに入れ、目的・利用者の文言を改め、P 検査を A3 の前提にし、入力の GUID の契約を固定した。レビューの採否は §5.1。
- branch: `claude/fervent-ritchie-zhhe6k`（`origin/develop` の `9e7d4148` から切った計画ブランチ。PR の base は `develop`）
- implementation base commit: `9e7d4148`（2026-11-26 の `origin/develop` の head。§1 の現況はこの commit で測り直した）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `high`（公開 API を増やす。[Phase A の内部フロー](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー) の高リスクの例に当たる）
- owner: root（依頼者）
- created: 2026-11-26
- expires: 2026-12-31、または置換 revision。延期の再開の条件より前に来る見込みで、書き換えは owner の判断（「延期と再開」の未裁定の事項、§2 U4）
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`（build の入力の入口と、生成物の root の読み口の契約。SceneResourceMap からの入口と並べて書く）と、program §2.2 X6・§2.3 の U-B の行の状態
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結する時に作る。A1 の時点で Git 外の補助 file は無い）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割と作業: 無し（延期中）。再開の条件（S4-4 の Phase D）を満たした後、owner が再開を指示したら、新しい session が「延期と再開」の「再開時にやり直すこと」を行ってから A1 を改訂し、A2 へ進む。
  - 次の担当が読む範囲: 既定どおり全節。先に「延期と再開」を読む。
  - 引き継ぐ commit: 再開時の `develop`。本書の実測と行番号は base `9e7d4148` の物で、再開時には古い。
  - 必要な権限: 再開時の preflight と Phase C は、Unity Editor と Git 外の P 検査の script がある環境が要る。
  - 停止規則: owner の再開の指示の前に A2・A3・Phase B へ入らない。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md` の A1 は本スライスの Phase D まで始めない（program §0。この条件を S4-4 との関係で見直すかは「延期と再開」の未裁定の事項）。
  - 引き渡さないもの: 目的レビューには §1 の「目的」と「利用者」の原文だけを渡し、本書の他の部分を渡さない。

### 延期と再開

- **延期の決定:** owner、2026-11-27。再開の条件は `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の S4-4 の Phase D の後。
- **理由:**
  - 本スライス（X6）の code は S4-3b〜d（取得元、取得のきっかけ、base の選び方）と直接は重ならない。重なるのは本スライスの後ろである。`CONTENT_DIRECTORY_STARTUP` は S4-4（要求の時点の install と登録、起動の順序、登録の寿命）と責務が直接重なる。X7 の target は S4-5（Windows 以外の target、target の固定を解く）と同じ課題で、X7 の出力先は S4-3c・S4-3d が触る SampleGame の呼び手（`SampleGameContentBatch.cs`、`VerifyPlayLauncher.cs`）と接する。asmdef 分割は S4 の各行が足す型の全てに影響される。
  - 本スライスだけを先に入れても、新しい public を使う物が無い。単独利用を宣言できるのは X7 と asmdef 分割の後の段階 4（program §5.2）で、SampleGame は新しい入口を使わない。public な API を先に凍結して、S4 の間維持する負担だけが残る。
  - preflight・Phase B・Phase C は Unity Editor が要り、S4 のラインと同じローカルの Editor を使う。S4 の merge のたびに U-B の集合に file が増えるので、P 検査の基準と GO の検査も動く。
  - S4-3b 以降は program の計画の行を読んだ評価で、未作成の HANDOFF との非衝突を確かめた物ではない。
- **再開時にやり直すこと（A1 の改訂の入力）:**
  1. **base の取り直し。** §1 の現況、行番号、可視性、呼び出しの数（35 箇所）、`InternalsVisibleTo` を再開時の `develop` で測り直す。S4-3a（PR #214。2026-11-27 の時点で head `97d56546`、Phase A）以降が `Runtime/BuildContent/Distribution/` と `Editor/Build/Content/` に足した file と、S4-4 が変えた登録・起動の契約を含める。
  2. **§1 の「公開面の導き方」の表の作り直し。** S4 の間に増えた規則（取得、origin、head、pin）が、U-B の利用者に書き直しを強いる箇所を足すかを同じ問い方で見る。S4-3b の LAN の取得元が U-B に入るか、外部依存を持つ adapter として分けるかは、その行の A1 が決めた結果を読む。
  3. **S4 の各行との責務の境界を本文に書く（F-04）。** 取得・origin・head・pin・cache・installer は取得の側の責務で、本スライスは build の入力と単独の root の解決だけを持つ。取得の結果の target・identity・digest を読み口が独自に解釈し直さない。読み口は session の代わりではない。prefetch、Play の transaction、checkout hook、base の選び方は対象外。
  4. **`BuildContentResult.Target` の意味（F-04）。** coordinator の固定値を返すだけの property を将来の契約にしない。「この結果が実際に build した target を保持して返す」と定め、その時点で扱える target を書く。S4-5 が target の固定を解いていれば、その契約に合わせる。target の対応表や検査の規則を X7 と S4-5 で別々に作らない（所有を 1 つにする）。
  5. **統合のゲート（F-05）。** GO 候補 head は、merge する時点の最新の `develop` を含むことを必須にする（今の AC12 の「含むなら」を改める）。統合した commit で P 検査、全 EditMode 回帰、取得（acquisition）と Play の既存の回帰を記録する。共有する契約に差分が出た時は、影響する実機の scenario を選んで取り直す。
  6. **P 検査の preflight（§5）。** 再開時の base で行う。S4 の完了の後に、集合の定義と、連携部分が越える internal の一覧（program §2.2 X4）を取り直す。古い集合のまま凍結しない。
  7. **目的・利用者の文言の承認（U5）。** 改稿への外部計画レビューは文言を支持したが、owner の承認はまだ無い。
- **未裁定の事項（延期の間、owner が持つ）:**
  - **program の列の後ろの順序。** 外部計画レビューの第一案は、S4-4 の実証を先にして、その時点の起動・登録・寿命の契約で `CONTENT_DIRECTORY_STARTUP` の A1 を作り直すこと、多 target を予定するなら S4-5 の契約も決めてから `CONTENT_DIRECTORY_STARTUP` と asmdef 分割を凍結すること。X7 は path の安全性だけを独立させ、多 target は S4-5 と所有を 1 つにすること。U3（§2）はこの裁定に含める。
  - **日付の期限。** program の期限（2026-11-30）と本書の期限（2026-12-31）は、再開の条件（S4-4 の Phase D の後）より前に来る見込みである。日付の書き換えは owner の判断なので、本書は書き換えていない（U4）。
- **改稿への外部計画レビュー（2026-11-27、入力は A1 改稿の head `feabd57b`）の記録:** 判定は「前回の指摘は解消の方向で、X6 の構成自体に新しい blocker は見当たらない。F-04・F-05 を A3 の前に解くことを推奨」。F-04（両 program の依存と変更の所有を A3 の前に照合する）と F-05（統合のゲートを具体化する）は、上の 3〜6 に入力として写した。本書の §2〜§5 には反映していない（延期のため）。静的レビューで、Unity と P 検査は未実行。

## 1. 目的と対象外

- 目的: BuildSystem を使う開発者が、OSM の SceneResourceMap を用意せず、OSM の内部向けの抜け道（`InternalsVisibleTo`）にも頼らずに、自分で選んだ asset を Content Directory として build し、その生成物を読める。その途中で、BuildSystem が決めている規則（何を依存として含めるか、生成物の root が正しいか、名前と表現からどの entry を選ぶか）を開発者が自分の code に書き直さなくてよい。既存の SceneResourceMap からの build と、既存の登録・読み込みの結果は変えない。
- 利用者: BuildSystem を自分の Unity プロジェクトから呼ぶ開発者。Editor の build script を書き、実行時に生成物から asset を引く code も書く。OSM の SceneResourceMap、Scene の仕組み、AssetManagement を使っていない。
- 目的・利用者の文言の変更（A1 の中での改訂。**owner の承認待ち、U5**）:
  - 元の文言（2026-11-26 の初稿）。目的: 「BuildSystem を使う開発者が、OSM の SceneResourceMap を用意せず、OSM の内部向けの抜け道（`InternalsVisibleTo`）にも頼らずに、public な API だけで、自分で選んだ asset を Content Directory として build する入力を組み立てて build し、その生成物を読める。既存の SceneResourceMap からの build の結果は変えない。」利用者: 「BuildSystem を自分の Unity プロジェクトの build script から呼ぶ開発者。OSM の SceneResourceMap と Scene の仕組みを使っていない。」
  - 変更理由: 元の文言は「public な API だけで読める」としか言わず、Unity の API で root の中身に触れられることと、BuildSystem の形式の意味（検査と解決の規則）で読めることを区別していなかった。初稿はこの文言のまま「触れられるので読み口は要らない」と結論し、入力の側では「閉包の規則を利用者に書き直させない」を理由に入口を作ったのに、読む側では解決の規則を利用者に書き直させる設計になっていた。利用者の欄も build script を書く人だけで、実行時に読む人が抜けていた。
  - この変更は利用者の入力を増やさない（UC3 の表）。
- 公開面の導き方（本書の設計の前提）:
  - **問い方。** 「いま public な API だけで動かせるか」ではなく、「利用者が BuildSystem の規則を自分の code に書き直さずに使えるか」で公開面を決める。書き直しが要る箇所は入口の欠けである。動かせるだけの経路（Unity の API で file に触れる）は、契約の成立の証拠にしない。
  - **数え方。** public の数を先に決めない。program の「最小の入口 1 つから始める」は実装の順序として守り（§4 は入力の入口から始める）、スライスが足す public は下の表の欠けから決める。
  - BuildSystem が持つ規則と、U-B の利用者から見た今の状態:

    | 規則 | 持っている場所 | 今の公開 | 利用者が書き直すことになるか | 本スライス |
    |---|---|---|---|---|
    | 依存の閉包に何を入れるか | `AssetDependencySnapshotBuilder`（internal） | 入口は `SceneResourceContentMaterializer` だけ | なる | 入力の入口を足す |
    | plan と snapshot の照合、表現の決定、Scene か Object か | `BuildContentProjection`、`BuildContentRootKeys`（internal） | `BuildContentCoordinator.Build` の中で必ず走る | ならない | 変えない |
    | 表現の dimension の名前 | `BuildRepresentationSelection.Dimension`（public な定数） | 公開済み | ならない | 変えない |
    | root の検査（schema、identity、target、stable key の重複、同じ組の重複、Scene・Object の locator） | `ContentDirectoryIndex.TryCreate`（internal） | 連携部分の session 経由だけ | なる | 読み口を足す |
    | 名前・表現・種別から entry を選ぶ（空の表現は Full、完全一致が先、無ければ Full） | `ContentEntryMatch`、`ContentEntryTable`（internal）。manifest の索引（`ContentPurposeIndex`）と同じ規則を共有する | 同上 | なる | 読み口を足す |
    | 上書きの root を単独では使わせない、層をまたいで引く順 | `ContentDirectoryLayers`（internal） | 同上 | 単独の directory では「上書きの root を拒む」だけが要る | 読み口が拒む。層は対象外 |
    | どの target 向けの build か | `BuildContentCoordinator.TargetName`（internal）、`ContentCacheStore` の private な値 | 配布・install の結果（`ContentInstallResult.Target`）には出る。build の結果には出ない | なる（文字列を自分で書く） | build の結果に出す |
    | 配布、install、install 済み revision の検証 | `ContentTransportPublisher`、`ContentInstaller`、`ContentCacheStore.ValidateInstalled` | 公開済み | 署名の上ではならない（通しての実行は未確認） | 変えない |
- 対象外:
  - 出力先と target の固定を解くこと（X7）。本スライスの build は、今の coordinator が許す root の配下にだけ出す。別の小スライスへ分けた（§2）。
  - 上書きを重ねた登録（base と上書きの 2 層）の読み口。本スライスの読み口は、上書きでない単独の directory だけを扱い、上書きの root は拒む。
  - `InstalledRevisionVerifier`、`ContentDirectoryIndex`、`ContentDirectoryLayers`、`ContentEntryMatch` を public にすること。
  - directory の登録・load・解放・登録解除の包み（session、寿命の管理、I/O の wrapper）を新しく作ること。U-B の利用者は Unity の API を直接呼ぶ。
  - asmdef の分割、production の asmdef 参照の追加、`InternalsVisibleTo` の削除、SampleGame の合成（`Bs4FixtureComposer`、`SampleGameBootstrapContentComposer`）を新しい入口へ移すこと。
  - 欠けを許す materialize（上書きの build 用）の一般の入口。Addressables 経路の Variant Build。
  - 配布から install までを通す経路の実証と、別の Unity プロジェクトでの実証（program §5.2 段階 4）。
- 現況（base `9e7d4148` で測った。path は `unity/Assets/OneStarMaker/Scripts/` からの相対）:
  - **入力の入口は program の実測のまま閉じている。** `Editor/Build/Materialization/BuildMaterializationSnapshot.cs` のコンストラクタは 3 つとも internal（21・28・37 行。3 つ目は欠けの記録を受ける版で、program の base の後に増えた）。public な型のうち snapshot を返すのは `SceneResourceContentMaterializer` の `Materialize(SceneResourceMap)` と `MaterializeAllowingAbsentSources(SceneResourceMap)` だけ。tag の provider の識別子は同 file 77 行で `osm.scene-resource-map.representation.v1` の固定値。
  - **`InternalsVisibleTo`。** `Materialization/AssemblyInfo.cs` は `OneStarMaker.Tests.Editor`、`SampleGame.DependOnAll.Editor`、`SampleGame.Tests.Editor` の 3 つに開く（program の時点から `SampleGame.Tests.Editor` が増えた）。`Content/AssemblyInfo.cs` は test の 2 つ、`Selection/AssemblyInfo.cs` は同じ 3 つ。
  - **internal なコンストラクタの呼び出し元。** `new BuildMaterializationSnapshot(` は、Framework の production が 1 file（`SceneResourceContentMaterializer.cs`）、SampleGame の production が 2 file（`Bs4FixtureComposer.cs`、`SampleGameBootstrapContentComposer.cs`）、Framework の test が 11 file・19 箇所、SampleGame の test が 9 file・13 箇所。計 35 箇所。SampleGame の合成は internal な `new BuildPlan(...)` と `AssetDependencySnapshotBuilder` も直接使う。
  - **snapshot 以外の入力は既に public。** 候補（`BuildContentCandidate`）、由来（`BuildProvenance`）、tag（`BuildTag`）、requirement（`BuildContentRequirement`）、graph（`BuildGraphNode`）は public なコンストラクタを持つ。選択（`BuildTagSelector.Select`、`BuildPurposeResolver.Resolve`）も public で、`BuildPlan` は選択の結果として得られる。`OneStarMaker.Build.Selection` の asmdef は参照 0 のまま。
  - **閉包を作る規則は internal。** `AssetDependencySnapshotBuilder` と `IAssetDatabaseGateway` / `UnityAssetDatabaseGateway` は internal。閉包に入れる file の規則（`Assets/` の配下、`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref` を除く、root 自身を含める、GUID と path の往復の一致）はこの builder だけが持つ。
  - **GUID の大文字小文字。** `AssetDependencySnapshotBuilder.NormalizeGuid` は 32 桁の 16 進を小文字にする。一方、`BuildMaterializationSnapshot.FindDependency` と `BuildContentProjection.Create` は、候補の `PhysicalKey` と閉包の `RootGuid` を ordinal で比べ、plan の候補と snapshot の候補の key も ordinal で照合する。大文字の GUID を持つ候補に小文字の閉包を付けると、materialize は成功して build の照合（`MissingClosure`）で落ちる。
  - **build の照合は snapshot の中身だけを見る。** `BuildContentProjection.Create` は、候補と閉包の対応、root が閉包に含まれること、path が `Assets/` で始まることを確かめ、表現は tag の `Representation` の dimension から読む（provider の名前と Scene の語彙は読まない）。entry の種別は root の path の拡張子（`.unity` なら Scene）で決まる。
  - **root の検査と解決の規則（`Runtime/BuildContent/ContentDirectoryIndex.cs`、191 行、全て internal）。** `TryCreate` は、schema が 1 か 2、identity と target が期待と一致、entry の欠け、stable key の重複、entry の種別の値、Scene の locator、Object の locator、schema 2 の file GUID と local ID、同じ（logical key・実効の表現・種別）の重複を検査する。`ContentEntryTable.TryResolve` は、空の表現を Full と読み、要求した表現の完全一致を先に引き、無ければ Full を引く（表現を指定しない要求は Full だけに当たる）。同 file のコメントは、この規則を登録した directory の索引と manifest の索引（`ContentPurposeIndex`）が共有し、片方だけを変えないと定めている。失敗は internal な `ContentDirectoryIssue` で返り、連携部分の session が public な `ContentDirectoryException`（共通契約）へ写す。
  - **`TryCreate` は上書きの root を拒まない。** `ContentDirectoryLayers.RegisterLayer` は base と上書きの両方の root を同じ `TryCreate` で索引にする。上書きの記録（`OverlayBase`）を持つ root を単独・base として拒むのは `ContentDirectoryLayers` の側で、code は `InvalidRoot`。層をまたぐ解決（上書き → base の順に完全一致、次に Full）も同じ file にある。
  - **Unity の API で root の中身には触れられるが、上の規則は通らない。** `BuildContentRoot`（`Entries`、`BuildIdentity`、`Target`、`SchemaVersion`、`OverlayBase`）と `BuildContentEntry`（`LogicalKey`、`Representation`、`Kind`、`SceneId`、`Object`）は public で、directory の登録と root の取得は Unity の `ContentLoadManager` の public API である（連携部分の `UnityContentDirectoryBackend` も同じ API を呼ぶ）。ただし `Entries` を自分で走査する code は、検査を 1 つも通らず、同じ logical key に Full と別の表現がある時にどちらを選ぶかを自分で決めることになる。初稿はこれを「読める」と数えていた。
  - **install 済み revision の検証は public。** `ContentCacheStore.ValidateInstalled`（内部で `InstalledRevisionVerifier` を呼ぶ）、`ContentInstaller.InstallAsync`、`ContentTransportPublisher.Publish`。`ContentCacheStore.cs` と `ContentInstaller.cs` は program の base から変わっていない。これは install した tree と manifest の検証で、直接 build した directory の root の構造と解決の規則の代わりにはならない。署名の可視性を見ただけで、publish → install → 検証を public だけで通す実行は確かめていない。
  - **target の名前。** `BuildContentCoordinator.TargetName` は internal、`ContentCacheStore` の `SupportedTarget` は private で、値はどちらも `StandaloneWindows64-Player`。`BuildContentResult` は target を持たない（outcome の file には書く）。`ContentInstallResult.Target` は public。
  - **出力先（X7）。** `BuildContentCoordinator.Build`（`Editor/Build/Content/BuildContentCoordinator.cs` 44〜56 行）は、root が Unity project の親の `artifacts/bs2b` か `TestResults/bs3-fixture` の配下でなければ `ArgumentException` を投げる。reparse point の検査（`ClearWorkspace`、`RejectReparsePoint`）は Full の build が workspace を空にする経路だけに掛かり、コメントも「汎用の filesystem 防御ではなく、この削除経路のための検査に限る」とする。report・staging・final への書き込みは、root が上の 2 つの配下であることだけに守られている。上書きの計画（`ContentOverlayPlanner`）と再利用の記録（`ContentOverlayReuse`）も同じ root を受け取る。
  - **program の base（`d4861882`）からの増分。** `develop` は 660 commit 進み、対象の folder で 50 file、+3,463 / −255 行が変わった。`Editor/Build/Content/` に上書きの build の 8 file、`Runtime/BuildContent/` に `ContentDirectoryLayers`・`ContentOverlayDigest`・`BuildContentOverlayBase`・`Distribution/ContentPurposeIndex` が増えた。`BuildContentCoordinator.cs` は 271 行になり、program が引いた行番号（33・45〜52 行）はずれている。
  - **`using` で見た向き（補助。P 検査の代わりではない）。** `Runtime/BuildContent/` で BuildContent の外の OSM の namespace を `using` するのは、`BuildContentRoot.cs`（`AssetType` のため）、連携の 2 file（`ContentDirectorySession.cs`、`UnityContentDirectoryBackend.cs`）、OSM 固有の 2 file（`PlayerContentConfiguration.cs`、`PlayerBakedContentConfiguration.cs`）だけ。`Editor/Build/Materialization/` で `OneStarMaker.Runtime.*` を `using` するのは OSM 固有の 2 file だけ。`OneStarMaker.Build.Content` と `OneStarMaker.Build.Materialization` は `OneStarMaker.Runtime` の `InternalsVisibleTo` に入っていないので、Runtime の internal は使えない。
  - **未確認。** P 検査（program §0）は Unity 同梱の `csc` と Git 外の script を使うので、この A1 を書いた環境では実施していない。program の base の後に増えた file を含めた U-A・U-B の集合が base `9e7d4148` で閉じているかは実測していない（§5 の preflight）。Unity のテストも実行していない。
  - **open な PR との照合（2026-11-26）。** PR #214（branch `claude/trusting-ramanujan-kxphtr`、head `d8bd66b3`）と PR #216（branch `claude/magical-gauss-7rycuj`）の差分に、本スライスが変える予定の既存 file（§3）は無い。#214 は `Editor/Build/Content/` に 1 file、`Runtime/BuildContent/Distribution/` に 7 file を新しく足し、SampleGame の `SampleGameContentBatch.cs`・`VerifyPlayLauncher.cs` 等を変える。順序の制約は §2「本文へ転記した実装制約」。
- UC ごとの利用者の入力:

  **UC1 build の入力を組む**（開発時に build script へ書く）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | build する root の asset（GUID。小文字 32 桁の 16 進） | 利用者・開発時の宣言 | 「自分で選んだ asset」を選ぶのは利用者だけ |
  | 候補の logical key | 利用者・開発時の宣言 | 実行時に生成物から引く名前。利用者の code が使う |
  | 候補の stable key と由来（`BuildProvenance`） | 利用者・開発時の宣言 | 候補は選択の public な入力（`BuildTagSelector.Select`）で、利用者は選択のために同じ候補を作る。build は plan の候補と snapshot の候補を key で照合するので、同じ物を渡す必要がある。由来は issue の帰属に出る |
  | 表現の tag（任意） | 利用者・開発時の宣言。無ければ表現なし（Full として読まれる） | 同じ logical key に複数の表現を持たせる時だけ要る |
  | requirement と graph（任意） | 利用者・開発時の宣言。既定は空 | 件数の規則や到達規則（`BuildPurposeResolver`）を使う時だけ要る |
  | 依存の閉包 | 機械・自動導出（root の GUID から `AssetDatabase`） | 利用者は入れない。規則は SceneResourceMap の経路と同じ builder が持つ |
  | tag の provider の識別子 | 機械・既定値（一般の入口の固定値） | 利用者は入れない。issue の帰属に出るだけで、利用者が選ぶ理由が無い |

  **UC2 build する**

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | plan | 機械・自動導出（snapshot の候補・tag・requirement を public な選択へ渡した結果） | 利用者は選択の要求（tag）を渡すだけ |
  | content set の名前 | 利用者・開発時の宣言 | workspace と配布の識別子 |
  | 出力先の root | 利用者・実行時の引数。本スライスでは今の許可 root の配下だけ | 置き場所を選ぶこと自体は X7 のスライスの問い |
  | workspace の扱い（`BuildContentMode`） | 利用者・実行時の引数。既定は `Incremental` | 配布する build は `Full` にする（既存の契約） |

  **UC3 生成物を読む**

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | directory の path、期待する identity と target | 機械・自動導出（build の結果の `ContentPath`・`Identity`・`Target`。install した物なら `ContentInstallResult`） | 利用者は結果を渡すだけ。target の文字列を自分で書かない |
  | 引く logical key、種別（Scene か Object か）、表現（任意。無ければ Full） | 利用者・実行時の操作 | UC1 で自分が付けた名前と表現。種別は、何として load するかを利用者の code が知っている |
  | root の検査、entry の選び方 | 機械（読み口） | 利用者は入れない |
  | directory の登録・load・解放・登録解除 | 利用者・実行時の操作（Unity の API） | 寿命は利用者の code が持つ。BuildSystem 単独では AssetManagement の寿命の管理を持ち込まない |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 利用者が BuildSystem の規則（依存の閉包、root の検査、entry の解決）を自分の code に書き直さず、SceneResourceMap も `InternalsVisibleTo` も使わずに、BuildSystem の public な契約だけで「入力を組む → build する → 生成物を解決して読む」を通せるか。既存の SceneResourceMap からの build と、既存の登録・読み込みを変えずに成立させられるか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1 既存の build と読み込みの結果が変わる。** SceneResourceMap からの経路で作る snapshot の中身（候補、tag、provider の識別子、requirement、閉包、graph、欠けの記録）、それを使う build の preflight・outcome・root の entry、連携部分の登録と解決（単独の directory、上書きを重ねた登録）の結果が、変更の前後で違ってはならない。
      - 崩しうる経路: (a) provider の識別子を引数にする時、既存の 3 つの internal なコンストラクタの既定値が今の固定値からずれる。(b) コンストラクタの overload を足すと、既存の 35 箇所の呼び出しの overload 解決が変わる、または曖昧になる。(c) 一般の入口のために `AssetDependencySnapshotBuilder` の規則や戻り方を変える（SceneResourceMap の経路と上書きの経路が同じ builder を使う）。(d) 読み口のために `ContentDirectoryIndex.TryCreate` へ上書きの root の拒否を足す（上書きの層の索引も同じ `TryCreate` を通るので、上書きを重ねた登録が落ちる）。(e) 読み口のために `ContentEntryMatch` / `ContentEntryTable` を変える（manifest の索引と session の解決が同じ規則を使う）。(f) `BuildContentResult` に target を足す時、outcome の file や publish の manifest に書く内容が変わる。
      - 探索した境界: `Editor/Build/Materialization/` の全 file、`Editor/Build/Content/` の `BuildContentProjection.cs`・`BuildContentCoordinator.cs`・`BuildContentReport.cs` の入力の読み方、`Editor/Build/Selection/` で `StableProviderKey` を読む箇所、`Runtime/BuildContent/` の `ContentDirectoryIndex.cs`・`ContentDirectoryLayers.cs` の `TryCreate` と解決の呼び出し。
      - 未確認: `BuildContentReport` と `ContentTransportPublisher` が provider の識別子を file に書くか（`StableProviderKey` の grep では Selection の中だけに出た。issue の文言を経由して report に出る経路は追っていない）。`ContentPurposeIndex` の中での `ContentEntryTable` の使い方の全箇所。
    - **I2 既存の公開面が壊れる・理由無く広がる。** 既存の public な型・メンバーの宣言を変えない、減らさない。`BuildMaterializationSnapshot` のコンストラクタ、`ContentDirectoryIndex`、`ContentDirectoryIssue`、`InstalledRevisionVerifier` を public にしない。増やす public は §3 に挙げた物だけで、どれも §1 の表の「書き直すことになる」の行に対応する。
      - 崩しうる経路: internal なコンストラクタや型の昇格、`AssetDependencySnapshotBuilder` や gateway の昇格、既存の enum の値の挿入（末尾以外）や改名、共通契約（`ContentDirectoryFailureCode`）への値の追加。
      - 探索した境界: 上の現況で挙げた型の可視性の一覧（`Editor/Build/` の 3 folder と `Runtime/BuildContent/`、`Runtime/AssetBuildContracts/`）。
    - **I3 新しい入口が OSM 固有の物や相方を持ち込む。** 入力の入口の file は `SceneSystem`、`AssetDescriptions`、`AssetManagement`、`Foundation` の型を参照しない。読み口の file は `AssetManagement` の型（共通契約の `ContentDirectoryException` / `ContentDirectoryFailureCode` を除く）、`Foundation`、連携の 2 file、OSM 固有の 2 file の型を参照しない。一般の入口で作った snapshot の provider の識別子は OSM の SceneResourceMap の値にならない。U-B（Runtime）と U-B（Editor）の P 検査の集合（program §5.1）は、新しい file を含めて閉じたままである。
      - 崩しうる経路: 候補の作り方を `ScenePayloadMappingPolicy` と共通化する、provider の識別子の既定値を共有する、読み口が session や `IContentDirectoryBackend` を使って load まで引き受ける。
      - 未確認: 現在の U-A・U-B の集合が base `9e7d4148` で閉じているか（§5 の preflight で A3 の前に確かめる）。
    - **I4 不完全な入力が build に渡る、または materialize が通した入力が build の照合で落ちる。** issue が 1 件でもあれば snapshot を返さない。閉包を作れなかった root を、閉包無しの候補として通さない。入口が成功を返した snapshot の候補は、どれも `PhysicalKey` と ordinal で一致する `RootGuid` の閉包をちょうど 1 件持つ。
      - 崩しうる経路: 欠けを許す版（`BuildAllowingAbsence`）を一般の入口が使う。issue を集めた後に部分的な snapshot を返す。大文字の GUID を builder が小文字にして閉包だけを作る（上の現況）。大文字小文字だけが違う 2 つの physical key を別の root として通す。
    - **I5 依存が増える。** production の asmdef の参照と `InternalsVisibleTo` を増やさない。足す asmdef は、U1 の裁定で認められた test 用の 1 つ（§3）だけである。
    - **I6 規則が 2 箇所に増える、または読み口が検査を通らない root を解決する。** 読み口は root の検査と entry の解決を自分で持たず、`ContentDirectoryIndex`（`TryCreate`、`TryResolveObject`、`TryResolveScene`）へ委譲する。検査に通らない root、identity か target が期待と違う root、上書きの記録を持つ root からは、読み口を得られない。
      - 崩しうる経路: 読み口が `Entries` を自分で走査して fallback を書く。上書きの root の拒否を忘れる（`TryCreate` は拒まない）。読み口が `TryGetExact` を公開して、層の無い所で fallback を外した解決を許す。
  - 成立条件（起きなければならないこと）:
    - **S1** public な入口 1 つで、SceneResourceMap を渡さずに、候補・tag・requirement・graph から `BuildMaterializationResult`（成功なら snapshot）を得られる。閉包は機械が導く。
    - **S2** S1 の snapshot と、その snapshot の候補・tag・requirement から public な選択で導いた plan で、public な `BuildContentCoordinator` の `Build` が成功し、Content Directory ができる。結果から identity と target を読める。
    - **S3** S2 の生成物の root を、public な読み口が検査し、名前・表現・種別から entry を解決する。解決の結果は BuildSystem の規則（完全一致が先、無ければ Full、表現無しは Full、無い名前は失敗）どおりで、解決した entry を Unity の API で load すると、元の asset と内容で識別できる物が得られる。
    - **S4** 既存の回帰が通る（全 EditMode、`contract-audit`）。U-A・U-B の P 検査が成功する。
- 受け入れ条件:
  - **AC1（S1、I2）** `Editor/Build/Materialization/` に public sealed class `BuildCandidateMaterializer` があり、public なメンバーは引数無しのコンストラクタと `Materialize` 1 つだけである（署名は §3）。`BuildMaterializationSnapshot` のコンストラクタは全て internal のままである。
  - **AC2（S1、I4）入口の成功経路** 単体テスト（fake の gateway）:
    - 依存を持つ root（fake が依存の path を返す）で、閉包が root 自身と依存を含み、`.cs` と `.meta` を含まない。
    - 候補の tag が snapshot の `TagProviders` から同じ dimension と値で返る。呼び手が渡した辞書と list を後から書き換えても、snapshot の返す値は変わらない。
    - 空でない requirement と graph が、snapshot の `Requirements` と `Graph` に件数と中身を保って入る。
    - 候補を渡す順を変えても、snapshot の候補・閉包・requirement・graph の並びは同じである。
  - **AC3（I4、失敗経路）** 単体テスト。次のそれぞれで `Snapshot` が null、`HasErrors` が true で、issue の code が括弧の値である: 小文字 32 桁の 16 進でない physical key（大文字を含む GUID、長さ違い、16 進でない字。`InvalidRootGuid`）、解決しない GUID（`UnresolvedRootGuid`）、root の file が無い（`MissingRootAsset`）、依存の file が無い（`MissingDependencyAsset`）、stable key の重複（`DuplicateStableKey`）、別の候補と同じ physical key（`PhysicalKeyCollision`）、gateway の例外（`GatewayFailure`）。
    - 複数の候補のうち 1 件だけが不正でも snapshot は null で、不正な候補が複数あれば issue は全件出る（最初の 1 件で止めない）。
    - 合格例: 候補 2 件のうち 1 件だけ GUID が解決しない → `Snapshot == null`、issue 1 件。不合格例: 解決した 1 件だけの snapshot を返す。
    - 合格例: `ABCDEF…` の大文字の GUID → `InvalidRootGuid`。不合格例: 小文字に直して閉包を作り、snapshot を返す（build の照合で `MissingClosure` になる）。
    - 引数の誤り（`candidates` か `tags` が null、`candidates` の要素が null、`tags` の key が候補に無い stable key、`tags` の値が null）は `ArgumentException` 系で、issue ではない。
  - **AC4（I3、失敗経路）** 単体テスト: 一般の入口の snapshot の `TagProviders[0].StableProviderKey` は `buildsystem.candidate-tags.v1` で、`osm.scene-resource-map.representation.v1` ではない。`BuildCandidateMaterializer.cs` は `OneStarMaker.Runtime` で始まる namespace を `using` せず、完全修飾でも参照しない（判定 C が file を読んで確かめる。最終の証明は AC12 の P 検査）。
  - **AC5（I1）** 既存の `SceneResourceContentMaterializerTests`、`MaterializeAllowingAbsentSourcesTests`、`BuildContentProjectionTests`、`BuildContentReportTests`、`ContentDirectoryIndexTests`、上書きの登録のテスト（`OverlayRootRecordTests`、`RegisteredDirectoryProviderTests`）を assertion を変えずに通す。加えて、SceneResourceMap の経路の snapshot の `StableProviderKey` が `osm.scene-resource-map.representation.v1` であることを固定するテストを 1 件足す（今は値を固定するテストが無い）。
    - 不合格例: 既存のテストの期待値を新しい値に書き換えて通す。
  - **AC6（I1、I2、失敗経路）** 既存の `new BuildMaterializationSnapshot(` の呼び出し 35 箇所（production 3 file、test 20 file）を書き換えずにコンパイルが通る。`ContentDirectoryIndex.cs`、`ContentDirectoryLayers.cs`、`ContentDirectorySession.cs` に差分が無い。書き換えや差分が要るなら I1 に当たるので B は Phase A へ戻す。
  - **AC7（S3、I2、I6）読み口の形** `Runtime/BuildContent/` に public sealed class `BuildContentRootReader` があり、public なメンバーは static な `Open` と、`ResolveObject`、`ResolveScene` の 3 つだけである（署名は §3）。file は root の entry を走査する loop と、表現の比較（`"Full"` の文字列、空の表現の読み替え）を持たない（判定 C が file を読んで確かめる）。
  - **AC8（S3、I6、失敗経路）読み口の単体テスト**（Unity の directory 無しで、root を code で組む。既存の `ContentDirectoryIndexTests` と同じ組み方）:
    - `Open` が `ContentDirectoryException` を投げ、code が括弧の値である: root が null（`InvalidRoot`）、schema が 1 でも 2 でもない（`UnsupportedSchema`）、identity が期待と違う（`IdentityMismatch`）、target が期待と違う（`TargetMismatch`）、stable key の重複（`InvalidRoot`）、同じ（logical key・実効の表現・種別）の重複（`EntryAmbiguous`）、Scene の entry に locator が無い・Object の entry に locator が無い（`InvalidRoot`）、上書きの記録を持つ root（`InvalidRoot`）。
    - 解決: 同じ logical key に Full と別の表現がある時、別の表現の要求はその entry、表現無しと空の表現の要求は Full の entry を返す。Full しか無い logical key への別の表現の要求は Full の entry を返す。別の表現しか無い logical key への表現無しの要求は `EntryMissing`。無い logical key は `EntryMissing`。同じ logical key の Scene と Object は、`ResolveScene` と `ResolveObject` がそれぞれの entry を返し、種別の違う方は `EntryMissing`。失敗の例外は logical key と表現を持つ。
    - 合格例: 上書きの記録を持つ、他は正しい root → `Open` が `InvalidRoot`。不合格例: `Open` が成功し、上書きの entry を単独で解決できる。
  - **AC9（S2、I1）** `BuildContentResult` に public な `Target` があり、coordinator が作った結果では build の target の名前（今は `StandaloneWindows64-Player`）を返す。outcome の file と publish の manifest の内容は変えない（既存の `BuildContentReportTests` と publish のテストが変更無しで通る）。
  - **AC10（S2、S3、I5）経路テスト** `InternalsVisibleTo` の相手でない test 用の asmdef `OneStarMaker.Tests.BuildStandalone.Editor`（§3。U1 の裁定）に置いた EditMode のテスト 1 件が、次を行う。internal を使うとコンパイルが通らないので、「public だけ」はコンパイラが強制する。
    1. test が所有する一時 folder に、内容で識別できる prefab を 3 つ作る（例: root の GameObject の名前が違う）。logical key `A` の Full、`A` の別の表現、`B` の Full。
    2. 候補 3 件と表現の tag を組み、`BuildCandidateMaterializer.Materialize` で snapshot を得る。
    3. plan は、snapshot の `Candidates`・`TagProviders`・`Requirements` を `BuildTagSelector.Select` へ渡して得る。snapshot と別に手で組んだ候補や plan を使わない。3 件とも選ばれる要求にする。選択の要求は dimension ごとの OR なので（`BuildTagSelector.Select` のコメント）、表現の dimension に 2 つの値を入れれば両方が選ばれる。既存の `BuildContentDirectoryIntegrationTests` も public な `BuildTagSelector.Select` と `BuildRequest` で 2 つの表現を 1 つの directory に入れている。
    4. `new BuildContentCoordinator().Build`（root は `TestResults/bs3-fixture` の配下の一意な folder、`Full`）が成功する。
    5. 結果の `ContentPath` を `ContentLoadManager.RegisterContentDirectory` で登録し、`GetRootAssets<BuildContentRoot>` の 1 件を、結果の `Identity` と `Target` で `BuildContentRootReader.Open` に渡す。
    6. `ResolveObject` の結果を load し、内容を照合する: `A` を別の表現で引くと別の表現の prefab、`A` を表現無しで引くと Full の prefab、`B` を別の表現で引くと `B` の Full の prefab（fallback）、無い key は `EntryMissing`。型だけでなく、手順 1 の識別できる内容で照合する。
    7. 違う identity を渡した `Open` が `IdentityMismatch` で失敗する。
    8. 解放して登録を外し、一時 folder と出力を消す。
    - SceneResourceMap、SampleGame の asset、SampleGame の型、AssetManagement の型を使わない。test の file が `using` する OSM の namespace は `OneStarMaker.Build.Selection`、`OneStarMaker.Editor.Build.Materialization`、`OneStarMaker.Editor.Build.Content`、`OneStarMaker.Runtime.BuildContent` だけである（この asmdef は `OneStarMaker.Runtime` 全体を参照するので、相方の public な型を使わないことはコンパイラが強制しない。判定 C が `using` と完全修飾の参照を読んで確かめる）。
    - 不合格例: `Entries` を走査して logical key が一致する最初の entry を load する。読み口を通さずに型だけを確かめる。
  - **AC11（S4）** GO 候補 head で、全 EditMode 回帰（`pwsh tools/run-tests.ps1`、filter 無し）と `pwsh tools/contract-audit.ps1` が合格する。
  - **AC12（S4、I3）** GO 候補 head の tree で、U-A・U-B（Runtime）・U-B（Editor）の P 検査（program §5.1 の集合。U-B（Runtime）は `BuildContentRootReader.cs`、U-B（Editor）は `BuildCandidateMaterializer.cs` を含む）がコンパイルに成功する。GO 候補 head が、他の PR が入った後の `develop` を含むなら、その統合した tree の commit と集合で行う。失敗の原因が他の PR の file でも、GO の代わりにはならない（原因の切り分けと修正の所有は記録するが、AC12 が合格するまで GO にしない）。
  - **AC13（I5）** `git diff` で、production の `.asmdef` と `AssemblyInfo.cs` に差分が無い。増えた `.asmdef` は `OneStarMaker.Tests.BuildStandalone.Editor` の 1 つだけで、その参照は §3 のとおりである。
- ここでは答えない問いと所有する後続スライス:
  - 利用者が選んだ安全な出力先で build できるか、「安全な出力先の検証」と「OSM が許す置き場所の規約」を分けられるか、複数の target を扱うか（X7）→ 新規の小スライス `BUILDSYSTEM_OUTPUT_ROOT`（仮称、未作成。program §4.3 の X7 の行、D5 の「別の小スライス」）。
  - 上書きを重ねた登録を BuildSystem 単独で読めるか（層をまたぐ解決の public な入口）→ asmdef 分割スライス（program §2.2 X4 の「BuildSystem 本体の internal 7 件」の裁定と同じ場所）。
  - publish → install → `ValidateInstalled` → 読み口、を public だけで通せるか → program §5.2 段階 4 の実証。署名の上では public だが、本スライスは通して実行しない。
  - SampleGame の合成を public な入口へ移し、`Materialization` の `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` を外せるか → asmdef 分割スライス。既存の snapshot に候補を足す合成と、欠けの記録の引き継ぎが要るので、本スライスの入口では足りない。
  - 欠けを許す materialize の一般の入口 → 要求が出た時に program へ足す（今は SampleGame の上書きの経路だけが使う）。
  - Content Directory 起動の facade → `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md`。
- スライス分割の判断:
  - 契機: 独立した変更理由の混在（入力の入口、読み口、X7 の出力先）と、高リスクで独立して失敗しうる未知が複数あること。
  - **X7 は分ける。** 理由は次の 4 つ。(1) 変更の種類が違う。本スライスは public を足すだけで既存の挙動を変えない。X7 は、処理を拒む既存の契約（許可 root の外を `ArgumentException` で拒む）を変える。(2) 「安全」の定義が未決である。今の reparse point の検査は workspace を空にする経路だけに掛かり、report・staging・final への書き込みは許可 root の配下であることだけに守られている（上の現況）。利用者が選んだ root では、その前提が無くなるので、何を検査すれば安全かを先に決める必要がある。これは path の安全性を専門に見るレビューが要る別の問いである。(3) Full の build は root の配下の workspace を消すので、失敗した時の影響（利用者の directory の削除）が大きく、本スライスの合否と独立に失敗しうる。(4) X7 は OSM の置き場所の規約をどこへ移すかで SampleGame の呼び手（`SampleGameContentBatch.cs`、`VerifyPlayLauncher.cs`）に触れる可能性があり、この 2 file は open な PR #214 が変えている。分けても program の D5 の範囲内である。順序の扱いは U3。
  - **入力の入口と読み口は分けない。** 2 つは assembly が違う（Editor と Runtime）が、同じ 1 つの問い（規則を書き直さずに通せるか）の両端で、片方だけでは問いに答えられない。入力だけのスライスにすると、S3 と経路テストは「Unity の API で file に触れた」ことしか示せず、初稿と同じ取り違えになる。読み口は既存の internal へ委譲する 1 file で、独立して失敗しうる未知は「上書きの root の拒否」1 点だけである。外部計画レビューは、分けるなら S3 を疎通までに狭めて読み口のスライスを asmdef 分割の前に置く案も選択肢として挙げたが、スライスを 1 つ増やして得る物が無いので採らない。
- 判定定義: GO は AC1〜AC13 が全て合格。NO-GO は 1 つでも不合格、または I1〜I6 の違反。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 未凍結）
- 本文へ転記した実装制約:
  - Unity側の `.cs` は先頭に `#nullable enable` を置く。`record` を使わない。Editor の code を Runtime の assembly に置かない。
  - 公開 API に `ZLogger*` の型を出さない。
  - asmdef の参照の追加は設計判断で、勝手に行わない。足してよい asmdef は U1 で裁定済みの test 用の 1 つ（§3 の名前・置き場所・参照）だけで、それ以外の asmdef の追加と参照の追加は Phase A へ戻す。
  - 参照 0 を削除の理由にしない。internal なコンストラクタと `InternalsVisibleTo` は残す。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。
  - 新しい file の path は `tools/path-budget.json` の予算に従う。
  - 共通契約（`Runtime/AssetBuildContracts/`）を変えない。読み口の失敗は既存の `ContentDirectoryFailureCode` の値だけで表す。
  - **PR #214 との順序。** 本スライスは #214 が変える既存 file に触れないので、どちらが先に merge されてもよい。#214 は U-B の集合に入る folder（`Runtime/BuildContent/Distribution/`、`Editor/Build/Content/`）へ新しい file を足すので、AC12 の扱いは同条件のとおり（統合した tree で検査し、他の PR が原因でも GO の代わりにしない）。Phase B は `SampleGameContentBatch.cs`・`VerifyPlayLauncher.cs`・`PlayEntry*.cs` を変えない。
  - `docs/handoff/PLAY_CONTENT_RESOLUTION_PROGRAM.md` と DebugStudio の HANDOFF は他のラインが編集するので、本スライスは変えない。
- 裁定済みの事項:
  - **U1 経路テスト用の asmdef を足す（owner、2026-11-26）。** `InternalsVisibleTo` の相手でない test 用の asmdef を 1 つ足し、AC10 の経路テストをそこに置く。名前・置き場所・参照は §3 のとおり。採らなかった案は、asmdef を足さずに `OneStarMaker.Tests.Editor` に置き、判定 C が file を読んで「public だけ」を確かめる案。この asmdef は `OneStarMaker.Runtime` 全体を参照するので、型の閉包は示さず（閉包は P 検査）、相方の public な型を使わないことも強制しない（AC10 の末尾で判定 C が読む）。
- 取り下げた未決事項:
  - **U2 実行時の読み口を本スライスから外してよいか。** 初稿の案（外す）を取り下げ、読み口を本スライスに入れた（2026-11-27 の改稿）。理由は §1 の「目的・利用者の文言の変更」と「公開面の導き方」。owner の指摘と外部計画レビュー F-01 による。A3 で owner が確認する。
- 未決事項（A3 の前に owner が裁定する）:
  - **U3 分けた X7 のスライスの順序。** 案: D4 の列を (2) 本スライス → (2') `BUILDSYSTEM_OUTPUT_ROOT` → (3) `CONTENT_DIRECTORY_STARTUP` → (4) asmdef とし、(3) の A1 の開始条件は今のまま「(2) の Phase D の後」にする。(2') と (3) を並行させる場合、(2') が変えるのは `Editor/Build/Content/` と、置き場所の規約を持つ SampleGame の Editor の呼び手で、(3) が変えるのは `Runtime/Bootstrap/` と `Runtime/BuildContent/` の起動の facade なので、変える file は重ならない。(2') の完了は (4) の開始条件にする（D5）。(2') は PR #214 の merge の後に A1 を始める。
  - **U4 期限。** program の期限は 2026-11-30 である。本スライスの A2・A3・B・C はそれまでに終わらない。program の期限を更新するか、置換 revision にするかは owner の判断で、本書と `docs/README.md` は期限を書き換えていない。本書の期限（2026-12-31）も仮の値で、owner が確認する。期限を理由に §5 の preflight や判定必須の検証を省かない。
  - **U5 目的・利用者の文言の承認。** §1 の改めた文言を owner が承認する。承認の事実を §1 に記録してから A2 の目的レビューへ渡す。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/` からの相対。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Scripts/Editor/Build/Materialization/BuildCandidateMaterializer.cs`（新規） | 利用者が渡した候補・tag・requirement・graph を受け、候補の同一性と GUID の形を検査し、root ごとの閉包を導いて snapshot にする公開入口（orchestration と入力の検査。Unity の I/O は gateway 越し） | 入力の入口（S1） | 状態を持たない。呼び出しごとに結果を返す | 入力: `OneStarMaker.Build.Selection` の public 型、同 folder の `AssetDependencySnapshotBuilder`・`IAssetDatabaseGateway`・`BuildMaterializationSnapshot`・`BuildMaterializationIssue`。`OneStarMaker.Runtime.*` は使わない | public: 型 1、コンストラクタ 1、メソッド 1。internal: gateway を受けるコンストラクタ | fake の gateway で Unity の `AssetDatabase` 無しに全分岐を検査できる | 0 → 約 140 |
| `Scripts/Editor/Build/Materialization/BuildMaterializationSnapshot.cs` | 候補一式の不変の入れ物 | provider の識別子を internal な引数で受ける（既存の 3 コンストラクタは今の固定値を渡す） | 変更なし | 変更なし | 変更なし（internal の増分だけ） | 既存のテスト + AC5 の 1 件 | 100 → 約 112 |
| `Scripts/Editor/Build/Materialization/BuildMaterializationIssue.cs` | issue の code と対象 | 候補を対象にする issue のため、`BuildMaterializationSubject` の末尾に `Candidate` を足す | 変更なし | 変更なし | public な enum の値 1 つ（末尾への追加） | AC3 | 53 → 約 54 |
| `Scripts/Runtime/BuildContent/BuildContentRootReader.cs`（新規） | 単独の directory の root を検査し、名前・表現・種別から entry を解決する公開入口。検査と解決は `ContentDirectoryIndex` へ委譲し、自分で持つのは上書きの root の拒否と、issue から公開の例外への写しだけ（policy を持たない薄い境界） | 読み口（S3） | 不変。`Open` が返した instance は root の寿命に従う（root を解放した後は使わない）。directory の登録と解放は利用者が持つ | 入力: 同 folder の `ContentDirectoryIndex`・`BuildContentRoot`・`BuildContentEntry`、共通契約の `ContentDirectoryException`。`AssetManagement` の他の型、session、`Unity.Loading` の登録 API は使わない | public: 型 1、static なメソッド 1、instance のメソッド 2 | root を code で組めば、Unity の directory 無しで全分岐を検査できる | 0 → 約 70 |
| `Scripts/Editor/Build/Content/BuildContentReport.cs` | build の要求と結果、report の書き出し | `BuildContentResult` に `Target` を足す（値は `BuildContentCoordinator.TargetName`） | 変更なし | 変更なし | public な property 1 つ | AC9 | 415 → 約 418 |
| `Tests/Editor/Build/BuildCandidateMaterializerTests.cs`（新規） | 入口の単体テスト（AC2〜AC4） | — | — | `OneStarMaker.Tests.Editor`（internal なコンストラクタを使う） | — | — | 0 → 約 300 |
| `Tests/BuildContent/BuildContentRootReaderTests.cs`（新規） | 読み口の単体テスト（AC8） | — | — | `OneStarMaker.Tests`（既存の `ContentDirectoryIndexTests` と同じ assembly） | — | — | 0 → 約 220 |
| `Tests/Editor/Build/SceneResourceContentMaterializerTests.cs` | 既存 | AC5 の 1 件を足す | — | — | — | — | 追加 約 15 |
| `Tests/Editor/BuildStandalone/BuildStandalonePathTests.cs`（新規） | AC10 の経路テスト | — | 一時 folder と出力は test が所有し、終わりに消す | 下の asmdef の参照だけ。public な型とメンバーだけを使う | — | 実 Unity の Content Directory build を 1 回行う | 0 → 約 230 |

- 入力の入口の署名（B が設計を足さないために固定する）:

  ```csharp
  namespace OneStarMaker.Editor.Build.Materialization
  {
      public sealed class BuildCandidateMaterializer
      {
          public BuildCandidateMaterializer();
          internal BuildCandidateMaterializer(IAssetDatabaseGateway gateway);

          public BuildMaterializationResult Materialize(
              IEnumerable<BuildContentCandidate> candidates,
              IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> tags,          // stable key → tag。無い候補は tag 無し
              IEnumerable<BuildContentRequirement>? requirements = null,
              IEnumerable<BuildGraphNode>? graph = null);
      }
  }
  ```

  - 候補の `PhysicalKey` を root の GUID として読み、閉包を `AssetDependencySnapshotBuilder.Build`（欠けを許さない版）で導く。
  - **GUID は小文字 32 桁の 16 進だけを受ける。** それ以外は `InvalidRootGuid` の issue で、入口は正規化しない。候補は利用者が選択へ渡す物と同じ instance で、入口が key を書き換えると plan の候補と snapshot の候補が食い違う。大文字を拒むので、大文字小文字だけが違う 2 つの physical key が別の root として通ることも無い。
  - **入口が検査する範囲。** 入口が持つのは、閉包を導くのに要る検査（GUID の形、解決、file の有無）と、SceneResourceMap の経路が snapshot に入れる前に行っている候補の同一性の検査（stable key の重複、physical key の衝突）だけである。tag の dimension と値の妥当性、requirement を満たすか、graph の構造は選択（`BuildTagSelector`、`BuildPurposeResolver`）が、plan と snapshot の対応は build の照合（`BuildContentProjection`）が今までどおり検査し、入口は書き直さない。requirement と graph は中身を見ずに snapshot へ渡す。
  - 引数の誤りは `ArgumentException` 系（AC3 の末尾）。入力の中身の不整合と `AssetDatabase` の結果は issue にし、issue があれば snapshot は null（既存の `BuildMaterializationResult` の規則）。
  - issue の code は既存の値だけを使う。新しい code が要ると分かったら B は Phase A へ戻す。
  - provider の識別子は `buildsystem.candidate-tags.v1` の固定値。
- 読み口の署名（同上）:

  ```csharp
  namespace OneStarMaker.Runtime.BuildContent
  {
      public sealed class BuildContentRootReader
      {
          // 検査に通った、上書きでない単独の directory の root だけを開く。通らなければ ContentDirectoryException。
          public static BuildContentRootReader Open(BuildContentRoot root, string expectedIdentity, string expectedTarget);

          // 完全一致が先、無ければ Full。representation が null か空なら Full。無ければ ContentDirectoryException(EntryMissing)。
          public BuildContentEntry ResolveObject(string logicalKey, string? representation = null);
          public BuildContentEntry ResolveScene(string logicalKey, string? representation = null);
      }
  }
  ```

  - `Open` は `ContentDirectoryIndex.TryCreate` を呼び、issue を `ContentDirectoryException`（code・文言・identity・target・logical key・表現）へ写す。写し方は `ContentDirectoryLayers.RegisterLayer` が今行っているのと同じである。その後、`root.OverlayBase` が null でなければ `InvalidRoot` で拒む（`ContentDirectoryLayers` が base に対して使う code と同じ）。
  - 解決は `TryResolveObject` / `TryResolveScene` を呼ぶだけである。`TryGetExact`、`Entries`、cache の key は公開しない。
  - 失敗の型は、共通契約に既にある public な `ContentDirectoryException` を使う。Try 形の戻り値と public な issue の型は足さない（失敗の表し方を 2 つにしない。U-C の session と同じ表し方になる）。
  - load・解放・登録・登録解除は持たない。利用者は、返った entry の `Object` / `SceneId` を Unity の API へ渡す。
- `BuildContentResult.Target`: 既存の public なコンストラクタの署名は変えない。property は coordinator の target の名前を返すだけで、file へ書く内容は変えない。
- test 用の asmdef（U1 の裁定。同上）:
  - 置き場所と名前: `Tests/Editor/BuildStandalone/OneStarMaker.Tests.BuildStandalone.Editor.asmdef`。既存の `Tests/Editor/TestObservation/OneStarMaker.Tests.TestObservation.Editor.asmdef` と同じ形（`Tests/Editor/` の下の folder に専用の test の asmdef を置く）。repo 相対 path は `.meta` を含めて最長 108 字で、`tools/path-budget.json` の上限 150 に収まる。
  - 参照: `OneStarMaker.Build.Selection`、`OneStarMaker.Build.Materialization`、`OneStarMaker.Build.Content`、`OneStarMaker.Runtime`、`nunit.framework.dll`。Unity のテストの実行に要る TestRunner の参照は、既存の test の asmdef に合わせて B が足してよい。SampleGame、`OneStarMaker.Editor`、`OneStarMaker.Tests.Editor`、`OneStarMaker.TestSupport` は参照しない。
  - 設定: Editor 専用、`UNITY_INCLUDE_TESTS`、`autoReferenced` は false。
  - この asmdef を参照する asmdef は無い。どの `AssemblyInfo.cs` の `InternalsVisibleTo` にもこの名前を足さない（足すと目的が無くなる）。
  - `unity/Assets/README.md` の folder の図と依存図に、TestObservation の行と同じ書き方で各 1 行を足す。
- 配置の理由:
  - 入力の入口は `SceneResourceContentMaterializer` と同じ folder・namespace・asmdef（`OneStarMaker.Build.Materialization`）。同じ「materialize の公開入口」で、同じ internal（builder、gateway、snapshot のコンストラクタ）を使う。program §2.1 の分類では BuildSystem 本体（Materialization の一般部分）に入り、OSM 固有の 2 file とは依存が違うので file を分ける。
  - 読み口は `ContentDirectoryIndex` と同じ folder・namespace（`Runtime/BuildContent/`、BuildSystem 本体。program §2.1）。形式の意味を持つ型の隣に、その公開の境界を置く。連携の 2 file（session、backend）とは依存（`AssetManagement` の internal）も寿命も違うので混ぜない。
- 行数警報: 該当なし（500 行・3 責務・50% 増のどれにも当たらない）。
- 採らなかった構成:
  - **`BuildMaterializationSnapshot` のコンストラクタを public にする。** program §4.3 が禁じている。provider の識別子が OSM の値のまま漏れ、閉包の規則を利用者が書き直すことになる。
  - **閉包を利用者が渡す factory（program §4.3 の文言どおり）。** 閉包は root の GUID から機械が導けるので、利用者の入力にする理由を目的から説明できない。A2 で見てほしい点。
  - **候補も機械が作る薄い入力の型（logical key・GUID・表現だけを受ける）。** 利用者の入力は減るが、候補は選択の public な入力として既に利用者が作る物なので、今回は足さない。
  - **`ContentDirectoryIndex` 自体を public にする（`TryCreate`・`TryResolve*` と `ContentDirectoryIssue` を公開）。** 採らない理由は 3 つ。(1) `TryCreate` は上書きの層の root も索引にするので、上書きの root を拒めない。単独の root の入口として公開すると、利用者が上書きの root を単独で解決できてしまう（I6）。拒否を `TryCreate` に足すと上書きを重ねた登録が落ちる（I1 (d)）。(2) 同じ型が層と cache のための internal（`Entries`、`TryGetExact`、`CacheKey`、`CachePrefix`、`OverlayLayerPrefix`）を持ち、公開の契約と連携部分の内部が 1 つの型に混ざる。(3) `ContentDirectoryIssue` を public にすると、失敗の表し方が共通契約の `ContentDirectoryException` と 2 つになる（program §1.2 G5）。
  - **読み口が登録と load まで引き受ける。** session と同じ寿命の管理を BuildSystem 単独の側に作り直すことになり、目的（規則を書き直させない）に要らない。Unity の API は規則ではない。
  - **読み口を入れず、Unity の API と `BuildContentRoot.Entries` で読ませる（初稿）。** §1 の変更理由のとおり。

## 4. 実装計画

- 変更対象: §3 の表の file。
- 順序（入力の入口から始める）:
  1. `BuildMaterializationSnapshot` の provider の識別子を internal な引数にし、既存の 3 コンストラクタが今の固定値を渡す形にする。AC5 のテストを足し、既存のテストが変更無しで通ることを確かめる（I1）。
  2. `BuildCandidateMaterializer` と単体テスト（AC1〜AC4）。
  3. `BuildContentRootReader` と単体テスト（AC7、AC8）。
  4. `BuildContentResult.Target`（AC9）。
  5. test 用の asmdef（§3）と経路テスト（AC10）。`unity/Assets/README.md` の folder の図と依存図に各 1 行を足す。
  6. `git diff` で AC6・AC13 を確かめる。
- Phase B から Phase A へ差し戻す条件: §3 に無い public な型・メンバー・enum の値が要る。既存の呼び出しの書き換えが要る（AC6）。`ContentDirectoryIndex.cs`・`ContentDirectoryLayers.cs`・`ContentDirectorySession.cs`・共通契約の変更が要る。production の asmdef の参照か `InternalsVisibleTo` の追加が要る。test 用の asmdef に §3 に無い参照が要る。経路テストの手順 3（snapshot から public な選択で 3 件を選ぶ plan を導く）が public だけでは書けない。`AssetDependencySnapshotBuilder` の規則の変更が要る。
- 対象外を維持する方法: `Editor/Build/Content/` は `BuildContentReport.cs` だけ、`Runtime/BuildContent/` は新しい 1 file だけに差分を出す。`Runtime/AssetBuildContracts/` と SampleGame の production に差分を出さない（`git diff --stat` で確かめる）。出力先は今の許可 root の配下だけを使う。

## 5. テストとレビュー計画

- 単体テスト: `BuildCandidateMaterializerTests`（fake の gateway。AC2〜AC4）、`BuildContentRootReaderTests`（code で組んだ root。AC8）、`SceneResourceContentMaterializerTests` の追加 1 件（AC5）。
- 差し戻し中の起点 `-Filter`: test class の `BuildCandidateMaterializerTests`、`BuildContentRootReaderTests`、`ContentDirectoryIndexTests`、`SceneResourceContentMaterializerTests`、`MaterializeAllowingAbsentSourcesTests`、`BuildContentProjectionTests`、`BuildContentReportTests`、`BuildStandalonePathTests`。書式は `pwsh tools/run-tests.ps1 -Filter` に従う。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（`pwsh tools/run-tests.ps1`、filter 無し）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、P 検査（AC12）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: AC10 の経路テスト（実 Unity の Content Directory build を 1 回）。既存の `SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` は U-C の非退行として全回帰に含まれる。同テストは build の所要時間で `[Timeout(360000)]` を超えたことがある（`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md` §2）。経路テストは prefab 3 つだけを build するが、timeout の値は B が既存の統合テストに合わせて付ける。
- **A3 の前の preflight（P 検査の基準の確定。A3 の凍結の前提）:**
  - 理由: AC12 は GO に必須だが、program の base から 660 commit 進んだ今の U-A・U-B の集合が閉じているかは未実測で、Git 外の P 検査の script を取得できるかも確かめていない。これを Phase C で初めて知る構成にしない。
  - 担当: Unity Editor と Git 外の script がある、ローカルの担当。この A1 を書いた環境では実行できない。
  - 手順: 固定 base `9e7d4148` の clean な checkout で、ASSET_CYCLE で凍結した P 検査（script、集合、除外する file、参照 assembly、define、実行コマンド）を取得し、U-A・U-B（Runtime）・U-B（Editor）を実行する。program の base の後に増えた file が、集合の規則（folder と除外の一覧）のどちらに入るかを照合する。
  - 記録する物（本書のこの項に追記）: script を取得できたか、実行した commit、各集合の成否、失敗した場合はその file と型、集合の定義を改める必要の有無。reference・hash の実値とローカルの path は書かない。
  - 結果の扱い: 3 つとも成功なら、その手順を AC12 の手順として A3 で凍結する。base で既に失敗するなら、それは本スライスの外の既存の閉包違反で、修正を先に入れるか program を再裁定するかを owner が決めてから凍結する（本スライスの AC を緩めて通さない）。script を取得できないなら、P 検査を作り直す手順（program §0 の実測の方法の再作成）と担当を A3 の前に決める。どの場合も、未実施のまま A3 を凍結しない。
  - 状態: 未実施。
- 操作・実行時・目視条件の検証経路: 該当する条件は無い（全ての条件は Unity のテストと P 検査で閉じる）。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点:
  - EditMode のテストから実 Unity の Content Directory build を行い、同じ process で `ContentLoadManager` に登録して load する経路は、既存の `BuildContentDirectoryIntegrationTests` と `UnityContentDirectoryAdapterTests` が使っている既知の経路である。違いは、session を通さずに Unity の API を直接呼ぶ点だけ。
  - 未確認。初回確認は Phase C: session を通さない登録・load・解除が EditMode で成立するか。理由: この A1 を書いた環境に Unity が無い。担当: Phase C の実行担当。不成立時: B へ戻し、原因が Unity の API の使い方なら B 適応、session の内部の処理（遅れて届く handle の解放など）が無いと成立しないなら、対象外の「登録・load の包みを作らない」が崩れるので Phase A を再開する。
  - P 検査の実行可能性は、上の preflight で A3 の前に確かめる（Phase C へ送らない）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（判定はテストの結果、`git diff`、file の読み取りで行う。P 検査は ASSET_CYCLE で凍結済みの script を使い、本スライスでは新しい照合 script を作らない。集合の定義は program §5.1 のままで、新しい 2 file は folder の規則で U-B（Runtime）と U-B（Editor）に入る）。
- 機械検査: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。A1 の各 commit の時点で両方を実行した（結果は PR 本文）。
- A0/A1 主担当・モデル・ベンダー: Cloud の計画 session。モデルの識別子は公開文書に書かない（起動元の記録にある）。
- C' 用に予約した担当・モデル・ベンダー: 未定。A2 で使う系列を決める時に、A・B・C に関与しない系列を 1 つ残す。外部計画レビューの担当の系列も、関与した系列として数える。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 の割当の後に書く）

### 5.1 A2 の独立レビューと A3 の採否

- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 未実施。予定する観点は、アーキテクチャゲート（特に §3 の「採らなかった構成」、読み口を facade にする判断、入口を `Materialization` に置く判断）、目的レビュー、A0 だけからの代替構成。各不変条件を破る順序を 1 つ挙げられるかも A2 で確かめる。
- Draft PR の外部計画レビュー（2026-11-27、入力は A1 初稿の head `70ee8b5a`、base `9e7d4148`。静的レビューで、Unity と P 検査は未実行。A2 の正規の 3 件には数えない）。主担当の対応は次のとおりで、採否の裁定は A3 で owner が行う。
  - **F-01（U2。Unity で root を読めることは、BuildSystem の読み込みの契約を使えることと同じではない）:** 指摘の事実（`TryCreate` の検査、`ContentEntryTable` の fallback、規則の共有のコメント、`TryCreate` が上書きの root を拒まないこと）を code で確かめ、全て正しかった。対応: 読み口 `BuildContentRootReader` を本スライスに入れた。公開の形は「`ContentDirectoryIndex` を公開」と「同じ internal へ委譲する facade」を比べて facade にした（§3）。単独の directory だけを対象にし、上書きの root は `Open` が拒む。`InstalledRevisionVerifier` と cache の key 等は公開しない。session・寿命の管理・I/O の wrapper は作らない。S3 と経路テスト（AC10）は公開の読み口を通し、同じ logical key の別の表現、完全一致の優先、Full への fallback、無い key の失敗を、内容の識別で確かめる形にした。検査の拒否は単体テスト（AC8）。分ける案（入力だけにして S3 を疎通に狭める）は採らなかった（§2 の分割の判断）。
  - **F-02（AC9。P 検査の基準と実行可能性を先に確定する）:** 対応: A3 の前の preflight を §5 に足し、A3 の凍結の前提にした。「他の PR が原因なら program への入力」という初稿の文を取り下げ、統合した tree で検査して、原因が何でも GO の代わりにしないと改めた（AC12）。「取り直す方法を A3 で合意」と「初回確認は Phase C」の順序の食い違いは、P 検査を preflight へ移して解いた。preflight 自体は未実施（ローカルの担当）。
  - **F-03（入力の GUID と snapshot の契約）:** 指摘の事実（`NormalizeGuid` の小文字化と、後段の ordinal の照合）を code で確かめた。対応: 小文字 32 桁の 16 進だけを受け、正規化しない（§3、I4、AC3）。AC2・AC3 に、実依存を含む閉包、tag の複写と伝播、空でない requirement と graph の保持、順序の安定、引数の誤り、複数候補の一括失敗を足した。入口と選択・照合の責務の分担を §3 に書いた。経路テストの plan は snapshot から導く（AC10 の手順 3）。
  - **裁定の推奨への対応:** U1（非 friend の asmdef は維持。Runtime 全体を参照するので U-B の型だけを使うことは別に担保）→ AC10 の末尾に、判定 C が `using` と完全修飾を読む条件を足した。U3（X7 は別スライス、並行するなら完了依存と変更範囲を明記）→ U3 に書いた。U4（暦日で検証を省かない）→ U4 に書いた。
- A3 統合担当・モデル・採否: 未実施

## 6. Phase B 実装結果

未着手

### 動作確認

未着手（検証経路に該当する条件が無いので、Phase B の後は `対象なし` と記録する予定）。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
