# 手元の編集から上書きを決めて作る（S3-4a）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S3-4 を A1 が 2 つに分けた前半。分割の理由は §2.5）
- status: `B`（Phase B 完了。A3 凍結版は revision 1。次は発見 C。§4 の (1) の観測と実装結果は §6。A2 の 4 件と担当の採否、外部計画レビューの 1〜3 巡目の指摘と担当の分類は §5.1）
- branch: `claude/overlay-from-local-edits`（PR の base は `develop`）
- implementation base commit: `63f171ac`（`origin/develop` の head、2026-11-21。§1 の現況と §3 の行数はこの base）
- implementation head commit: この票を含む commit
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: 承認者は owner（root、依頼者）。2026-11-21。対象は revision 1（この記録を含む commit の本書の全体）。根拠は、外部計画レビューの 3 巡の反映の後に owner が凍結と Phase B の開始を指示したこと。§2.8 の U1・U2 は担当の案のとおりで、§5.1 の担当の分類を覆した行は無い。目的・利用者の 2 欄は変えていない
- risk: `high`（publish する manifest に欄を足す、手元に無い source を build に渡さない fail-closed の契約、依存閉包の作り方と上書きの選び方を変える）
- owner: root（依頼者）
- created: 2026-11-21
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md)「上書きの build と逆引き」（編集の検出、欠けを許す materialize、作り直せない root、認める口、再利用）と「配信用 transport の公開境界」（`sourceClosure` の欄）、[13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」（作り直せない root の entry が base から載り、その直接参照が base の build の時の bytes であること。「同じ id の物」と「id による影」の規則は変えない）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（AC6 の観測）、program §5.3・§8・§10
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§1、§4.3、§5.3「上書きの方式を選ぶ理由と見直しの条件」「編集の種類ごとの扱い」、§8 の S3-4 の行と S3-2・S3-3 からの移送）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase C（発見 C）。動作確認は `対象なし`（§5、§6）。
  - 次の作業: implementation base から head までの差分を、§2 の受け入れ条件と §3 の責務マップに照らしてレビューし、§5 の起点 filter と判定必須テストを走らせて §7 に書く。R8 は同じ head の 2 つの checkout にまたがる `[Explicit]` の test で、走らせ方は §6 の「判定 C への引渡し」。
  - 読む範囲: 既定（§0〜§8）。
  - 引き継ぐ commit: この票を含む commit（Phase B の出口の commit）。SHA は起動指示と PR 状態ブロックで渡す。
  - 必要な権限: Unity の batch test（`pwsh tools/run-tests.ps1`）と、R8 のための新しい worktree の作成と初回の import（約 11 分）。Content Directory の build を伴う test は他の Unity を止めて走らせる（§5）。資格情報は要らない。
  - 停止規則: §2.6。NO-GO (i)〜(iv) に当たる観測（特に AC6 の R4・R8 で `B` の `T` が編集後の色）は owner に返す。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 取得した目的 build（base）を使う Editor の Play で開発者が自分の編集を確認できるように、base の記録と手元の file を比べて、編集の影響を受ける root のうち手元の source が揃うものだけを上書きとして build する。何を編集したかを開発者が入力する必要は無い。編集が無ければ上書きを作らず、前回と同じ内容なら前回の上書きを使い回す。担当外の source が手元に無くても上書きを作れる。編集の影響を受けるのに手元では作り直せない root があれば、その root と理由を build の前に示し、呼び手が明示的に認めない限り上書きを作らない。Play の入口と起動への接続は次のスライスが行う。
- 利用者: (a) Editor の Play の入口を実装する者（次のスライスの SampleGame の入口と、他の host の Editor code）。install した base、宣言から作った選択、他の供給元が届ける Scene を渡して、上書きの計画と build を呼ぶ。(b) 目的 build を build して publish する者（CI、検証 Play）。新しく選ぶ・入れるものは無く、publish した manifest に root ごとの依存閉包が入る。(c) 編集する開発者。このスライスでは操作を持たない。入口がつながった後に、編集を入力せずに済むことと、反映されない root を知らされることを受ける。
- 目的・利用者の文言の経緯: 上の 2 欄は program §8 の S3-4 の行、§5.3「上書きの方式を選ぶ理由と見直しの条件」「編集の種類ごとの扱い」、S3-2・S3-3 から移送した入力から、本書で初めて書いた。S3-4 の行の問い（Play ボタンから上書きを重ねた Play に入る）のうち、上書きを決めて作る側だけを取り出している（§2.5）。owner の承認は未了である（§2.8 U1）。A2 の目的レビューはこの原文で行う。A3 で owner が文言を変えた時は目的レビューをやり直す。
- 対象外:
  - Play の入口（経路、判定表、取り消しと再 Play の順序、record と詳細窓）、起動の設定の key と `AbstractApplicationInitializer` の配線、Player の構成で拒むこと、実 Editor の Play と Domain Reload 有効の Play での確認（S3-4b）。
  - 作り直せない root がある時に、通知して続けるか止めるかの規則（S3-4b。本書は「呼び手が明示しない限り作らない」口までを作る。§2.8 U3）。
  - 保存していない asset の編集（Editor の memory にだけある変更）と、disk に書いたが import していない変更。本書は disk の file の内容で決め、計画の前に import が済んでいることを呼び手の前提にする。保存の時点と、複製の準備が Dirty の asset を file に書く順（S3-3 から移送）は S3-4b。
  - 削除した asset を、checkout していない asset と区別すること（§2.3、§2.8 U7）。
  - `Runtime/ContentResolution/`、`ContentDirectorySession`、`ContentRevisionGate`、`AssetManagement`、`Bootstrap`、`SceneSystem`、Distribution の検証と install の変更。登録と解決は S3-2 のまま使い、`Runtime/` に足すのは manifest の任意の欄だけである。
  - SampleGame の製品 code（宣言からの上書きの host 関数、bootstrap の合成が欠けの記録を引き継ぐこと）。S3-4b が入口と一緒に足す。本書は SampleGame を使わない合成 fixture で確かめる（program D10）。
  - script（閉包が除く拡張子）、`Packages/` の下、import 時の依存の変更。今の逆引きと同じく、どの root にも当たらない（§2.3）。
  - 編集の検出を速くする工夫（時刻や大きさの cache）、古い上書きの directory の掃除、上書きの build の所要の表示。
  - base の選び方（どの revision を自分の base にするか）、要求の時点の install、Local / LAN の供給元（S4）。
  - 閉包の記録が manifest の上限を超える規模で、別の file に分けること（S4。§2.4 P3）。
- 現況（base `63f171ac`。code の読みと `grep` で確かめた。path は `unity/Assets/OneStarMaker/Scripts/` からの相対で、SampleGame と test だけ `unity/Assets/` からの相対）:
  - **上書きの入口は呼ばれていない。** 逆引き `ContentOverlaySelection.Select(plan, snapshot, editedPaths)`（`Editor/Build/Content/ContentOverlaySelection.cs` 238 行、164〜226 行）と上書きの `BuildContentRequest`（`Editor/Build/Content/BuildContentReport.cs` 65 行目からの constructor）を呼ぶ製品の code は無く、呼ぶのは test（`OverlayRegistrationIntegrationTests`、`ContentOverlaySelectionTests` など）だけである。編集した path の一覧は呼び手が渡す。
  - **手元の source が 1 つでも欠けると、逆引きの入力が作れない。** `SceneResourceContentMaterializer.MaterializeCore`（`Editor/Build/Materialization/SceneResourceContentMaterializer.cs` 257 行）は、issue が 1 件でもあれば snapshot を null にする（167〜171 行）。materializer は map の全ての `SceneResource` の全ての payload（宣言が選ばない表現、到達しない node、付随 asset を含む）に閉包を作らせる（72〜151 行）。root の file が無ければ `AssetDependencySnapshotBuilder.Build`（同 folder、97 行）が `UnresolvedRootGuid`（19〜23 行）か `MissingRootAsset`（34〜38 行）の issue を足す。`MissingDependencyAsset`（52〜56 行）は、`AssetDatabase.GetDependencies` が返した path の file が無い時だけ出る（checkout していない依存の path を Unity が返すかは未測定 (iii)）。SampleGame の `SampleGameContentBuild.Materialize`（`SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` 109〜118 行）は snapshot が null なら例外にする。program §5.3 の D は「手元の逆引きは手元に無い root を知らない」と書くが、今の code では、担当外の source が手元に無い checkout は逆引きの前の materialize で失敗する。
  - **選択は全ての到達 node に候補を求める。** `BuildPurposeResolver.DeriveRequirements`（`Editor/Build/Selection/BuildPurposeResolver.cs` 211〜240 行）は到達した node の宣言済みの表現から requirement を作り、候補の存在と一意性は selector が materialize 済みの候補で検査する（`BuildTagSelector`、`RequiredGroupMissing` / `CardinalityViolation`）。手元に無い root を候補から落とすだけでは plan が出ない。
  - **逆引きは選択 root ごとに閉包をちょうど 1 つ求める。** `Select` は、selected の root に `snapshot.Dependencies` の閉包がちょうど 1 つ対応しないと `ArgumentException` にする（174〜182 行）。閉包は `Assets/` の下の file だけである。
  - **候補の identity は file が無くても決まり、projection は selected の root にだけ閉包を求める。** `ScenePayloadMappingPolicy` の stable key は logical key・表現・GUID から作り、path を使わない（`Editor/Build/Materialization/ScenePayloadMappingPolicy.cs` 14〜15 行）。`BuildContentProjection`（`Editor/Build/Content/BuildContentProjection.cs` 168 行）は、除外した候補には snapshot に在ることだけを求め、閉包は selected の root に求める（81〜122 行）。
  - **base の manifest は source の file の一覧を既に持つが、hash は publish の時点である。** `ContentTransportPublisher.Publish`（`Editor/Build/Content/ContentTransportPublisher.cs` 186 行）は、preflight の `closure`（全 root の閉包の和集合。`guid|path` の行、`BuildContentReport.cs` 226 行）から、file と `.meta` の path と、publish の時点の disk の SHA-256 を `sourceFiles` に書き、その時に無い file を落とす（78〜85 行）。root ごとの閉包（どの file がどの root の閉包に入るか）は manifest に無い。build の時点では `snapshot.Dependencies` と `projection.Roots` にある。
  - **手元と base の比較は「無い」を優先して「違う」を返さない。** `ContentSourceAdvisor.Inspect`（`Runtime/BuildContent/Distribution/ContentSourceAdvisor.cs` 77 行、public）は `sourceFiles` と手元の file の SHA-256 を比べ、無い file が 1 件でもあれば `Missing` だけを返す（checkout の案内用）。
  - **manifest の検証は知らない欄を拒まない。** install と再検証（`ContentManifestValidation.ValidateRequiredFields`、`Runtime/BuildContent/Distribution/ContentManifestValidation.cs` 353 行）は必須欄の有無と型だけを見る。任意の欄 `index` は、あれば `version` と `entries` の型を見る。manifest は 4 MiB（`MaxManifestBytes`）と file 100,000 件（`MaxFiles`）が上限。`ContentPurposeIndex.Read`（同 folder、282 行）は `index.version` が 2 以上なら「未対応の索引」として飛ばす。JsonUtility は入れ子の欄を省けないので、索引を持たない publish も版 0 の空の欄を書く。
  - **Editor の Content は Runtime の internal を呼べない。** installed の revision の検証と manifest の読み（`InstalledRevisionVerifier`、`ValidatedContentManifest`）は internal で、`Runtime/AssemblyInfo.cs` の `InternalsVisibleTo` に `OneStarMaker.Build.Content` は無い。public の `ContentInstallResult` は revision の root と digest・contentSet・revision・target を持ち、宣言の名前は持たない。Content は Materialization の internal（gateway、builder）も見られない。
  - **build の成果物。** `BuildContentCoordinator.Build`（`Editor/Build/Content/BuildContentCoordinator.cs` 221 行）は build ごとに新しい identity を作り、`reports/<identity>/`（preflight・outcome）と `content/<identity>/` を残す。上書きの build は mode が `Full` に固定で、base と同じ contentSet（大小無視）・違う宣言の名前・違う target・形の違う digest を report の前に拒む（`ValidateOverlay`）。前回の成果物を探して使い回す口は無い。
  - **登録の口は S3-2 のまま使える。** `ContentDirectorySession.RegisterVerifiedWithOverlay`（`Runtime/BuildContent/ContentDirectorySession.cs` 132 行、internal）は、上書きの root の記録と base の検証済みの値を照合して base → 上書きの順に登録する。`OverlayRegistrationIntegrationTests`（`SampleGame/Tests/Editor/Build/`、392 行）が、製品の build 経路の base（build → publish → install）と上書きを Play mode で登録して読んでいる。fixture の plan と snapshot はその test の中の合成（357〜368 行）で、`SceneResourceMap` と materializer を通らない。
  - **S3-3 の観測（入力）。** 複製から載った Scene が参照する asset は手元の Editor の版で、directory から載った Scene の同じ参照は build の時の版だった（O-1・O-2。program §8「S3-3 から移送した入力」）。開いている Scene 自体と、その Scene から見える編集は、snapshot が届ける。
  - **未測定:** (i) root の Scene の file が手元に無い（map の参照が解決しない）project で、Unity の Content Directory の build が、選んだ root だけを build できるか。(ii) 上書きに入らなかった base の root が直接参照する編集した asset が、製品の build 経路でも base の版で出るか（S2-1 の合成 fixture では base の版だった。program §5.3）。(iii) root の file はあるが依存の file が手元に無い時、`AssetDatabase.GetDependencies` がその依存の path を返すか。(iv) asset を `.meta` と一緒に `Assets` の外へ退かして戻した時、GUID と map の参照が元のまま残るか。
- UC ごとの利用者の入力:

  **UC-1 入口の code が上書きを計画して build する**（利用者 (a)）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | install した base（検証済みの revision） | 入口の候補の選択が Play のたびに機械で決める（今の経路 Built と同じ） | 目的の「base の記録と手元の file を比べて」。比べる相手が要る |
  | 宣言から作った手元の選択（plan と snapshot） | 開発者が永続の選択として選んだ RunPurpose から、host の code が Play のたびに機械で作る | 目的の「影響を受ける root」。root は宣言の選択で決まる |
  | 他の供給元が届ける Scene の logical key の集合 | 入口の code が Play のたびに機械で決める（snapshot の宣言） | program §5.3 の見直しの条件 (1)。snapshot が届ける Scene を上書きに入れると、開いている Scene だけの編集で build が走る |
  | 作り直せない root の集合を認める呼び出し | host の入口の code が開発時に規則を書く。規則は S3-4b が決める（§2.8 U3） | 目的の「呼び手が明示的に認めない限り上書きを作らない」 |
  | 編集した path の一覧 | **入力にしない。** base の記録と手元の file から機械が導く | 目的の「何を編集したかを開発者が入力する必要は無い」 |

  **UC-2 目的 build を build して publish する**（利用者 (b)）: 新しい入力なし。root ごとの閉包の記録は build が持つ値から機械が書く。

  利用者 (c) はこのスライスで操作を持たない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **検証済みの install した base の manifest と手元の file だけから、開発者の入力なしに、(1) 上書きが要るか、(2) 上書きに入れる root、(3) 編集の影響を受けるが手元で作り直せない root を build の前に決められ、(2) を製品の build 経路で作って base に重ねた Play で、上書きに入れた root は手元の版、(3) の root は base の entry から載り、その直接参照は base の build の時の版で出ると言えるか。source の一部が手元に無い checkout でも言えるか。**
- 誤って YES を出す道（受け入れ条件はこれを塞ぐ）:
  - 全ての source がある checkout だけで確かめる。部分 checkout は materialize が失敗して上書きが作れず、入口が失敗を握ると base だけの Play になる。
  - base の root の source だけを欠けさせて確かめる。base が選ばない表現の Scene や、依存の file だけが欠ける checkout が現れない。
  - 編集の一覧を test が手で渡す。検出が漏らす編集（`.meta` だけの変更、base に無い新しい file、移動した file）が現れない。
  - 作り直せない root を「上書きに入らなかった root」として黙って落とす。plan は出て build も通り、Play は base の版で動く。
  - 2 回目の計画で前回の上書きを常に使い回す。build は 0 回になるが、編集を足した後も古い上書きが出る。
  - 閉包の記録を持たない base（今ある全ての publish 済みの build）で、部分 checkout の計画が「編集なし」を返す。
  - build の後に source を変えてから publish した base。記録の hash が build した中身と違い、「編集なし」になる。hash を取った後から Unity の build までに source が変わった base も同じである。
  - 担当外の source を一度 import した `Library` で、file を退かして確かめる。最初から持たない project での GUID の解決と build が現れない。
  - install の digest と合わない base の manifest を、記録だけが壊れていると読んで `sourceFiles` と比べる。

### 2.1 進める最低条件

用語:

- **閉包の記録:** publish が manifest に書く、base の build の時点の root ごとの依存閉包（asset の GUID と path、どの root の閉包に入るか）。
- **編集:** 次のどれか。削除は入らない（下の「削除」）。
  - 内容の変更: 閉包の記録の asset（GUID）が手元にあり、file か `.meta` の SHA-256 が base の `sourceFiles` の値と違う。
  - 移動: 閉包の記録の asset が、手元では base と違う path にある。
  - 追加: 手元の選択の selected の root の閉包にあり、閉包の記録に GUID が無い file（除外した候補の閉包は見ない）。
  - 閉包の記録が無い・未対応・壊れている base では、閉包の記録を使わず、path で `sourceFiles` と比べる（内容の変更と、selected の root の閉包にあって `sourceFiles` に path の無い file）。
- **source の欠けた root:** root の file が手元に無い、手元の閉包の file が無い、または閉包の記録にあるその root の asset が手元で解決しない root。
- **作り直せない root:** 次のどれか（program §5.3 の D を含む）。理由を結果に持つ。「他の供給元が届ける」に入った root は除く（優先は AC4 の (a)）。
  - source の欠けた selected の root で、閉包の記録の閉包に編集を含む（`SourceAbsent`）。
  - source の欠けた selected の root で、閉包の記録に無い（`NotInBase`。base にも上書きにも無い root になる）。
  - source の欠けた selected の root があり、閉包の記録が無い・未対応の版・壊れている（`RecordMissing` / `RecordUnsupported` / `RecordInvalid`。欠けた selected の root の全て。編集が 0 件に見えても）。
  - 閉包の記録にあって手元の選択に無い root で、記録の閉包に編集を含む（`NotInLocalSelection`）。
- **削除:** 手元に無い file は、消したのか checkout していないのかを VCS なしに区別できない。本書は「手元に無い」を編集に数えず、source の欠けとして扱う（§2.3、§2.8 U7）。記録が「あり」の時は、欠けた root に編集が当たらなければ何も出ない。記録が無い・未対応・壊れている時は、手元から見えた欠けは原因を問わず作り直せない root になる（AC4 の (f)）。

不変条件（起きてはいけないこと）:

- **I-1 編集の影響を受ける root が、どの分類にも現れずに base の版で動くことは無い。** 手元の選択の root と閉包の記録の root のうち、閉包（手元の閉包か、記録の閉包）に編集を 1 つでも含む root は、計画の「上書きに入れる」「他の供給元が届ける」「作り直せない」のどれか 1 つに必ず現れる。閉包の記録があり、その root の集合に無い root（手元で足した root）は、source が揃えば「上書きに入れる」、欠ければ「作り直せない」に現れる。編集は「root に当たった理由」か「当たらなかった理由」のどちらかに必ず現れる。
  - 主張を崩しうる経路: (1) `ContentSourceAdvisor.Inspect` が `Missing` を優先して `Changed` を返さない。(2) 部分 checkout で materialize が失敗し、呼び手が例外を握って base だけで進む。(3) 手元に無い root を候補から落として plan を作り、その root が逆引きの対象から外れる。(4) base に無い file は `sourceFiles` との比較に現れない。(5) `.meta` だけの変更。(6) 移動して編集した asset（path で突き合わせると、source の欠けた root の記録の閉包に当たらない）。(7) 閉包の記録が無い・壊れている。(8) 大文字小文字だけ違う path。(9) 表現が複数ある node で、記録の閉包を logical key で引いて別の表現の閉包を当てる。(10) base にあって手元の選択に無い root（手元の map が古い、選択の規則が違う）。(11) hash を取った後に source を変えて build・publish した base（今の `sourceFiles` の hash は publish の時点。build の前へ移すだけでは、hash の後から Unity の build が読むまでの変更が残る。adapter は Unity の build の前に `AssetDatabase.Refresh()` を呼ぶので、その間に変わった file は build に入る）。(12) 閉包が持たない依存（script、`Packages/`、import 時の依存）。(13) 削除。(14) install の digest と合わない・読めない base の manifest を、記録だけが「壊れている」と読んで `sourceFiles` を使う。(15) 記録が無い base で、既にある asset だけを指す root の宣言を手元で足す（閉包の file が変わらないので編集に出ない）。
  - 探索した境界: `Editor/Build/Materialization/`、`Editor/Build/Selection/`、`Editor/Build/Content/`、`Runtime/BuildContent/Distribution/` の manifest と検証。(12)(13)(15) は塞がない（§2.3）。(15) は base にも上書きにも無い root になり、Play では解決の失敗として現れる（古い版は出ない）。
  - 未確認: SampleGame の bootstrap の合成（`SampleGameBootstrapContentComposer`）が欠けの記録を引き継ぐ形（S3-4b）。引き継がない合成は、閉包の無い selected の root で計画が例外になる（黙って通らない）。
- **I-2 作り直せない root がある上書きは、呼び手がその集合を認めない限り渡されない。** 作り直せない root が 1 つでもある計画から、build の request を作ることも、前回の上書きを受け取ることも、その root の集合そのものを認めた計画でなければ例外になる（report と Unity の build の前）。認めた時は、preflight に root・理由・当たった編集・足りない file が残る。上書きに入れる root が無く、作り直せない root だけがある計画は、編集の無い計画と同じ状態（上書き不要）にならない。
  - 崩しうる経路: (6) 上書きが空の計画は request も再利用も通らないので、状態だけを見る呼び手が、作り直せない root だけの計画を編集なしとして進める。(1) 既存の上書きの constructor（逆引きの結果から作る口）。source の欠けた selected の root は閉包が無く、今の逆引きの検査（選択 root ごとに閉包がちょうど 1 つ）が例外にする。(2) 逆引きを経由せずに `BuildPlan.WithSelectedSubset` で絞った plan を上書きでない build に渡す（今も publish は拒む。既存の public の口で、本書は変えない。§2.3）。(3) 再利用の口が、認めていない計画に前回の成果物を返す。(4) 前の計画の集合を、作り直せない root が増えた後の計画に流用する。(5) 「他の供給元が届ける」に、root の file が手元に無い root の key を渡して作り直せない root から外す。
- **I-3 手元に無い source を Unity の build に渡さない。** source の欠けた root は上書きの plan の selected に入らない。閉包の無い root を selected に持つ plan は、Unity の build の前の照合（projection）で失敗する（今の検査）。計画から作った上書きの build は、Unity を呼ぶ前に selected の閉包の全ての file が手元にあることを確かめる。
  - 崩しうる経路: (1) 計画が source の欠けた root を上書きの root に数える。(2) root の file はあるが依存が手元に無い root を、手元の閉包が欠けを含まないために「揃う」と読む（未測定 (iii)）。(3) host の合成が欠けの記録を落とす。
- **I-4 利用者の source、installed cache、base の directory を書かない。** 計画・検出・再利用の照合は読むだけである。build は今の adapter の契約（利用者の file を書かない。[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)）のまま。再利用の記録は成果物の root の下で、`reports/`・workspace・publish 先・installed cache の外に置く。
  - 崩しうる経路: (1) 計画が manifest を読む時に installed の revision の file を書き換える。(2) 再利用の記録を workspace（`Full` が消す）や installed cache に置く。
  - 確かめた事実（A2）: 今の `InspectInstalled` の read lease は `%LocalAppData%\OneStarMaker\RevisionLocks\v1\` に lock の file を作って残し、cache root と revision の下には作らない。本書の計画は lease を取らずに manifest の bytes を読む（§2.4 P6）。
- **I-5 再利用で古い上書きを出さない。** 前回の上書きを返すのは、上書きの入力の鍵（base の identity の 5 欄、上書きの contentSet、target、build する Editor の Unity の版、root に写る宣言の値、上書きの root ごとの stable key・logical key・表現・root の GUID と path、上書きの閉包の全ての file の GUID・path・file と `.meta` の SHA-256、鍵の版の定数）が前回と同じで、前回の outcome が成功で、build が成功した時に記録した成果物の file（`content/<identity>/` の下の全ての file の相対 path と SHA-256）が全て同じ内容で残っている時だけである。1 つでも違えば build する（成果物の file が 1 つ欠けた・書き換わった時と、再利用の記録か outcome が読めない時を含む）。計画から作った上書きの build は、Unity の build の後に閉包の file を hash し直し、計画の時と違えば失敗にして再利用の記録を書かない。
  - 崩しうる経路: (1) 鍵に入らない入力（script、project の設定、package、Framework の build の code）。既知の制約として書き、Framework の code の変更は鍵の版の定数を上げる（§2.3）。(2) 前回の成果物が消された・壊れた（directory は残り、中の file が 1 つ欠けた・書き換わった場合を含む。鍵は入力しか見ないので、壊れた成果物が同じ鍵で返り続ける。登録（S3-2）は上書きの directory の存在と base の identity を見るが、中の file は見ない）。(3) 別の base に重ねて作った上書き（登録の照合も拒む）。(4) 計画と build の間、build の間に source が変わり、変わった中身の上書きが古い鍵で記録される。
- **I-6 上書きを使わない利用は変わらない。** 今の `Materialize`、今の閉包の作成、上書きでない build、今の逆引きの呼び方は、同じ入力に同じ結果を返す。例外は I-1 (11) のための 2 つである: publish の `sourceFiles` の hash の時点を build の前へ移す。build の前と Unity の build の後で閉包の file か `.meta` の hash が違う build は、上書きでなくても失敗にする（§2.8 U10）。hash の時点で無い file は「無い」と記録し、前後とも「無い」なら等しいと扱う（上書きでない build は、今と同じく閉包の file が無くても失敗にしない。無い file を失敗にするのは、計画から作った上書きの build だけである。I-3）。新しい欄を持つ manifest を、この変更より前の runtime の検証（必須欄と型）が受け付け、`index` の版と目的 build の選択の答えは変わらない。`Runtime/` の変更は manifest の DTO の任意の欄だけで、`Runtime/ContentResolution/`、session、gate、`AssetManagement`、`Bootstrap`、`SceneSystem`、Distribution の検証と install に差分が無い（program §5.3 の見直しの条件 (3)）。

成立条件（起きなければならないこと）:

- **S-1 全ての source がある checkout:** 共有 asset の編集（program §5.3 の B・C）から、閉包に編集を含む root が上書きに入り、製品の build 経路で上書きが出来て、base に重ねた Play でその root が手元の版で載る（AC6 の R1）。新しい asset を参照に足す編集（E）は、足した asset が「追加」、参照を足した asset が「内容の変更」として同じ root に当たり、その root が上書きに入って build できる（AC6 の R7。Play では確かめない）。閉包の記録が無い・未対応・壊れている base でも、計画の答えは同じである（AC4 の (g)）。
- **S-2 source の一部が手元に無い checkout:** 計画が失敗せずに出る。欠けるのが、base の root の Scene、base が選ばない表現の Scene、root の依存の file のどれでも出る。手元で揃う root の上書きが build でき、作り直せない root が、root・理由・当たった編集・足りない file とともに計画に出る。認めた計画の上書きを base に重ねた Play で、上書きの root は手元の版で載り、作り直せない root は base の entry から載って（record は base）、その root の直接参照は base の build の時の bytes である。上書きにある id の物を id で引いた時の版は、ここでは言わない（§2.3）。担当外の source を一度も import していない `Library` の checkout でも同じである（AC6 の R8）。source が欠けていても、欠けた root の記録の閉包に編集が無ければ、上書き不要で作り直せない root も無い（AC4 の (m)、R8 の前半）。
- **S-3 build の回数:** 編集が無い計画は上書き不要で build 0 回。同じ内容の 2 回目は前回の上書きを返して build 0 回。編集を足すと build 1 回（program §5.3 の見直しの条件 (2)）。
- **S-4 他の供給元が届ける Scene:** 編集が当たる root が全て「他の供給元が届ける」Scene なら上書き不要で build 0 回（見直しの条件 (1)）。
- **S-5 閉包の記録の publish:** 目的 build を publish した manifest に閉包の記録が入り、root の集合は `index` の entry の集合と等しく、各 root の閉包は build に渡した閉包と等しく、記録の asset の path は全て `sourceFiles` にあり、`sourceFiles` の hash は build の前の値で、Unity の build の後に取り直した値と等しい（違う build は成功にならず、publish できない）。閉包の file の hash が 1 つでも取れなかった build（hash の時点で file が無い。合成の test の build）と、索引が版 0 の publish は、記録を版 0 にして、`sourceFiles` は今と同じく在る file だけを書く。manifest が上限を超える時は記録を版 0（記録なし）にして publish する。その事実は Console の警告（`Debug.LogWarning`）に出て、publish した manifest の `sourceClosure` の版 0 として機械でも読める。記録を版 0 にしても上限を超える manifest は、今までどおり publish が失敗する。

### 2.2 受け入れ条件

| ID | 条件 | 検証する条件 | 種別 |
|---|---|---|---|
| AC1 | 閉包の記録の読み口: 正しい記録を、root（logical key・kind・表現・root の GUID）ごとの asset（GUID・path）として返す。欄が無い・版 0 は「記録なし」、版 2 以上は「未対応」、値が壊れた記録（GUID の形か重複、path の重複か `sourceFiles` に無い path、範囲外の番号、`index` の entry と違う root の集合、自分の閉包に root の GUID が無い root）は「壊れている」と理由を返し、どれも閉包を返さない。読み口に渡すのは、計画が install の結果と照合し終えた bytes だけである（照合に落ちた manifest は AC4 の (k)）。表現が 2 つある node は表現ごとに別の閉包を返す。新しい欄を持つ manifest が今の `ValidateRequiredFields` を通り、`ContentPurposeIndex.Read` の答えが欄の無い manifest と同じ | I-1 (7)(9)、I-6、S-5 | 単体（EditMode） |
| AC2 | 編集の検出（fake の手元。test は編集の一覧を渡さず、手元の file と GUID の対応だけを変える）: 内容の変更、`.meta` だけの変更（本体の path で出る）、追加、移動、移動して編集、を同じ入力から同時に返す。手元に無い asset があっても他の編集を返す。大文字小文字だけ違う path を別の file として扱わない。記録が無い・未対応・壊れている base では、どれも path で比べる。除外した候補の閉包にだけある file は編集に出ない | I-1 (1)(4)(5)(6)(8) | 単体 |
| AC3 | 欠けを許す materialize: 今の `Materialize` は、root の file が無い・依存が無い入力に今と同じ issue を返し snapshot が null。新しい口は、同じ入力で snapshot を返し、欠けた payload の候補が残り（stable key は今と同じ）、欠けの記録（stable key・logical key・root の GUID・理由・足りない path）が付き、その候補に閉包は無い。root の GUID が手元で解決しない時、足りない path は空である（計画が閉包の記録の path で補う）。宣言が選ばない表現の Scene が欠けていても snapshot が出て、選択は今の規則でその候補を除外する。構造の失敗（GUID と path の不一致、衝突、graph の欠損、map の null）は新しい口でも issue のまま | I-1 (2)(3)、I-6、S-2 | 単体 |
| AC4 | 計画の分類（fake の手元と、bytes で組んだ base の manifest）: (a) 編集を閉包に含む root が「上書きに入れる」「他の供給元が届ける」「作り直せない」のちょうど 1 つに入る。優先は、root の file が手元に無い → 作り直せない、root の file が手元にある Scene で key が渡された → 他の供給元（依存の file が欠けていても。開いている Scene は snapshot が手元の版で届ける）、残りの source の欠けた root → 作り直せない、それ以外 → 上書き。(b) 「他の供給元」は Scene の node の logical key で、その node の全ての表現の Scene に効き、付随 asset の root には効かない。root の file が欠けた root の key を渡しても作り直せない root のまま。root の file はあるが依存の欠けた Scene の key を渡すと他の供給元に入り、作り直せない root に出ない。(c) 記録の閉包の asset が手元で解決しない root は、手元の閉包が出来ていても source の欠けた root になる。(d) 移動して編集した asset が、source の欠けた root の記録の閉包に当たる。(e) 記録にあって手元の選択に無い root が、編集を含めば `NotInLocalSelection` で出る。(f) 欠けた selected の root があり記録が無い・未対応・壊れている時、編集が 0 件でも欠けた selected の root が全て作り直せない root になり、「上書き不要・作り直せない root なし」を返さない。欠けの原因（削除か未 checkout か）は区別せず、fake の gateway が欠けた依存の path を返した root も同じに入る。(g) 全ての source がある checkout は、記録が無い・未対応・壊れているのどれでも、`sourceFiles` との path の比較と手元の閉包だけで決まり、編集があるのに編集 0 件を返さない。(h) 記録が「あり」で、その root の集合に無い root は、編集が無くても「上書きに入れる」（source が欠ければ `NotInBase`）。記録が無い・未対応・壊れている base では (h) を使わず、手元で足した root は編集（`sourceFiles` に path の無い file）に出た分だけが上書きに入る。(i) 「上書きに入れる」も「作り直せない」も空なら状態は「上書き不要」。「上書きに入れる」が空で「作り直せない」が 1 つでもあれば、状態は「作り直せない root だけ」で、「上書き不要」を返さない（build は要らないが、編集の無い計画と同じ状態にしない）。状態の名前の「だけ」は、上書きに入れる root が無いことを指し、他の供給元が空であることは言わない。他の供給元と当たらなかった編集は、3 つのどの状態でも計画に残る。当たらなかった編集は状態を変えない（どの root の閉包にも無い）。(j) 閉包が無く欠けの記録も無い selected の root は例外。(k) base の `transport.json` の bytes が上限を超える、SHA-256 が install の digest と違う、contentSet・revision・target が install の結果と違う、JSON として読めない、のどれでも計画は例外で、分類を返さない（記録の「壊れている」に読み替えず、`sourceFiles` を使わない）。(m) 記録が「あり」で、source の欠けた selected の root の記録の閉包に編集が無ければ、その root はどの分類にも出ない（他に編集が無ければ、状態は「上書き不要」で作り直せない root も無い）。(n) 同じ編集が、key を渡された Scene（root の file は手元にある）と source の欠けた root の両方に当たり、他に当たる root が無い時、状態は「作り直せない root だけ」で、他の供給元にその Scene、作り直せない root に欠けた root が出る（他の供給元を落とさない）。計画は build を起こさず file を書かない | I-1、I-2 (5)(6)、I-3 (1)(2)(3)、S-4 | 単体 |
| AC5 | 認める口・build の口・再利用（fake の adapter）: 作り直せない root がある計画は、認めないと request の作成も前回の上書きの受け取りも例外（report の directory は増えない）。集合が計画の作り直せない root と完全に一致しない認めは例外。認めた計画の build は preflight に `overlayUnbuildable`（root・理由・編集・足りない file）と `overlaySupplied` を残す。上書き不要の計画と、作り直せない root だけの計画から request は作れない。再利用が返す前回の結果の preflight（`overlayUnbuildable`・`overlaySupplied`・編集）は、その build を作った時の記録である。今回の分類と理由の正本は今回の計画で、鍵が同じでも理由は違いうる（例: `SourceAbsent` だった root が、手元の選択から外れて `NotInLocalSelection` になる。上書きの閉包は同じなので前回の上書きを返し、計画は今回の理由を持つ）。Unity の前に selected の閉包の file が 1 つ無ければ失敗して Unity を呼ばない。build の後の hash が計画の時と違えば失敗で、再利用の記録を書かない。成功した上書きの outcome に鍵が残る。再利用は、鍵が同じで、記録した成果物の file が全て同じ内容で残り、outcome が成功の時だけ前回の結果を返す。鍵の入力（I-5 の列挙）のどれか 1 つを変える・成果物の directory を消す・directory を残して中の file を 1 つ消す・中の file を 1 つ書き換える・outcome を失敗にする・outcome か再利用の記録を読めない内容にする、のどれでも返さず、その後の build は成功して、記録は新しい成果物を指す。成果物の directory に記録に無い file が増えただけでは拒まない。再利用の記録の path が `reports/`・workspace・publish 先・installed cache の下に無い | I-2、I-3、I-4 (2)、I-5 | 単体 |
| AC6 | 実機（合成 fixture、製品の経路。下の「AC6 の回」） | S-1〜S-4、I-1〜I-5、未測定 (i)〜(iv) | 実機（EditMode の `[UnityTest]`、build を伴う） |
| AC7 | 実機（AC6 の base の publish）: 記録の root の集合が `index` の entry の集合と等しく、各 root の閉包（GUID と path）が build に渡した snapshot の閉包と生の値で等しく、記録の asset の path が全て `sourceFiles` にある。Local と HTTP から install した manifest が同じ bytes。単体（fake の adapter）: preflight の hash の後、adapter の build の中で閉包の file を書き換えた build は、上書きでなくても失敗になり（issue は「build の間に source が変わった」）、`content/<identity>/` を残さず、publish できない。`.meta` だけを書き換えた build も同じである（比べる集合は、preflight に残した閉包の file と `.meta`）。閉包の file が hash の時点で無い上書きでない build は今と同じく成功し、その publish は記録が版 0 で、既存の `BuildContentFullModeTests` と `ContentPurposeIndexPublishTests` の publish は変えずに通る。索引が版 0 の publish は記録も版 0。build が成功した後に source を書き換えてから publish した manifest の `sourceFiles` の hash は build の前の値である。上限を超える記録は版 0 で publish され、`Debug.LogWarning` が 1 件出る（`LogAssert` で確かめる）。記録を版 0 にしても上限を超える manifest は publish が失敗する。hash の記録を持たない古い preflight からの publish は、記録を版 0 にして今までどおり publish する | S-5、I-1 (11)、I-6 | 実機 + 単体 |
| AC8 | 非退行と範囲: 全 EditMode 回帰が「XML の failed 0 と root result `Passed`」。Phase B の commit が変える file は §3 の file と本書だけ。`unity/Assets/OneStarMaker/Scripts/Runtime/` の差分は `BuildContent/Distribution/ContentTransportManifest.cs` だけ。`unity/Assets/SampleGame/`（`Tests/` を除く）と `Editor/Build/Selection/` に差分が無い（`git diff --stat`）。asmdef と `AssemblyInfo.cs` を変えていない | I-6 | 機械検査 |

**AC6 の回。** fixture は合成の `SceneResourceMap` で、Scene `A`（Full と Whitebox の 2 つの表現。base の宣言は Full だけを選ぶ）・`B`・`C`、`A` と `B` が直接参照する texture `T`、`C` だけが参照する texture `U` と material `M`、合成で足した root 1 つ。base は欠けを許す materialize（全て揃った状態）→ 選択 → build（`Full`）→ publish → install。Play mode の登録は S3-2 の口（`RegisterVerifiedWithOverlay`）を直接呼ぶ。file を退かす時は `.meta` と一緒に `Assets` の外へ移し、戻した後に GUID が base と同じことを確かめる。

| 回 | 操作 | 期待 |
|---|---|---|
| R0 | 編集なしで計画 | 上書き不要、作り直せない root なし、build 0 回 |
| R1 | `T` の pixel を書き換えて計画 → build → 登録して Play | 上書きに `A`（Full）と `B`、build 1 回。Play で `A`・`B` の `T` が編集後の色、record は上書き。`C` は base |
| R2 | そのまま計画 | 前回の上書き、build 0 回 |
| R3 | `A` と `B` の key を「他の供給元」に渡して計画 | 上書き不要、他の供給元に `A` と `B`、build 0 回 |
| R4 | `B` の Scene と、`A` の Whitebox の Scene を退かして計画。認めずに request、認めて build → 登録して Play | 計画が出る。上書きに `A`、作り直せないに `B`（`SourceAbsent`、編集 `T`、足りない file は `B` の Scene）。`A` の Whitebox は除外のまま。認めない request は例外。認めて build 1 回。Play で `A` の `T` は編集後（record は上書き）、`B` の `T` は base の色（record は base）、`C` は base |
| R5 | そのまま計画。認めずに前回の上書きを受け取る、認めて受け取る | 認めないと例外。認めると前回の上書き、build 0 回 |
| R6 | `B` と Whitebox を戻す。`M` を退かし、`U` の pixel を書き換えて計画 → 認めて build | 上書きに `A` と `B`、作り直せないに `C`（`SourceAbsent`、編集 `U`、足りない file は `M`）、build 1 回 |
| R7 | `M` を戻す。`T` を別の folder へ移動し（GUID は同じ）、`B` の Scene を退かし、`C` の Scene に base に無い新しい texture `N` を参照に足して計画 → 認めて build | 上書きに `A` と `C`（`C` の理由は、追加の `N`、内容の変更の `C` の Scene と `U`）、作り直せないに `B`（編集は移動した `T`）、build 1 回 |
| R8 | clean な checkout（fixture を一度も import していない `Library`。新しい worktree）で、`B` の Scene と `A` の Whitebox の Scene を最初から置かずに fixture を作る。同じ GO 候補 head の別の checkout が build・publish した base を install する。前半: `T` を base と同じ内容のまま計画。後半: `T` の pixel を base と違う色にして計画 → 認めて build → 登録して Play | 前半: 状態は上書き不要、作り直せない root なし、build 0 回（source が欠けていても、編集が無ければ認めを求めない）。後半: R4 と同じ。計画が出る。上書きに `A`、作り直せないに `B`（`SourceAbsent`、編集 `T`、足りない file は記録の `B` の Scene の path）。`A` の Whitebox は除外のまま。build 1 回。Play で `A` の `T` は手元の色（record は上書き）、`B` の `T` は base の色（record は base） |

- 各回の前後で、その回の操作が変える file（書き換える・退かす・戻す・移動する・足す file と、それぞれの `.meta`。R7 では `M`、`T`、`B` の Scene、`C` の Scene、`N`）を除く fixture の source と、installed の revision の全ての file の SHA-256 が同じ。
- 前の回の操作は、後の回が戻すと書かない限り残る（R1 で書き換えた `T` は R7 まで編集後のまま。R6 で書き換えた `U` は R7 でもそのまま）。R8 は別の checkout で、R0〜R7 の操作を引き継がない。
- 「build 0 回」の読み方: 合格は、その回に build の口を呼ばず、成果物の root の `reports/` の下の directory の数が増えないこと。report の前に拒まれた呼び出し（認めない request）も directory を増やさない。projection の失敗は directory を増やすので 0 回に数えない。
- R4 の「`B` の `T` は base の色」の読み方: 合格は、`B` から載った renderer の texture の pixel が base の build の時の色であること。編集後の色が出た場合は、作り直せない root の直接参照が base の build の時の版で出るという前提が崩れるので、NO-GO (ii) で owner に返す（§2.6）。`T` は root ではなく、id では引かない。R4 と R8 の合格を、上書きにある id の物を base の root から id で引いた時の版（13 章「同じ id の物」で上書きの版）の証拠に使わない。
- 合成で足した root の読み方: host が「欠けを許す materialize → 選択 → 合成 → `Plan`」の順に呼ぶ経路を通すために置く。この root の閉包に編集は入らないので、R0〜R7 のどの回でも、上書きにも作り直せない root にも出ない。
- R6 の読み方: `M` を退かした時、Unity の `GetDependencies` が `M` の path を返しても返さなくても、`C` が作り直せない root になること（記録の閉包の asset が手元で解決しないことで決まる）。どちらを返したかは観測として §6 に残す（未測定 (iii)）。
- R8 の読み方: R0〜R7 は `B` を一度 import した `Library` で file を退かすので、GUID から path への解決、`GetDependencies` の答え、Unity の build が、担当外の source を最初から持たない project と同じとは言えない。R8 はその project で R4 と同じ答えが出ることを確かめる。fixture は 2 つの checkout で同じ GUID になるように作る（`.meta` を固定の GUID で書く）。base を 2 つの checkout の間で渡す形は B が決めて §6 に書く。R8 は clean な checkout でだけ走らせ、全 EditMode 回帰の合否（AC8 の「failed 0 と root result `Passed`」）を変えない形にする（別の filter でだけ走らせる方法は B が決める）。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| Play ボタンから上書きを重ねた Play に入ること、経路と判定表、build の時点、record と表示（逆引きした root の数と build の所要）、起動の設定の key と配線、Player の構成で拒むこと、実 Editor の Play と Domain Reload 有効の Play、上書きを重ねた時の SceneDirector の経路 | S3-4b |
| 作り直せない root がある時に通知して続けるか止めるか | S3-4b（§2.8 U3） |
| 部分 checkout で作り直せない root がどれくらい生じ、編集 → Play の体験が成り立つか（program §8 の S3-4 の行の gate。成り立たなければ入口の配線の前に上書きの方式を見直す） | S3-4b の最初の作業（入口の配線の前）。本書が確かめるのは検出と分類の正しさまでで、頻度は測らない（§2.8 U14） |
| 記録が無い・未対応・壊れている base で、既にある asset だけを指す root の宣言を手元で足した場合（閉包の file が変わらないので編集に出ず、その root は base にも上書きにも無い。Play では解決の失敗になり、古い版は出ない） | 後続（S3-4b の A1 が、manifest の `index` の entry と比べて拾うかを決める）。記録のある base では AC4 の (h) が拾う |
| 作り直せない root から、上書きにある id の物を id で引いた時の版（上書きの版になる。13 章「同じ id の物」。影は上書きの root の entry だけに効く。「id による影」）と、状態を持つ共有 asset の実体が 2 つに分かれること（13 章「実体の重複」） | 既存の登録契約（S3-2）。本書は覆さず、確かめない。作り直せない root について本書が言うのは、entry が base から載ることと、直接参照が base の build の時の bytes であることまでである |
| build の間に source を書き換えて、Unity の build の後までに元へ戻した変更（前後の hash が同じになる） | 塞がない。build の間に source を変えないことは、今の逆引きと同じく呼び手の前提である |
| 保存していない asset の編集、複製の準備が Dirty の asset を file に書く順、disk に書いたが import していない変更（計画の前の `AssetDatabase.Refresh`） | S3-4b（計画の呼び手の前提として本書の API の doc に書く） |
| Play に入る時の domain reload が前の Play の native の登録の残りを消すか、OS の lease の file handle、close が失敗した session の扱い（S3-2 から移送） | S3-4b |
| SampleGame の宣言からの host 関数と、bootstrap の合成が欠けの記録を引き継ぐこと、実データでの確認 | S3-4b、S5 |
| 削除（消した asset と checkout していない asset の区別）。asset を消した編集は、その asset を閉包に持っていた root を source の欠けた root にし、他の編集が当たれば作り直せない root になる | S5（VCS の adapter）。それまでは CI での作り直しか、参照を外した file の編集として現れる分だけが届く（§2.8 U7） |
| script・`Packages/`・import 時の依存・project の設定の変更を上書きに反映すること、再利用の鍵に入れること | 後続（所有は S3-4b の A1 が決める。今の逆引きの既知の制約） |
| 偽の編集（改行の違い、取得していない LFS の pointer）と、base より古い checkout（他人の新しい変更が編集として出て、上書きが古い版に戻す）。どれも編集として計画に出るので黙らないが、上書きが要らずに大きくなる | S4（base の選び方）、host の VCS の設定 |
| 記録に無い root（手元で足した root）の依存だけが手元に無い場合（記録が無いので、手元の閉包が欠けを含まなければ気づけない） | 後続（未測定 (iii) の結果を見て S3-4b の A1 が決める） |
| 逆引きを経由しない `WithSelectedSubset` の plan を上書きでない build に渡す口を塞ぐか | BuildSystem の後続（publish は今も拒む） |
| 閉包の記録が manifest の上限を超える規模での置き場（別の file）、実データでの大きさ | S4（§2.4 P3） |
| 検出の所要を減らす工夫、古い上書きの directory の掃除、再利用の記録を複数持つこと（今は contentSet ごとに最後の 1 件） | S5 か BuildSystem の後続 |
| 上書きに依存として入った同じ id の物を id で読む時の版、別の PC の base | S4（program §8） |

### 2.4 代替案の比較

**編集の出所（P1）**

| 案 | 内容 | 判断 |
|---|---|---|
| P1-a | base の記録（`sourceFiles` の hash と、閉包の記録の GUID）と手元の file を比べる | **採用。** 利用者の入力が無く、VCS に依らない。GUID で比べるので移動が「追加」と「欠け」に割れない |
| P1-b | VCS の状態（未 commit の変更） | 不採用。VCS の実装を Framework に持ち込む（program I7）。commit 済みで base より新しい変更が漏れる |
| P1-c | 利用者が編集した path を入れる | 不採用。目的に反する。入れ忘れが黙った古い版になる |
| P1-d | AssetDatabase の変更の通知を Editor の起動中に貯める | 不採用。Editor を閉じている間の変更と、VCS の更新が漏れる |
| P1-e | `ContentSourceAdvisor` に比較の口を足す（初稿） | 不採用。GUID と手元の閉包が要るので Editor の計画の中に置く。Runtime の変更を DTO だけにできる |

**部分 checkout での plan の作り方（P2）**

| 案 | 内容 | 判断 |
|---|---|---|
| P2-a | source の欠けた payload も、閉包を持たない候補として snapshot に残す（欠けの記録付き）。選択は今のまま全ての候補で走り、計画が欠けた root を上書きから外す | **採用**（A2 の代替構成）。候補の stable key は path を使わないので file が無くても決まる。base が選ばない表現が欠けても選択が除外するだけで済み、閉包の記録が無い base でも plan が出る。projection は selected の root にだけ閉包を求めるので、欠けた root が build に渡る道が無い |
| P2-b | source の欠けた root に base の閉包を代わりに当てる（初稿） | 不採用。base の閉包は選ばれた root だけを持つので、base が選ばない表現や到達しない node が 1 つ欠けると materialize が失敗する（A2 の blocker）。記録の無い base で「決められない」を返す経路が無い。代わりの閉包を build の root と取り違える危険がある |
| P2-c | 上書きの root を base の索引からだけ決め、手元の選択を使わない | 不採用。手元で足した root と選択の変更が見えない。全 checkout の経路（今の逆引き）と 2 本になる |
| P2-d | 全ての source がある checkout だけを対象にする | 不採用。上書きの方式を採る制約（担当外の source を手元に持たなくてよい。program §5.3）に反する |
| P2-e | 手元に無い root を候補から落とし、選択の規則を緩める | 不採用。Selection の検査（到達した node の候補の存在）を上書きの都合で弱める |

**閉包の記録の置き場と形（P3）**

| 案 | 内容 | 判断 |
|---|---|---|
| P3-a | `transport.json` の任意の欄 `sourceClosure`（`index` の兄弟）。root の表（logical key・kind・表現・root の GUID。`index` の entry と同じ集合）、asset の表（GUID・path・所属の組の番号）、所属の組の表（その組の asset を閉包に持つ root の番号の集合を 1 回だけ）。版 1。hash は持たず、path で `sourceFiles` と突き合わせる | **採用。** manifest の digest に覆われ、Local と LAN が同じ bytes を見る。install と再検証は知らない欄を見ないので古い runtime も受け付け、`index` の版を上げずに済む。所属の組で持つと、大きさが「閉包の大きさの和」ではなく「asset の数 + 異なる所属の組の大きさの和」になる（広く共有された asset は 1 組）。上限は manifest の 4 MiB で、超える時は版 0（記録なし）で publish して警告する。JsonUtility は欄を省けないので、記録なしは版 0 の空の欄である |
| P3-b | `index` の版を 2 に上げて entry に閉包を持たせる | 不採用。版 2 は今の読み口が「未対応」として飛ばすので、目的 build の選択まで読めなくなる |
| P3-c | publish した directory の別の file | 送る（S4）。installer・検証・cache の配置を変えるので、上限を超える実データが出てから決める |
| P3-d | root ごとの番号の列（初稿） | 不採用。大きさが閉包の大きさの和に比例し、上限に早く当たる |
| P3-e | 記録の DTO を Runtime に置かず、publisher が JSON を継ぎ足す・Editor に manifest の写しを持つ | 不採用。継ぎ足しは壊れやすく、写しは同じ欄の一覧が 2 か所になる |

大きさは導入先の規模で決まり、SampleGame の数値は判断に使わない。読み口の値の規則（AC1）は Editor の読み口だけが持ち、install と再検証には型の検査を足さない（足すと、版を上げた時に古い runtime が install できなくなる。`index` の前例）。

**作り直せない root の扱い（P4）**

| 案 | 内容 | 判断 |
|---|---|---|
| P4-a | 計画に分類として出し、その root の集合そのものを認めた計画だけが、build の request と前回の上書きを得られる | **採用**（A2 の代替構成）。黙って混ぜる道が既定で閉じ、続けるか止めるかは入口（S3-4b）が決められる。集合が変われば認め直しになる |
| P4-b | 真偽の引数で認める（初稿） | 不採用。前の計画の時の判断のまま真を渡せる |
| P4-c | 作り直せない root があれば常に計画を失敗にする | 不採用。共有 asset の編集のたびに上書きが作れず、方式の見直しになる（program §8 の S3-4 の行） |
| P4-d | 警告だけ出して build する | 不採用。入口が警告を落とすと黙って混ざる |

**再利用（P5）**

| 案 | 内容 | 判断 |
|---|---|---|
| P5-a | 上書きの入力の鍵（SHA-256）を outcome に書き、contentSet ごとに最後の成功 1 件を指す記録を成果物の root に置く。計画の鍵が同じなら前回の結果を返す。鍵は編集の一覧ではなく、上書きの閉包の内容から作る。認めた集合は鍵に入れない（上書きの中身を変えない。認めは受け取りの口が毎回求める）。記録は成功した成果物の file の相対 path と SHA-256 も持ち、返す前に全て確かめる（§2.8 U17） | **採用。** 編集の無い再 Play と同じ編集の再 Play が build 0 回になる。同じ path をもう一度編集すれば鍵が変わる |
| P5-b | 毎回 build し、Unity の差分 build の速さに任せる | 不採用。見直しの条件 (2)「build 0 回」を満たさない |
| P5-c | 入口が memory に前回の結果を持つ | 不採用。domain reload と Editor の再起動で消える |
| P5-d | 記録を置かず、`reports/*/outcome.json` を新しい順に走査する（A2 の代替構成） | 不採用。report は build のたびに増え、掃除の口が無いので、走査が増え続ける。記録の file は ignore された成果物で、path の予算の対象外 |

**base の読み方（P6）**

| 案 | 内容 | 判断 |
|---|---|---|
| P6-a | 計画は public の `ContentInstallResult` を受け、`RevisionRoot` の `transport.json` の bytes を 1 回読み、SHA-256 を install の digest と比べ、contentSet・revision・target を照合してから、その bytes だけを使う | **採用**（A2 の代替構成）。Runtime の internal（`InstalledRevisionVerifier`）を Editor の Content から呼べないので（`Runtime/AssemblyInfo.cs` の対象に無い）、Runtime を変えずに済む。照合に落ちた時は計画が例外になり、分類を返さない（AC4 の (k)。今の `ContentPurposeIndex.Select` と `ContentSourceAdvisor.InspectInstalled` も、照合に落ちた manifest では止まる）。読んだ後の差し替えは、登録（S3-2）の照合が拒む |
| P6-b | Distribution に、検証と lease の下で manifest を返す public の口を足す（初稿） | 不採用。Runtime の public が増える。計画は manifest の bytes と digest だけで足りる |

### 2.5 スライス分割の判断

**S3-4 を 2 つに分ける。** 契機は「高リスクの初回実機スライスで独立して失敗しうる未知が複数ある」と「独立した変更理由が混在する」である。program の S3-4 の行は、上書きを作る側（編集の出所、逆引き、D の検出、閉包の記録、build の回数）と、Play に載せる側（入口の経路と順序、起動の key と配線、Domain Reload、record）を 1 行に持つ。前者は BuildSystem の Editor の code と manifest の欄、後者は SampleGame の入口と Runtime の Bootstrap で、変わる理由も失敗の仕方も別である。行自身が「入口の配線を積み上げる前に」D の検出と部分 checkout の体験を確かめることを求めている。このうち本書が確かめるのは D の検出と分類までで、作り直せない root の頻度と体験の評価は S3-4b の最初の作業に残る（§2.3、§2.8 U14）。

- **S3-4a（本書）:** 上書きを決めて作る側。Runtime の登録・解決と入口は変えない。縦断（変更 → 計画 → build → 登録 → Play → 再計画）は、S3-2 の登録の口を直接呼ぶ実機の test で通す。
- **S3-4b:** Play の入口と起動の配線。問い・gate・入力は program §8 の行に書いた。S3-4a の Phase D の後に A1 を始める。

A1 の発見（部分 checkout では逆引きの入力が作れない。§1 の現況）は、分割の理由を強める。program §5.3 の D は検出の材料（base の閉包）だけを未詰めとしていたが、その手前の materialize が部分 checkout を通さない。

分割しない案（1 スライスで入口まで）は、入口の順序と起動の配線を、作る側の契約（分類の形、認める口、再利用）が動く間に凍結することになるので採らない。

**S3-4a をさらに「閉包の記録の publish」と「計画と build」に分けるか（A2 の代替構成の提案）:** 分けない（担当の決定。§2.8 U8）。分ける利点は、記録の publish を先に入れれば、以後に CI が作る base が記録を持つことである。分けない理由は、記録の形を決めるのは読む側（計画）で、読む側の実機（AC6）を通す前に publish の形を凍結して出荷すると、形を直す時に版が増えること、Phase A〜D を 2 回回す費用に対して前半が答える問いが小さいことである。1 スライスのまま、実装の順を記録 → 計画にし（§4）、記録の無い base の扱い（AC4 の (f)(g)）を必須にする。§3 の増分は製品 code 約 1,100 行で、B が実装の途中で膨らむと判断した時は、この境界で Phase A へ戻す（§4）。

### 2.6 判定定義と停止規則

- GO: AC1〜AC8 が全て合格。
- NO-GO（owner に返す）: (i) root の Scene の file が手元に無い project で、Unity の build が選んだ root を build できない（未測定 (i)。AC6 の R4 と R8）。(ii) 作り直せない root の直接参照が base の build の時の版で載らない（未測定 (ii)。AC6 の R4）。(iii) 実装に `Runtime/` の DTO の欄より外の変更、Selection の変更、asmdef の参照か `InternalsVisibleTo` の追加が要ると分かった（program §5.3 の見直しの条件 (3)）。(iv) 欠けを許す materialize の snapshot を今の選択と projection が受け付けない。
- CONDITIONAL ACCEPT は使わない（スパイクではない）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。本書は `unity/Assets/SampleGame/` の製品 code を変えない。asmdef の参照と `InternalsVisibleTo` を足さない。
- 新規・編集する `.cs` は先頭に `#nullable enable`。Unity 側で `record` を使わない。
- `UnityEditor` に依存する code は Editor 用 asmdef に置く。`Runtime/` に足すのは manifest の DTO の欄と serializable の型だけで、logic を足さない。
- 公開 API に `ZLogger*` を出さない。
- test で `Task.Delay` / `Thread.Sleep` を使わない。
- tracked の file の path は `tools/path-budget.json` の予算に従う。
- 参照 0 を削除理由にしない。本書が足す口は S3-4b が呼ぶ先行の宣言である。今の逆引きの public の口（`ContentOverlaySelection.Select` と、その結果から作る上書きの request）は残す。
- 機械向けのデータ（期待値の一覧、baseline）を git に入れない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- Unity の版は `unity/ProjectSettings/ProjectVersion.txt`（6000.6.9f1）。
- 上書きのために S3-2 が足した public の値（`ContentSourceKind.ContentOverlay` など。program §5.3）は S3-4 の縦断の実証まで暫定で、本書は足しも外しもしない。

### 2.8 owner の裁定が要る点と、担当が決めた点

**owner が凍結で決めた点。** 目的・利用者の文言と、program が owner の gate として書いた条件の読みである。owner は 2026-11-21 の凍結で、どちらも担当の案のとおりに決めた（§0 の凍結記録）。未決事項は無い。

| ID | 点 | 担当の案 |
|---|---|---|
| U1 | §1 の目的と利用者の文言、S3-4 を S3-4a・S3-4b に分けること（§2.5）。目的レビューは文言の曖昧さを 11 件挙げた（§5.1）。答えは本文にあるが、目的の文に入れるかは owner の裁定 | 本書の文言と分割で進める。文言を改める時は、凍結の前に目的レビューをやり直す |
| U2 | program §5.3 の見直しの条件 (3)「core の public の値か 2 層のための Runtime の特例をさらに 1 つでも要する時」の読み。本書が `Runtime/` に足すのは、manifest の任意の欄 1 つ（`sourceClosure`）と serializable の型 4 つだけで、logic は足さない | (3) が指すのは `Runtime/ContentResolution/Core` の値と session・gate の層の特例で、本書の追加は当たらないと読む。program §5.3 は閉包の情報を publish に足す形を案として既に書いている。当たると裁定された時は §2.4 P3-c（別の file）へ送り、本書は止める |

**担当が決めた点。** 裁定は求めない。外部計画レビューか owner が覆す時だけ改める。

| ID | 点 | 決定と理由 |
|---|---|---|
| U4 | 閉包の記録が manifest の上限を超えた時 | 記録なし（版 0）で publish して警告する（出し先は U13）。publish を失敗にしない（版 0 でも上限を超える manifest は今までどおり失敗）。その base では部分 checkout の計画が、欠けた selected の root を全て作り直せない root にする（毎回認めが要る）。全ての source がある checkout は今までどおり動く |
| U5 | 未測定 (i)〜(iv) を凍結の前に実 Editor で確かめるか | 確かめない。Phase B の最初の作業にし（§4 の (1)）、不成立なら NO-GO で返す。Phase A では Unity を動かさない。§4 の (1) は B が対象を限定した Editor 操作で観測し、build を伴う test の実行と判定は Phase C が行う（§0 の引継ぎ票） |
| U6 | 編集の検出と鍵の計算の所要 | base の `sourceFiles` の file と手元の閉包の file の SHA-256 を毎回計算する。正しさを先に取り、速くする工夫は入れない。所要の表示は S3-4b |
| U7 | 削除の扱い | 編集に数えない（未 checkout と区別できない）。**記録が「あり」の時:** asset を消すと、その asset を閉包に持っていた root は source の欠けた root になり、他の編集が当たれば作り直せない root になる。消しただけなら何も出ず、その root は base から載る。**記録が無い・未対応・壊れている時:** 欠けが手元から見えた root（root の file が無い。または Unity が返した依存の path の file が無い）は、消したのか未 checkout かに関わらず、AC4 の (f) で作り直せない root になる。Unity が消した依存の path を返さなければ欠けは見えず、何も出ない。どちらになるかは未測定 (iii) で決まり、結果を §6 に残す。区別は VCS の adapter（S5）へ送る。全ての source がある checkout で日常的に asset を消す開発者には不便が残る |
| U8 | S3-4a をさらに「閉包の記録の publish」と「計画と build」に分けるか（§2.5） | 分けない |
| U9 | A2 の後に設計を改めた（部分 checkout の plan の作り方 P2、記録の形 P3、認める口 P4、base の読み方 P6）。A2 の再レビューを行うか | 行わない。Draft PR の外部計画レビューで見てもらう（§5.1） |
| U10 | build の前後で閉包の file の hash が違う build の扱い（外部計画レビュー） | 上書きでない build も失敗にする。hash を build の前へ移すだけでは、hash の後から Unity の build までの変更が base の記録と中身を食い違わせ、手元が「編集なし」になる（I-1 (11)）。I-6 の例外が 1 つ増え、build ごとに閉包の hash を 2 回計算する（U6 と同じく正しさを先に取る）。owner が覆す時は、「目的 build の間は source を変えない」を publish する者の前提として書く形に戻す |
| U11 | S-2 を、担当外の source を一度も import していない checkout で確かめるか（外部計画レビュー） | 確かめる（AC6 の R8、判定必須）。S-2 を「import 済みの `Library` で file を退かした状態」に限る書き方にはしない。担当外の source を持たなくてよいことは上書きの方式を採る制約（program §5.3）で、その成立を入口の配線の後まで送らない。R8 の経路が作れない時は Phase A へ戻し、S-2 を限って clean な checkout を S3-4b の入口の配線の前の必須の gate にするかを owner が決める（§4） |
| U12 | root の file はあるが依存の欠けた Scene に「他の供給元」の key が渡された時の分類（外部計画レビュー） | 他の供給元に入れる。開いている Scene は依存が欠けていても snapshot が手元の版で届けるので（S3-3 の O-1）、作り直せない root にすると、「base の entry から載る」という記録が実際（snapshot の複製から載る）と食い違う。root の file が無い root は、snapshot が届けられないので作り直せない root のままにする（I-2 (5)） |
| U13 | 閉包の記録を版 0 にした時の警告の出し先（外部計画レビュー） | `Debug.LogWarning` と、publish した manifest の `sourceClosure` の版 0。`ContentTransportPublisher.Publish` の public の形は変えない。CI が機械で確かめる時は manifest の版を読む |
| U14 | program の S3-4 の行の gate「fail-closed せざるを得ない編集がどれくらい生じ、編集 → Play の体験が成り立つか」を、分割の後にどこで評価するか（外部計画レビュー） | S3-4b の最初の作業で、入口の配線の前に評価する。owner の条件（2026-11-19）は「成り立たなければ入口の配線を積み上げる前に方式を見直す」で、これを満たす。本書の製品 code と manifest の欄は、その評価の前に入る。本書は頻度を測らない（宣言からの host 関数が要り、SampleGame の製品 code を変えない対象外と衝突する）。評価が方式の見直しになった時、`sourceClosure` は任意の欄のまま残してよい（読むのは Editor の計画だけで、Runtime の検証と install は見ない）。外す時と形を変える時だけ版を上げる。本書が 13 章・18 章へ harvest した文と、計画・認める口・再利用の code を戻すかは、見直しを行うスライスが決める（S3-4b の最初の作業の結果を受ける） |
| U15 | 上書きに入れる root が無く、作り直せない root だけがある計画の状態（外部計画レビュー） | 「上書き不要」と分けて、3 つ目の状態にする。認めの門（request と再利用）を通らない計画が、編集の無い計画と同じ状態で返ると、状態だけを見る入口が黙って base だけで進める。その状態で止めるか続けるかは S3-4b（U3）。当たらなかった編集は状態を変えない（指摘は「編集なし」の条件に入れる案だったが、どの root の閉包にも無い編集は Play の中身を変えない）。他の供給元は、この状態でも計画に残る。同じ編集が開いている Scene と source の欠けた root の両方に当たると、上書きは空でこの状態になり、他の供給元を落とすと snapshot が届ける Scene が分類から消える（AC4 の (i)(n)） |
| U16 | hash の時点で閉包の file が無い、上書きでない build（Phase C で突かれる点の見直し） | 失敗にせず、今までどおり成功しうる。既存の test（`BuildContentFullModeTests`、`ContentPurposeIndexPublishTests`）は、disk に無い path と GUID の形でない値の閉包を build・publish している。この build の publish は閉包の記録を版 0 にする。全ての build で hash が取れることを要求すると I-6 を破る |
| U17 | 再利用する前回の成果物の中身を確かめるか（外部計画レビュー） | 確かめる。build が成功した時に `content/<identity>/` の全ての file の相対 path と SHA-256 を再利用の記録に持ち、返す前に全て比べる。鍵は入力しか見ないので、成果物の file が 1 つ欠けても同じ鍵で返り続け、Play の登録や読み込みが失敗しても、編集を変えるまで同じ成果物が返る。登録（S3-2）が見るのは上書きの directory の存在と base の identity で、中の file ではない。記録に無い file が増えただけでは拒まない（Unity が Play の間にこの directory へ file を書くかは確かめていない。拒むと、S-3 の「同じ内容の 2 回目は build 0 回」が、その未確認の挙動に依る）。再利用のたびに成果物を hash する（鍵が上書きの閉包の source を計画のたびに hash するのと同じ形）。owner が覆す時は、成果物の root を「利用者が書き換えない場所」という前提として書き、I-5 (2) から「壊れた」を外す |

**S3-4b へ送った点（本書では決めない）。**

| ID | 点 | S3-4b への入力 |
|---|---|---|
| U3 | 作り直せない root がある時の Play（通知して続けるか、止めるか） | 担当の見立ては「止めずに Play に入り、毎回 Console と record に出す。黙らせる設定は作らない」（止めると共有 asset の編集のたびに Play が止まり、program §8 の行が方式の見直しの条件にしている。開いている Scene は snapshot が手元の版で出す）。本書は、どちらの規則でも使える口（集合を認めた計画だけが上書きを得る）と、作り直せない root だけの計画を別の状態で返すこと（U15）までを作る |

## 3. 責務マップ

path は §1 の現況と同じ相対。行数は base `63f171ac`。

| file | 責務と変更 | 所有者・寿命 | 依存・公開面 | test の境界 | 行数 → 増分 |
|---|---|---|---|---|---|
| `Runtime/BuildContent/Distribution/ContentTransportManifest.cs` | manifest の DTO に任意の欄 `sourceClosure` と、その serializable の型（記録、root、asset、所属の組）を足す。logic は足さない | publish が書き、manifest の digest が覆う | public の field 1 つと型 4 つ。必須欄・検証・install は変えない | AC1、AC7 | 174 → +40 |
| `Editor/Build/Materialization/AssetDependencySnapshotBuilder.cs`（internal） | 今の `Build` は変えない。結果を「閉包」「欠け（root が解決しない・root の file が無い・依存の file が無い）」「構造の失敗」に分けて返す関数を足す | builder と同じ | internal のまま | AC3 | 97 → +40 |
| `Editor/Build/Materialization/BuildMaterializationAbsence.cs`（新） | 欠けた候補 1 件の記録（stable key、logical key、root の GUID、理由、足りない path） | 値 | public | AC3 | 新 約 45 |
| `Editor/Build/Materialization/BuildMaterializationSnapshot.cs` | 欠けの記録の一覧（今の口では常に空）を持つ | 値 | public の property を足す。constructor は internal のまま | AC3 | 82 → +20 |
| `Editor/Build/Materialization/SceneResourceContentMaterializer.cs` | 欠けを許す口 `MaterializeAllowingAbsentSources` を足す。欠けた payload の候補を、閉包を持たずに残す（provenance に欠けの理由）。今の `Materialize` は変えない | 今と同じ | public の method を足す | AC3 | 257 → +50 |
| `Editor/Build/Content/ContentSourceClosureRecord.cs`（新） | 閉包の記録の値の規則を 1 か所で持つ。build の閉包から記録を作る関数（publisher が使う）と、manifest の bytes から読む読み口（「あり」「記録なし」「未対応」「壊れている」と理由） | 値 | internal。Runtime の DTO だけを使う | AC1 | 新 約 190 |
| `Editor/Build/Content/ContentOverlayLocalSource.cs`（新） | 計画が手元を読む port（GUID → path、file の有無、SHA-256）と、`AssetDatabase` と `File` の実物。test は fake に替える | 計画の間 | internal | AC2、AC4 | 新 約 70 |
| `Editor/Build/Content/ContentOverlayPlan.cs`（新） | 計画の入力と結果の値。状態（上書き不要・build が要る・作り直せない root だけ）、編集（path・種類）、上書きの root と理由、他の供給元、作り直せない root（key・理由・編集・足りない file）、当たらなかった編集、共有 asset、base の identity、鍵。集合を認める口 `Acknowledge` | 値（不変） | public | AC4、AC5 | 新 約 190 |
| `Editor/Build/Content/ContentOverlayPlanner.cs`（新） | 計画の入口 `Plan`。base の manifest の bytes を読んで照合し（§2.4 P6。落ちれば例外）、編集を検出し、手元の plan と snapshot（欠けの記録付き）と閉包の記録から分類し、鍵を計算して再利用の記録を照合する。build を起こさず file を書かない | 1 回の計画 | public static。host は「欠けを許す materialize → 選択 → 合成 → `Plan`」の順に呼ぶ | AC2、AC4、AC6 | 新 約 260 |
| `Editor/Build/Content/ContentOverlayReuse.cs`（新） | 鍵の計算と、contentSet ごとの最後の成功の記録（成果物の root の下の `overlay/<contentSet>.json`）の読み書き。記録は鍵と、成果物の file の相対 path と SHA-256 を持つ。返す前に、鍵・成果物の全ての file の hash・outcome の成功を確かめる | 記録の file | internal。受け取りは計画の口から | AC5 | 新 約 170 |
| `Editor/Build/Content/ContentOverlaySelection.cs` | 逆引きの中身を、対象の root の範囲を引数に取る internal の関数に出し、計画が使う。public の `Select` は結果を変えない | 値 | public は変えない | AC4、既存の `ContentOverlaySelectionTests` | 238 → +25 |
| `Editor/Build/Content/BuildContentReport.cs` | 計画から上書きの request を作る constructor（認めていない計画と、build が要らない計画（上書き不要・作り直せない root だけ）を拒む）。preflight に root ごとの閉包、file と `.meta` の build の前の SHA-256、`overlayUnbuildable`、`overlaySupplied`、鍵。outcome に鍵。`BuildContentResult` に鍵の property | 今と同じ | public の constructor と property を足す。今の constructor は変えない | AC5、AC7 | 279 → +80 |
| `Editor/Build/Content/BuildContentCoordinator.cs` | preflight の前に閉包の file と `.meta` を hash し、Unity の後に hash し直して、違えば失敗にする（全ての build。成果物の directory を残さない。hash の時点で無い file は「無い」と記録し、前後とも「無い」なら等しい）。計画から作った上書きの build は、加えて Unity の前に閉包の file の存在を確かめ、Unity の後の hash を計画の時の値とも比べる。成功した上書きの後に、`content/<identity>/` の file を hash して再利用の記録を書く | 今と同じ | public は変えない。issue code を足す | AC5、AC7 | 221 → +70 |
| `Editor/Build/Content/BuildContentProjection.cs`（issue code の定義） | issue code に「build の間に source が変わった」「閉包の file が手元に無い」を足す。照合の規則は変えない | 今と同じ | public の enum に値を足す | AC5 | 168 → +6 |
| `Editor/Build/Content/ContentTransportPublisher.cs` | preflight の root ごとの閉包と build の前の hash から、`sourceFiles` と閉包の記録を書く。書いた bytes を読み口で読み直す。上限を超える時は記録を版 0 にして `Debug.LogWarning` を出す（上限の超過は今の読み直し `ContentPurposeIndex.Read` の `TooLarge` で分かる。版 0 でも超えれば今までどおり失敗）。hash の記録の無い古い preflight、hash が 1 つでも取れなかった build、索引が版 0 の publish は、今の書き方で記録を版 0 にする | 今と同じ | 変えない | AC7 | 186 → +55 |
| `OneStarMaker/Tests/Editor/Build/`（test） | AC1〜AC5・AC7 の単体（新しい test class）と、既存の `ContentOverlaySelectionTests`・`SceneResourceContentMaterializerTests`・`ContentPurposeIndexPublishTests`・`BuildContentReportTests` の非退行 | — | 既存の `OneStarMaker.Tests.Editor` の参照と `InternalsVisibleTo` の範囲 | — | 新 約 900 |
| `SampleGame/Tests/Editor/Build/OverlayFromLocalEdits*.cs`（新、test。fixture と R8 を別の file に分けてよい） | AC6・AC7 の実機。合成の `SceneResourceMap` を materializer に通す fixture（GUID は固定。R8 は clean な checkout で別に走る） | — | 既存の `SampleGame.Tests.Editor` の参照の範囲 | — | 新 約 550 |

- 公開 API の増分（まとめ）: Runtime は manifest の field 1 つと serializable の型 4 つ。Materialization は `MaterializeAllowingAbsentSources`、`BuildMaterializationAbsence`、`BuildMaterializationSnapshot.Absences`。Content は `ContentOverlayPlanner.Plan`、計画の入力と結果の型（`ContentOverlayPlan` と、状態・編集の種類・作り直せない理由の enum）、`BuildContentRequest` の constructor 1 つ、`BuildContentResult` の property 1 つ、`BuildContentIssueCode` の値 2 つ。今の public の型の意味は変えない（`ContentOverlayUnmatchedReason` に値を足さない。作り直せない root と他の供給元の root にだけ当たった編集は、それぞれの分類の理由に出て、「当たらなかった編集」には入らない）。
- 配置の理由: 欠けを許す materialize は閉包と候補を作る所（Materialization）に置く。記録の値の規則・編集の検出・分類・鍵は、manifest と手元の閉包の両方を読む Editor の build の関心なので Content に置く。Selection は変えない。Runtime は DTO の欄だけにする（install と再検証はこの欄を見ない）。
- 依存の向き: `Editor/Build/Content` → `Editor/Build/Materialization`・`Editor/Build/Selection`・`Runtime`（今の asmdef の参照のまま）。Content は Materialization の internal を見られないので（`InternalsVisibleTo` の対象に無い）、手元を読む port は Content が自分で持つ。
- 行数の警報（500 行・3 責務・50% 以上の増加）: 50% を超える file は無い。500 行を超える file は無い。計画の入口（`ContentOverlayPlanner.cs`）は「base を読む・検出・分類・鍵と再利用の照合」を順に呼ぶ 1 つの関心で、検出と分類の中身が 300 行を超える時は B が検出を別 file に分ける。記録の作成と読みは値の規則を共有するので 1 file に置く。

## 4. 実装計画

- 順序: (1) AC6 の fixture を作り、未測定 (i)〜(iv) を最小の形で先に確かめる（`B` の Scene を `.meta` と一緒に退かした project で `A` だけを build する、依存 `M` だけを退かした時に `GetDependencies` が何を返すか、退かして戻した file の GUID、登録した Play での `B` の `T` の色）。あわせて R8 の経路を先に確かめる（新しい worktree の clean な `Library` で、`B` を一度も置かずに `A` だけを build できるか。同じ GUID の fixture を 2 つの checkout で作れるか。base を 2 つの checkout の間で渡せるか）。結果を §6 に書き、不成立なら §2.6 の NO-GO で止める。(2) manifest の欄、build の前の hash、記録の作成と読み口、publish（AC1、AC7）。(3) 欠けを許す materialize（AC3）。(4) 検出と分類（AC2、AC4）。(5) 認める口、build の口、再利用（AC5）。(6) AC6 の全ての回。
- Phase B から Phase A へ差し戻す条件: §2.6 の NO-GO (i)〜(iv)。§3 に無い file の変更が要る。未測定 (iii) の結果、記録の閉包の asset の解決だけでは依存の欠けを見分けられない。R8 の経路（clean な checkout、同じ GUID の fixture、base の受け渡し）が作れない（§2.8 U11）。製品 code の増分が §3 の見込みを大きく超え、§2.5 の境界で分ける方がよいと B が判断した。
- 対象外を維持する方法: AC8 の `git diff --stat`。SampleGame の製品 code、Selection、`Runtime/` の DTO の外に差分を作らない。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC5 と AC7 の単体の部分（Unity の build を伴わない EditMode）。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: AC1〜AC5 の新しい test class、`ContentOverlaySelectionTests`、`SceneResourceContentMaterializerTests`、`ContentPurposeIndexPublishTests`、`ContentPurposeIndexTests`、`ContentManifestValidationTests`、`BuildContentReportTests`、`BuildContentProjectionTests`、`BuildContentFullModeTests`。
- 判定必須テスト（GO 候補 head）: 上の起点 filter、AC6・AC7 の実機の test（R8 は clean な checkout で別に走らせる）、`OverlayRegistrationIntegrationTests`、`PurposeBuildIndexIntegrationTests`、最終の全 EditMode 回帰（空 filter。合否は「XML の failed 0 と root result `Passed`」で読む。`-nographics` の VFX 25 件の Inconclusive で `step.json` が `failed` になるため）。
- 全 EditMode 回帰の適用除外: なし。Player の build は判定必須にしない（Player の構成に入る変更は manifest の DTO の任意の欄だけで、install と再検証はその欄を見ない。代替証拠は AC1 の「新しい欄を持つ manifest が今の検証を通る」と AC8 の `Runtime/` の差分）。
- 統合・Unity テスト: AC6 の R0〜R7 は Content Directory の build を 5 回伴う（base 1、上書き 4）。R8 は clean な checkout（新しい worktree。初回の import を伴う）で上書きの build を 1 回伴う。他の Unity が同時に動くと build を伴う test が失敗した既知の事例がある（program §8）ので、判定 C は他の Unity を止めて走らせる。build を伴う batch の後の Unity の終了 stall は既知（`tools/Harness/README.md`）で、XML の完成で判定する。coordinator の staging の `Directory.Move` が access denied で失敗する既知の不安定（program §8）に当たった時は、同じ head で走らせ直してよく、その事実を §7 に記録する。
- 操作・実行時・目視条件の検証経路: 該当なし。全ての受け入れ条件は EditMode の test（batch）と機械検査で閉じる。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: **未確認。初回確認は Phase B の最初の作業（§4 の (1)）と Phase C:** EditMode の `[UnityTest]` の中で、fixture の file を `.meta` と一緒に `Assets` の外へ退かして `AssetDatabase.Refresh` し、計画と build を行い、戻すこと（既存の実機の test は file を消さない）と、未測定 (i)〜(iv)。理由は、Unity の build を伴う試行を Phase A で行わないため（§2.8 U5）。R0〜R7 は `B` を一度 import した `Library` で測るので、一度も import していない project の答えは R8 が clean な checkout で取る。R8 の経路（新しい worktree の初回の import、同じ GUID の fixture、2 つの checkout の間の base の受け渡し）も未確認で、初回確認は同じく Phase B の最初の作業と Phase C。不成立なら §2.6 の NO-GO で owner に返し、R8 の経路が作れない時は Phase A へ戻す（§4）。build → publish → install → Play mode の登録と読みは `OverlayRegistrationIntegrationTests` と `RegisteredDirectoryRouteIntegrationTests` で成立した経路である。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし（R8 は build を伴う EditMode の test を別の checkout で走らせるだけで、不可逆な操作も Player の build も無い）。
- 判定 C の照合 script の方式: なし（test の XML を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC8 の `git diff --stat`。
- A0/A1 主担当・モデル・ベンダー: AI（Claude Code、Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: Codex CLI（OpenAI 系）。A0〜A2 と統合では使っていない（A2 は Anthropic 系だけ）。動かない時だけ cursor-agent の Grok 系。外部計画レビューの 1 巡目は 2 件で、2 件目は Claude Code（Anthropic 系。主担当と同じ系列）である。1 件目の系列は本書の担当からは確かめられなかった。2 巡目は Cursor の Grok（xAI 系）で、Grok 系も Phase A に関与した。C' は起動の時点で、Phase A〜C に関与していない系列から進行役が選ぶ。1 件目が OpenAI 系で、未関与の系列が残っていなければ、Grok 系で `独立性制約あり` と記録する。
- 独立性の強化条件を満たせない場合の理由: A3 で記録する。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-21、同一 PC）。入力版は固定 base `63f171ac` の clean な detached checkout と、HANDOFF copy（sha256 `cfd07455c30841b37b1056a4e14d8367f6c9e39e92495785468be7e151e83cc0`。A1 初稿）。4 件とも Claude Code の subagent（Anthropic 系。主担当と同じ系列で、主担当とは別のモデル）。互いの指摘は渡していない。
  - アーキテクチャゲート: HEAD・clean・hash の照合は一致。結論は blocker 1、major 5、minor 10。配置の向き、asmdef の参照を足さないこと、Selection を変えないこと、manifest の任意の欄を古い runtime が受け付けること、`index` の版を上げないことを code で裏付けた。§1 と §3 の行数と行番号は 1 件（fixture の所在）を除いて全て正しかった。
  - 不変条件の反証と AC の読み違い: 照合は一致。結論は blocker 0、major 10、minor 7。I-4・I-6 は破れず、I-1（移動して編集、base にあって手元の選択に無い root、表現が複数ある root）、I-2（認めない再利用、「他の供給元」による洗浄）、I-5（計画と build の間の変更の持続、publish の時点の hash）は破れた。
  - 目的レビュー: §1 の目的と利用者の本文だけを渡した別の担当（道具を使わない）。入力の sha256 `b156c4ab454d3f9f8053366e78b8a7813e2c08a7ef02d80c7a6cfc3b62e1a145`。
  - A0 だけからの代替構成（risk `high` の追加分）: 目的レビューとは別の担当に、A0 の packet（sha256 `5e6d50dc2b2e3589290c8405d24db39b605856db061213a1f6b7ef85335ba787`。目的、利用者、対象外、現況、問い、誤って YES を出す道、実装制約）と固定 base を渡した。初稿は読ませていない。目的レビューのやり直しに使える未関与の担当は残してある。
- A3 の分類（担当の分類。owner は凍結の時に、覆す行だけを指す。目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは今は無い）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | ゲート B1 | 部分 checkout の materialize は、base の閉包に無い payload（base が選ばない表現、到達しない node）が 1 つ欠けても失敗する。初稿の「代わりの閉包」は選ばれた root にしか無い | 採用 | 代替構成の「閉包を持たない候補」に改めた（§2.4 P2-a、AC3、AC6 の R4 の Whitebox、§3）。`SceneResourceMap` と `SceneResource` の asset が手元にあることは前提で、欠ければ今までどおり issue（AC3） |
  | ゲート M1、反証 F13 | 記録の無い base で「決められない」を返す経路が無い（snapshot が null になる） | 採用 | 欠けを許す materialize は記録に依らず snapshot を出し、計画が欠けた selected の root を全て作り直せない root にする（§2.1 の用語、AC4 の (f)(g)）。materialize の他の issue は読み替えない |
  | ゲート M2 | Editor の Content から installed の base を読む public の口が無く、§3 に無い Runtime の変更が要る | 採用 | 代替構成の読み方に改めた（§2.4 P6-a）。Runtime の変更は DTO の欄だけ |
  | ゲート M3、反証 F2 | path で突き合わせると、移動して編集した asset が source の欠けた root に当たらない | 採用 | 記録は GUID を持ち、編集と記録の閉包の突き合わせを GUID で行う（§2.1 の用語、AC2、AC4 の (d)、AC6 の R7） |
  | ゲート M4、反証 F1 | root はあるが依存が手元に無い root を、builder の issue で拾える前提は未確認（Unity が欠けた依存の path を返さない可能性） | 採用 | 記録の閉包の asset が手元で解決しない root を source の欠けた root にする（AC4 の (c)、AC6 の R6）。Unity の挙動は未測定 (iii) として Phase B の最初に測る。§1 の現況を確かめた事実と未測定に分けた |
  | ゲート M5、反証 F5 | 再利用の口が認める印を見ない | 採用 | 前回の上書きの受け取りも認めた計画だけが通る（I-2、AC5、AC6 の R5） |
  | 反証 F3、目的レビュー W1 | 削除は未 checkout と区別できず、黙って base の版が残る | 記録（既知の制約として受け入れる） | 編集に数えず source の欠けとして扱い、既知の制約に書いた（§2.1 の用語、§2.3、§2.8 U7） |
  | 反証 F4、代替構成 | base にあって手元の選択に無い root が分類されない | 採用 | `NotInLocalSelection`（§2.1、AC4 の (e)） |
  | 反証 F6、ゲート m9 | 分類の優先順位が無く、「他の供給元」に欠けた root の key を渡すと作り直せない root から外れる | 採用 | 優先順位を書いた（AC4 の (a)(b)、I-2 (5)） |
  | 反証 F7、代替構成 | 計画と build の間の変更が、再利用の記録で以降の回に残る | 採用 | build の後に hash し直し、違えば失敗にして記録しない（I-5、AC5、§3） |
  | 反証 F8、代替構成 | `sourceFiles` の hash が publish の時点で、build の後に変えた source が「base の版」として記録される | 採用 | hash を build の前に取り、publisher はその値を書く（I-1 (11)、S-5、AC7、§3）。I-6 の例外として書いた |
  | 反証 F9、ゲート m7 | Scene だけを退かすと `.meta` が消され GUID が変わる（推測） | 採用 | `.meta` と一緒に退かし、戻した後に GUID を確かめる（AC6、未測定 (iv)） |
  | 反証 F10、ゲート m2 | 表現が複数ある node で、記録の閉包を logical key で引くと取り違える | 採用 | 記録の root は index の鍵と root の GUID の両方を持つ（§2.4 P3-a、AC1） |
  | 反証 F11 | 閉包を作り直す合成が印を落とす | 解消 | 代わりの閉包をやめたので印が無い。欠けの記録を落とした合成は、閉包の無い selected の root で例外になる（AC4 の (j)）。Unity の前の file の存在の確認も足した（I-3、AC5） |
  | 反証 F12 | 「build 0 回」の読み方が、report の前の例外と projection の失敗で違う | 採用 | AC6 の読み方 |
  | 反証 F14、ゲート m8 | 鍵に path と、root に写る宣言の値が無い | 採用 | I-5 の列挙。requirements は preflight の記録専用で成果物を変えないので入れない |
  | 反証 F15、代替構成 | S-4 の純粋な場合の回が無い。「他の供給元」の単位が曖昧 | 採用 | AC6 の R3、AC4 の (b)（Scene の node の logical key。全ての表現、付随 asset は外さない） |
  | 反証 F16 | 再利用の記録の置き場を見る条件が無い。read lease の lock の場所 | 採用 | AC5、I-4 の「確かめた事実」 |
  | 反証 F17、代替構成 | LFS の pointer、base より古い checkout、改行の違い、一度も import していない project | 記録（変えない） | §2.3、§5 の未知の操作経路。どれも編集として計画に出る |
  | ゲート m1 | §1 の fixture の所在の誤り | 採用 | §1 の現況 |
  | ゲート m3 | JsonUtility は欄を省けないので「欄なし」は版 0 | 採用 | §2.4 P3-a、S-5、AC1、AC7、§2.8 U4 |
  | ゲート m4 | 公開 API の一覧の漏れ（outcome に鍵を運ぶ `BuildContentResult`、`Unmatched` の扱い） | 採用 | §3 の「公開 API の増分」 |
  | ゲート m5 | 行数の警報を gate の閾値で書いていない | 採用 | §3 の「行数の警報」 |
  | ゲート m6 | Distribution の test の置き場と起点 filter | 採用 | Runtime に logic を足さないので新しい test は `Tests/Editor/Build/` だけ。起点 filter に `ContentManifestValidationTests`・`ContentPurposeIndexTests` を足した |
  | ゲート m10 | 計画の呼び方の順が曖昧 | 採用 | §3 の `ContentOverlayPlanner.cs` の行（host は「欠けを許す materialize → 選択 → 合成 → `Plan`」） |
  | 代替構成 | 記録の形（所属の組）、読み口を Editor に置く、認めは集合そのもの、鍵は閉包の内容から、手元を読む port を Content が持つ | 採用 | §2.4 P3-a・P4-a・P5-a、§3 |
  | 代替構成 | 再利用は `reports/` の走査で探す | 不採用 | §2.4 P5-d |
  | 代替構成 | 記録に無く手元にも無い selected の root、他の供給元に渡された未知の key の診断 | 一部採用 | 前者は `NotInBase`（§2.1）。後者は入れない（入口が渡す key は入口の宣言で、診断は S3-4b が要る時に足す） |
  | 代替構成 | S3-4a を「記録」と「計画」に分ける | 不採用 | §2.5、§2.8 U8 |
  | 代替構成 | 計画の前の `AssetDatabase.Refresh` | 移送 | S3-4b（§2.3） |
  | 目的レビュー | 保証すべきこと 10 件 | 初稿にある・採用 | 入力なしの検出、漏れも過剰も無い分類、欠けていても作れる、揃う root だけ、build 0 回、認めない限り渡さない（部分的にも作らない）、計画と build を分ける、SampleGame に依らない、publish に入力を足さない、root と理由を戻り値で返す、は I-1〜I-5・S-1〜S-5 と §3。「計画は build を起こさない」は AC4 に足した |
  | 目的レビュー | 作らないべきもの 8 件 | 7 件は一致、1 件は不採用 | 入力の UI、入口と表示、自動で認める既定、全体の作り直し、source の取得、publish の操作、空の上書きは作らない。「閉包を手元で解析し直さず、正本を base の閉包にする」（回答が推測と書いた項目）は採らない: 手元で足した参照は base の閉包に無いので、揃う root は手元の閉包で決める |
  | 目的レビュー | 文言への指摘 11 件（欠けた file が削除か未 checkout か、追加した file、判定の基準、「揃う」の範囲、「前回と同じ内容」の対象、認める粒度と持続、認めた後の整合、選択の外の root、記録の無い base、理由の種類と示す形、build の失敗時） | 記録（2 欄は変えない。owner が凍結で決めた。§2.8 U1） | 目的・利用者の文言そのものへの指摘なので、担当は 2 欄を変えていない。答えは本文にある（削除は §2.8 U7、追加と基準と範囲は §2.1 の用語、再利用の対象は I-5、認めは集合単位で受け取りのたび（§2.4 P4-a）、認めた後は 13 章の「同じ Play に同じ asset の 2 つの版」、選択の外は `NotInLocalSelection`、記録の無い base は AC4 の (f)(g)、理由は §2.1 の列挙で計画の値として返す、build の失敗は今の coordinator と同じく成果物を残さない） |

- A2 の再レビューの要否: 行わない（担当の決定。§2.8 U9）。A2 の後、部分 checkout の plan の作り方、記録の形、認める口、base の読み方を改めた。どれも A2 の指摘と代替構成に沿った直しで、Runtime の変更は初稿より減り（DTO の欄だけ）、Selection と asmdef は変えないままである。新しく入った設計（欠けを許す materialize、GUID での突き合わせ、所属の組の形）は A2 のどの担当も統合後の形では見ていないので、Draft PR の外部計画レビューで見てもらう。
- 外部計画レビューの 1 巡目（2026-11-21、Draft PR、対象は A3 統合案の commit `aacfa8a1`）。2 件で、指摘は計 6 件。下は担当の分類である（owner は凍結の時に、覆す行だけを指す）。2 件目のレビューと、この巡の反映は同じ session（Claude Code、Anthropic 系）が行ったので、2 件目の 4 行は独立した採否ではない。

  | 出所 | 指摘 | 担当の分類 | 反映 |
  |---|---|---|---|
  | 1 件目 Major | hash を build の前へ移すだけでは、hash の後から Unity の build までに変わった source が base の中身になり、記録の hash と食い違う。後の hash の確認は上書きの build にしか無い | 採用 | 全ての build で、build の前と Unity の build の後の hash を比べ、違えば失敗にする（I-1 (11)、I-6 の例外、S-5、AC7 の fake adapter の test、§3 の coordinator、§2.8 U10）。書き換えて戻した変更は塞がない（§2.3） |
  | 1 件目 Major | S-2 の根拠が「一度 import した後に file を退かした `Library`」に限られ、担当外の source を最初から持たない project での成立を言えない | 採用（指摘の 2 案のうち、clean な `Library` の回を判定必須にする方） | AC6 の R8、S-2、§2.6 の (i)、§4 の順序と差し戻し条件、§5、§2.8 U11。経路は未確認で、作れない時は Phase A へ戻して owner が S-2 の範囲を決める |
  | 2 件目 Medium | S3-4 を分けると、program の gate「fail-closed せざるを得ない編集がどれくらい生じ、体験が成り立つか」の評価が S3-4a の投資の後になるが、どこにも書かれていない | 採用 | 評価は S3-4b の最初の作業（入口の配線の前）で行う（§2.8 U14、§2.3、§2.5、program §8 の S3-4b の行）。owner の条件は「入口の配線を積み上げる前に」で、S3-4b の最初の作業はそれを満たす |
  | 2 件目 Medium | base の manifest が install の結果との照合に落ちた時の答えが AC に無く、AC1 が digest の不一致を記録の「壊れている」に数えるので、`sourceFiles` を使って進める読みが残る | 採用 | 照合に落ちた manifest では計画が例外になり、分類を返さない（AC4 の (k)、AC1、I-1 (14)、§2.4 P6-a、§3 の planner） |
  | 2 件目 Minor | AC4 の読みが分かれる 2 点: root の file はあるが依存の欠けた Scene に「他の供給元」の key が渡された時の分類。(h) の「記録に無い root」が、記録の無い base では全ての root に当たる | 採用。記録の無い base で既にある asset だけを指す root の宣言を足した場合は、既知の制約として記録 | AC4 の (a)(b)(h)、§2.1 の用語、I-1 の本文と (15)、I-2 (5)、§2.3、§2.8 U12 |
  | 2 件目 Minor | 閉包の記録を版 0 にした時の「警告」の出し先が決まっていない | 採用 | `Debug.LogWarning` と manifest の版 0（S-5、AC7、§3 の publisher、§2.8 U13）。publisher の public の形は変えない |

- 外部計画レビューの 2 巡目（2026-11-21、Cursor の Grok、xAI 系。対象は `cf344338` と `60b69684`）。指摘は 7 件で、1 巡目の 6 件が反映後の文で塞がっていることも確認している。下は担当の分類である。

  | 指摘 | 担当の分類 | 反映 |
  |---|---|---|
  | Major: 「作り直せない root は base の版」は、R4 と未測定 (ii) が測る直接参照の bytes より広い。13 章は、上書きにある id の物はどちらの root から引いても上書きの版になると受け入れている | 採用 | 問い、S-2、NO-GO (ii)、R4 の読み方、harvest 先、program §8 の S3-4a の行を、「entry は base から載り、直接参照は base の build の時の bytes」に揃えた。id で引く経路は既存の契約として §2.3 に書き、R4 と R8 の合格をその証拠に使わない |
  | Medium: 状態が 2 値で、作り直せない root だけが当たった計画が「上書き不要」になる | 採用（「当たらなかった編集」を状態に入れる所だけ不採用） | 3 つ目の状態（I-2 と経路 (6)、AC4 の (i)、AC5、§3、§2.8 U15） |
  | Medium: 記録が未対応・壊れている時、全ての source がある checkout の検出規則が無い | 採用 | 記録が無い・未対応・壊れているの 3 つとも path の比較で決める（§2.1 の用語、AC2、AC4 の (g)、S-1） |
  | Medium: U7 の「消しただけなら何も出ない」と (f) が、記録の無い base の削除で別の答えになる | 採用（U7 を記録が「あり」の時に限る方） | §2.1 の削除、AC4 の (f)、§2.8 U7。Unity が消した依存の path を返すかは未測定 (iii) |
  | Minor: 再利用が返す outcome の `overlayUnbuildable` が今回の理由と違いうる。AC7 の途中の書き換えが `.meta` だけの変化を落とせない | 採用 | 今回の分類と理由の正本は今回の計画（AC5）。比べる集合は閉包の file と `.meta`（I-6、AC7、§3 の coordinator） |
  | Minor: U14 に、gate が方式の見直しになった時の `sourceClosure` の扱いが無い | 採用 | 任意の欄のまま残してよく、外す・形を変える時だけ版を上げる。harvest した文を戻すかは見直しを行うスライスが決める（§2.8 U14） |
  | Minor: §0 の次の作業が §5.1 と食い違う | 採用 | §0 の引継ぎ票 |

- Phase C で突かれる点の担当の見直し（2 巡目の反映と同じ commit）。外部の指摘ではなく、担当が自分で見つけて直した点である。

  | 見つけた点 | 直し |
  |---|---|
  | 全ての build で閉包の hash を取ると、disk に無い path の閉包を build・publish する既存の test（`BuildContentFullModeTests`、`ContentPurposeIndexPublishTests`）が落ち、GUID の形でない値の記録を書いた publish が読み直しで失敗する。I-6 と AC8 を破る | hash の時点で無い file は「無い」と記録して失敗にせず、その publish は記録を版 0 にする（I-6、S-5、AC7、§3、§2.8 U16） |
  | source の欠けた selected の root を全て作り直せない root にする実装でも、AC4 と AC6 を通る（欠けた root に編集が当たらない case が無い） | AC4 の (m) と、R8 の前半（部分 checkout で編集が無ければ、上書き不要で認めも求めない）、S-2 |
  | S-1 は E（新しい asset を参照に足す）を Play まで言うが、実機の回が無い | R7 に E を足し、S-1 を「計画の分類と build の成功まで」に揃えた |
  | 「追加」の「手元の閉包」の範囲が無く、除外した候補（base が選ばない表現）の file が毎回編集に出る実装が通る | selected の root の閉包に限る（§2.1 の用語、AC2） |
  | 索引が版 0 の publish（coordinator を通さない result）で、記録の root の集合を索引と比べられない | 記録も版 0（S-5、AC7、§3 の publisher） |
  | fixture の「合成で足した root」に期待が無い | どの回でも上書きにも作り直せない root にも出ない（AC6 の読み方） |
  | AC8 の「§3 の file だけ」は、本書の §6 の記入と、fixture と R8 を別の file に分けることを違反にする | 本書を除き、test の file は接頭辞で指す（AC8、§3） |
  | R8 の base がどの head の物か、root の GUID が解決しない時の「足りない path」が何かが無い | 同じ GO 候補 head の別の checkout が publish した base（R8）。空で、計画が記録の path で補う（AC3） |

- 外部計画レビューの 3 巡目（2026-11-21、対象は `f1194a7a`）。2 件で、指摘は計 3 件。1 件目は Cursor の Grok（xAI 系。2 巡目と同じ担当）で、2 巡目の 7 件と、担当の見直しの U16・AC4 の (m) が反映後の文で成り立つことも確認している。2 件目は owner の account から投稿されたレビューで、系列は確かめられなかった。下は担当の分類である。

  | 出所 | 指摘 | 担当の分類 | 反映 |
  |---|---|---|---|
  | 1 件目 Medium | R7 が変える file（`M`、`C` の Scene、`N`）が、回の前後で hash を比べる対象から外れておらず、R7 の回がその検査で落ちる。`U` が R6 のまま残ることも回の中に書かれていない | 採用 | 除外を「その回の操作が変える file」にして R7 の file を挙げ、前の回の操作が残ることを 1 行にした（AC6 の回の下の読み方） |
  | 1 件目 Minor | 3 つ目の状態でも他の供給元が計画に残ることが (i) に無い。同じ編集が開いている Scene と欠けた root の両方に当たると、他の供給元を落とす読みが残る | 採用 | 他の供給元と当たらなかった編集はどの状態でも残る。状態の名前の「だけ」は上書きが空であることだけを指す。その case を (n) に足した（AC4 の (i)(n)、§2.8 U15） |
  | 2 件目 Medium | 再利用の条件が鍵・directory の存在・outcome の成功だけで、directory を残して中の file が欠けた・書き換わった成果物を返せる。I-5 (2) は「壊れた」を経路に挙げている | 採用（成果物の中身を確かめる方。成果物の不変を前提として外に出す方は採らない） | 成功した成果物の file の相対 path と SHA-256 を再利用の記録に持ち、返す前に全て比べる（I-5、AC5、§2.4 P5-a、§3 の `ContentOverlayReuse` と coordinator、§2.8 U17）。登録（`RegisterVerifiedWithOverlay`）が上書きについて見るのは directory の存在と identity だけであることを code で確かめた |

- 外部計画レビューの後の A2 の再レビューの要否: 行わない（担当の決定）。3 巡目の変更は、再利用の条件を 1 つ足すこと（internal の記録の欄が増える。公開 API と依存の向きは変えない）と、読みの確定である。2 巡目の変更は、主張を測る範囲に揃えること、計画の状態を 1 つ足すこと、読みの確定で、責務マップの file・依存の向きは変えていない（公開 API の増分は、計画の状態の enum の値が 1 つ増える）。1 巡目の変更は、不変条件を崩す経路を塞ぐ追加（build の前後の hash、照合に落ちた manifest での停止）、読みの確定（AC4 の優先と (h)、警告の出し先）、証拠の追加（R8）で、責務マップの file・依存の向き・公開 API の増分は変えていない。上書きでない build の成功条件を変える U10 と、未確認の経路を足す U11 は、2 巡目が反映後の文で確かめている。担当の見直しで足した U16 と AC4 の (m) は、3 巡目の 1 件目が確かめている。3 巡目で足した U17 と AC4 の (n) は、どの外部のレビューも反映後の文では見ていない。
- A3 統合担当・採否: 統合は AI（Claude Code、Anthropic）。owner は 2026-11-21 に凍結した。上の表の担当の分類を覆した行は無い（§0 の凍結記録）。

## 6. Phase B 実装結果

### §4 の (1) 未測定 (i)〜(iv) と R8 の経路の観測

2026-11-21、Unity 6000.6.9f1、A3 凍結版（`b0c78b47`）の worktree で、対象を限定した Editor 操作（`tools/unity-editor.cmd eval`）として取った。観測用の一時 script と fixture は commit せず、観測の後に消した。fixture は §2.2 の「AC6 の回」の形（Scene `A`・`AWhitebox`・`B` が texture `T` を Light の cookie として直接参照し、Scene `C` が material `M`（texture `U`）を使う）。plan と snapshot は手で組んだ（製品の計画の code は未実装の時点）。どれも不成立ではなく、§2.6 の NO-GO と §4 の差し戻しの条件には当たらない。

| 未測定 | 観測 |
|---|---|
| (i) | `B` と `AWhitebox` を `.meta` と一緒に `Assets` の外へ退かして `Refresh` した project で、`A` だけの上書き（`T` を編集後の色）を製品の coordinator で build した。成功（約 15 秒）。R8 の経路（下）でも成功 |
| (ii) | base（`A`・`B`・`C`）と、`A` だけの上書きを Play mode で `RegisterVerifiedWithOverlay` で登録して読んだ。`A` は上書きから載り（record は上書き）cookie の pixel は編集後の色、`B` は base から載り（record は base）cookie の pixel は base の build の時の色、`C` は base。close の後の登録は 0 件 |
| (iii) | `M` だけを退かした時、`AssetDatabase.GetDependencies(C, true)` は `C` 自身だけを返した（`M` の path も、`M` からしか届かない `U` も返さない）。欠けの記録は出ず、手元の閉包が小さく見えるだけである。閉包の記録の asset の解決（GUID が file を指すか）で `C` の欠けを見分けられる（下の (iv) の注意のとおり、file の有無で判定する）。§4 の差し戻しの条件「記録の閉包の asset の解決だけでは依存の欠けを見分けられない」には当たらない。記録の無い base では、依存の file だけが欠けた root は検出に現れない（§2.3 の「記録に無い root の依存だけが手元に無い場合」と §2.8 U7 の既知の制約のとおり） |
| (iv) | 退かして戻した `B`・`AWhitebox`・`M` の GUID と、`C` の依存は元のまま。**退かしている間も、一度 import した Library の `AssetDatabase.GUIDToAssetPath` と `AssetPathToGUID` は消えた asset の GUID と path の対応を返し続けた。** そのため、手元の解決は file の有無で決める（`UnityContentOverlayLocalSource`）。materialize では、この状態の root は `UnresolvedRootGuid` ではなく `MissingRootAsset` の欠け（`RootFileMissing`）になる |

R8 の経路: 新しい worktree（`git worktree add --detach`、clean な `Library`。初回の import は約 11 分）に、`B` と `AWhitebox` を除く fixture の file を `.meta` ごと写してから Editor を開いた。`B` と `AWhitebox` の GUID は `GUIDToAssetPath` が空を返し（一度も import していない）、写した file の GUID は元の checkout と同じ。元の checkout が build・publish した base を install し、この worktree で `A` だけの上書きを build した（成功）。Play で `A` は上書きの編集後の色、`B` は base の色だった。観測の後に worktree を消した。

### 実装

- `Runtime/BuildContent/Distribution/ContentTransportManifest.cs`: 任意の欄 `sourceClosure` と serializable の型 4 つ（`ContentSourceClosure`・`Root`・`Asset`・`Group`）。logic は無い。
- Materialization: `MaterializeAllowingAbsentSources`、`BuildMaterializationAbsence`（理由は `RootUnresolved` / `RootFileMissing` / `DependencyMissing`）、`BuildMaterializationSnapshot.Absences` と欠けの記録を持つ internal の constructor、`AssetDependencySnapshotBuilder.BuildAllowingAbsence`。今の `Materialize` と `Build` は同じ入力に同じ結果を返す。
- Content:
  - `ContentSourceClosureRecord`（記録の作成と読み口。状態は Present / Missing / Unsupported / Invalid）。
  - `ContentOverlayPlanner.Plan` と `ContentOverlayEditDetection`（検出）、`ContentOverlayPlan`（状態 3 つ、編集、上書き・他の供給元・作り直せない root、`Acknowledge`、`GetReusableResult`）。
  - `ContentOverlayLocalSource`（手元を読む port と実物、閉包の hash の共通関数 `ContentSourceHashes`）、`ContentOverlayReuse`（鍵、`overlay/<contentSet>.json` の記録）。
  - `ContentOverlaySelection`（上書きの root が決まった後の結果を作る internal の `Result` を出し、計画と共有する。public の `Select` の結果は変えない）。
  - `BuildContentRequest(ContentOverlayPlan)`、`BuildContentResult.OverlayKey`、preflight の `rootClosures`・`sourceHashRecord`・`sourceHashes`・`overlayUnbuildable`・`overlaySupplied`・`overlayKey`、outcome の `overlayKey`。
  - coordinator の build の前後の hash（全ての build）と、計画から作った上書きの Unity の前の存在の確認・計画の時の hash との比較・再利用の記録の書き込み。issue code `SourceChangedDuringBuild`・`SourceFileMissing`。
  - publisher の build の前の hash からの `sourceFiles`、記録の作成と読み直し、上限の超過で版 0 と `Debug.LogWarning`。
- test（新）: `ContentSourceClosureRecordTests`（AC1）、`ContentOverlayPlannerTests`（AC2・AC4）、`MaterializeAllowingAbsentSourcesTests`（AC3）、`ContentOverlayBuildAndReuseTests`（AC5）、`SourceClosurePublishTests`（AC7 の単体）、共有の fixture `OverlayPlanWorld`（以上 `OneStarMaker/Tests/Editor/Build/`）。`OverlayFromLocalEditsIntegrationTests`（AC6 の R0〜R7 と AC7 の実機の一部）、`OverlayFromLocalEditsR8Tests`（R8）、共有の fixture `OverlayFromLocalEditsFixture`（以上 `SampleGame/Tests/Editor/Build/`）。
- 行数: 製品 code は約 1,450 行増（§3 の見込みは約 1,100）。`ContentOverlayPlan.cs` が見込みより約 90 行多いのは公開型の doc comment で、責務は増えていない。§2.5 の境界で分けるほどではないと判断した（500 行・50% の警報に当たる file は無い）。test は約 1,470 行（OneStarMaker）と約 800 行（SampleGame）。

### HANDOFF との差（B 適応）

- 検出を `ContentOverlayEditDetection.cs` に分けた。§3 が許す分割（計画の入口と検出・分類の中身が 300 行を超える）で、新しい責務・依存・公開 API は無い（internal）。
- `BuildMaterializationAbsence` に `RootPath`（root の GUID が解決しない時は null）を足した。依存だけが欠けた root の kind（Scene / Object）を計画が path から決めるために要る。root の path も GUID も手元で分からず記録にも無い root は、kind を空のままにする（記録の無い base で root の file が無い root の表示だけに効く）。
- 欠けを許す materialize で root の GUID が解決しない候補の provenance は、`rootPath` に `(unresolved)` を入れる。`ScenePayloadMappingPolicy.cs` は §3 に無いので変えず、stable key と 3 つの key は path に依らないので選択と projection に影響しない。
- 手元の解決（`UnityContentOverlayLocalSource.ResolveGuid`）は、`GUIDToAssetPath` の結果に加えて file の有無を見る（上の (iv) の観測）。
- 計画は base の manifest の上限・digest・UTF-8 の JSON かを、選択の読み口 `ContentPurposeIndex.Read`（public）と同じ規則で見る。索引の型が壊れた manifest もこの検査で例外になる（記録の「壊れている」に読み替えない）。
- 再利用の照合（鍵・outcome・成果物の file の hash）は、`Plan` の時ではなく受け取りの口 `GetReusableResult` を呼んだ時に行う。計画と受け取りの間に成果物が変わる隙を作らないため。鍵は `Plan` が計算する。
- 認めの照合（`Acknowledge`）の同一性は root の logical key・kind・表現・root の GUID で、理由と stable key は入れない（記録にだけある root は stable key を持たない）。
- AC6 の fixture: `T` は Light の cookie として Scene から直接参照する（material を挟まない）。合成で足す root は prefab `Z`。map は memory の上だけで作る（file にしない）。宣言は `A`・`B`・`C` を root にし、filter は空、表現は Full だけ。
- R8 の経路（§2.2 で B が決める点）: fixture の file は `.meta` を固定の GUID で書く代わりに、元の checkout が作った file を `.meta` ごと写して 2 つの checkout で同じ bytes と GUID にする（Unity の API で作る Scene の file は object の id が毎回変わり、同じ内容を 2 回作れないため）。base の受け渡しも同じ package に入れる。R8 は `[Explicit]` の 2 つの test に分け、全 EditMode 回帰では走らない（AC8 の合否を変えない）。手順は下の「判定 C への引渡し」。

### 未実行

- Unity の batch test は全て未実行（Phase C の責任。§0 の引継ぎ票）。AC1〜AC7 と AC8 の全 EditMode 回帰の合否は判定 C で読む。
- Editor のコンパイル確認: 済み（6000.6.9f1、`error CS` 0 件。足した code の警告も無い。`SceneResourceContentMaterializer.cs` の `IsStable` の CS8602 は base からある警告）。
- 機械検査: `pwsh tools/contract-audit.ps1` は errors 0・warnings 0。`pwsh tools/docs-audit.ps1` は errors 0・warnings 1（`PLAY_CONTENT_RESOLUTION_PROGRAM.md` の §7・§8 が埋まっているという harvest の案内で、本書の変更ではない）。
- AC8 の範囲: `Runtime/` の差分は `ContentTransportManifest.cs` だけ。`Editor/Build/Selection/`、`SampleGame/`（`Tests/` を除く）、asmdef、`AssemblyInfo.cs` に差分は無い（`git diff --stat 63f171ac`）。

### 判定 C への引渡し（R8 の走らせ方）

R8 は同じ head の 2 つの checkout にまたがる。test の class の doc comment にも同じ手順を書いた。

1. 判定 C の checkout で `OverlayFromLocalEditsR8Tests.R8_ProduceBaseAndSourcesForACleanCheckout` を名前の filter で選んで走らせる（`[Explicit]` なので名前で選ぶ）。base を build・publish し、publish した directory、`B` と `AWhitebox` を除く fixture の file（`.meta` を含む）、識別と GUID（`base.json`）を package に書いて、fixture を消す。package の既定は checkout の `TestResults/s34a-r8-package`、環境変数 `OSM_S34A_R8_PACKAGE` で別の場所を指せる。
2. 同じ head の新しい worktree（`Library` の無い checkout）を作り、1 の package をその worktree から見える場所に置く（環境変数で指すか、`TestResults/s34a-r8-package` へ写す）。
3. その worktree で `OverlayFromLocalEditsR8Tests.R8_PartialCheckout_PlansBuildsAndPlaysLikeR4` を名前の filter で選んで走らせる。test は始めに、fixture の folder が無いことと、`B`・`AWhitebox` の GUID が解決しないこと（一度も import していない）を確かめ、満たさなければ失敗する。

### 動作確認

- 対象 head: なし
- 条件ごとの実行担当・環境・実行したコマンド: なし
- 生の観測と保存先・状態: なし
- 検証経路の期待と違う観測と、B への差し戻し: なし
- `未実施` の条件と理由・担当: `対象なし`（§5「操作・実行時・目視条件の検証経路: 該当なし」）

- implementation head commit: §0 の欄（この票を含む commit）
- Phase B 担当・ベンダー: AI（Claude Code、Anthropic）

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
