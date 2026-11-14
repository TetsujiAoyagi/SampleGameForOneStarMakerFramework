# Scene の印の方式を選ぶスパイク（S2-2b の前段）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2b-0。S2-2b から切り出した）
- status: `C`（**revision 1 の発見 C・判定 C 完了、2026-11-14（§7.4）。§2.5 の読みは CONDITIONAL ACCEPT (iii)**（M と F2 は成り立ち、AC3b は M が未判定、F2 が不合格）。発見 C で阻害する script の誤りは無く、batch のやり直しは無い（§7.3）。次は revision 1 の C' 1 巡目。以下は revision 1 の Phase B 完了時点の記述: **revision 1 の Phase B 完了、2026-11-14（§6.1）。次は発見 C の実行担当。** 以下は revision 1 の A3 凍結版の記述: **revision 1 の A3 凍結版。** 2026-11-14 に owner の承認で凍結した。A2 1 件の指摘 8 件を全て採用した（§5.2）。A2 入力版は commit `ff80a6a0`。2026-11-14。AC3b の印の計算の手順だけを、製品の依存閉包を受け取る形に変えた（§5.2）。A3 で凍結するまで、revision 0 の条件（§2・§5 の revision 0 の文）は §7.2・§8 の結果を読むためにだけ使う。owner の決定で Phase A を revision 1 として再開する、2026-11-14（§0 の引継ぎ票の owner の決定）。revision 0 の C' 1 巡目は完了、2026-11-14（§8）。C' の読みも **GO・NO** で、現在の問いを阻害する findings は無い。判定 C 完了、§2.5 の読みは **GO・NO**（M と F2 は成り立ったが、両方とも AC3b が不合格）、2026-11-14（§7.2）。発見 C で阻害する script の誤りは無く、batch のやり直しは無い。発見 C の実行担当は 2026-11-14 に batch を 1 回実行し、record を evidence bundle に固定した（§7.1）。Phase B は 2026-11-14 に完了（§6）。Phase A は A3 凍結版 revision 0 のまま。以下は Phase A の経緯: **A3 凍結版、revision 0。** 2026-11-14 に owner の承認で凍結した。A2 の 2 件、凍結前の外部レビュー 5 巡（draft PR #191）、主担当の自己点検を反映してある（§5.1）。A2 入力版は commit `37ec408e`、外部レビューの対象は 1 巡目が commit `8259fb0c`、2 巡目が `d5b60674`、3 巡目が `719ea649`、4 巡目が `006334ba`、5 巡目が `2c657c6b`）
- branch: `claude/content-scene-stamp-spike`（PR の base は `develop`）
- implementation base commit: `41099fb7`（`develop` の head、2026-11-14）
- implementation head commit: `9197e907`
- risk: `normal`（製品 code と SampleGame を変えない。実測は repository の外の project で行い、この repository に入るのは文書だけ）
- owner: root（依頼者）
- created: 2026-11-14
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」（印の形と検知の鍵を測った結果、測っていないもの）、program §8 のこのスライスの行と S2-2b の行。S2-2b の A1 は事実の文書のその節を再開入力として読む（S2-2b の初稿へ結論を写さない）。revision 1 の AC3b の結果を写す時は、§2.5 の 3 番の前提の文を必ず付ける。結論が出たら、既存の GitHub Pages（`gh-pages` ブランチ）も更新する（2026-11-14 の owner の依頼。載せる内容は Phase D で owner と決める）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§5.3、§8）。S2-2b の A1 初稿（`docs/handoff/CONTENT_PREFAB_STAMP.md`、ブランチ `claude/content-prefab-stamp` の作業ツリーにある未 commit の file。本スライスは読むだけで変えない）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script を持たない。§5。revision 1 も同じ）
- Phase B result snapshot の取得案内・状態: 未生成（repository の差分は文書だけ。実験の script は repository の外にあり、次項）
- 実験の Editor script の取得案内・状態（revision 0）: ローカル固定済み（未昇格）。実行した script を evidence bundle に収録した（次項、§7）。原本はローカル PC の repository の外の実験 project にある。revision 1 の script: ローカル固定済み（未昇格）。実行した script を revision 1 の evidence bundle に収録した（次項。sha256 は起動指示の値と一致、§7.3）。原本はローカル PC の repository の外の新しい実験 project（revision 0 の project とは別）にある。
- evidence bundle の取得案内・状態（revision 0）: ローカル固定済み（未昇格）。Artifact Storage の task `content-scene-stamp-spike` に `evidence stage` で固定し、別の process の fetch で hash の照合と必須の file の閲覧を確かめた（§7）。判断担当も別の process の fetch で照合を通して読んだ（§7.2）。取得案内は実験の project に置き、所在は起動指示で渡す。revision 1 の evidence bundle: ローカル固定済み（未昇格）。発見 C の実行担当が同じ task に `evidence stage` で固定し（125 file）、実行担当と判断担当がそれぞれ別の process の fetch で hash の照合を通して読んだ（§7.3、§7.4）。取得案内は revision 1 の実験の project に置き、所在は起動指示で渡す。
- C' blind bundle の取得案内・状態（revision 0）: ローカル固定済み（未昇格）。Artifact Storage の task `content-scene-stamp-spike` に `evidence stage` で固定し、別の process の fetch で hash の照合を通した（§8）。取得案内は実験の project に置いた。revision 1 の C' blind bundle は未生成（進行役が作る。下の票）。
- 引継ぎ票（revision 1 の判定 C の出口。revision 1 の発見 C の実行担当への票は commit `ccd75872` の本節にある）:
  - 次の Phase・役割: C'（revision 1 の 1 巡目、全体。最終 C'。高コスト証拠なし）。担当は §5 で予約した Grok 系（cursor-agent）。進行役が blind bundle を作って起動し、出力を §8 に記録する（revision 0 と同じ方式。進行役は監査をしない）。
  - 次の作業: 進行役は、revision 1 の evidence bundle（判定 C と同じ。`record/`・`run.log`・`Assets/Spike/`）、Phase B の出口の commit `ccd75872` の本書の blob、base から `ccd75872` までの本書以外の差分、`AGENTS.md`、監査の指示書から blind bundle を作り、`evidence stage` で固定して、別の process の fetch で照合した写しを C' の workspace にする。`run-notes.txt`（実行の記録）と、本書の §7.3・§7.4（この票を含む commit で足した記録）と §0 のこの票は入れない。C' は §2.2 の AC と §2.5 の読みを、期待値を §5 の fixture の値から導いて独立に行う（record の `ac*`・`holds` は補助値）。
  - 読む範囲: C' は所定の blind 入力だけ。進行役は §0 と §8。
  - 必要な権限: Artifact Storage の `evidence stage`・`fetch`（事前承認の範囲）、cursor-agent の起動（Grok 系のモデルを明示する）。Unity は起動しない。
  - 停止規則: C' の出力が GO・NO / CONDITIONAL ACCEPT の判定と findings を持たない時は、同じ重さで撃ち直さず owner へ返す。
  - 引き渡さないもの: 判定 C（§7.4）の結論と所見（blind のため）。
  - 引き継ぐ commit: この票を含む commit（revision 1 の判定 C の出口。SHA は起動指示で渡す）。

## 1. 目的と対象外

- 目的: S2-2b（Scene に置いた prefab の編集を、同じ `Library` の差分 build の出力に入れる）が採る方式を、owner の 3 つの条件を全て満たすものに実測で絞る。満たす方式が無いなら、どの条件がどの事実で満たせないかを示し、owner が条件を見直せるようにする。
- 利用者: S2-2b の A1 を再開する担当と owner（結論の読み手）。製品の利用者の操作は無い。
- 目的・利用者の文言の経緯: 上の 2 欄は、2026-11-14 の owner の依頼（問いと 3 条件）から本書で初めて書いた文である。owner は 2026-11-14 にこの 2 欄を承認した（§2.8 U1）。
- 方式のあらまし（本書が「印」と呼ぶもの）:
  - Unity は Scene を build する時、Scene の file が前回と同じなら前回の出力を使い回し、prefab の中身は見ない。prefab だけを編集すると Scene の file は変わらないので、古い prefab を焼き込んだ前回の出力が出る。
  - そこで、build の直前に、Scene の file へ「今の prefab の版を表す短い文字列」（印）を 1 か所だけ書き込み、build の後に Scene の file を元のバイトへ戻す。印は prefab の版だけから決まるので、prefab が変わった時だけ Scene の file が前回と違って見えて作り直され、変わっていなければ使い回され、前の版に戻せばその版の出力が使い回される。
  - 本スライスが決めるのは 2 つ。印をどこに書くか（置き場を source に残さない形。§2.7 の M と F2）と、印の値を prefab の file を読まずに作れるか（Unity が import の時に持つハッシュを聞く。§2.7 の K1〜K3）。
- owner の 3 条件（2026-11-14 の owner 決定。本書はこの文を変えない）:
  1. **cache を捨てない。** `CleanBuildCache` は使わない。build のたびに鍵が変わる形（毎回新しい fileID の object を足す等）も不可。prefab が変わっていない Scene は再利用され、prefab を前の版に戻した build は前の出力を再利用する。
  2. **「prefab によって Scene が変わった」の検知が安い。** 何も変えていない build に乗る上乗せがほぼ無い。毎 build で prefab・model（FBX 等）の全バイトを読む方式は不可。
  3. **build の後の source に何も残らない、またはチームで揉めない。** build が置き場を自動で足して残す形は不可（diff の雑音、2 人が足した時の衝突、CI の作業ツリーの汚れ）。
- 前提（同じ owner 決定）: 主な利用場面は、実機へ転送する build をローカルで作ることと CI。Editor Play は別経路。編集途中の Scene は考えなくてよい（未保存なら止めてよい）。ロック方式の VCS は対象外。
- 想定する規模（2026-11-14 の owner の指摘）: SampleGame ではなく、中規模のゲーム開発。条件 2 の「上乗せがほぼ無い」は、その規模の Scene と prefab の数で読む。本書は規模の fixture を §5 に置く（数は U5）。
- 対象外:
  - 製品 code（`unity/Assets/OneStarMaker/`）と SampleGame の変更。選んだ方式の製品への実装、build が途中で落ちた時の戻し、開いている Scene・読み取り専用の file の扱い（S2-2b）。
  - この repository の `unity/Library` での build。SampleGame の実データ。
  - Player の build、別の PC・`Library` の間の cache、archive。
  - prefab instance 以外の理由で Scene の出力が古くなる経路（script の code、lightmap、sprite atlas、import 設定）。
  - prefab variant・override を持つ instance・unpack した prefab の実測（§2.3）。
- 現況（base `41099fb7`。出典の無い行は本 session が 2026-11-14 に確かめた）:
  - **不成立と原因は実測済み。** prefab を編集した後に同じ `Library` で Content Directory を build すると、Scene の build instruction が変わらず、前回の Scene の serialized file が再利用される。build は成功し、Warning も出ない。目的 build でも上書きでも起きる（[事実の文書](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」、commit `52612ede` の `docs/handoff/CONTENT_OVERLAY_BUILD.md` §7「スパイク（F-1 の原因）」）。
  - **印の原理は最小の再現 project で実測済み。** Scene に最初から置いた `EditorOnly` の GameObject の名前を、build の直前に prefab の中身のハッシュに書き換え、build の後に Scene を元のバイトに戻すと、prefab を v1 → v2 → v2 → v1 → v2 と進めて戻した 5 回が全て正しく、cache も再利用された。毎回新しい object を足す形は fileID が毎回変わり、cache が当たらなかった。範囲は Scene 1 つ・prefab 1 つ（入れ子なし）・1 つの Editor process（同じ出典の追記）。この形は置き場を source に残すので条件 3 を満たさず、ハッシュは prefab の全バイトから作ったので条件 2 を満たさない。
  - **cache の鍵が Scene の file の生のバイトか、読み取った後のデータかは未確認。** 既存の実験は、どれもバイトと中身の両方を変えた。同じ追記の実験で、Scene の値を 1 つ変えた build は Unity の統計で instruction の変更 0 のまま出力が作り直され、空の GameObject を足した build は instruction が 1 つ変わった。鍵が 2 段ある可能性を示すが、切り分けていない。
  - **build の API に、Scene の鍵へ入力を足す口は無い。** Unity 6000.6.0f1 の `UnityEditor.CoreModule.xml` で、`BuildContentDirectoryParameters` の member は `compression`・`extraScriptingDefines`・`name`・`options`・`outputPath`・`rootAssetPaths`、`BuildContentOptions` の値は `None`・`CleanBuildCache`・`DisableWriteTypeTree`・`FailBuildWhenErrorsLogged`・`SerializeUnityVersion`・`SkipExportToOutputPath`・`UseArchive` だけ。この API の範囲では、印は Scene の file に入れるしかない。
  - **Unity の公開文書は、UnityYAML がコメントに対応しないと書く。** Unity 6000.3 の manual「UnityYAML」の対応しない機能の表に Comments があり、「UnityYAML does not support comments.」とある。コメント行のある file を読んだ時に無視するのか失敗するのかは書いていない。知らない key を持つ object の読み方も書いていない。同じ manual の「Format of text serialized files」は、object の ID を「unique within the file」とし、範囲や作り方の規則を書いていない。「YAML scene example」は「Unity tries to assign default values to any properties for which a value hasn't been set」と書く。
  - **検知に使える API（6000.6.0f1 の assembly を reflection で見た）:** `AssetDatabase.GetAssetDependencyHash(string / GUID)` は public。`AssetDatabase.GetSourceAssetFileHash(string)`・`GetSourceAssetMetaFileHash(string)` は存在するが internal（XML doc にも無い）。`AssetDatabaseExperimental.LookupArtifact(ArtifactKey)` は public（`Experimental` の namespace）。`GetAssetDependencyHash` の公開文書の remarks は、source asset の path・source asset・meta file・target platform・importer version をまとめた値と読める（要約経由で、逐語は確かめていない）。**prefab の編集でこれらの値が変わるか、前の版に戻すと値も戻るかは未確認。**
  - **検知の鍵の先行確認（build なし、Phase A の未知の経路の小さい確認）:** repository の外の使い捨ての空の project（Unity 6000.6.0f1、package なし、batch、1 process）で、prefab `Inner`、`Inner` を nest した `Outer`、model の `.obj` について値を採った。`GetAssetDependencyHash` は、`Inner` と model を編集すると変わり、元のバイトへ書き戻すと最初の値に戻り、更新時刻だけを変えても変わらず、v2 のバイトをもう一度書くと v2 の時の値に戻った。`Outer` の値は、`Outer` の file を変えていなくても `Inner` の編集で変わり、戻すと戻った。`LookupArtifact` の ID は全ての時点で `GetAssetDependencyHash` と同じ値だった。`GetSourceAssetFileHash` は file のバイトにだけ追従し（`Outer` は変わらない）、`GetSourceAssetMetaFileHash` は変わらなかった。所要は `GetAssetDependencyHash` が 10,000 回で 27 ms、`GetDependencies(Outer, true)` が 1,000 回で 66 ms。範囲: 小さい prefab 2 つと 3 頂点の `.obj`、各 1 回。大きい file、依存の多い Scene に対する `GetDependencies` の所要、prefab variant は測っていない。判定の証拠ではなく、§5 の batch の段 2 で採り直す。
  - **印の形の先行確認（build あり、同じ使い捨ての project、2026-11-14。owner が batch の実行を承諾した後に行った）:** Scene `S`（`Inner` を nest した `Outer` の instance）と prefab の無い Scene `P` を 1 つの root から build し、`Inner` を v1 → v2 → 変更なし → v1 → v2 と進めた。1 process、各 1 回。
    - **Scene に何も書かない形は成り立たなかった。** `AssetPostprocessor` で Scene の import に custom dependency を宣言し、`RegisterCustomDependency` の値を印にすると、Scene の `GetAssetDependencyHash` は印に追従した（Scene と `.meta` のバイトは変わらない）が、build の出力は v1 のままだった。build は Scene の import のハッシュを見ていない。印なしの対照も v1 のままで、取りこぼしはこの project で再現した。
    - **Scene の `.meta` の `userData` に印を書いて戻す形（M）は成り立った。** 5 回とも正しい版が出て、v1 へ戻した回の Scene の出力は 1 回目と同じ名前・同じ sha256、v2 の 2 回も互いに同じだった。`P` の出力は全ての回で同じ。印の文字列は出力に無く、各 build の後の source は build の前と一致した。Scene 本体は 1 バイトも書いていない。
    - **Scene の末尾に知らない key の行を足して戻す形（F2）も、同じ結果で成り立った。** Error は 0。
    - **`SerializeBuildInstructions` の `changedCount` は、Scene を作り直した回でも 0 だった**（cache を消した直後の回だけ 5）。この値は作り直しの有無を表さない。build の呼び出しの所要は、Scene が変わった回が約 1.35 秒、他の回が約 0.86 秒だった。
    - **Scene の serialized file を書いた数は、`BuildContentTEP.json` の `BuildPipeline::WriteSerializedFile` の event の数で数えられる。** M の形で 4 回 build した追加の確認で、cache の無い回は 5、`Inner` を v2 にした回は 1（同じ回に `BuildMetaDataImporter(worker)` が Scene `S` の path で 1 件）、変更なしの回は 0、v1 へ戻した回は 0 だった。前の版へ戻した build は、前の出力を作り直さずに使った。event の `filePath` は `mem:/Temp/<32 桁>.cfid` の形で、Scene `S` の file は cache の無い回と `Inner` を v2 にした回で同じ値だった。S2-2 の追記が「Unity の統計で作り直し 0」と書いた値の名前は、その記録に無い。
    - 範囲と限界: 入れ子は 1 段、共有の prefab・model・複数の Scene への印・規模は測っていない。上乗せは `Refresh` を含めて書き込みが 16〜48 ms、戻しが 15〜44 ms（Scene 1 つ）。判定の証拠ではなく、§5 の batch で採り直す。
  - **規模の fixture と model の先行確認（build なし、同じ使い捨ての project、2026-11-14、各 1 回）:** `PrefabUtility.InstantiatePrefab` で置いて位置だけを変えた prefab instance は、Scene の text で 1 つ約 2.4 KB だった（0 個で 3,539 バイト、100 個で 242,627 バイト、600 個で 1,439,501 バイト）。`.obj` の main asset は `GameObject`、importer は `ModelImporter` だった。`v 31 32 33` と書いた頂点は、import した Mesh で (-31, 32, 33) だった。`o` で付けた名前は子の名前にならなかった（子は `default`）。
  - **`GetAssetDependencyHash` を Scene に対して呼んだ値は、依存の中身を表さない。** prefab・texture を v2 にした前後で、それを置いた Scene の値は変わらなかった（S2-2 のスパイクの参考の観測）。prefab 自身に対して呼んだ値は測っていない。
  - **この repository の Scene の text:** `unity/Assets` の `.unity` 668 件の全てが `SceneRoots` の document を持ち、全てでそれが file の最後の document である（file は `m_Roots` の最後の行で終わる）。最大の Scene は約 23 KB で、規模の参考にならない。`#` で始まる行を持つ `.unity`・`.prefab` は無い。`EditorOnly` tag を持つ Scene は無い。`ProjectSettings/EditorSettings.asset` は `m_SerializationMode: 2`（Force Text）。
  - **製品で Unity の build を呼ぶのは 1 か所。** `UnityContentDirectoryAdapter.Build`（`Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs`、173 行、`BuildContentOptions.None`）。root ごとの依存閉包（`AssetDatabase.GetDependencies(path, true)`）は build の入力の snapshot が既に持つ（S2-2b の A1 初稿 §1）。
  - **製品の依存閉包の作り方（revision 1 で確かめた。commit `88311fef` の code）:** `SceneResourceContentMaterializer.MaterializeCore` が、SceneResource の Scene の payload（と付随 asset の payload）ごとに、その GUID を root として `AssetDependencySnapshotBuilder.Build` を呼ぶ。`Build` は root の path を `GetDependencies(root, true)`（`UnityAssetDatabaseGateway`）で列挙し、`\` を `/` に替え、`Assets/` で始まらない path・`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref`・folder を除き、root 自身を足して、path の序数順に並べ、path ごとに GUID を引いた `BuildDependencySnapshot`（GUID と path の組の列。asset の型は持たない）を返す（`AssetDependencySnapshotBuilder.cs` 40〜72 行）。これは build のたびに、印の有無に関係なく行われる。snapshot は materialize の結果として `BuildContentCoordinator` へ渡され（`request.Snapshot`）、coordinator が adapter の `Build`（83 行）を呼ぶより前にできている。adapter の `Refresh`（`UnityContentDirectoryAdapter.cs` 42 行）より前である。Scene の閉包は Scene ごとの root から作るので、閉包の path の集合は、revision 0 の script が Scene ごとに呼んだ `GetDependencies(scene, true)` の結果から同じ除外をした物に root を足した集合と同じになる（revision 0 の印の入力は main asset が `GameObject` の物と `ModelImporter` の物だけなので、除外される path と root の Scene は入力に入らない）。
  - **revision 0 の結果（§7.2、§8）:** 印の形 M と F2 はどちらも AC1・AC2・AC4・AC5 を満たし、検知の鍵 K1 は §2.7 の (1) で選ばれたが、AC3b が両形とも不合格で GO・NO になった。1 回の上乗せ（厳しい読み）は M が 3,701〜4,008 ms、F2 が 3,667〜4,669 ms（割る値は M 15,343 ms、F2 14,658 ms）。内訳の「計算」が約 3.0 秒で、Scene ごとの `GetDependencies` と型の判定と K1 を分けて測っていない（§7.2 の N3）。計算を除く残り（書き込み、`Refresh` の差、戻し、戻し後の `Refresh`、build の差）は、M が 652〜979 ms（4.2〜6.4%）、F2 が 666〜1,645 ms（4.5〜11.2%）で、build の差が回ごとに 0〜994 ms と大きく揺れた。実行中に無関係な Unity の batch が 3 本動いていた（§7.1）。
- UC ごとの利用者の入力: `なし`（利用者の操作が無い。測る方式は、どれも build の呼び手に新しい入力を求めない）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 同じ `Library` の Content Directory の差分 build で、Scene に置いた prefab（入れ子を含む）の編集が出力に入るようにする方法のうち、owner の 3 条件を全て満たすものはあるか。あるなら、印の形と検知の鍵の組はどれか。

### 2.1 進める最低条件

**不変条件**（起きてはいけないこと）

- **I1 この repository の製品と `Library` を変えない。** 本スライスの全ての commit の差分は `docs/` の文書だけで、実測はこの repository の `unity/` の下で Unity の build を 1 回も行わない。
  - 主張を崩しうる経路: 実験の script や fixture を `unity/Assets` に置いて流す（S2-0〜S2-2 の harness はそうした）。実験の project の path を取り違えて、この repository の project を `-projectPath` に渡す。
  - 探索した境界: S2-2 の追記の実験は repository の外で行われ、この repository に何も足していない。実験の project は、実行担当が repository の外に新しく作る（§2.8 U2）。
- **I2 cache が当たった理由を取り違えない。** 「正しい版が出た」だけで条件 1 の成立としない。S2-2 の追記で、毎回新しい object を足す形は 5 回とも正しかったが、鍵が毎回新しいだけだった。
  - 主張を崩しうる経路: 印を入れた Scene のバイトが、同じ prefab の状態でも build ごとに違う（時刻、連番、列挙の順、改行）。列の途中で cache を消す。前の方式の列が作った cache の entry を、次の方式の列が再利用して見かけの再利用になる。
  - 探索した境界: 既存の実験の手順。方式の違う印入りの Scene が同じ cache の entry に当たる経路（鍵が読み取った後のデータなら起こりうる）は、形ごとに cache を消してから列を始めることで塞ぐ（§5）。

**成立条件**（起きなければならないこと。印の形と検知の鍵の組ごとに判定する）

- **S0 取りこぼしが、この fixture で再現する（前提）。** 印を入れない build の列で、prefab を編集した後の build の Scene が古い版のまま出る。
- **S1 正しさ。** 印を入れた全ての build で、各 Scene の出力が、その build の直前の source の prefab の版を持つ。入れ子の内側だけを編集した時と、2 つの Scene が共有する prefab を編集した時を含む。
- **S2 条件 1（cache）。** prefab を変えていない build は Scene を作り直さず、prefab を前の版に戻した build は、その版で build した時と同じ Scene の file を出す。
- **S3 条件 2（安い検知）。** 印の値を、prefab・model の file を開かずに作れ、その値は file の中身が変われば変わり、前の中身に戻せば戻り、中身が同じなら変わらない。
- **S4 条件 3（source に残らない）。** 各 build の後、project の `Assets` の下の全ての file が、その build の前と同じ名前と同じバイトである。

### 2.2 受け入れ条件

観測は全て §5 の 1 回の batch の record で閉じる。期待値は fixture の source の値から導き、実験の出力から作らない。「組」は印の形（§2.7 の M・F2）と検知の鍵（K1〜K3）の組をいう。「書いた数」は、その build の `Library/BuildHistory/<build>/BuildContentTEP.json` にある `BuildPipeline::WriteSerializedFile` の event の数をいう（§1 の先行確認）。「書いた file の ID」は、その event の `filePath` の値をいう。各 Scene の `.cf` は、その Scene の目印の列（§5）を持つ出力の file をいう。「出力」は、その build の専用の出力先（§5）にある file の全てをいい、前の build の出力を含まない。

- **AC0（S0）前提。** 印なしの列 C1（全て v1）→ C2（`Inner` だけ v2）で、C2 の `SOuter` の `.cf` が `Inner` の v1 の float 列を持ち、v2 の列を持たず、C2 の書いた数が 0。成り立たない時は、この batch から問いを判定しない（§2.5 の未判定）。
- **AC1（S1）正しさ。** 印の形ごとの列 B1〜B6（§5）の各回で、各 Scene の `.cf` が、その回の source の prefab の版の float 列を全て持ち、別の版の列を持たない。`Model` は、全ての回で、出力の全ての file（`.resS` を含む）を走査して、その回の版の頂点の列がどれかにあり、別の版の頂点の列がどこにも無い（頂点は Scene と別の file に書かれうる）。合格例: B2（`Inner` だけ v2）で、`Inner` を直接は置かず `Outer`・`Mid` 経由でだけ焼き込む `SOuter` が、`Inner` の v2 の列と、`Mid` と `Outer` 自身の子の列 (10,11,12)・(7,8,9) を持ち、`Inner` の v1 の列を持たない。不合格例: B5 で `SShared1` は v2 だが `SShared2` が v1。
  - B6 の読み: 印なしの C3 が既に `Model` の v2 の頂点を出していた時（model の頂点の編集は、印が無くても取りこぼされない）、B6 が示すのは「印が model の編集を妨げない」ことだけである。その時は結論の文に、model の編集のうち Scene に焼き込まれる側（階層、Transform）は測っていないと書く。
- **AC2（S2・I2）cache。** (a)〜(f) が全て成り立ち、(g) が成り立つか「読めない」である。(g) が成り立たなければ AC2 は成り立たない。(g) が「読めない」形は AC2 を満たすが、§2.5 の「全てを満たす形」には入れない（指標が読めないことを、条件 1 の不成立と読まない）。列の中で cache を消さない。
  - (a) B3（変更なし）の書いた数が 0 で、出力の全ての Scene の file が B2 と同じ名前・同じ sha256。
  - (b) B4（`Inner` を v1 に戻す）の書いた数が 0 で、各 Scene の file の名前と sha256 が B1 と一致する。
  - (c) B5（`Inner` と `Shared` を v2）の書いた数が 2（`SShared1` と `SShared2` の分）で、`SOuter` の file が B2 の `SOuter` と一致する。
  - (d) B2（`Inner` だけ v2）の書いた数が 1。
  - (e) 印を入れた file（M は `.meta`、F2 は Scene）のバイトの sha256 が、同じ prefab の状態の回（B2 と B3、B1 と B4）で一致する。sha256 は、印を書いて `Refresh` した後、build の直前に採る。
  - (f) 印の値を固定した対照の列 X1（全て v1、印は固定の文字列）→ X2（`Inner` だけ v2、同じ固定の文字列）で、X1 と X2 のどちらも、`Refresh` の後・build の直前の、印を入れた file（M は `.meta`、F2 は Scene）に固定の文字列が残っていて、X2 の書いた数が 0 で、X2 の `SOuter` が `Inner` の v1 の列を持つ。印を書いて戻す操作そのものは作り直しを起こさず、作り直しを起こすのは印の値の違いである、を分けて確かめる。
  - (g) B6（`Model` だけ v2）で、`SModel` 以外の Scene の印を入れた file の sha256 と出力の file が B5 と一致し、次の 2 つの指標で、無関係な Scene を作り直していない。B6 の書いた数そのものは固定しない（頂点が Scene と別の file に書かれうるため）。
    - path の指標: B6 の `BuildMetaDataImporter(worker)` の event の `assetPath` に、`SOuter`・`SShared1`・`SShared2`・`SPlain` が無い（先行確認で、作り直した Scene の path がこの event に出た）。この指標が働くことは、同じ形の B2 の event の Scene の path が `SOuter` だけ、B5 が `SShared1` と `SShared2` だけで、B6 の event に `SModel` の path があることで確かめる（B6 の event に Scene の path が 1 件も無いと、禁止の path が無いだけで通ってしまう）。
    - ID の指標: B6 で書いた file の ID に、B2 と B5 で書いた file の ID が 1 つも無い。この指標が働くことは、B2 と B5 で書いた ID がどれも B1 で書いた ID にあること（ID が Scene ごとに build をまたいで同じ。先行確認ではそうだった）で確かめる。ID が build ごとの番号なら、この指標は使わない。この指標は `SPlain` を見ない（`SPlain` には印を書かず、B1〜B5 のどの回でも書かれないので、禁止の集合に入れる ID が無い）。path の指標が働かない時は、`SPlain` の作り直しを見ていないことを record に書き、印なしの C3 の event と並べて読む（印を書かない Scene の作り直しは、印によるものではない）。
    - 働くと確かめた指標の全てで成り立てば (g) は成り立ち、1 つでも成り立たなければ成り立たない。どちらの指標も働かない時は、(g) を「読めない」と記録し、その形の AC2 は (g) を除いて判定する（§2.5 の GO・YES には入れない）。
  - 不合格例: B4 の出力は B1 と同じ sha256 だが、書いた数が 1（出力の名前は中身の hash なので、作り直しても同じ名前・同じ sha256 になる。出力の一致だけで再利用と読まない）。
- **AC3（S3）検知の鍵。** 鍵ごとに、Unity の build を伴わない次の観測が全て成り立つ。(a) `Inner` の file を v2 にすると `Inner` の値が変わる。(b) v1 のバイトに戻すと、最初の値に戻る。(c) バイトを変えず更新時刻だけを変えても値が変わらない。(d) model の file（fixture の `.obj`）でも (a)〜(c) が成り立つ。加えて、実験の script が印の値を作る時に、prefab・model の file を file の API で開かない（script の code で確かめる）。
  - (e)（過剰な作り直しの観測）material と texture を持つ prefab `Dep` について、texture のバイトを変えた時と、material の値を変えた時に、`Dep` の鍵の値が変わるかを記録する。変わる鍵は、Scene の出力が変わらない編集でも、その prefab を置いた Scene を作り直す。「(e) で値が変わらない」は、texture の変更でも material の変更でも値が変わらないことをいう。読みは §2.5。
- **AC3b（S3）規模での上乗せ。** 成り立った印の形のそれぞれについて、§5 の規模の列（形ごとの独立した block。source を変えない build を、印なしの N1〜N3 と、その形の印ありの T1〜T3 で交互に行う）で判定する。結果は形ごとに、合格・不合格・未判定の 3 つ。
  - (a) T1〜T3 の書いた数が全て 0 で、T1〜T3 の出力の `.cf` と `.resS` の file の（名前, sha256）の集合が、その形の T0（印ありの初回）と同じ。manifest の `.json` と `BuildManifestHash.txt` は build ごとに作り直すので比べない（revision 1 で、revision 0 の §6 の B 適応の読みを条件の文にした。§8 の (B) の 3 番目）。
  - (a2) N1〜N3 の書いた数が全て 0 で、出力の `.cf` と `.resS` の file の（名前, sha256）の集合が W と同じ（比べない file は (a) と同じ。N が再利用になっていないと、N の所要が膨らみ、上乗せが小さく見える）。
  - (b) 1 回の上乗せは次の合計: 印の値の計算（受け取った依存閉包から印の入力を絞る型の判定、鍵の取得、ハッシュ。依存閉包を作る所要は入れない。§2.7 の「依存閉包の受け取り」）、印の書き込み、書き込みの後の `Refresh` の所要から N の `Refresh`（同じ位置で測る、変更なし）を引いた値（負なら 0）、戻し、戻しの後の `Refresh`、T の build の所要から N の build の所要を引いた値（負なら 0）。製品は build の前に `Refresh` を 1 回呼ぶ（`UnityContentDirectoryAdapter.cs` 42 行）ので、その分を印の上乗せに数えない。棚卸しと record の書き出しは、どの所要にも入れない。依存閉包は、T でも N でも build ごとに同じ位置（棚卸しの後、T では印を書く前）で作り、その所要を record に書くが、どの所要にも入れない（製品は印の有無に関係なく build ごとに作る。§1。revision 1 の owner の決定）。上乗せに入れる計算の所要は外側の 1 つの計測の値で、型の判定、鍵の取得、ハッシュの内訳は、revision 0 と同じ Scene ごとの loop の中で累計した参考値として record に書く（loop の構造は変えない。§7.2 の N3）。割る値は、N1〜N3 の build の所要の最小に固定する。引く値（N の `Refresh` と build の所要）に、N1〜N3 の最小を使う読みを「厳しい読み」、中央値を使う読みを「緩い読み」という（最大を使うと、遅い N が 1 回あるだけで差が 0 に潰れる）。
  - 形ごとの結果は、次の順に当てはめ、最初に当たったものにする。
    0. **未判定:** §2.7 の「依存閉包の受け取り」の関数の分け方を判断担当が script の code で確かめ、成り立たない（script の誤り。§2.5 のやり直しの対象）。
    1. **未判定:** その形の block が T3 まで届かなかった（測り切っていない形を不合格にしない）。
    2. **不合格:** (a) が成り立たない。
    3. **未判定:** (a2) が成り立たない（N が基準にならない）。
    4. **不合格:** 緩い読みでも、T1〜T3 のどれかの回の上乗せが割る値の 5% を超える。
    5. **合格:** 厳しい読みで、T1〜T3 のどの回の上乗せも割る値の 5% 以下。
    6. **未判定:** 上のどれでもない（N のばらつきで 5% の上下が分かれる）。1 回ずつの計測のばらつきは、事実の文書の「同じ `Library` での再 build の所要」で 3 割あった。
- **AC4（S4）source。** 各 build の前（source の版を切り替えて `Refresh` した後、印を書く前）と後（元のバイトへ戻して `Refresh` した後）で、`Assets` の下の全ての file（`.meta` を含む）の path と sha256 の棚卸しが一致する。batch の終わりに fixture を消した後の棚卸しが、batch の始めと一致する。不合格例: build の後に Scene は元のバイトだが、新しい `.meta` が 1 つ増えている。
- **AC5（S1 の補助）印が出力と log を汚さない。** 印の形ごとに次が成り立つ。(a) 全ての build の出力の file のバイトに、印の文字列（接頭辞とハッシュ）が無い。(b) B6 の出力と、列の後に cache を消して印なしで build した基準 R（B6 と同じ source）の出力が、build ごとに作り直す file（root、manifest、`BuildManifestHash.txt`）と Scene の file を除いて、名前と sha256 で全て一致する。Scene の file が R と名前・sha256 で一致するかは、形ごとに record に書く。一致しない形は、AC5 の不合格にはせず、§2.5 の CONDITIONAL ACCEPT (iv) で読む（中身の版は AC1 が見る。印なしの出力と違う理由は、この batch では分からない）。(c) 印を入れた file を import した時、Scene を additive に開いた時（§5 の段 3）、印ありの build の間に、印なしの列（C1〜C3）に無い Error・Warning が出ない。
- **AC6（I1）範囲。** このブランチ（`claude/content-scene-stamp-spike`）の commit の差分が `docs/` の下だけである。record の project の path がこの repository の外で、record の開始時の棚卸しで、`Assets` の下が実験の script と root の型の file だけである（`Assets/OneStarMaker` が無い。この repository の複製や別の worktree を project にしていない）。GitHub Pages の更新（§0）は別のブランチの作業で、この条件の対象にしない。

### 2.3 ここでは答えない問いと所有する後続

- 選んだ方式の製品への実装、build が途中で落ちた時の戻し、開いている Scene・読み取り専用の file、印の形が Editor の source Play に影響しないこと: S2-2b（`CONTENT_PREFAB_STAMP`）。
- prefab variant・override を持つ instance・unpack した prefab、FBX そのもの（本スライスの model は `.obj`）、SampleGame の実データ: S2-2b の A1 が AC に入れるかを決め、入れない分は S5。
- 実 project（実データ）での上乗せ: S2-2b の A1 が AC に入れるかを決める（本スライスは合成の規模の fixture で測る。AC3b）。
- Player の build の差分 build、別の PC・`Library`・CI の agent をまたぐ cache: S3・S4（S2-2b の初稿 §2.3 と同じ送り先）。
- cache の鍵の仕組みの全体（Scene 以外の asset、cache の置き場と寿命）: 所有 program 未定。本スライスは、印の形の合否に要る範囲だけを読む。
- script の code を変えた時に検知の鍵が変わるか（batch の途中で compile と domain reload が入るので、1 process の batch では測らない）: S2-2b の A1。script の code による Scene の出力の変化は、もともと対象外（§1）。
- 製品の依存閉包を印に使う時の時点の問題（revision 1）: 製品の snapshot は adapter の `Refresh` より前にできる（§1）ので、Editor の外で変えた file が `Refresh` の前の閉包に反映されているか、印の値を `Refresh` の後に作るか、Scene の間で鍵の結果を使い回すかは、S2-2b の A1 が決める（本スライスは source を変えない規模の列で上乗せだけを測り、小さい列は版を切り替えて `Refresh` した後に閉包を作る）。
- 結論の範囲: 本スライスの判定は、batch の 1 process で採る。開いている Editor で build した時に F2 が Scene の読み直しを起こすか等は S2-2b が確かめ、そこで結論が覆りうることを harvest の文に書く。
- Unity への不具合報告を送るか: owner（S2-2 の追記のとおり、報告の材料は owner の再現 project にある）。

### 2.4 スライス分割の判断

- 契機: 独立して失敗しうる未知が 2 つある（印の形が成り立つか、検知の鍵が成り立つか）。
- 判断: 分割しない。検知の鍵（AC3）は build を伴わず、印の形（AC1・AC2・AC4・AC5）と別々に判定できる。片方だけが成り立った時の読みを §2.5 に定義してある。両方を同じ fixture・同じ batch で測るので、分けると Unity の起動と fixture の用意が 2 回になるだけである。

### 2.5 判定定義と停止規則

「印の形が成り立つ」は、その形が AC1・AC2・AC4・AC5 を全て満たすことをいう。「鍵が成り立つ」は、その鍵が AC3 (a)〜(d) を全て満たすことをいう。「build の鍵」は、§2.7 の選び方で build の列と規模の列に実際に使った鍵をいう。「全てを満たす形」は、成り立ち、AC3b が合格で、B6 の Scene の出力が基準 R と一致し（AC5 (b)）、AC2 (g) を働く指標で読めた印の形をいう。判定は、次の順に当てはめ、最初に当たったものにする（2 つ以上に同時に当たる読みを作らない）。

1. **未判定（owner へ）:** AC0 が成り立たない、batch が段 7 の中間の終了行まで動かなかった（段 1〜段 3 の確認で止まった時を含む）、または AC6 が成り立たない。
2. **GO・NO:** 成り立つ印の形が 1 つも無い、または成り立つ印の形の全てで AC3b が不合格。満たせなかった条件と観測を、条件ごとに対応づけて owner へ返す（3 条件のどれを見直すかは owner が決める）。AC3b の不合格で当たった時は、上乗せの内訳（計算、書き込み、`Refresh`、戻し、build の差）を返し、測った手順（受け取った依存閉包から Scene ごとに値を作る、書いて `Refresh`、戻して `Refresh`）と違う手順は測っていないと書く。
3. **GO・YES:** 全てを満たす形が 1 つ以上あり、build の鍵が §2.7 の (1)（public な K1 または K3 で、成り立ち、AC3 (e) の texture の変更でも material の変更でも値が変わらない）。推奨する組は、全てを満たす形のうち §2.7 の順で最初の物と、build の鍵。測った組そのものが 3 条件を同時に満たした時だけ、ここに入る。結論の文には「AC3b の合格は、印の計算と書き込みを製品の snapshot の後・adapter の `Refresh` の前に置き、その snapshot の閉包をそのまま使う場合の値である。閉包を `Refresh` の後に作り直す製品の形では、record の「依存閉包を作る所要」（製品の snapshot の所要ではなく、`GetDependencies` と除外だけの所要）を足して読み直す」を必ず書く（revision 1）。
4. **CONDITIONAL ACCEPT:** 上のどれでもない（成り立つ印の形があり、その中に AC3b が合格か未判定の形がある）。当たった項目の観測を並べて owner へ返し、owner が条件の読みを決める。AC3b の結果を書く時は、3 番と同じ前提の文を付ける。
   - (i) build の鍵が internal の K2、または全バイトの sha256（成り立つ鍵が無かった）。検知の代替は、K2 を reflection で使う、または Unity の API に依らない K4（§2.7）。
   - (ii) build の鍵が AC3 (e) で値が変わる（Scene の出力が変わらない編集でも Scene を作り直す。条件 1 を部分的に満たさない）。
   - (iii) AC3b が合格の形が無く、未判定の形がある（N のばらつき、または規模の段の未完了）。
   - (iv) AC3b が合格の形はあるが、全てを満たす形が無い（どの形も、B6 の Scene の出力が基準 R と一致しない、または AC2 (g) を読めていない）。

- 結論の文では、不成立と測った形・鍵と、測っていない形・鍵（§2.7）を書き分ける。測っていない物を不成立と書かない。代替として残す印の形は、その形自身の AC3b の結果を付けて書き、AC3b が合格の時だけ条件 2 を満たすと書く。
- 停止規則: 上の 4 つのどれかに決まれば終了する。未判定の時だけ、原因が実験の script の誤りと分かる場合に限り、batch を 1 回だけやり直してよい（§5）。それ以外の追加の実測は、owner の指示なしに行わない。

### 2.6 A3 後の例外承認・決定記録

- A3 後の例外承認: なし
- 決定記録: なし（revision 1 は、AC3b の測る中身を変えるので決定記録ではなく revision にした。§0 の owner の決定）

### 2.7 本文へ転記した実装制約

- **測る印の形（どちらも build の直前に text を書き、build の後に元のバイトへ戻す。Scene を Unity の API で開いて保存しない）:**
  - **M（`.meta` の `userData`）:** Scene の `.meta` の `userData` の行に印を書く。Scene 本体は触らない。`userData` に既に値がある時は、その値を残して後ろに足す（戻しは元のバイト）。
  - **F2（知らない key）:** Scene の file の末尾（最後の document である `SceneRoots`）に、Unity が知らない key の行を 1 つ足す（値は印）。object も fileID も足さない。戻しは file を元の長さへ切り詰める。Scene を全部読んで書き直さない。
  - **測らない形:** E（固定の fileID の GameObject を build の間だけ差し込む）は、M と F2 が先行確認で成り立ち、どちらよりも書く量と前提が多いので外した。Scene に何も書かない形（custom dependency）は先行確認で成り立たなかった（§1）。
  - **F（YAML のコメント行）は測らない。** 公開文書が非対応と明記しており（§1）、通っても製品の前提にできないため。
- **測る検知の鍵（Scene ごとに、その Scene を root とする依存閉包（下の「依存閉包の受け取り」）のうち、main asset が `GameObject` の物と `ModelImporter` が import した物を path の序数順に並べ、鍵の値を連ねてハッシュにする。各 Scene の印の入力に入った path の一覧と、`Model.obj` の main asset の型を record に書き、B6 が成り立たない時に、原因が印の形か印の入力かを分けて読めるようにする。段 1 で、各 Scene の印の入力が fixture から導いた一覧（`SOuter` は `Inner`・`Mid`・`Outer`、`SShared1`・`SShared2` は `Shared`、`SModel` は `Model.obj`、`SPlain` は無し）と一致することを確かめ、一致しなければ batch を止める）:** K1 `AssetDatabase.GetAssetDependencyHash`。K2 `GetSourceAssetFileHash` と `GetSourceAssetMetaFileHash`（internal、reflection）。K3 `AssetDatabaseExperimental.LookupArtifact` の結果の ID。build の列と規模の列は、次の順で最初に当たる鍵の値で印を作る（batch の中で段 2 の観測から自動で選び、選んだ鍵と当たった順位を record に書く）。(1) public な鍵（K1、K3 の順）で、AC3 (a)〜(d) を満たし、(e) の texture の変更でも material の変更でも値が変わらない物（片方だけで変わる鍵は (2)）。(2) K1、K3、K2 の順で、AC3 (a)〜(d) を満たす物。(3) どれも無い時は、prefab・model の file の全バイトの sha256（条件 2 を満たさないが、印の形の合否を鍵の不成立から切り離すため）。GO・YES になりうるのは (1) の時だけである。(2)・(3) の時は、§2.5 の 2 番（GO・NO）に当たらなかった時だけ、CONDITIONAL ACCEPT (i)・(ii) で読む。
- **依存閉包の受け取り（revision 1）:** 印の値を作る関数は、Scene ごとの依存閉包（path の列）を引数で受け取り、自分では `GetDependencies` を呼ばない。依存閉包を作る関数が呼ぶ Unity の API は `GetDependencies` と folder を除くための `AssetDatabase.IsValidFolder` だけで、型の判定（`GetMainAssetTypeAtPath`、`AssetImporter.GetAtPath`）、鍵（K1〜K3）、`Load*` を呼ばない（判断担当が両方を script の code で確かめる。AC3 の file の API の確かめと同じ。成り立たない時は AC3b の当てはめの 0 番）。依存閉包は、script の別の関数が、製品の `AssetDependencySnapshotBuilder.Build` と同じ規則（§1。Scene の path を root として `GetDependencies(root, true)`、`\` を `/` に、`Assets/` で始まらない path・`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref`・folder を除く、root を足す、path の序数順）で、印を書く build（X・B・T0〜T3）と N1〜N3 の各回に Scene ごとに作る。製品の assembly は実験の project に入れない（I1、AC6）。GUID の引き直しと issue の判定は、印の入力に使わないので写さない。印の値を作る関数は、revision 0 と同じく Scene ごとに型の判定と鍵の取得を行い、Scene の間で結果を使い回さない（使い回す手順は測らない）。依存閉包を作る所要は AC3b の上乗せに入れない（AC3b (b)）。
- **測らない検知の代替（結論の文に候補として残すだけ）:** K4 は、file の更新時刻と大きさが前回と同じなら前回の sha256 を使い、違う時だけ読む（状態は `Library` の下で、source に残らない。初回と `Library` を消した後は全バイトを読む）。K5 は `AssetPostprocessor` で prefab の保存を拾い、依存する Scene に印を付ける（状態を持ち、Editor の外で変わった file の取りこぼしを別に塞ぐ必要がある）。どちらも Unity の未確認の前提を持たないので、実測の対象にしない。
- **推奨する組の選び方（結果を見る前に固定する）:** 印の形は、§2.5 の「全てを満たす形」のうち、M、F2 の順で最初の物を推奨し、もう一方も成り立てば、その形自身の AC3b の結果を付けて代替として記録する。CONDITIONAL ACCEPT の時は、成り立つ形を同じ順に並べ、形ごとに欠けている点を付けて書く。M を先にする理由は 3 条件の外にある（M は Scene 本体を書かず、`userData` は API として公開された欄で、Scene の text の形式に依らない。F2 は文書に無い「知らない key を無視する」に依る）。条件 2 を M と F2 が同じに満たすかは測るまで分からないので、規模の列は成り立った形の両方で行う。検知の鍵は、build の鍵（上の選び方）をそのまま推奨する。
- **実験の置き場:** 実験の script と fixture は、実行担当が repository の外に新しく作る空の project（Unity 6000.6.0f1、package なし。先行確認と同じ作り）に置き、この repository に入れない。S2-2 の追記で使った owner の再現 project は使わない。script は build ごとの fixture を自分で作り、終わりに消す。
- 本スライスは public な型・メソッド、asmdef、製品の file を増やさない。

### 2.8 owner の決定と未決事項

owner は 2026-11-14 に次を決めた。未決事項は無い。

- **U1 目的・利用者の文言:** §1 の 2 欄を承認した。
- **U2 Unity の build を伴う実測:** §5 の 1 回の batch を実行してよい。project は repository の外に新しく作る空の project で、この repository の `Library` は使わない。実行は AI の session が行う。
- **U3 進め方の重さ:** slice のまま進める。A2 は 2 件（目的レビューと、実験の統制を見る反証レビュー。後者がアーキテクチャゲートを兼ね、「製品を変えない」ことと責務マップを見る）、B は script を書くだけ、C は batch の実行と record の判定、C' は script と record の blind 監査 1 巡。A3 の統合の後は凍結せず、draft PR の外部レビューを受けてから凍結する。
- **U4 program §8 の行:** このスライスを `S2-2b-0` として S2-2b の上に足し、S2-2b の行を「A1 初稿で保留、方式は S2-2b-0 の結論による」に替えた（A2 入力版の commit）。
- **U5 規模の fixture の数と、上乗せの合否の割合:** Scene 300、prefab 3,000（prefab の 3 分の 1 は入れ子）、Scene の file は 1 つ約 1 MB、上乗せの割合は 5%。想定は中規模のゲーム開発（§1）。
  - **instance の数（owner が確認した）:** owner が決めた時の案は「Scene 1 つが 30 個の prefab の instance を 200 個ずつ置く」（計 6,000 個）だった。先行確認で instance は 1 つ約 2.4 KB と分かり（§1）、6,000 個では Scene 1 つが約 14 MB（300 個で約 4.3 GB）になって「約 1 MB」と両立しない。本書は「約 1 MB」を残し、instance を 30 個の prefab × 14 個（計 420 個）に直した。owner は 420 個を承認した（理由: OSM は大きい Scene を分割して扱うことも狙う framework なので、Scene 1 つは約 1 MB の方が想定に合う）。
- **GitHub Pages:** 結論が出たら、既存の GitHub Pages を更新する（§0 の harvest to）。

## 3. 責務マップ

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `docs/handoff/CONTENT_SCENE_STAMP_SPIKE.md`（本書） | 計画、実測の結果、判定 | — | Phase D で harvest して削除 | — | — | — | 作業台 | 新規 |
| `docs/handoff/PLAY_CONTENT_RESOLUTION_PROGRAM.md` | §8 の行（§2.8 U4） | このスライスの追加と S2-2b の状態 | program | — | — | — | 既存 | 2 行 |
| `docs/unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md` | 実測の結果（Phase D の harvest。判定の前には書かない。事実の文書の「測り直し」の慣例に合わせ、実験の script の取得先も書く） | 印の形と検知の鍵の事実 | 永続 | — | 公開面 | — | 既存の同じ節 | 数行 |
| （repository の外）実験の Editor script 1 つ | fixture の作成、検知の観測、build の列、record | — | batch 1 回。git に入れず、script と record は evidence bundle で残す | `UnityEditor` | — | — | 再現 project。I1 | revision 0: 0 → 約 400（実績は約 950）。revision 1: 約 950 → 約 1,050（revision 0 の script を写して直し、依存閉包の関数と record の値の分だけ増える） |

- 行数警報: なし。依存の向き・asmdef・Runtime / Editor の境界は変えない。
- 実験の script は 1 file にまとめる（残さない道具なので、構造の分割をしない）。

## 4. 実装計画

- 順序: (1) fixture を作る code。(2) 検知の観測（build なし）。(3) 印の形 M・F2 の書き込みと戻し。(4) build の列と record。(5) script を project に置いた状態で compile が通ることを、Editor を batch で 1 回起動して確かめる（build は呼ばない）。
- revision 1 の順序: (1) revision 0 の evidence bundle の `StampSpike.cs` を、新しく作る実験の project（§2.7 の置き場と同じ作り）へ写す。(2) 依存閉包を作る関数と、閉包を受け取る印の値の関数に分ける（§2.7）。(3) §5 の段 2 の material の確かめ、段 5・段 8 の手順、record の値（§5 の revision 1 で足す値）を足す。(4) compile の確認（上の (5) と同じ。build は呼ばない）。fixture、build の列、判定の導出の行は、revision 1 で変えた条件に合わせる所の他は変えない。
- Phase B から Phase A へ差し戻す条件: §2.7 に無い印の形や鍵を足す必要が出た時、または build の回数が §5 の列を超える時。印の形が成り立たない等の結果は、差し戻さず record に残す（それが測りたい事実である）。
- 対象外を維持する方法: この repository の `unity/` を変えない（AC6）。

## 5. テストとレビュー計画

- 単体テスト: なし（製品の code を足さない）。
- 差し戻し中の起点 `-Filter`: なし。
- 判定必須テスト: 下の batch 1 回の record。
- 全 EditMode 回帰の適用除外: 適用しない。理由: この repository の実装を変えない（差分は文書だけ）。代替証拠: AC6 の `git diff --stat`。
- fixture（script が作り、終わりに消す）: 内側の prefab `Inner`（子の位置 v1 (1,2,3) / v2 (4,5,6)）、`Inner` を nest した `Mid`（自分の子 (10,11,12)。変えない）、`Mid` を nest した `Outer`（自分の子 (7,8,9)。変えない。入れ子は 2 段で、先行確認より 1 段深い）、共有の `Shared`（v1 (13,14,15) / v2 (16,17,18)）、model `Model.obj`（頂点 1 つの座標が v1 / v2 で違う。期待する頂点の列は、import した Mesh の頂点の値を Editor の API で読んで作り、`.obj` の text の値からは作らない。先行確認で import が x の符号を変えた。§1）、material と texture を持つ `Dep`（どの Scene にも置かない。AC3 (e) の観測だけに使う）。Scene は `SOuter`（`Outer` の instance）、`SShared1`・`SShared2`（`Shared` の instance）、`SModel`（`Model` の instance）、対照の `SPlain`（prefab なし）。各 Scene は、その Scene だけの位置を持つ目印の object を 1 つ持つ（`SOuter` (40,41,42)、`SShared1` (43,44,45)、`SShared2` (46,47,48)、`SModel` (49,50,51)、`SPlain` (20,21,22)）。出力の file と Scene の対応は目印の列で決め、`SShared1` と `SShared2` の出力が同じバイトにならないことも目印で保つ。v1 と v2 の file（`Dep` の material と texture を含む。revision 1）は、最初に 1 回ずつ Unity に保存させたバイトを控え、以後はそのバイトを書き戻して切り替える（「前の版に戻す」が同じバイトになるようにする）。
- 出力先: build ごとに、別の新しい空の directory を出力先にする（`Library` はそのまま使う。製品の build も、検証済みの作業用の folder へ出す）。build の前に出力先が空であることを record に書く。AC1・AC2・AC5 の「出力の file」は、その directory の file だけを指す。規模の列の出力は、名前と sha256 を record に書いた後に消してよい。
- 規模の fixture: Scene 300、prefab 3,000（3 分の 1 は入れ子。10 個の material を共有して参照する）、Scene 1 つが 30 個の prefab の instance を 14 個ずつ置き（計 420 個。先行確認の 1 つ約 2.4 KB から、Scene の file が約 1 MB になる数。§2.8 U5）、Scene の file は 1 つ約 1 MB。作った Scene の file の大きさを record に書く。texture は持たない（record に限界として書く）。割り当ては次のとおりに固定する: Scene i（0〜299）は、prefab の番号 (10i + k) mod 3,000（k は 0〜29）を置く（どの prefab も 3 つの Scene が置く）。Scene は、置く 30 種類のそれぞれを、位置を変えて 14 個ずつ置く。番号が 3 の倍数の prefab 1,000 個は、次の番号の prefab を nest する。prefab n は、material (n mod 10) を参照する Renderer を 1 つ持つ。作った後に、全ての Scene の `GetDependencies` から、どれかの Scene の依存閉包に入った prefab の種類の数、入れ子を持つ prefab の数、依存閉包に入った material の種類の数、Scene 1 つあたりの prefab の依存の数と instance の数と file の大きさの最小・中央・最大を record に書く。次のどれかが外れた時は、規模の block を行わない（どの形の AC3b も未判定になる）: 種類の数が 3,000、入れ子を持つ prefab が 1,000、material の種類が 10、どの Scene も instance が 420 個で file の大きさが 0.8 MB 以上 1.5 MB 以下。
- batch の順（Unity の process 1 つ。前の段が失敗しても、後の段へ進めるなら進めて record に残す。ただし段 1〜段 3 の確認（印の入力、棚卸しの一致）が成り立たない時は先へ進めず、中間の終了行も書かない。§2.5 の未判定）:
  1. **棚卸し:** `Assets` の下の全 file の path と sha256（AC6 の開始時の棚卸し）。小さい fixture を作り、作り終えた直後の棚卸しを「基準の棚卸し」として控える。各 Scene の印の入力を、fixture から導いた一覧と照らす（§2.7。build で使う依存閉包の関数と印の値の関数を通した結果で照らす。一致しなければ batch を止める）。
  2. **検知の観測（build なし、AC3）:** `Inner`・`Mid`・`Outer`・`Shared`・`Model` について K1〜K3 の値を、v1、`Inner` と `Model` を v2、v1 のバイトへ戻す、更新時刻だけ変える、の各時点で記録する。`Mid` と `Outer` の値が `Inner` の編集で変わるかも記録する（合否には使わない）。`Dep` について、texture のバイトを変えた時と material の値を変えた時の値を記録する（AC3 (e)）。material の値を変えた時は、変える前と、保存して `Refresh` した後の `Dep.mat` の sha256 を記録し、同じなら batch を止める（material の変更が file に落ちていない。revision 0 の §8 の (B) の 1 番目。止めた時は §2.5 の未判定で、script の誤りとして 1 回のやり直しの対象）。段の終わりに、prefab・model・`Dep`・texture・material を控えたバイトへ書き戻し、棚卸しが基準の棚卸しと一致することを確かめる（一致しなければ、以後の列の「v1」は控えた v1 ではないので、batch を止めて record に残す）。
  3. **印の読み取り（build なし、AC5 (c)）:** M と F2 のそれぞれについて順に、印を `SOuter` に書いて import し、Scene を additive に開いて、出た Error・Warning と root の数を記録し、保存せずに閉じて戻す（どちらの形の Error・Warning かを分ける）。戻しは `SOuter` の `.unity` と `.meta` の両方を控えたバイトへ書き戻し、棚卸しが基準の棚卸しと一致することを確かめる（同上）。
  4. **`CleanBuildCache()`** → **印なしの列:** C1（全て v1）→ C2（`Inner` だけ v2。AC0）→ C3（`Inner` を v1 へ戻し、`Model` を v2。model の編集が印なしで取りこぼされるか、頂点の列がどの file に入るかの観察で、合否には使わない）。
  5. **印の形 M の列:** 全て v1 へ戻し、**`CleanBuildCache()`** → X1（全て v1、印は固定の文字列）→ X2（`Inner` だけ v2、同じ固定の文字列。AC2 (f)）。全て v1 へ戻し、B1（全て v1）→ B2（`Inner` だけ v2）→ B3（変更なし）→ B4（`Inner` を v1 へ戻す）→ B5（`Inner` と `Shared` を v2）→ B6（`Model` を v2）。各回は、source の版を切り替える → `Refresh` → 棚卸し → 依存閉包を作る（§2.7）→ 印を書く → `Refresh` → build → 元のバイトへ戻す → `Refresh` → 棚卸し。`SPlain` には印を書かない。
  6. **印の形 F2 の列:** 段 5 と同じ（始めに全て v1 へ戻して `CleanBuildCache()`）。M の列が作った cache の entry の上で F2 を測らない。
  7. **`CleanBuildCache()`** → 基準 R（印なし、B6 と同じ source）→ **中間の終了行**を書く。
  8. **規模の列（AC3b）:** 小さい fixture を消し、規模の fixture を小さい fixture と別の folder に作り、作り終えた直後の棚卸しを控える。成り立った印の形ごとに、独立した block を行う: **`CleanBuildCache()`** → W（印なし、cache を温める）→ T0（その形の印ありの初回。作り直しになる。所要は記録だけ）→ N1 → T1 → N2 → T2 → N3 → T3。source はどの回も変えず、各 block の前の棚卸しが、控えた棚卸しと一致することを確かめる。T の各回は段 5 と同じ手順（棚卸し → 依存閉包を作る → 印を書く → `Refresh` → build → 元のバイトへ戻す → `Refresh` → 棚卸し）で、N の各回は、棚卸し → 依存閉包を作る（印は作らない）→ 変更なしの `Refresh`（T と同じ位置で測る）→ build → 棚卸し。規模の build も全て AC4 の対象である（棚卸しは所要に入れない）。各 build は別の空の出力先へ出す。別の形の block が作った cache の entry の上で測らない。成り立った印の形が無い時は、この段を行わない。
  9. fixture を消し、最後の棚卸しと終了行を書く。
- record が必ず持つ値: project の path、Unity の版、各 build の出力先と、build の前にそこが空だったこと、規模の fixture の照合の値（上の一覧）、選んだ検知の鍵と当たった順位、各 Scene の印の入力の path の一覧と `Model.obj` の main asset の型、段 2 の値の表、段 3 の観測、各 build の番号・印の形・source の版、印を入れた file の sha256（X の回は固定の文字列が残っていたか）、build の前後の棚卸しの差、各出力の file の名前と sha256、出力の全ての file の float 列の有無（S2-2 のスパイクと同じバイト走査）、書いた数と書いた file の ID（AC2）、`BuildMetaDataImporter(worker)` の event の `assetPath`、段 3 と各 build の間の Error・Warning、依存閉包を作る所要（T と N の各回。上乗せに入れない。製品の snapshot の所要ではなく `GetDependencies` と除外だけの所要と record に添える）、各 build の印の入力の一覧（小さい列は一覧そのもの、規模の列は一覧の hash）、印の値の計算（型の判定・鍵の取得・ハッシュに分けた値と合計）・書き込み・`Refresh`（N の回の変更なしの `Refresh` を含む）・戻し・build の呼び出しの所要、規模の Scene の file の大きさ、中間の終了行と終了行。各 build の `BuildContentTEP.json` は、その build の物であること（path、その `BuildHistory/<build>` の directory が build の前に無かったこと、build の開始より後の更新時刻）を record に書き、file そのものを record の隣に複写する（書いた数の元の記録）。revision 1 で足す値（revision 0 の §8 の (B) の 2 番目）: 印を入れた file の sha256 は 64 桁で書く（規模の列は 300 個をまとめた hash に加え、file ごとの一覧を record の隣の file に書く）。各 build の出力先の絶対 path。規模の fixture が texture を持たないという限界の 1 行。source の版を切り替えた時に出た Error・Warning（model の import の Warning を含む）を、その回の行に写す。段 2 の material の sha256（上）。規模の build の AC5 (a) の走査は、印の接頭辞を全ての出力の file で探し、印の値は T0〜T3 の全ての値を全ての出力の file で探す（revision 0 は値を 5 個に切っていた）。
- 統合・Unity テスト: 上の batch。この repository の `tools/run-tests.ps1` は使わない（対象がこの repository の project でないため）。
- 操作・実行時・目視条件の検証経路: 担当は Phase C の実行担当（AI の session）。環境は owner の PC、実験の project を開いている Editor が無いこと。revision 1 では、起動の前に他の `Unity.exe`（Editor と batch。Unity Hub と licensing の process は数えない）が動いていないことを確かめ、動いていれば 30 分まで待つ。30 分を過ぎても動いていれば、一覧を記録して起動する（判断担当が所要の読みに添える）。起動の前と終了行の後の Unity の process の一覧を実行の記録に書く（revision 0 は無関係な batch 3 本と重なり、所要に CPU の競合が入りえた。§7.2 の N4）。操作は `<Unity 6000.6.0f1 の Unity.exe> -batchmode -nographics -projectPath <実験の project> -executeMethod <script の入口> -logFile <log>`（先行確認と同じ起動）。観測は record の終了行と上の値。record・log・script を evidence bundle に入れ、判断担当と C' へ渡す。process の自然終了をゲートにしない（終了行の後も終わらなければ、その PID だけを止め、時刻と PID を実行の記録に残す。既知の終了 stall は `tools/Harness/README.md` の規則のとおり調査しない）。動作確認は `対象なし`。
- 未知の操作経路: 印（M・F2）が入れ子 2 段・共有の prefab・model・複数の Scene でも効くか、規模での上乗せ。小さい fixture での成立は先行確認済み（§1）。**未確認。初回確認はこの batch（段 4〜段 8）。** 理由: Phase A の主担当は Unity の build を行わない。不成立時: その印の形・鍵を不成立として record に残し、§2.5 で読む（基盤の不備ではなく、測りたい事実）。成立済み: repository の外の空の project で batch から Content Directory を build する経路、`.cf` のバイト走査、`BuildContentTEP.json` の読み取り（先行確認、S2-2 のスパイクと追記）。
- やり直し: 未判定で、原因が script の誤りと分かる時に限り 1 回。
- 人間の判断が必要な条件: §2.5 の CONDITIONAL ACCEPT の各項目の読み（owner）。
- 高コスト証拠: なし（小さい build 20 回と規模の fixture の build 最大 16 回（形ごとに 8 回）。規模の列の所要は fixture の数に依り、測るまで分からない。消す cache は実験の project の物だけ）。
- 判定 C の照合 script の方式: 持たない。判断担当が record を AC ごとに読み、期待値は上の fixture の値から導く。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー（revision 1）: 新しい session（Anthropic）。入力は本書の全節（revision 0 の §7.2・§8 を含む）、§0 の owner の決定、`AssetDependencySnapshotBuilder.cs`・`SceneResourceContentMaterializer.cs`・`BuildContentCoordinator.cs`・`UnityContentDirectoryAdapter.cs`（commit `88311fef`）。
- A0/A1 主担当・モデル・ベンダー（revision 0）: 本 session（Anthropic）。入力は owner の依頼（問い、3 条件、候補）、事実の文書の同じ節、commit `52612ede` の S2-2 の HANDOFF §7、S2-2b の A1 初稿、`UnityContentDirectoryAdapter.cs`、Unity 6000.6.0f1 の assembly と XML doc、Unity 6000.3 の manual。
- A2 の担当: 2 件とも Anthropic の系列で、A1 の主担当と別のモデルの新しい文脈に依頼する。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。Phase A では使わない。
- 独立性の強化条件を満たせない場合の理由: 対象外（risk は `normal`）。

### 5.1 A2 の独立レビューと A3 の採否

**A2（2026-11-14）。** 2 件とも Anthropic の系列で、A1 の主担当と別のモデル（Claude Opus、Agent の model 指定 `opus`）の新しい文脈。互いの指摘は渡していない。

| 観点 | 入力版 | 結果 |
|---|---|---|
| 目的レビュー | §1 の目的と利用者の 2 欄の原文だけ。sha256 `75845d2989d57207e15535c83038ffb0d471bf5364880255f36d9bd3e8fa03d0` | 保証すべきこと 5 項目、作らないべきもの 6 項目 |
| 実験の統制の反証とアーキテクチャゲート | A2 入力版の本書の全文。sha256 `6ac2e291e913601d6bbf9a192be8da7b09631aa1802661a02a2f0cd5b33b9801`（補助: commit `37ec408e`） | blocker 1、major 5、minor 5。I1 は実質破れず、I2 は破れた |

**A3 の統合（主担当: 本 session、Anthropic。この時点では未凍結）。** 2026-11-14 に owner が「A3 の統合まで進め、凍結せずに draft PR で外部レビューを受ける」と指示した。Phase A の commit は A2 入力版と A3 凍結版の 2 点が原則だが、外部レビューの読み手はリモートにしか居ないので、この統合版を owner の指示で 3 点目として commit した。

| # | 指摘（出所） | 採否 | 理由と反映先 |
|---|---|---|---|
| R1 | AC3b が判定定義に入っていない。「合計」の単位と、N のばらつきの時の判定が曖昧（反証、blocker） | 採用 | 条件 2 を規模で読むという owner の指定が判定に効いていなかった。§2.5 に AC3b を入れ、上乗せは 1 回分、全ての T の回で判定、ばらつきの時は CONDITIONAL ACCEPT (iii) |
| R2 | AC3b の測り方。build の所要の差を数えていない、規模で再利用を確かめていない、製品にもともとある `Refresh` を上乗せに数える、順が T に有利（反証、major） | 採用 | AC3b (a)・(b)、§5 の段 8（N と T を交互に 3 回ずつ） |
| R3 | 作り直しても出力は同じ sha256 になるので、AC2 が再利用を確かめていない。印の値と書いて戻す操作を分ける対照が無い。変わっていない Scene の再利用を見ていない。S2-2 の記録の「作り直し 0」の統計が不明（反証、major） | 採用（一部は形を変えた） | 書いた数を数えられる値を追加の確認で特定した（§1。S2-2 の記録に統計の名前は無かった）。AC2 を書いた数で判定し、(c)・(d) で回ごとの数を固定、(f) に印の値を固定した対照 X1・X2 を足した。build cache の置き場の file の一覧を控える案は、書いた数が直接の観測になったので採らない |
| R4 | 鍵の自動選択が AC3 (a) しか見ず、鍵の不成立が印の形の不合格になる。全て不成立の時の扱いが無い（反証、major） | 採用 | §2.7（(a)〜(c) を満たす最初の鍵、無ければ全バイトの sha256 で build の列だけ流す） |
| R5 | `Model` が build の列で一度も変わらず、model の編集を確かめていない。頂点は Scene の file に入らない見込み（反証、major） | 採用 | C3（印なしの観察）と B6、AC1 の B6 は出力の全ての file を走査する。B5 から model を分けたのは、AC2 (c) の書いた数を固定するため |
| R6 | 検知の鍵が prefab 以外の依存（material・texture・script）にも追従し、不要な作り直しを起こしうる（反証、major、推測を含む） | 採用（script を除く） | AC3 (e) と §2.5 の CONDITIONAL ACCEPT (ii)。script の code の変更は batch の途中で domain reload が入るので測らず、§2.3 で S2-2b へ送った。規模の fixture は material を持たせ、texture を持たないことを限界として書く |
| R7 | 規模の段が終わらないと全体が未判定になる（反証、minor） | 採用 | 段 7 の中間の終了行、§2.5 の CONDITIONAL ACCEPT (iii) |
| R8 | 段 3 の観測がどの AC にも使われていない。印による Error・Warning を合否に入れる（反証、minor） | 採用 | AC5 (c) |
| R9 | AC6 の文面が GitHub Pages の更新と合わない。path だけでは、この repository の複製を project にしても通る（反証、minor） | 採用 | AC6（このブランチの commit、開始時の棚卸し） |
| R10 | AC2 の sha256 を採る時点、AC5 で Scene の file が R と違う時の扱い（反証、minor） | 採用 | AC2 (e)、AC5 (b) |
| R11 | 結論の文に「batch での判定」の範囲を書く（反証、minor） | 採用 | §2.3 |
| R12 | harvest の時に実験の script の取得先を書く（反証、補足） | 採用 | §3 |
| P1 | 候補の合否は 3 条件のそれぞれに対して実測で示し、文書や推測だけで決めない（目的） | 採用 | 外した形（コメント行、差し込む GameObject）と鍵（K4・K5）は「測っていない」と書き、不成立と書かない（§2.5、§2.7） |
| P2 | 3 条件と関係のない比較の軸で順位を付けない（目的） | 一部採用 | 判定は 3 条件だけで行う。M と F2 の両方が成り立った時の推奨の順は残す（S2-2b は 1 つを選ぶ必要があり、理由は製品が依る前提の少なさ）。3 条件の外の理由であることと、両方の結果を書くことを §2.7 に明記した |
| P3 | S2-2b の本体、製品向けの入力・設定、汎用の検証基盤を作らない。条件を緩めない（目的） | 採用（初稿のまま） | §1 の対象外、§3（残さない script 1 つ）、§2.5（条件の見直しは owner） |
| P4 | 結論は、条件ごとに事実を対応づけ、S2-2b の A1 がそのまま使える形にする（目的） | 採用 | §2.5 の GO・NO、§0 の harvest to |

保留は無い。

**外部レビュー 1 巡目（2026-11-14、draft PR #191、cursor[bot]。対象は A3 の統合版の commit `8259fb0c`）。** 総括 1 件とインライン 4 件（全て「重大」）。全て採用した。

| # | 指摘 | 採否 | 理由と反映先 |
|---|---|---|---|
| E1 | GO・YES が、build に使った鍵と、AC3 (e) を満たす別の鍵を足し合わせて成立する。鍵の選びが (d)・(e) を見ないので、鍵の不成立が印の形の不成立になる経路も残る | 採用 | §2.7（build の鍵は、(a)〜(d) を満たし (e) で変わらない public な鍵を最初に選ぶ）、§2.5（GO・YES は、測った組そのものが全てを満たした時だけ） |
| E2 | 印の入力の filter（main asset が `GameObject`）に model が入らないと、B6 の失敗が印の形の不成立に見える | 採用 | §2.7（`ModelImporter` が import した物を入力に含め、入力の一覧と main asset の型を record に書く） |
| E3 | GO・NO と CONDITIONAL ACCEPT が同時に真になる。N のばらつきだけで、上乗せが明らかに大きくても CONDITIONAL に入る | 採用 | §2.5（上から順に当てはめる 4 つ）、AC3b（厳しい読みと緩い読みで、合格・不合格・未判定を分ける） |
| E4 | 規模の列が推奨の形だけで、M が落ちると F2 を測らないまま「無い」になる。「3 条件はどちらも同じに満たす」は条件 2 について未測定 | 採用 | AC3b と §5 の段 8（成り立った形の両方を測る）、§2.7（その文を削った） |
| E5 | AC2 が B6 の書いた数を見ておらず、model だけを変えた時に無関係な Scene を作り直しても通る | 採用 | AC2 (g)（総数は固定せず、無関係な Scene を書いていないことを見る） |
| E6 | AC2 (f) が、固定の文字列が build の直前まで残っていることを見ていない | 採用 | AC2 (f) |
| E7 | 段 2 の後に控えた v1 へ戻す行が無い。段 3 の戻しが印を書いた 1 file だけ | 採用 | §5 の段 1〜段 3（基準の棚卸しと、段ごとの一致の確認） |
| E8 | 印の形と形の間、規模の列の前に `CleanBuildCache` が無い。規模の fixture の path が小さい fixture と重なりうる | 採用 | §5 の段 5・段 6・段 8、§2.1 の I2 |

**外部レビュー 2 巡目（2026-11-14、draft PR #191、cursor[bot]。対象は 1 巡目の修正の commit `d5b60674`）。** 総括 1 件とインライン 5 件（重大 4、中 1）。全て採用した（G2 は形を変えた）。

| # | 指摘 | 採否 | 理由と反映先 |
|---|---|---|---|
| G1 | §2.7 の「(2)・(3) は CONDITIONAL ACCEPT」が、§2.5 の順（先に GO・NO）と食い違い、全バイトを読む鍵のまま成功側に残る読みができる。「(e) で値が変わらない」が texture と material の両方かが書いていない | 採用 | §2.7（GO・NO に当たらなかった時だけ (i)・(ii)。(1) は両方で変わらない鍵）、AC3 (e)、§2.5 の GO・YES |
| G2 | T3 の前で規模の列が止まると、不合格と未判定の両方に入る。緩い読みが割る値にも N の最大を使い、遅い N が 1 回あるだけで大きい上乗せが未判定になる | 採用（引く値は中央値にした） | AC3b（順に当てはめる 6 つ。T3 まで届かない形は未判定だけ。割る値は N の最小に固定）。指摘の案は「緩い読みの引く値だけ最大」だが、指摘の例（N が 10・10・20 秒、T が 12 秒）では差が 0 に潰れたままで、指摘が書く「不合格」にならない。中央値にすると、その例は不合格になる |
| G3 | 規模の列が、1 本の N の列に M と F2 の T を挟む。F2 の T は M の直後に走り、F2 の再利用が M の cache で通りうる | 採用 | §5 の段 8（形ごとに `CleanBuildCache` から始まる独立した block、出力は上書きの前に記録）、build の上限を 16 回に直した |
| G4 | AC2 (g) の ID が build ごとの番号なら、無関係な Scene の作り直しがまた通る | 採用 | AC2 (g)（`BuildMetaDataImporter` の path を指標にし、ID は build をまたいで同じと確かめた時だけ使う。指標が働くことを B2・B5 で確かめる）。先行確認では ID は build をまたいで同じだった（§1 に足した）が、batch で確かめ直す |
| G5 | AC2 (f) の「file」が M の `.meta` を指していない | 採用 | AC2 (f) |
| G6 | 段 2・段 3 の「止める」が、段の先頭の「進めるなら進める」と食い違う。終了行を書いて止めると、汚れた source の部分結果が判定の対象になる | 採用 | §5 の batch の先頭、§2.5 の未判定 |

**主担当の自己点検（2026-11-14、owner の依頼。Phase C・C' で指摘されうる穴を、2 巡目の指摘と別に探した）。** build を伴わない確認を 1 回行った（§1 の「規模の fixture と model の先行確認」）。

| # | 見つけた穴 | 反映先 |
|---|---|---|
| Z1 | 規模の fixture の「instance 6,000 個」と「Scene 1 つ約 1 MB」が両立しない（実測で 1 つ約 2.4 KB。6,000 個は約 14 MB） | §2.8 U5（owner が 420 個を承認した）、§5 の規模の fixture |
| Z2 | model の頂点の期待値を `.obj` の text から作ると外れる（import が x の符号を変えた）。頂点は Scene と別の file に書かれうるのに、AC1 が B6 以外で Scene の `.cf` を見ていた | §5 の fixture、AC1 |
| Z3 | model の頂点の編集が印なしでも取りこぼされないなら、B6 は印が無くても通る | AC1 の「B6 の読み」（C3 と合わせて読み、測っていない範囲を結論に書く） |
| Z4 | 規模の列で、N が再利用になっていないと上乗せが小さく見える | AC3b (a2) |
| Z5 | 出力の file と Scene の対応の決め方が無い。`SShared1` と `SShared2` の出力が同じバイトになりうる | §5 の fixture（Scene ごとの目印）、§2.2 の前置き |
| Z6 | AC5 (b) の「Scene の file が R と違う時は理由を書く」は合否が決まらない | AC5 (b)、§2.5 の「全てを満たす形」と CONDITIONAL ACCEPT (iv) |
| Z7 | 印の入力に model が入っていない時、B6 の不成立が印の形の不成立として GO・NO になる | §2.7 と §5 の段 1（fixture から導いた一覧と照らし、違えば止める） |
| Z8 | 「入れ子 2 段」と書いたが、fixture の入れ子は先行確認と同じ 1 段だった | §5 の fixture（`Mid` を足した）、AC1、段 2 |
| Z9 | 票の「Unity の実行は Phase C まで行わない」が、§4 の Phase B の compile の確認と食い違う | §0 の票 |
| Z10 | AC4 の「build の前」が、版の切り替えの前か印を書く前かが決まらない。棚卸しを所要に入れるかが決まらない | AC4、AC3b (b)、§5 の段 5 |
| Z11 | 書いた数の元の記録（`BuildContentTEP.json`）が evidence に残らず、その build の物かも確かめていない | §5 の record |
| Z12 | AC3b の不合格による GO・NO が、測った手順（書いて `Refresh`、戻して `Refresh`）以外も不成立と読める | §2.5 の GO・NO |
| Z13 | 段 3 で M と F2 を同時に書くと、Error・Warning がどちらの形の物か分からない | §5 の段 3 |

**外部レビュー 3 巡目（2026-11-14、draft PR #191、owner が依頼した別の agent。対象は commit `719ea649`）。** 総括 1 件とインライン 2 件。両方採用した。Unity の実測は行っていないレビューである。

| # | 指摘 | 採否 | 理由と反映先 |
|---|---|---|---|
| H1 | AC1・AC5 は「出力の全ての file」を見るが、出力先に前の build の file が残らない保証が無い。古い版の file が残ると AC1 が偽の不合格になり、model の版を全 file から探す判定は偽の合格にもなる | 採用 | §5 の出力先（build ごとに別の空の directory。先行確認の script はそうしていたが、計画に書いていなかった）、§2.2 の前置き、record |
| H2 | 規模の fixture は、prefab 3,000 個が実際に Scene の依存閉包に入ることを保証していない。全 Scene が同じ 30 個を置いても要件を満たしたように読め、5% の結果の母集団が想定と違う | 採用 | §5 の規模の fixture（割り当ての規則、作った後の照合。合わなければ規模の block を行わない） |

**外部レビュー 4 巡目（2026-11-14、draft PR #191、cursor[bot]。対象は commit `006334ba` のうち、`d5b60674` より後に書き換えた所）。** 総括 1 件とインライン 3 件（重大 2、中 1）。採用した（J2 の一部は形を変えた）。

| # | 指摘 | 採否 | 理由と反映先 |
|---|---|---|---|
| J1 | 規模の割り当ての規則が「14 個ずつ」と「10 個の material」を運んでいない。照合も見ないので、約 72 KB の Scene のまま 5% の判定へ進める | 採用 | §5 の規模の fixture（14 個ずつ置く規則、material の割り当て、instance の数・file の大きさ・material の種類の照合。外れたら block を行わない） |
| J2 | AC2 (g) の path の指標は、B6 の event が 0 件でも通る。ID の指標の禁止の集合に `SPlain` が無い。見出しの「7 つ全て」と、(g) が読めない時の扱いが食い違う | 採用（`SPlain` の ID は形を変えた） | AC2 の見出しと (g)。path の指標は、B6 の event に `SModel` の path がある時だけ使う。指摘の案は「B1 で書いた `SPlain` の ID を禁止の集合に足す」だが、`SPlain` は印を書かないので B1 では書かれず（X1 の出力を再利用する）、足せる ID が無い。ID の指標は `SPlain` を見ないと書き、path の指標が働かない時はその旨を record に残す |
| J3 | 段 8 の T の手順が、段 5 の 2 回の棚卸しを落としている。規模の build の後に印が残っても AC4 が見ない | 採用 | §5 の段 8（T も N も棚卸しで挟む。規模の build も AC4 の対象） |

**外部レビュー 5 巡目（2026-11-14、draft PR #191、owner が依頼した別の agent。対象は commit `2c657c6b` のうち、`719ea649` より後に書き換えた所）。** 指摘なし。偽の合格・偽の不合格・実行できない手順の反例は見つからなかった、との結果だった。Unity の実測は行っていないレビューである。

**A3 の凍結（2026-11-14、revision 0）。** owner は 5 巡目の結果を見て凍結を承認した。採用・不採用・保留の分類は上の表のとおりで、保留は無い。

### 5.2 revision 1 の Phase A

**再開の理由と範囲。** §0 の owner の決定のとおり。3 条件、規模の fixture、build の列と回数、AC0〜AC6 の合否の基準（5% を含む）、§2.5 の判定の順は変えない。

**A1 の変更一覧（revision 0 の A3 凍結版 commit `89604c70` からの差。revision 0 の条件は、§7.2・§8 を読むためにその commit の §2・§5 を使う）。**

| # | 変えた所 | 内容 | 出所 |
|---|---|---|---|
| V1 | AC3b (b)、§2.7 の「依存閉包の受け取り」と検知の鍵の項、§5 の段 5・段 8 | 印の値は、製品と同じ規則で作った Scene ごとの依存閉包を受け取って作る。閉包は T と N の両方で build ごとに同じ位置で作り、その所要は上乗せに入れない。印の値を作る関数は `GetDependencies` を呼ばない | owner の決定 |
| V2 | AC3b (b)、§5 の record | 計算の所要を、型の判定・鍵の取得・ハッシュに分けて書く | §7.2 の N3 |
| V3 | AC3b (a)・(a2) | 出力の比べ方を `.cf` と `.resS` の（名前, sha256）の集合と書いた。revision 0 の判断担当と C' が採った読みと同じで、合否の基準は変えていない | §8 の (B) の 3 番目、§6 の B 適応 |
| V4 | §5 の段 2 | material の変更が `Dep.mat` のバイトに落ちたかを確かめ、落ちていなければ止める | §8 の (B) の 1 番目 |
| V5 | §5 の record | 印の file の sha256 を 64 桁で、規模の列は file ごとの一覧も、出力先の絶対 path、texture の限界の 1 行、版の切り替えの時の Error・Warning、規模の印の値を全て走査、TEP の directory が build の前に無かったこと | §8 の (B) の 2 番目、§8 の残存リスク（TEP） |
| V6 | §5 の検証経路 | 起動の前に他の Unity の process が無いことを確かめ、前後の一覧を記録する | §7.2 の N4 |
| V7 | §1、§2.3 | 製品の依存閉包の作り方と revision 0 の結果を現況に足した。閉包の時点と使い回しを S2-2b へ送った | 本 session の確認 |

**取り込まなかった revision 0 の (B) と理由。**
- model の頂点が印なしでも出力に入る（§8 の (B) の 4 番目）: AC1 の「B6 の読み」が既にその読み方を定めている。変える条件は無い。
- `Refresh` を製品の 1 回にまとめる等の、上乗せを下げる別の手順（§8 の (B) の 5 番目のうち依存閉包以外）: owner の決定の範囲の外。測らない手順として結論の文に残す（§2.5 の GO・NO）。
- AC0 が落ちても script が止まらない（§8 の残存リスク）: §2.5 の 1 番が未判定として扱うので、止めなくても読みは変わらない。
- 規模の `.cf` の全ての突き合わせ、出力の生バイトの保存（§8 の残存リスク）: 規模の出力は大きく、AC3b (a)・(a2) は（名前, sha256）の集合と書いた数で閉じる。

**revision 1 で結果が変わりうる所の見込み（判定の基準ではない）。** revision 0 の計算を除く残りは、M が 4.2〜6.4%、F2 が 4.5〜11.2% だった（§1）。型の判定と鍵の取得の所要は revision 0 で分けて測っておらず、合格・不合格・未判定のどれにもなりうる。

**目的レビュー:** 行わない。§1 の目的・利用者の 2 欄を revision 1 で変えていない（revision 0 の A2 の目的レビューの入力 sha256 `75845d2989d57207e15535c83038ffb0d471bf5364880255f36d9bd3e8fa03d0` の原文のまま）。

**A2（revision 1、2026-11-14）。** 実験の統制の反証とアーキテクチャゲートを兼ねる 1 件。入力版は A2 入力版の commit `ff80a6a0` の本書（sha256 `87af6c61bf50ada9fa913d87d09f27c096f11587de77a61d8391f987e68085bf`）と製品の 4 file。担当は A1 の主担当と同じモデルの新しい文脈（Agent）で、独立性の制約として記録する（高コストのモデルへ上げない方針。risk `normal`）。結果は major 3、minor 5。§1 の製品の記述は正しいと確かめた（script の規則との差は、拡張子の比べ方の大文字小文字と `FileExists` で欠けた path を落とすことで、この fixture の印の入力の集合は変わらない）。I1・I2 を revision 1 の変更で破る順序は見つからなかった。

**A3 の統合（revision 1、主担当。未凍結）。**

| # | 指摘 | 採否 | 反映先 |
|---|---|---|---|
| W1 | 型の判定や鍵の取得が「依存閉包の所要」の側へ漏れても検出できない。逆に計算の関数が閉包を作り直すと二重に数える（major） | 採用 | §2.7（閉包の関数が呼ぶ API を限り、判断担当が両方の関数を code で確かめる）、AC3b の当てはめの 0 番（未判定） |
| W2 | 測った上乗せは、印を製品の snapshot の後・adapter の `Refresh` の前に置く前提の値で、合格の読みがその前提を運ばない（major） | 採用 | §2.5 の 3 番・4 番（結論の文に前提を書く）、§0 の harvest to |
| W3 | 段 2 の material の止める条件が、revision 0 の書き方の不備で初回に当たり、1 回のやり直しを使い切りうる（major） | 採用 | §5 の fixture（`Dep` の material と texture も控えたバイトで切り替える）。止める条件は残す |
| W4 | 段 1 の照合が新しい閉包の関数を通るか 2 通りに読める（minor） | 採用 | §5 の段 1、record（各 build の印の入力の一覧） |
| W5 | 計算の合計と内訳の関係が決まっていない（minor） | 採用 | AC3b (b)（外側の計測を上乗せに入れ、内訳は参考。loop は変えない） |
| W6 | record の閉包の所要は製品の snapshot の所要ではない（minor） | 採用 | record、§2.5 の前提の文 |
| W7 | 「他の Unity の process」の範囲と待つ上限が無い（minor） | 採用 | §5 の検証経路（`Unity.exe` に限り、30 分まで待ち、過ぎたら記録して起動） |
| W8 | §3 の行数の予想が古い（minor） | 採用 | §3 |

保留・不採用は無い。

**A3 の凍結（2026-11-14、revision 1）。** owner は統合版を見て凍結を承認した。

## 6. Phase B 実装結果

2026-11-14。§4 の順序 (1)〜(5) のとおり、実験の Editor script を 1 つ書き、compile を確認した。この repository の差分は本書（§0 と §6）だけで、製品・SampleGame・`unity/` は変えていない（I1、AC6）。implementation head 欄は、repository の実装差分が無いので、A3 凍結版の commit `89604c70` を指す（そこから HEAD までの差分は HANDOFF だけ）。実験の script が実装の本体で、取得案内は §0 のとおり。

**書いたもの（repository の外。git に入れていない）。** 空の project（Unity 6000.6.0f1、package なし）に `Assets/Spike/Root.cs`（root の型）と `Assets/Spike/Editor/StampSpike.cs`（約 950 行。§3 の予想の約 400 行より大きい。段ごとの record の出力と、record 用の判定の導出が行を使った。構造は分けていない）。入口は 2 つ: `StampSpike.Run`（§5 の batch の段 1〜段 9、1 process）と `StampSpike.CompileCheck`（compile の確認だけ。build を呼ばない）。

**確認したこと。**
- Editor を batch で 1 回起動し、`StampSpike.CompileCheck` が compile を通って終了行を書くことを確かめた（Unity 6000.6.0f1。compile の error 0）。
- `pwsh tools/contract-audit.ps1` は errors=0、warnings=0。`pwsh tools/docs-audit.ps1` は errors=0（warning 1 件は program の §7(C)・§8(C') の既存の指摘で、本スライスと無関係）。
- **未実行:** `StampSpike.Run`（Unity の build を伴う全ての段）。Phase B は build を行わない（§4）。

**B 適応（凍結した条件の意味は変えていない）。**
- 規模の列（段 8）を行う印の形は、script が段 5・段 6 の record から自動で決める。「成り立つ」の導出は AC1、AC2、AC4、AC5 (a)・(c) で、AC5 (b)（印ありの出力と基準 R の出力の一致）は導出に入れない。AC5 (b) は Scene の file の一致が合否に入らず（§2.2）、build ごとに作り直す file の見分け方が出力の拡張子（`.json`、`BuildManifestHash.txt`）に頼るためである。AC5 (b) で .json・`BuildManifestHash.txt` を除く非 Scene の file に差があれば、script はその一覧を record に書き、判断担当が読む。規模の block は、AC5 (b) だけを理由に省くより、測って判断担当に渡す方が、build の上限（16 回）の中で情報が増える。
- 規模の列の「Scene の出力が T0 と同じ名前・同じ sha256」（AC3b (a)・(a2)）は、規模では目印の列で Scene の file を見分けられない（Scene 300 個を全 file に当てる走査は重い）ので、`.cf` と `.resS` の file の (名前, sha256) の集合の一致で測る。`.json` の manifest と `BuildManifestHash.txt` は build ごとに作り直すので除く。判断担当は、出力の一覧（`record/outputs/`）から読み直せる。
- script が record に書く `ac1`・`ac2`・`ac2g`・`ac3`・`ac4`・`ac5`・`ac5b`・`ac3b` の行と `holds` は、判断担当が読み直せる補助の導出値で、合否の裁定ではない（§5 の「判定 C の照合 script の方式: 持たない」を変えていない）。
- `Dep` と規模の prefab の Renderer は `MeshRenderer`（`MeshFilter` なし）にした。material の参照だけが要るため。material の shader は `Standard`（無ければ `Sprites/Default`）。
- 検知の観測（段 2）の鍵 K2 は reflection（`GetSourceAssetFileHash`・`GetSourceAssetMetaFileHash`）、K3 は `ArtifactKey` と `LookupArtifact` の reflection で呼ぶ（先行確認と同じ呼び方）。

**Phase C へ渡すこと。**
- 起動: `<Unity 6000.6.0f1 の Unity.exe> -batchmode -nographics -projectPath <実験の project> -executeMethod StampSpike.Run -logFile <log>`。実行の前に、実験の project の `record/` と `Out/` を消す（`Out/` に前回の出力が残ると、script は出力先が既にあるとして build を止める）。`Library` は残してよい（段 4 の前に `CleanBuildCache()` を呼ぶ）。
- script は `Run` の最後に `EditorApplication.Exit(0)` を呼ぶ。終了行は `END`、止めた時は `HALT`、想定外の例外は `EXCEPTION`。中間の終了行は `MIDEND`（段 7 の後）。record は 1 行ごとに追記するので、途中で process が落ちても、そこまでの行が残る。
- script が `HALT` で止まる条件は 4 つ: project が repository に見える（AC6）、fixture が v1 でない、各 Scene の印の入力が fixture から導いた一覧と違う（段 1）、段 2・段 3 の後の棚卸しが基準と違う。規模の fixture が §5 の数に合わない時は `HALT` でなく `scale fixture gate false` を書いて規模の block だけを行わない。
- 規模の fixture の作成は、prefab 3,000 個と Scene 300 個を Unity の API で作る。所要は測っておらず、段 8 の最初の `scale prefabs created` と `scale fixture created` の行に ms が出る。

### 6.1 revision 1 の Phase B 実装結果

2026-11-14。§4 の「revision 1 の順序」(1)〜(4) のとおり、revision 0 の script を新しい実験 project へ写して直し、compile を確認した。この repository の差分は本書（§0 と §6.1）だけで、製品・SampleGame・`unity/` は変えていない（I1、AC6）。implementation head 欄は、repository の実装差分が無いので、A3 凍結版（revision 1）の commit `9197e907` を指す。

**書いたもの（repository の外。git に入れていない）。** `D:\repositories\unity\stamp-spike-r1`（Unity 6000.6.0f1、package なし。`ProjectSettings` と `Packages/manifest.json` は revision 0 の project から写し、`Library` は無かった）。`Assets/Spike/Root.cs`（revision 0 と同じ）と `Assets/Spike/Editor/StampSpike.cs`（1,078 行。revision 0 は 951 行）。入口は `StampSpike.Run` と `StampSpike.CompileCheck`（revision 0 と同じ）。元にした revision 0 の script は、evidence bundle の記録（sha256 `297574b1…`）と一致する手元の file を写した（bundle の再 fetch は、sha256 が実行前の記録と同じなので省いた）。

**revision 1 で変えたこと（§5.2 の V1〜V6 との対応）。**
- V1: 依存閉包を作る関数 `Closure`（Scene の path を root として `GetDependencies(root, true)`、`\` を `/` に、`Assets/` で始まらない path・`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref`・folder を除く、root を足す、序数順。製品の `AssetDependencySnapshotBuilder.Build` と同じ規則）と、閉包を引数で受け取る `StampInputs(scene, closure)`・`Stamp(scene, closure)` に分けた。`Closure` が呼ぶ Unity の API は `GetDependencies` と `IsValidFolder` だけ。`StampInputs`・`Stamp` は `GetDependencies` を呼ばず、型の判定（`GetMainAssetTypeAtPath`・`AssetImporter.GetAtPath`）と鍵の取得（K1〜K3）は `StampInputs` と `Stamp` の中だけにある。印の値は Scene ごとに作り、Scene の間で結果を使い回さない。閉包は、印を書く build（X・B・T0〜T3）と N1〜N3 の各回に、棚卸しの後・印を書く前に作り、所要を `closure-ms` として record に書く（上乗せに入れない）。段 1 の印の入力の照合も `Closure` を通す（各 Scene の閉包も record に書く）。
- V2: 計算の外側の所要（`ComputeMs`、上乗せに入れる値）に加え、型の判定・鍵の取得・ハッシュを、Scene ごとの loop の中で累計して `compute breakdown` として record に書く（参考値）。loop の構造は変えない。
- V4: `CreateSmall` で `Dep.mat` の v2 も Unity に保存させたバイトを控え、以後は texture と同じくバイトを書き戻して切り替える。v1 と v2 が同じなら fixture の段で止める。段 2 は、変える前と、書いて保存・`Refresh` した後の `Dep.mat` の sha256 を record に書き、同じなら止める。
- V5: 印を入れた file の sha256 を 64 桁で書き、規模の列は file ごとの一覧を `record/stamped/` に書く。各 build の出力先の絶対 path、規模の fixture が texture を持たないという限界の 1 行、版の切り替えの時に出た Error・Warning（`SWITCH-LOG`）、TEP の directory が build の前に無かったか（`tepDir=`）を足した。規模の AC5 (a) は、全ての出力の file と file 名から集めた 32 桁の 16 進の列を、block の終わりに T0〜T3 の全ての印の値と突き合わせる（`ac5a scale`）。
- V3・V6: V3（AC3b (a)・(a2) の比べ方）は revision 0 の script のとおり（`.cf` と `.resS` の（名前, sha256）の集合）。V6（他の Unity の process の確認）は script の仕事ではなく、実行担当の手順（§5 の検証経路）。

**確認したこと。**
- 新しい project で Editor を batch で 1 回起動し、`StampSpike.CompileCheck` が compile を通って終了行を書くことを確かめた（error 0、warning 0）。起動の前に他の `Unity.exe` は動いていなかった。確認の record は消した（`record/` は無い状態で引き渡す）。
- script の code で、`Closure` が `GetDependencies` と `IsValidFolder` 以外の Unity の API を呼ばないこと、`StampInputs`・`Stamp` が `GetDependencies` を呼ばないことを読んで確かめた（`GetDependencies` は他に規模の fixture の照合（`CreateScale`）にだけある）。最終の判断は判断担当が code で行う（AC3b の当てはめの 0 番）。
- **未実行:** `StampSpike.Run`（Unity の build を伴う全ての段）。Phase B は build を行わない（§4）。

**B 適応（凍結した条件の意味は変えていない）。** 規模の AC5 (a) は、出力を消す前に集めた 16 進の列で突き合わせる（出力を block の終わりまで残さないため。印の値は 32 桁の `Hash128` で、32 桁以上の 16 進の列の全ての 32 桁の窓を集める）。それ以外は §6 の B 適応のとおり。

**Phase C へ渡すこと。**
- 起動: `<Unity 6000.6.0f1 の Unity.exe> -batchmode -nographics -projectPath D:\repositories\unity\stamp-spike-r1 -executeMethod StampSpike.Run -logFile <log>`。他は §6 の「Phase C へ渡すこと」のとおり（`record/` と `Out/` を消す、`Library` は残してよい、終了行は `END`・`HALT`・`EXCEPTION`・`MIDEND`）。
- `HALT` で止まる条件に、`Dep.mat` の変更が file のバイトに落ちていない時（fixture の作成と段 2）が加わった。

## 7. Phase C

### 7.1 発見 C の実行（batch 1 回）

- 種別: 発見 C の実行担当（判断はしない。AC の合否と §2.5 の読みはこの節に書かない）。
- 引き継いだ commit: `c2947bd8`（HEAD と一致を確認）。implementation head は `89604c70`。
- 使用した evidence bundle の取得案内・状態: §0 と同じ案内（ローカル固定済み（未昇格））。収録は 117 file: `record/`（`record.txt`、`outputs/`、`tep/`、`inventory-*.txt`）、Unity の log、実行の記録（`run-notes.txt`）、`Assets/Spike/Editor/StampSpike.cs`、`Assets/Spike/Root.cs`（script が使う root の型）。`Out/` の出力そのものは入れず（名前と sha256 は `record/outputs/` にある）、実験の project に残した。
- 実行前の確認: script の sha256 が起動指示の期待値と一致した。実験の project を開いている Unity の process と `Temp/UnityLockfile` は無かった。`record/` と `Out/` は無かった。別の session の、無関係な使い捨ての project に対する Unity の batch が 3 本動いていた（触っていない。所要の値（AC3b・AC4）に CPU の競合が入った可能性がある。実行の記録に書いた）。
- 起動: §5 の検証経路のとおり（Unity 6000.6.0f1、`-executeMethod StampSpike.Run`）。2026-11-14 19:17:29 JST に起動した。
- 終了行: `MIDEND` が 10:19:29Z、`END` が 10:50:02Z（record）。`HALT` と `EXCEPTION` の行は無い。process は `END` の後に自分で終わった（約 20 秒後に無いことを確かめた）。止めた PID は無い。
- やり直し: なし（1 回だけ実行した）。
- 担当: 発見 C の実行担当（Phase B と異なるモデル）。

### 7.2 発見 C・判定 C の判断

- 種別: 判断担当（発見 C と判定 C）。2026-11-14。Unity は起動していない。batch のやり直しは無い。
- 引き継いだ commit: `3e2f8552`（HEAD と一致を確認）。implementation head は `89604c70`。base から `3e2f8552` までの差分は `docs/` の 2 file だけ。
- 使用した evidence bundle の取得案内・状態: §0 と同じ案内（ローカル固定済み（未昇格））。別の process の fetch で期待 packageSha256 の照合が通り、収録の 117 file を読んだ。bundle の `StampSpike.cs` の sha256 は §7.1 の実行前の値と同じ。
- 独立性: 判断担当は Phase B と異なるモデル（commit の trailer で確かめた）。発見 C の実行担当とは同じモデルだが、実行担当は判断をしていない（§7.1）。進行役のモデルは確かめていない。独立性の制約として記録する（risk `normal`）。
- 機械検査: `pwsh tools/contract-audit.ps1` は errors=0、warnings=0。

**発見 C（script と record の構造・統制）。** 現在の問いを阻害する script の誤りは無い。やり直しはしない。
- 責務マップとの照合: 実験の script は repository の外の 1 file（約 950 行。予想の約 400 行との差は §6 が説明している）。repository の差分は文書だけ。
- AC3b (b) の上乗せは、script では計算（`StampInputs` の依存の列挙と型の判定、K1、`Hash128`）、書き込み、書き込み後の `Refresh` から N の `Refresh` を引いた値、戻し、戻し後の `Refresh`、build の差（負なら 0）の合計である。割る値は N の build の最小、引く値は最小（厳しい読み）と中央値（緩い読み）。§2.2 の定義と合う。record の T1〜T3 の値から合計を計算し直し、record の値と一致した。
- 印の値の計算で、prefab・model の file を file の API で開く経路は無い（`KeyOf` の K0 だけが file を読み、段 2 の観測にだけ使う）。
- 次の 4 点は判定を変えないので、阻害する誤りに入れない。
  - N1: AC3 (e) の material の変更で、`Dep.mat` のバイトが変わったことを record が示していない（`SaveAssets` の後の sha256 を書いていない）。(e) は鍵の順位（GO・YES と CONDITIONAL ACCEPT (ii) の分かれ目）にだけ効き、下の GO・NO に影響しない。
  - N2: 規模の build の AC5 (a) の走査は、接頭辞（`osm-stamp`・`OsmStamp`）と、印の値のうち最初の 5 つだけを探す。形が成り立つかの導出は、全ての印の値を探す小さい fixture の列で行っている。
  - N3: 計算の所要は、依存の列挙、型の判定（`GetMainAssetTypeAtPath`・`AssetImporter.GetAtPath`）、鍵の取得に分けて採っていない。AC3b の不合格の内訳（§2.5 の GO・NO が求める粒度）は record で足りる。
  - N4: 実行中に、無関係な project の Unity の batch が 3 本動いていた（§7.1）。所要の値に CPU の競合が入りうる。上乗せは 5% の基準の約 5 倍で、この競合で合否は変わらないと読む。

**判定 C（AC ごと。期待値は §5 の fixture の値から導いた。script の `ac*`・`holds` の行は読み直しにだけ使った）。**

| AC | M | F2 | 根拠（record・`record/outputs/`） |
|---|---|---|---|
| AC0 | 成立（共通） | — | C2 の `SOuter` の `.cf`（目印 (40,41,42) を持つ file）が `Inner` の (1,2,3) を持ち (4,5,6) を持たない。C2 の書いた数 0 |
| AC1 | 成立 | 成立 | B1〜B6 の各回で、`SOuter` の file が `Mid` (10,11,12)・`Outer` (7,8,9) とその回の `Inner` の列だけを持ち、`SShared1`・`SShared2` がその回の `Shared` の列だけを持つ。`Model` の頂点は `.resS` にあり、その回の版だけがある（v1 (-31,32,33)、v2 (-34,35,36) は import した Mesh から作った値） |
| AC2 | 成立 | 成立 | 書いた数は B2 1、B3 0、B4 0、B5 2、X2 0。B3 の Scene の file は B2 と、B4 は B1 と名前・sha256 が一致。B5 の `SOuter` は B2 と一致。印を入れた file の sha256 は B2=B3、B1=B4。X1・X2 で固定の文字列が build の直前に残り、X2 の `SOuter` は `Inner` の v1。(g): path の指標は働き（B2 は `SOuter`、B5 は `SShared1`・`SShared2`、B6 は `SModel`）、B6 に禁止の path は無い。ID の指標も働き（B2・B5 の ID はどれも B1 にある）、B6 の ID に B2・B5 の ID は無い。B6 の `SModel` 以外の印の file と出力は B5 と一致 |
| AC3 | 鍵: K1 成立、K2 成立、K3 成立 | — | 段 2 の値で、`Inner` と `Model.obj` は v2 で変わり、v1 のバイトで最初の値に戻り、更新時刻だけでは変わらない（v2 をもう一度書くと v2 の値）。K3 の値は全ての時点で K1 と同じ。(e): K1・K2・K3 とも、texture の変更でも material の変更でも `Dep` の値は変わらない（N1 の限界あり）。K2 は `Inner` の編集で `Mid`・`Outer` の値が変わらない（file のバイトだけを見る）。build の鍵は K1、§2.7 の (1) |
| AC3b | **不合格** | **不合格** | 規模の fixture の照合は全て通った（prefab 3,000、入れ子 1,000、material 10、Scene ごとの instance 420、file 約 1.01 MB）。両形とも T3 まで届き、(a)・(a2) が成り立つ。緩い読みの上乗せが割る値の 5% を超える（下の表）ので、順に当てはめた 4 番目で不合格 |
| AC4 | 成立 | 成立 | 小さい列と規模の列の全ての build で、前後の棚卸しが一致。batch の終わりの棚卸しは始め（6 file）と名前・バイトとも一致 |
| AC5 | 成立 | 成立 | (a) どの出力にも印の文字列が無い（N2 の範囲）。(b) B6 と R の出力は、Scene の file を含めて名前・sha256 が全て一致（manifest と `BuildManifestHash.txt` を除く）。(c) 出た Warning は C1 にもある「Fallback fonts are not supported」だけで、段 3 の Error・Warning は 0 |
| AC6 | 成立（共通） | — | ブランチの差分は `docs/` だけ。record の project は repository の外で、開始時の棚卸しは実験の script と root の型の 6 file だけ |

AC3b の上乗せ（ms、厳しい読み。割る値は N の build の最小で、M 15,343、F2 14,658）:

| 回 | 計算 | 書き込み | `Refresh` の差 | 戻し | 戻し後の `Refresh` | build の差 | 合計（厳しい / 緩い） |
|---|---|---|---|---|---|---|---|
| M T1 | 3,048 | 263 | 97 | 152 | 105 | 35 | 3,701（24.1%）/ 3,665（23.9%） |
| M T2 | 3,014 | 268 | 102 | 150 | 105 | 155 | 3,792（24.7%）/ 3,637（23.7%） |
| M T3 | 3,029 | 267 | 97 | 157 | 105 | 353 | 4,008（26.1%）/ 3,746（24.4%） |
| F2 T1 | 3,002 | 204 | 118 | 207 | 137 | 0 | 3,667（25.0%）/ 3,667（25.0%） |
| F2 T2 | 3,042 | 205 | 118 | 212 | 129 | 16 | 3,721（25.4%）/ 3,705（25.3%） |
| F2 T3 | 3,024 | 208 | 105 | 219 | 119 | 994 | 4,669（31.9%）/ 4,263（29.1%） |

**§2.5 の読み: GO・NO（2 番）。** 上から順に当てはめた。
- 1 番（未判定）には当たらない: AC0 は成立し、batch は `MIDEND` と `END` まで動き（`HALT`・`EXCEPTION` なし）、AC6 は成立。
- 2 番（GO・NO）に当たる: 成り立つ印の形は M と F2 の 2 つで（AC1・AC2・AC4・AC5 を全て満たす）、その全てで AC3b が不合格。
- 3 番（GO・YES）・4 番（CONDITIONAL ACCEPT）は、2 番に当たったので当てはめない。

**owner へ返す内容（3 条件ごと）。**
- 条件 1（cache）: M と F2 の両方で満たした。小さい fixture では、変更なしと前の版へ戻した build が Scene を書かず（書いた数 0）、編集した prefab を置いた Scene だけを書き、model だけの編集で他の Scene を作り直さなかった（AC2 (g) を 2 つの指標で読めた）。規模でも、source を変えない印ありの build が書いた数 0 で T0 と同じ出力を出した。
- 条件 2（安い検知）: 鍵は満たした。K1（`GetAssetDependencyHash`、public）は file を開かずに、中身の変更・戻し・更新時刻だけの変更を正しく区別し、入れ子の外側の prefab の値も内側の編集に追従した。**しかし規模（Scene 300、prefab 3,000）での 1 回の上乗せが、印なしの build の約 24〜32% で、5% を満たさない。** 上乗せの約 8 割は印の値の計算（Scene ごとに `GetDependencies(scene, true)` で依存を列挙し、`GameObject` と `ModelImporter` の物に絞って K1 を連ねる。300 Scene で約 3.0 秒）である。測ったのは「Scene ごとに依存を列挙して値を作る → 書いて `Refresh` → build → 戻して `Refresh`」の手順だけで、別の手順（例えば、製品の build の入力の snapshot が既に持つ依存閉包（§1）を使い回す、列挙を Scene の間で共有する）は測っていない。
- 条件 3（source）: M と F2 の両方で満たした。全ての build の後、`Assets` の下の全ての file が build の前と同じ名前・同じバイトだった。印の文字列は出力に無く、B6 の出力は印なしの基準 R と Scene の file を含めて一致した。
- 測っていないもの: 印の形 E・F、検知の代替 K4・K5（§2.7）。K2 は成り立ったが build に使っていない。model の編集は印なしでも取りこぼされなかった（C3 で v2 の頂点が出た）ので、B6 が示すのは「印が model の頂点の編集を妨げない」ことだけで、model の編集のうち Scene に焼き込まれる側（階層、Transform）は測っていない。範囲は 1 process の batch、Unity 6000.6.0f1、texture を持たない合成の規模の fixture で、CPU の競合がありうる（N4）。

- 停止規則: GO・NO で終了する（§2.5）。追加の実測はしない。§0 の次の作業 (3) の C' へ進む。

### 7.3 revision 1 の発見 C の実行（batch 1 回）

実行担当は本書に記録していないので、判断担当が bundle の `run-notes.txt`（実行の記録）と record から写した。

- 引き継いだ commit: `ccd75872`（実行の記録で HEAD と一致）。implementation head は `9197e907`。
- 実行前の確認（2026-11-14 21:26:36 JST）: script の sha256 が起動指示の値と一致（`StampSpike.cs` `a4e788fd…`、`Root.cs` `00973301…`）。起動の前に Unity の process は無く、30 分の待ちは要らなかった（revision 0 の §7.2 の N4 の競合は今回は無い）。`Temp/UnityLockfile`、`record/`、`Out/`、`run.log` は無かった。
- 起動: §5 の検証経路のとおり（`D:\UnityEditor\6000.6.0f1\Editor\Unity.exe`、`-executeMethod StampSpike.Run`）。21:26:37 JST、PID 35548。
- 終了行: `START` 12:26:43Z、`MIDEND` 12:27:54Z（`holds M=True F2=True`）、`END` 12:58:51Z。`HALT`・`EXCEPTION` の行は無い。build は 36 回（小さい列 20、規模の列 16）で全て `Succeeded`。
- 終了: PID 35548 は `END` の後も一覧に残り（22:27:00 JST、`UnityCrashHandler64.exe` が attach）、その PID だけに `taskkill /F` を 3 回試みて「no running instance」が返った。既知の終了 stall として調査していない（`tools/Harness/README.md`）。判断はこれを blocker にしない（record は `END` まで書かれ、log の最終書き込みは 21:58:57 JST）。
- evidence bundle: §0 の案内（ローカル固定済み（未昇格）、125 file）。`Out/` は入れず、名前と sha256 は `record/outputs/` にある。
- やり直し: なし。

### 7.4 revision 1 の発見 C・判定 C の判断

- 種別: 判断担当（発見 C と判定 C）。2026-11-14。Unity は起動していない。batch のやり直しは無い。
- 引き継いだ commit: `ccd75872`（HEAD と一致を確認）。implementation head は `9197e907`。base から `ccd75872` までの差分は `docs/` の 2 file だけ。
- 使用した evidence bundle: §0 の revision 1 の案内。別の process の fetch で期待 packageSha256 の照合が通り（`status passed`）、`record/`・`run.log`・`run-notes.txt`・script を読んだ。bundle の `StampSpike.cs`（1,078 行）の sha256 は起動指示の値と同じ。実験の project の file は直接読んでいない。
- 独立性: Phase B は Sonnet 5.5（commit `ccd75872` の trailer）、発見 C の実行担当と判断担当は Opus 5.5。判断担当は Phase B と異なるモデルで、実行担当とは同じモデルだが、実行担当は判断をしていない。独立性の制約として記録する（risk `normal`、高コストのモデルへは上げない）。
- 機械検査: `pwsh tools/contract-audit.ps1` は errors=0、warnings=0。
- 導き直しの方法: record の build の行（source の版、書いた数、印の file の sha256、所要）、`record/outputs/`（file の名前・sha256・float 列の有無）、`.events.txt`（書いた file の ID と `BuildMetaDataImporter` の path）を、判断担当が書いた使い捨ての読み取りで集計し、期待値は §5 の fixture の版から当てた。float 列の有無そのものは script のバイト走査の結果に依る（出力の生バイトは bundle に無い）。

**発見 C（script と record の構造・統制）。** 現在の問いを阻害する script の誤りは無い。やり直しはしない。

- **閉包の関数分離（AC3b の当てはめの 0 番）は成り立つ。** `Closure(root)`（`StampSpike.cs` 347〜356 行）が呼ぶ Unity の API は `AssetDatabase.GetDependencies(root, true)` と `AssetDatabase.IsValidFolder` だけで、`\` を `/` に替え、`Assets/` で始まらない path・`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref`・folder を除き、root を足して序数順に並べる（§2.7 の規則と同じ）。`StampInputs(scene, closure)`（375〜383 行）と `Stamp(scene, closure)`（428〜441 行）は閉包を引数で受け取り、`GetDependencies` を呼ばない。型の判定（`GetMainAssetTypeAtPath`・`AssetImporter.GetAtPath`）と鍵（`KeyOf`）はこの 2 つの中だけにある。`GetDependencies` は他に規模の fixture の照合（965・974 行）にだけある。
- **閉包の位置と計測（AC3b (b)）は §2.2 と合う。** `DoBuild`（604 行〜）は、`Refresh` → 棚卸し → `Closures`（所要を `ClosureMs` に書き、上乗せに入れない）→ 印の計算（外側の計測 `ComputeMs`、内訳は Scene ごとの loop で累計）→ 書き込み → `Refresh` → build → 戻し → `Refresh` → 棚卸し の順。N の回は同じ位置で閉包を作り、印を作らずに変更なしの `Refresh` を測る（997 行で N に `closureScenes` を渡す）。印の値は Scene ごとに作り、Scene の間で使い回さない。
- 印の値の計算で prefab・model の file を file の API で開く経路は無い（`KeyOf` の K0 だけが file を読み、段 2 の観測にだけ使う）。
- record の `ac3b` の割合は小数 1 桁に丸めてある（M T1 の緩い読み 4.75% が `4.7%`）。判断は下の導き直しの値で行い、丸めは読みを変えない。
- revision 0 の発見 C の N1（material の変更が file に落ちたか）は閉じた: 段 2 の `Dep.mat` の sha256 は変える前 `3ac4da2c…`、保存して `Refresh` した後 `4dd0eedb…` で異なる。N2（規模の印の値の走査を 5 個に切った）も閉じた: 規模の block ごとに T0〜T3 の印の値 300 種を全ての出力で探し、一致 0（`ac5a scale`）。N4（CPU の競合）は今回は無い（§7.3）。

**判定 C（AC ごと。期待値は §5 の fixture の値から導いた。script の `ac*`・`holds` の行は読み直しにだけ使い、全て判断担当の導き直しと一致した）。**

| AC | M | F2 | 根拠（record・`record/outputs/`） |
|---|---|---|---|
| AC0 | 成立（共通） | — | C2（`Inner` だけ v2）の `SOuter` の file（目印 (40,41,42)）が `Inner` の v1、`Mid`、`Outer` の列を持ち、v2 の列を持たない。C2 の書いた数 0 |
| AC1 | 成立 | 成立 | B1〜B6 の各回で、`SOuter` の file が `Mid`・`Outer` とその回の `Inner` の版の列だけ、`SShared1`・`SShared2` がその回の `Shared` の版の列だけを持つ。`Model` の頂点は全ての出力の file のうちその回の版だけがある（v1 (-31,32,33)、v2 (-34,35,36) は import した Mesh から作った値）。C3（印なし、`Model` を v2）も v2 の頂点だけを出した（AC1 の「B6 の読み」が当たる） |
| AC2 | 成立 | 成立 | 書いた数は X1 10、X2 0、B1 4、B2 1、B3 0、B4 0、B5 2、B6 3（両形とも同じ）。(a) B3 の出力の全ての file（manifest を除く）が B2 と名前・sha256 で一致。(b) B4 の各 Scene の file が B1 と一致。(c) B5 の `SOuter` が B2 と一致。(d) B2 は 1。(e) 印の file の sha256（64 桁）が B2=B3、B1=B4、B1≠B2。(f) X1・X2 とも固定の文字列が build の直前に残り、X2 の書いた数 0、X2 の `SOuter` は `Inner` の v1。(g) path の指標は働き（B2 は `SOuter`、B5 は `SShared1`・`SShared2`、B6 は `SModel` と `Model.obj`）、B6 に禁止の path は無い。ID の指標も働き（B2・B5 の ID はどれも B1 にある）、B6 の ID 3 つに B2・B5 の ID は無い。B6 の `SModel` 以外の印の file と Scene の file は B5 と一致 |
| AC3 | 鍵: K1 成立、K2 成立、K3 成立 | — | 段 2 で、`Inner` と `Model.obj` は v2 で値が変わり、v1 のバイトで最初の値に戻り、更新時刻だけでは変わらず、v2 をもう一度書くと v2 の値に戻った。K3 は全ての時点で K1 と同じ値。(e) K1・K2・K3 とも、texture の変更でも material の変更でも `Dep` の値は変わらない（material の変更は `Dep.mat` のバイトに落ちている）。K1・K3 は `Mid`・`Outer` が `Inner` の編集に追従し、K2 は追従しない。build の鍵は K1、§2.7 の (1) |
| AC3b | **未判定**（6 番） | **不合格**（4 番） | 規模の fixture の照合は全て通った（prefab 3,000、入れ子 1,000、material 10、Scene ごとの instance 420、file 1,015,074〜1,017,200 バイト。texture を持たない限界の行あり）。両形とも T3 まで届いた。(a) T1〜T3 の書いた数 0、`.cf`・`.resS` の（名前, sha256）の集合が T0 と同じ。(a2) N1〜N3 の書いた数 0、集合が W と同じ。上乗せは下の表 |
| AC4 | 成立 | 成立 | 小さい列と規模の列の全て（36 回）の build で前後の棚卸しが一致。段 2・段 3 の後と各規模の block の前の棚卸しも控えと一致。batch の終わりの棚卸しは始め（6 file）と一致 |
| AC5 | 成立 | 成立 | (a) 全ての出力に印の文字列（接頭辞・固定の文字列・その回の値）が無い。規模は T0〜T3 の値を全ての出力で探して一致 0。(b) B6 と R の出力は、Scene の file を含めて名前・sha256 が全て一致（manifest と `BuildManifestHash.txt` を除く）。(c) build の間の Warning は C1 にもある「Fallback fonts are not supported」だけ、Error は 0。段 3 の Error・Warning は両形とも 0。版の切り替えの時の model の import の Warning（`SWITCH-LOG`）は印なしの C3 にも出ており、印による物ではない |
| AC6 | 成立（共通） | — | ブランチの差分は `docs/` だけ（`git diff --stat 41099fb7 HEAD`）。record の project は repository の外。開始時の棚卸しは実験の script と root の型の 6 file だけ |

AC3b の上乗せ（ms。割る値は N1〜N3 の build の最小で、M 15,752、F2 15,768。N の build は M が 22,316 / 16,089 / 15,752、F2 が 22,053 / 16,054 / 15,768。N の `Refresh` は 2.5〜2.8）:

| 回 | 計算 | 書き込み | `Refresh` の差 | 戻し | 戻し後の `Refresh` | build の差（厳しい / 緩い） | 合計（厳しい / 緩い） |
|---|---|---|---|---|---|---|---|
| M T1 | 114 | 267 | 105 | 154 | 109 | 65 / 0 | 813（5.16%）/ 748（4.75%） |
| M T2 | 106 | 279 | 101 | 157 | 113 | 0 / 0 | 757（4.80%）/ 756（4.80%） |
| M T3 | 111 | 290 | 93 | 150 | 106 | 53 / 0 | 803（5.10%）/ 750（4.76%） |
| F2 T1 | 105 | 201 | 120 | 236 | 128 | 102 / 0 | 892（5.66%）/ 790（5.01%） |
| F2 T2 | 106 | 214 | 118 | 198 | 121 | 95 / 0 | 851（5.40%）/ 756（4.79%） |
| F2 T3 | 109 | 292 | 108 | 198 | 195 | 89 / 0 | 992（6.29%）/ 903（5.72%） |

- 当てはめ: M は 4 番（緩い読みで 5% を超える回）に当たらず（最大 4.80%）、5 番（厳しい読みで全ての回が 5% 以下）にも当たらない（T1 5.16%、T3 5.10%）ので 6 番の未判定。F2 は緩い読みで T1 5.01%、T3 5.72% が 5% を超えるので 4 番の不合格。
- 割る値に対する内訳の割合（厳しい読み）: 計算 0.66〜0.72%、書き込み 1.28〜1.85%、`Refresh` の差 0.59〜0.76%、戻し 0.95〜1.50%、戻し後の `Refresh` 0.68〜1.24%、build の差 0〜0.65%。上乗せの約 8 割は書き込み・戻し・2 回の `Refresh` で、計算は約 1 割強。
- 計算の内訳（参考値、Scene ごとの loop の累計）: 型の判定 44〜48 ms、鍵の取得 58〜61 ms、ハッシュ 1.4〜5.6 ms。revision 0 の計算 約 3.0 秒の大半は、Scene ごとの `GetDependencies` だった。
- 依存閉包を作る所要（上乗せに入れない。製品の snapshot の所要ではなく、`GetDependencies` と除外だけの所要）: T と N の各回で 2,954〜3,480 ms（割る値の 18.7〜22.1%）。
- N のばらつき: 各 block の N1（T0 の作り直しの直後）が約 22 秒で、N2・N3 と T1〜T3（約 15.7〜16.1 秒）より 6 秒遅い。M の厳しい読みが 5% を超えた 2 回は、build の差（53〜65 ms、割る値の 0.3〜0.4%）が加わった分である。build の差を除いた M の上乗せは 748〜757 ms（4.75〜4.80%）で、5% の 788 ms との差は 31〜40 ms。

**§2.5 の読み: CONDITIONAL ACCEPT (iii)（4 番）。** 上から順に当てはめた。

- 1 番（未判定）には当たらない: AC0 は成立し、batch は `MIDEND` と `END` まで動き（`HALT`・`EXCEPTION` なし）、AC6 は成立。
- 2 番（GO・NO）には当たらない: 成り立つ印の形は M と F2 の 2 つで（AC1・AC2・AC4・AC5 を全て満たす）、M の AC3b が不合格でない（未判定）。
- 3 番（GO・YES）には当たらない: AC3b が合格の形が無く、「全てを満たす形」が無い。
- 4 番（CONDITIONAL ACCEPT）に当たる。当たる項目は (iii)（AC3b が合格の形が無く、未判定の形がある。N のばらつき）。(i) は当たらない（build の鍵は public な K1）。(ii) は当たらない（K1 は AC3 (e) で値が変わらない）。(iv) は当たらない（AC3b が合格の形が無い）。

**前提の文（§2.5 の 3 番・4 番。AC3b の結果を写す時に必ず付ける）。** AC3b の結果は、印の計算と書き込みを製品の snapshot の後・adapter の `Refresh` の前に置き、その snapshot の閉包をそのまま使う場合の値である。閉包を `Refresh` の後に作り直す製品の形では、record の「依存閉包を作る所要」（製品の snapshot の所要ではなく、`GetDependencies` と除外だけの所要）を足して読み直す。この batch ではその所要が 2,954〜3,480 ms（割る値の 18.7〜22.1%）で、足すと両形とも 5% を大きく超える。

**owner へ返す内容（3 条件ごと。owner が条件の読みを決める）。**

- 条件 1（cache）: M と F2 の両方で満たした。小さい fixture で、変更なしと前の版へ戻した build が Scene を書かず（書いた数 0）、編集した prefab を置いた Scene だけを書き、model だけの編集で他の Scene を作り直さなかった（AC2 (g) を 2 つの指標で読めた）。規模でも、source を変えない印ありの build が書いた数 0 で T0 と同じ出力を出した。
- 条件 2（安い検知）: 鍵は満たした（K1、public、file を開かない、(e) で値が変わらない）。規模での 1 回の上乗せは、上の前提の文の置き方で、**M が 4.75〜5.16%（未判定）、F2 が 4.79〜6.29%（不合格）**。M は N のばらつきで 5% の上下が分かれ、build の差を除いた固定の部分（書き込み・戻し・2 回の `Refresh`・計算）だけで 4.75〜4.80% と、5% の余裕が 31〜40 ms しかない。F2 は戻し（Scene 300 個の切り詰め）と戻し後の `Refresh` が M より重い。測ったのは「受け取った依存閉包から Scene ごとに値を作る → 書いて `Refresh` → build → 戻して `Refresh`」の手順だけで、別の手順（`Refresh` を製品の 1 回にまとめる、Scene の間で鍵の結果を使い回す）は測っていない。
- 条件 3（source）: M と F2 の両方で満たした。全ての build の後、`Assets` の下の全ての file が build の前と同じ名前・同じバイトだった。印の文字列は出力に無く、B6 の出力は印なしの基準 R と Scene の file を含めて一致した。
- 形の並び（§2.7 の順）: M は成り立ち、欠けている点は AC3b が未判定であること。F2 は成り立ち、欠けている点は AC3b が不合格であること（その形自身の AC3b の結果を付けて書き、条件 2 を満たすとは書かない）。
- 測っていないもの: 印の形 E・F、検知の代替 K4・K5（§2.7）。K2・K3 は成り立ったが build に使っていない。model の編集は印なしでも取りこぼされなかった（C3）ので、B6 が示すのは「印が model の頂点の編集を妨げない」ことだけで、model の編集のうち Scene に焼き込まれる側（階層、Transform）は測っていない。範囲は 1 process の batch、Unity 6000.6.0f1、texture を持たない合成の規模の fixture。開いている Editor での build（F2 の読み直し等）は §2.3 のとおり S2-2b が確かめ、そこで結論が覆りうる。

**指摘の分類。**

- 現在の問いを阻害する欠陥（凍結済み条件・常時契約への違反）: なし。根拠: 閉包の関数分離（§2.7「依存閉包の受け取り」、AC3b の 0 番）、AC3b (b) の計測の位置、AC3 の file の API の確かめは全て script の code で成り立ち、record は §5 が求める値を持つ。
- 後続の入力（違反根拠なし。現スライスを拡張しない）:
  - L1（owner、CONDITIONAL ACCEPT (iii) の読み）: M の 5% の判定は N1 の遅さ（約 22 秒）と 1 回ずつの build の差に依る。AC3b の 6 番のとおり、未判定を合格・不合格のどちらに読むかは owner が決める。条件の読みを変えずに決めるなら、§2.5 の停止規則により追加の実測には owner の指示が要る。
  - L2（S2-2b の A1）: 上乗せの約 8 割は書き込み・戻し・2 回の `Refresh` で、印の計算（約 110 ms）ではない。上乗せを下げる余地は、製品の `Refresh`（`UnityContentDirectoryAdapter.cs` 42 行）と印の `Refresh` をまとめる等の手順にあるが、測っていない。
  - L3（S2-2b の A1）: 製品の snapshot は adapter の `Refresh` より前にできる（§2.3）。閉包を `Refresh` の後に作り直す形を採ると、閉包の所要（この batch で約 3 秒）が上乗せに乗る（前提の文）。
  - L4（S2-2b の A1）: F2 は M より、戻し（file の切り詰め）が 41〜86 ms、戻し後の `Refresh` が 8〜89 ms 重い。M を先にする §2.7 の推奨の理由（3 条件の外）に、条件 2 の側の差も加わった。
  - L5（残存リスク）: 規模の出力の生バイトと各 build の棚卸しの全文は bundle に無い（名前・sha256 の一覧と、差が空だという行だけ）。float 列の有無は script の走査に依る。
  - L6（運用）: batch の process が `END` の後に残った（§7.3）。既知の終了 stall で、調査しない。

- 停止規則: CONDITIONAL ACCEPT で終了する（§2.5）。追加の実測はしない。§0 の引継ぎ票のとおり C' へ進む。

## 8. Phase C'

- 担当方式: AI（cursor-agent の CLI、1 回。進行役が起動し、出力をそのまま本節へ記録した。進行役は監査をしていない）
- 種別と巡: 最終 C'（高コスト証拠なし）、1 巡目、全体。2026-11-14。
- 受け取った決定記録の版: なし
- 使用した blind audit bundle の取得案内・状態: ローカル固定済み（未昇格）。Artifact Storage の task `content-scene-stamp-spike` に `evidence stage` で固定し、別の process の fetch で hash の照合を通した写しを C' の workspace にした（120 file）。中身は、判定 C と同じ evidence bundle の `record/`・`run.log`・`Assets/Spike/`、Phase B 完了時点の commit `c2947bd8` の本書の blob、base から `c2947bd8` までの本書以外の差分（program の表の 1 行）、`AGENTS.md`、監査の指示書。`run-notes.txt`（実行の記録）、§7、発見 C・判定 C の所見は入れていない。取得案内は実験の project に置き、所在は起動指示で渡す。C' の出力の原文も同じ所に置いた。
- 確認範囲・方法: blind bundle の全体。record の行、`record/outputs/`、`tep/`、`run.log`、script の行を AC ごとに読み、期待値は §5 の fixture の値から導いた（script の `ac*` の行は使っていない）。Unity は再実行していない。
- 判定: **GO・NO。** M と F2 は AC1・AC2・AC4・AC5 を満たすが、両方とも AC3b が不合格である（緩い読みの上乗せが M は 23.7〜24.4%、F2 は 25.0〜29.1%。基準は 5%）。§2.5 の 2 番に当たる。満たせなかったのは条件 2。上乗せの大半は印の計算（約 3.0 秒。Scene ごとの依存の列挙を含む）で、書き込み、`Refresh` の差、戻し、build の差はそれぞれ約 0.1〜0.3 秒。測った手順は、印を書いて `Refresh`、戻して `Refresh` だけ。AC ごとの読みは、AC0 合格、AC1 合格、AC2 合格（(g) 成立）、AC3 は (a)〜(d) 合格、(e) は texture で値が変わらず、material は未判定、AC3b 不合格、AC4 合格、AC5 合格、AC6 合格。build の鍵は K1（順位 1）。
- 現在の問いを阻害する findings: なし（凍結条件と AGENTS.md の、この問いを止める違反は見つからなかった。条件 2 の不合格は AC3b の定義どおりの結果で、script の誤りではない）。
- 後続スライスへ移送する findings（全て (B)）:
  - AC3 (e) の material 側が未確認。`Dep.mat` に色を代入した後、中身が変わった再 import が log に無く、import の出力 ID は作った時の値のまま。「material でも値が変わらない」は、編集が file に落ちたことを示していない。GO・NO は AC3b で先に決まるので、この穴は鍵の順位 1 の確定だけを弱める。
  - §5 が record に求めた値の一部が欠けている。小さい列の印の file の sha256 は 12 桁で、規模の列は 300 個をまとめた hash だけ。出力先の絶対 path は無く、`outputEmptyBefore=True` だけ。規模の build の印の値の走査は 5 個に切ってある（接頭辞は全 file を見ている）。規模の fixture が texture を持たないという限界の文が record に無い。版の切り替えの時の FBX の Warning は record に写していない（AC5 (c) が見る build の相と段 3 には、印だけが出す Error・Warning は無い）。
  - AC3b (a2) の「出力」を全 file と読むと、manifest が毎回違うことだけで未判定になる。§6 の `.cf`・`.resS` の読みと、書いた数 0 を合わせると (a)・(a2) は成り立ち、AC3b は不合格になる。
  - model の頂点は印なしでも出力に入る（C3）。B6 が示すのは、印がこの頂点の更新を妨げないことだけ。model の階層と Transform は測っていない。
  - 上乗せを下げる別の手順は測っていない（製品が既に持つ依存閉包を使い回す、`GetDependencies` を Scene の数だけ呼ばない、`Refresh` を製品の 1 回にまとめる）。計算だけで 5%（M 約 767 ms、F2 約 733 ms）を超えている。
- 残存リスク: TEP が新しいかの判定は、build の開始の 1 秒前より後の更新なら真になる（今回は build ごとに BuildHistory の directory が別で、取り違えは起きていない）。規模の `.cf` は全てを突き合わせていない。出力の生バイトは bundle に無く、float の列は script の走査結果に依る。AC0 が落ちても script は止まらない（今回は成り立った）。build が途中で落ちた時の戻し、開いている Scene、読み取り専用の file、prefab variant・override・unpack、FBX、実データでの上乗せ、別の PC の `Library` は §2.3 のとおり測っていない。F2 を開いている Editor で読み直す場合は、この batch の 1 回では見ていない。
- 監査できなかった範囲: Unity の再実行、network、workspace の外、git の実際の diff（入力の patch と HANDOFF だけを見た）。`StampSpike.cs` の sha256 を開始時の棚卸しの値と照合し直していない。build ごとの棚卸しの全文は残っていない（差が空だという record の行と、開始・終了の 6 file の一致だけを見た）。規模の build の出力は script が消すので、走査し直しと全 sha の突き合わせはできない。`run.log` の Warning を種別ごとに数えていない。
- 独立性: Phase A（Anthropic）、Phase B（Anthropic）、発見 C の実行担当・判断担当（Anthropic）には関与していない。Phase A の外部レビュー（cursor[bot]、§5.1）が使ったモデルは公開されておらず、確かめられなかった。独立性の制約として記録する（risk `normal`、強化条件は必須ではない）。
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: なし（blind bundle だけを入力にした）。
- 担当・モデル: cursor-agent、Grok 4.7 High（`grok-4.7-high`）。

## 9. Phase D

未着手
