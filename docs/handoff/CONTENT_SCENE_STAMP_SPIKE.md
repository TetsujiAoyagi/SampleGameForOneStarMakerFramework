# Scene の印の方式を選ぶスパイク（S2-2b の前段）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2b-0。S2-2b から切り出した）
- status: `A`（A2 入力版。owner は 2026-11-14 に §2.8 の U1〜U5 を決めた。A2 のレビュー 2 件と A3 の統合はこれから。owner の指示で、A3 の統合の後は凍結せず、draft PR の外部レビューを受けてから凍結する）
- branch: `claude/content-scene-stamp-spike`（PR の base は `develop`）
- implementation base commit: `41099fb7`（`develop` の head、2026-11-14）
- implementation head commit: `未固定`
- risk: `normal`（製品 code と SampleGame を変えない。実測は repository の外の project で行い、この repository に入るのは文書だけ）
- owner: root（依頼者）
- created: 2026-11-14
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」（印の形と検知の鍵を測った結果、測っていないもの）、program §8 のこのスライスの行と S2-2b の行。S2-2b の A1 は事実の文書のその節を再開入力として読む（S2-2b の初稿へ結論を写さない）。結論が出たら、既存の GitHub Pages（`gh-pages` ブランチ）も更新する（2026-11-14 の owner の依頼。載せる内容は Phase D で owner と決める）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§5.3、§8）。S2-2b の A1 初稿（`docs/handoff/CONTENT_PREFAB_STAMP.md`、ブランチ `claude/content-prefab-stamp` の作業ツリーにある未 commit の file。本スライスは読むだけで変えない）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script を持たない。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー 2 件）と A3 の統合。A1 の主担当が続けて行う。
  - 次の作業: §5 の A2 の 2 件を同じ入力版（この票を含む commit の本書）で行い、指摘を採用・不採用・保留に分けて §5.1 に記録し、本書を改訂する。凍結はしない。統合した版を commit して draft PR を作り、外部レビューを受ける（owner の指示）。
  - 読む範囲: 既定のとおり。
  - 必要な権限: 無し（文書だけ）。Unity の実行は Phase C まで行わない。
  - 停止規則と引き渡さないもの: 目的レビューには §1 の目的と利用者の 2 欄の原文だけを渡す。
  - 引き継ぐ commit: この票を含む commit（SHA は tree の外で渡す）。

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
    - 範囲と限界: 作り直しの有無を直接示す統計は採れていない（出力の一致と所要からの読み）。入れ子は 1 段、共有の prefab・model・複数の Scene への印・規模は測っていない。上乗せは `Refresh` を含めて書き込みが 16〜48 ms、戻しが 15〜44 ms（Scene 1 つ）。判定の証拠ではなく、§5 の batch で採り直す。
  - **`GetAssetDependencyHash` を Scene に対して呼んだ値は、依存の中身を表さない。** prefab・texture を v2 にした前後で、それを置いた Scene の値は変わらなかった（S2-2 のスパイクの参考の観測）。prefab 自身に対して呼んだ値は測っていない。
  - **この repository の Scene の text:** `unity/Assets` の `.unity` 668 件の全てが `SceneRoots` の document を持ち、全てでそれが file の最後の document である（file は `m_Roots` の最後の行で終わる）。最大の Scene は約 23 KB で、規模の参考にならない。`#` で始まる行を持つ `.unity`・`.prefab` は無い。`EditorOnly` tag を持つ Scene は無い。`ProjectSettings/EditorSettings.asset` は `m_SerializationMode: 2`（Force Text）。
  - **製品で Unity の build を呼ぶのは 1 か所。** `UnityContentDirectoryAdapter.Build`（`Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs`、173 行、`BuildContentOptions.None`）。root ごとの依存閉包（`AssetDatabase.GetDependencies(path, true)`）は build の入力の snapshot が既に持つ（S2-2b の A1 初稿 §1）。
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
  - 探索した境界: 既存の実験の手順。未確認: 方式の違う印入りの Scene が、同じ cache の entry に当たることがあるか（鍵が読み取った後のデータなら起こりうる。§5 の列の順と AC2 の読みで扱う）。

**成立条件**（起きなければならないこと。印の形と検知の鍵の組ごとに判定する）

- **S0 取りこぼしが、この fixture で再現する（前提）。** 印を入れない build の列で、prefab を編集した後の build の Scene が古い版のまま出る。
- **S1 正しさ。** 印を入れた全ての build で、各 Scene の出力が、その build の直前の source の prefab の版を持つ。入れ子の内側だけを編集した時と、2 つの Scene が共有する prefab を編集した時を含む。
- **S2 条件 1（cache）。** prefab を変えていない build は Scene を作り直さず、prefab を前の版に戻した build は、その版で build した時と同じ Scene の file を出す。
- **S3 条件 2（安い検知）。** 印の値を、prefab・model の file を開かずに作れ、その値は file の中身が変われば変わり、前の中身に戻せば戻り、中身が同じなら変わらない。
- **S4 条件 3（source に残らない）。** 各 build の後、project の `Assets` の下の全ての file が、その build の前と同じ名前と同じバイトである。

### 2.2 受け入れ条件

観測は全て §5 の 1 回の batch の record で閉じる。期待値は fixture の source の値から導き、実験の出力から作らない。「組」は印の形（§2.7 の M・F2）と検知の鍵（K1〜K3）の組をいう。

- **AC0（S0）前提。** 印なしの列 C1（全て v1）→ C2（`Inner` だけ v2）で、C2 の `SOuter` の `.cf` が `Inner` の v1 の float 列を持ち、v2 の列を持たない。成り立たない時は、この batch から問いを判定しない（§2.5 の未判定）。
- **AC1（S1）正しさ。** 印の形ごとの列 B1〜B5（§5）の各回で、各 Scene の `.cf` が、その回の source の版の float 列を全て持ち、別の版の列を持たない。合格例: B2（`Inner` だけ v2）で、`Inner` を直接は置かず `Outer` 経由でだけ焼き込む `SOuter` が、`Inner` の v2 の列と `Outer` 自身の子の列 (7,8,9) を持ち、`Inner` の v1 の列を持たない。不合格例: B5 で `SShared1` は v2 だが `SShared2` が v1。
- **AC2（S2・I2）cache。** 次の 4 つが全て成り立つ。(a) B3（変更なし）で、出力の全ての Scene の file が B2 と同じ名前・同じ sha256 で、Scene の serialized file を書いた数が 0。(b) B4（`Inner` を v1 に戻す）の各 Scene の file の名前と sha256 が B1 と一致し、Scene の serialized file を書いた数が 0。(c) B5 の `SOuter` の file が B2 の `SOuter` と一致する。(d) 印を入れた file（M は `.meta`、F2 は Scene）のバイトの sha256 が、同じ prefab の状態の回（B2 と B3、B1 と B4）で一致する。列の中で cache を消さない。「書いた数」は、Phase B が `Library/BuildHistory/<build>/` の `BuildContentTEP.json` か `BuildLog.jsonl` から、Scene の serialized file の書き込みを数えられる値を特定して使う。`SerializeBuildInstructions` の `changedCount` は、作り直した回でも 0 だった（§1）ので使わない。数えられる値が無い時は、(a)・(b) の「書いた数」を未確認として record に残し、判断担当は出力の一致と build の所要の差で読んだことを明記する。不合格例: B4 の出力は B1 と同じ sha256 だが、Scene の serialized file を 1 つ書いている。
- **AC3（S3）検知の鍵。** 鍵ごとに、Unity の build を伴わない次の観測が全て成り立つ。(a) `Inner` の file を v2 にすると `Inner` の値が変わる。(b) v1 のバイトに戻すと、最初の値に戻る。(c) バイトを変えず更新時刻だけを変えても値が変わらない。(d) model の file（fixture の `.obj`）でも (a)〜(c) が成り立つ。加えて、実験の script が印の値を作る時に、prefab・model の file を file の API で開かない（script の code で確かめる）。
- **AC3b（S3）規模での上乗せ。** §5 の規模の fixture で、何も変えていない build を、印なしで 2 回（N1・N2）、印ありで 2 回（T1・T2）行い、record に次を残す: 各回の build の呼び出しの所要、印の値の計算の所要、印の書き込みと戻しの所要、その後の `AssetDatabase.Refresh` の所要。合否は「T1・T2 の上乗せ（計算 + 書き込み + 戻し + Refresh）の合計が、N1・N2 の build の所要の短い方の U5 の割合以下」で決める。N1 と N2 の差がその割合より大きい時は、差を併記して owner が読む（1 回ずつの計測のばらつきは、事実の文書の「同じ `Library` での再 build の所要」で 3 割あった）。
- **AC4（S4）source。** 各 build の前後で、`Assets` の下の全ての file（`.meta` を含む）の path と sha256 の棚卸しが一致する。batch の終わりに fixture を消した後の棚卸しが、batch の始めと一致する。不合格例: build の後に Scene は元のバイトだが、新しい `.meta` が 1 つ増えている。
- **AC5（S1 の補助）印が出力を汚さない。** 印の形ごとに、全ての build の出力の file のバイトに、印の文字列（接頭辞とハッシュ）が無い。B5 の出力と、列の後に cache を消して印なしで build した基準 R（B5 と同じ source）の出力が、build ごとに作り直す file（root、manifest、`BuildManifestHash.txt`）と Scene の file を除いて、名前と sha256 で全て一致する。Scene の file が R と一致するかは record に残し、合否には使わない。
- **AC6（I1）範囲。** 本スライスの commit の差分が `docs/` の下だけで、record の project の path がこの repository の外である。

### 2.3 ここでは答えない問いと所有する後続

- 選んだ方式の製品への実装、build が途中で落ちた時の戻し、開いている Scene・読み取り専用の file、印の形が Editor の source Play に影響しないこと: S2-2b（`CONTENT_PREFAB_STAMP`）。
- prefab variant・override を持つ instance・unpack した prefab、FBX そのもの（本スライスの model は `.obj`）、SampleGame の実データ: S2-2b の A1 が AC に入れるかを決め、入れない分は S5。
- 実 project（実データ）での上乗せ: S2-2b の A1 が AC に入れるかを決める（本スライスは合成の規模の fixture で測る。AC3b）。
- Player の build の差分 build、別の PC・`Library`・CI の agent をまたぐ cache: S3・S4（S2-2b の初稿 §2.3 と同じ送り先）。
- cache の鍵の仕組みの全体（Scene 以外の asset、cache の置き場と寿命）: 所有 program 未定。本スライスは、印の形の合否に要る範囲だけを読む。
- Unity への不具合報告を送るか: owner（S2-2 の追記のとおり、報告の材料は owner の再現 project にある）。

### 2.4 スライス分割の判断

- 契機: 独立して失敗しうる未知が 2 つある（印の形が成り立つか、検知の鍵が成り立つか）。
- 判断: 分割しない。検知の鍵（AC3）は build を伴わず、印の形（AC1・AC2・AC4・AC5）と別々に判定できる。片方だけが成り立った時の読みを §2.5 に定義してある。両方を同じ fixture・同じ batch で測るので、分けると Unity の起動と fixture の用意が 2 回になるだけである。

### 2.5 判定定義と停止規則

- **GO・YES:** AC0 と AC6 が成り立ち、AC1・AC2・AC4・AC5 を全て満たす印の形が 1 つ以上あり、AC3 を満たす public な検知の鍵（K1 または K3）が 1 つ以上ある。推奨する組は §2.7 の選び方で決める。
- **CONDITIONAL ACCEPT:** AC0 と AC6 が成り立ち、印の形は上のとおり成り立つが、AC3 を満たす鍵が internal の API（K2）だけ、または 1 つも無い。印の形と、検知の代替（K2 を reflection で使う、または Unity の API に依らない K4。§2.7）を並べて owner へ返し、owner が検知の条件の読みを決める。
- **GO・NO:** AC0 と AC6 が成り立ち、どの印の形も AC1・AC2・AC4・AC5 のどれかを満たさない。満たせなかった条件と観測を示して owner へ返す（3 条件のどれを見直すかは owner が決める）。
- **未判定（owner へ）:** AC0 が成り立たない、batch が record の終了行まで動かなかった、または AC6 が成り立たない。
- 停止規則: 上の 4 つのどれかに決まれば終了する。未判定の時だけ、原因が実験の script の誤りと分かる場合に限り、batch を 1 回だけやり直してよい（§5）。それ以外の追加の実測は、owner の指示なしに行わない。

### 2.6 A3 後の例外承認・決定記録

- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- **測る印の形（どちらも build の直前に text を書き、build の後に元のバイトへ戻す。Scene を Unity の API で開いて保存しない）:**
  - **M（`.meta` の `userData`）:** Scene の `.meta` の `userData` の行に印を書く。Scene 本体は触らない。`userData` に既に値がある時は、その値を残して後ろに足す（戻しは元のバイト）。
  - **F2（知らない key）:** Scene の file の末尾（最後の document である `SceneRoots`）に、Unity が知らない key の行を 1 つ足す（値は印）。object も fileID も足さない。戻しは file を元の長さへ切り詰める。Scene を全部読んで書き直さない。
  - **測らない形:** E（固定の fileID の GameObject を build の間だけ差し込む）は、M と F2 が先行確認で成り立ち、どちらよりも書く量と前提が多いので外した。Scene に何も書かない形（custom dependency）は先行確認で成り立たなかった（§1）。
  - **F（YAML のコメント行）は測らない。** 公開文書が非対応と明記しており（§1）、通っても製品の前提にできないため。
- **測る検知の鍵（Scene ごとに、`GetDependencies(scene, true)` のうち main asset が `GameObject` の物を path の序数順に並べ、鍵の値を連ねてハッシュにする）:** K1 `AssetDatabase.GetAssetDependencyHash`。K2 `GetSourceAssetFileHash` と `GetSourceAssetMetaFileHash`（internal、reflection）。K3 `AssetDatabaseExperimental.LookupArtifact` の結果の ID。build の列は K1 の値で印を作り、K1 が AC3 (a) を満たさないと build の前の観測で分かった時は K3、次に K2 に替える（batch の中で自動で選び、選んだ鍵を record に書く）。
- **測らない検知の代替（結論の文に候補として残すだけ）:** K4 は、file の更新時刻と大きさが前回と同じなら前回の sha256 を使い、違う時だけ読む（状態は `Library` の下で、source に残らない。初回と `Library` を消した後は全バイトを読む）。K5 は `AssetPostprocessor` で prefab の保存を拾い、依存する Scene に印を付ける（状態を持ち、Editor の外で変わった file の取りこぼしを別に塞ぐ必要がある）。どちらも Unity の未確認の前提を持たないので、実測の対象にしない。
- **推奨する組の選び方（結果を見る前に固定する）:** 印の形は、M と F2 の両方が成り立てば M を推奨し、F2 を代替として記録する（M は Scene 本体を書かず、`userData` は API として公開された欄で、Scene の text の形式に依らない。F2 は文書に無い「知らない key を無視する」に依る）。片方だけなら成り立った方。検知の鍵は K1、K3、K2 の順で、AC3 を満たす最初の物。
- **実験の置き場:** 実験の script と fixture は、実行担当が repository の外に新しく作る空の project（Unity 6000.6.0f1、package なし。先行確認と同じ作り）に置き、この repository に入れない。S2-2 の追記で使った owner の再現 project は使わない。script は build ごとの fixture を自分で作り、終わりに消す。
- 本スライスは public な型・メソッド、asmdef、製品の file を増やさない。

### 2.8 owner の決定と未決事項

owner は 2026-11-14 に次を決めた。未決事項は無い。

- **U1 目的・利用者の文言:** §1 の 2 欄を承認した。
- **U2 Unity の build を伴う実測:** §5 の 1 回の batch を実行してよい。project は repository の外に新しく作る空の project で、この repository の `Library` は使わない。実行は AI の session が行う。
- **U3 進め方の重さ:** slice のまま進める。A2 は 2 件（目的レビューと、実験の統制を見る反証レビュー。後者がアーキテクチャゲートを兼ね、「製品を変えない」ことと責務マップを見る）、B は script を書くだけ、C は batch の実行と record の判定、C' は script と record の blind 監査 1 巡。A3 の統合の後は凍結せず、draft PR の外部レビューを受けてから凍結する。
- **U4 program §8 の行:** このスライスを `S2-2b-0` として S2-2b の上に足し、S2-2b の行を「A1 初稿で保留、方式は S2-2b-0 の結論による」に替えた（A2 入力版の commit）。
- **U5 規模の fixture の数と、上乗せの合否の割合:** Scene 300、prefab 3,000（Scene 1 つが 30 個の prefab の instance を 200 個ずつ置き、prefab の 3 分の 1 は入れ子）、Scene の file は 1 つ約 1 MB、上乗せの割合は 5%。想定は中規模のゲーム開発（§1）。
- **GitHub Pages:** 結論が出たら、既存の GitHub Pages を更新する（§0 の harvest to）。

## 3. 責務マップ

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `docs/handoff/CONTENT_SCENE_STAMP_SPIKE.md`（本書） | 計画、実測の結果、判定 | — | Phase D で harvest して削除 | — | — | — | 作業台 | 新規 |
| `docs/handoff/PLAY_CONTENT_RESOLUTION_PROGRAM.md` | §8 の行（§2.8 U4） | このスライスの追加と S2-2b の状態 | program | — | — | — | 既存 | 2 行 |
| `docs/unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md` | 実測の結果（Phase D の harvest。判定の前には書かない） | 印の形と検知の鍵の事実 | 永続 | — | 公開面 | — | 既存の同じ節 | 数行 |
| （repository の外）実験の Editor script 1 つ | fixture の作成、検知の観測、build の列、record | — | batch 1 回。git に入れず、script と record は evidence bundle で残す | `UnityEditor` | — | — | 再現 project。I1 | 0 → 約 400 |

- 行数警報: なし。依存の向き・asmdef・Runtime / Editor の境界は変えない。
- 実験の script は 1 file にまとめる（残さない道具なので、構造の分割をしない）。

## 4. 実装計画

- 順序: (1) fixture を作る code。(2) 検知の観測（build なし）。(3) 印の形 M・F2 の書き込みと戻し。(4) build の列と record。(5) script を project に置いた状態で compile が通ることを、Editor を batch で 1 回起動して確かめる（build は呼ばない）。
- Phase B から Phase A へ差し戻す条件: §2.7 に無い印の形や鍵を足す必要が出た時、または build の回数が §5 の列を超える時。印の形が成り立たない等の結果は、差し戻さず record に残す（それが測りたい事実である）。
- 対象外を維持する方法: この repository の `unity/` を変えない（AC6）。

## 5. テストとレビュー計画

- 単体テスト: なし（製品の code を足さない）。
- 差し戻し中の起点 `-Filter`: なし。
- 判定必須テスト: 下の batch 1 回の record。
- 全 EditMode 回帰の適用除外: 適用しない。理由: この repository の実装を変えない（差分は文書だけ）。代替証拠: AC6 の `git diff --stat`。
- fixture（script が作り、終わりに消す）: 内側の prefab `Inner`（子の位置 v1 (1,2,3) / v2 (4,5,6)）、`Inner` を nest した `Outer`（自分の子 (7,8,9)。変えない）、共有の `Shared`（v1 (13,14,15) / v2 (16,17,18)）、model `Model.obj`（頂点 1 つの座標が v1 / v2 で違う）。Scene は `SOuter`（`Outer` の instance）、`SShared1`・`SShared2`（`Shared` の instance）、`SModel`（`Model` の instance）、対照の `SPlain`（prefab なし）。v1 と v2 の file は、最初に 1 回ずつ Unity に保存させたバイトを控え、以後はそのバイトを書き戻して切り替える（「前の版に戻す」が同じバイトになるようにする）。
- batch の順（Unity の process 1 つ。前の段が失敗しても、後の段へ進めるなら進めて record に残す）:
  1. **棚卸し:** `Assets` の下の全 file の path と sha256。
  2. **検知の観測（build なし、AC3）:** `Inner`・`Outer`・`Shared`・`Model` について K1〜K3 の値を、v1、`Inner` と `Model` を v2、v1 のバイトへ戻す、更新時刻だけ変える、の各時点で記録する。`Outer` の値が `Inner` の編集で変わるかも記録する（合否には使わない）。
  3. **印の読み取り（build なし）:** M と F2 の印を `SOuter` に書いて import し、Scene を additive に開いて、出た Error・Warning と root の数を記録し、保存せずに閉じて元のバイトへ戻す。
  4. **`CleanBuildCache()`**（1 回目）。
  5. **印なしの列（AC0）:** C1（全て v1）→ C2（`Inner` だけ v2）。
  6. **印の形ごとの列（M、次に F2）:** 列の始めに全て v1 へ戻し、B1（全て v1）→ B2（`Inner` だけ v2）→ B3（変更なし）→ B4（`Inner` を v1 へ戻す）→ B5（`Inner` と `Shared` を v2）。各回は、印を書く → build → 元のバイトへ戻す → 棚卸し。`SPlain` には印を書かない。
  7. **`CleanBuildCache()`**（2 回目）→ 基準 R（印なし、B5 と同じ source）。
  8. **規模の列（AC3b）:** 小さい fixture を消し、規模の fixture（U5 の数。prefab の子の位置は prefab ごとに違う値）を作る。印なしで build（cache を温める）→ N1 → N2 → 段 6 で成り立った印の形（§2.7 の選び方）で T0（印入りの Scene の初回なので作り直しになる。所要は記録だけ）→ T1 → T2。source はどの回も変えない。印の形がどちらも成り立たなかった時は、この段を行わない。
  9. fixture を消し、最後の棚卸しと終了行を書く。
- record が必ず持つ値: project の path、Unity の版、選んだ検知の鍵、段 2 の値の表、段 3 の観測、各 build の番号・印の形・source の版、印を入れた Scene の sha256、build の前後の棚卸しの差、各出力の file の名前と sha256、各 Scene の `.cf` の float 列の有無（S2-2 のスパイクと同じバイト走査）、Scene の serialized file を書いた数（AC2）、印の step と build の呼び出しの所要、終了行。
- 統合・Unity テスト: 上の batch。この repository の `tools/run-tests.ps1` は使わない（対象がこの repository の project でないため）。
- 操作・実行時・目視条件の検証経路: 担当は Phase C の実行担当（AI の session）。環境は owner の PC、実験の project を開いている Editor が無いこと。操作は `<Unity 6000.6.0f1 の Unity.exe> -batchmode -nographics -projectPath <実験の project> -executeMethod <script の入口> -logFile <log>`（先行確認と同じ起動）。観測は record の終了行と上の値。record・log・script を evidence bundle に入れ、判断担当と C' へ渡す。process の自然終了をゲートにしない（終了行の後も終わらなければ、その PID だけを止め、時刻と PID を実行の記録に残す。既知の終了 stall は `tools/Harness/README.md` の規則のとおり調査しない）。動作確認は `対象なし`。
- 未知の操作経路: 印（M・F2）が入れ子 2 段・共有の prefab・model・複数の Scene でも効くか、規模での上乗せ。小さい fixture での成立は先行確認済み（§1）。**未確認。初回確認はこの batch（段 2・段 3）。** 理由: Phase A の主担当は Unity の build を行わない。不成立時: その印の形・鍵を不成立として record に残し、§2.5 で読む（基盤の不備ではなく、測りたい事実）。成立済み: repository の外の空の project で batch から Content Directory を build する経路、`.cf` のバイト走査、`BuildContentTEP.json` の読み取り（先行確認、S2-2 のスパイクと追記）。
- やり直し: 未判定で、原因が script の誤りと分かる時に限り 1 回。
- 人間の判断が必要な条件: AC3b で N1 と N2 の差が合否の割合より大きかった時の読み（owner）。
- 高コスト証拠: なし（小さい build 13 回と規模の fixture の build 6 回。規模の列の所要は fixture の数に依り、測るまで分からない。消す cache は実験の project の物だけ）。
- 判定 C の照合 script の方式: 持たない。判断担当が record を AC ごとに読み、期待値は上の fixture の値から導く。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: 本 session（Anthropic）。入力は owner の依頼（問い、3 条件、候補）、事実の文書の同じ節、commit `52612ede` の S2-2 の HANDOFF §7、S2-2b の A1 初稿、`UnityContentDirectoryAdapter.cs`、Unity 6000.6.0f1 の assembly と XML doc、Unity 6000.3 の manual。
- A2 の担当: 2 件とも Anthropic の系列で、A1 の主担当と別のモデルの新しい文脈に依頼する。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。Phase A では使わない。
- 独立性の強化条件を満たせない場合の理由: 対象外（risk は `normal`）。

### 5.1 A2 の独立レビューと A3 の採否

未着手。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
