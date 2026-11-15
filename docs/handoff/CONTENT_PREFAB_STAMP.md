# Scene に焼き込む prefab の版の印（S2-2b）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2b）
- status: `A`（A3 統合版に凍結前の外部レビュー 2 巡（PR #193）と主担当の点検を反映した版、未凍結。A3 凍結の承認待ち）
- branch: `claude/content-prefab-stamp`（PR の base は `develop`）
- implementation base commit: `980a69ad`（`develop` の head、2026-11-15。S2-2b-0 の Phase D（PR #191）を含む）
- implementation head commit: `未固定`
- risk: `high`（全ての製品の Content Directory build が、build の間だけ利用者の Scene の `.meta` を書き換える。破壊的な生成器に当たる）
- owner: root（依頼者）
- created: 2026-11-14
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4（「既知の不成立（未解消）」の段落を、印の契約に置き換える）、[13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「既知の不成立」の行、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」と「印の形と検知の鍵」（製品の経路で測った結果と測り直し）、program §8 の S2-2b の行
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§5.3、§8 の S2-2・S2-2b-0・S2-2b の行）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）。本スライスは `Scripts/Runtime/` を変えないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない制約の対象外
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script を持たない。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票（2026-11-15、A3 の凍結へ）:
  - 次の Phase・役割: Phase A の A3（主担当と owner）。この票を含む commit は、A3 統合版に凍結前の外部レビュー 2 巡（PR #193）と主担当の点検を反映した版（未凍結）である。owner が凍結を承認したら、票を Phase B へ向けて書き直して A3 凍結版を commit する（引き継ぐ commit は凍結版の commit で、SHA は起動指示と PR 状態ブロックで渡す）。外部レビューをもう 1 巡受ける場合は、指摘を §5.1 の表に足して分類する（目的・利用者の 2 欄の文言を問う指摘は owner が裁定し、2 欄を変えたら未関与の担当で目的レビューを 1 回やり直す）。
  - 読む範囲: Phase A の定めどおり全節。
  - 開始時の照合: Phase A の内部なので SHA の照合は無い。ブランチ `claude/content-prefab-stamp` の HEAD がこの票を含む commit で、作業ツリーが dirty でないことを確かめる。
  - 必要な権限: なし（A3 は Unity を起動しない）。
  - 引き渡さないもの: なし。
  - Phase A の commit の例外: Phase A の commit は A2 入力版と A3 凍結版の 2 点と定めるが、owner の外部レビューの指示（2026-11-15）で、A3 統合版とその外部レビューの修正を公開 PR の commit として積んでいる（公開 PR の 1 巡ごとの修正 1 commit）。凍結版は別の commit として切る。
  - 未決: A3 凍結の承認。
## 1. 目的と対象外

- 目的: 製品の build 経路で Content Directory を build する時、Scene に置いた prefab の instance（入れ子を含む）を編集した後に同じ `Library` で build し直しても、出力の Scene が、build した時点の手元の prefab の版を焼き込んだものになるようにする。目的 build と上書きの build の両方に効かせる。cache を捨てず、prefab が変わっていない Scene では差分 build の再利用を失わない。prefab が変わったかの検知は、prefab の file を読まない軽さに保つ。build が終わった後の source には、build が書いた物を何も残さない。Editor で開いて保存していない Scene を含む build は止めてよく、その編集を壊さない。
- 利用者: 製品の build 経路（`BuildContentCoordinator`）で Content Directory を build する開発者（主に、実機へ転送する build をローカルの Editor で作る人）と CI。目的 build を作る人と、上書きを作る人の両方。build の呼び手（host application の Editor code）に新しい入力を求めない。
- 目的・利用者の文言の経緯: 2 欄は本書で初めて書いた文で、承認された前の版は無い。2026-11-14 の初稿（未承認）から、owner の 3 条件（cache を捨てない、検知を重くしない、build の後の source に何も残さない）と利用場面（実機へ転送するローカルの build と CI、未保存の Scene は止めてよい）に合わせて書き直した。初稿の「印の置き場のほかに build が書いた物を残さず」は、M では置き場が無いので「何も残さない」にした。2026-11-15 に owner（root）がこの 2 欄を承認した（§2.8 U1）。
- 対象外:
  - `Scripts/Runtime/` の変更。登録の実装（S3）。coordinator と snapshot の作り方の変更。
  - Player の build（`BuildPipeline.BuildPlayer`）の差分 build。事実の文書は Player を測っていない。
  - prefab の instance 以外の理由で Scene の出力が古くなる経路（script の code・field の既定値の変更、lightmap、sprite atlas、import 設定・stripping の設定の違い）。
  - 外で変えて Unity が import していない prefab（Auto Refresh を切った Editor で外部の道具・VCS が file を変えた時）。製品の snapshot は今も import 済みの状態を前提に作るので、印も同じ前提に乗る（§2.1 S1 の経路）。
  - prefab variant・model（FBX 等）の instance と、override を持つ instance の実機の確認。印の規則（§2.7）はこれらを対象に含めるが、AC の fixture には入れない（§2.3）。
  - ロック方式の VCS（checkout・lock の操作）。読み取り専用の `.meta` は拒否するだけ（§2.7）。
  - `Assets/` の外（`Packages/` の embedded・local package）の prefab の instance。製品の snapshot の閉包は `Assets/` の下だけなので、その編集は印に入らず、Scene の出力は古いまま Warning も出ない。S4（閉包の使い回し）を守る限り対象外で、harvest 先の「既知の不成立」に残す。
  - Scene の `.meta` の `userData` に値を持つ project。印を書く Scene の `userData` が空でなければ build を止める（§2.7）。この repository の Scene は全て空。
  - 別の PC・`Library` の間で cache を共有すること。
  - 事実の文書の「build をまたぐ同じ file」の書き直し（§2.8 U4）。
- 現況（base `980a69ad`）:
  - **不成立は実測済み。** prefab を編集した後に同じ `Library` で build すると、Scene の build instruction が変わらず、前回書いた Scene の serialized file が再利用され、Scene に焼き込まれた prefab が古い版のまま出る。build は成功し、Warning も出ない。上書きに限らず、目的 build でも起きる。cache の鍵は Scene の file のバイトで、prefab の状態は鍵に入らない（[事実の文書](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」）。
  - **印の形と検知の鍵は S2-2b-0 で決まった（2026-11-14 の owner 決定、PR #191）。** 印の形は M: build の直前に Scene の `.meta` の `userData` の行へ印の文字列を text で書き、build の後に元のバイトへ戻す（Scene 本体は書かず、Scene を Unity の API で開かない）。Scene の `.meta` のバイトも再利用の鍵に入る。検知の鍵は `AssetDatabase.GetAssetDependencyHash`（public）: prefab と model の編集で値が変わり、元のバイトへ戻すと最初の値に戻り、更新時刻だけでは変わらず、入れ子の外側の prefab の値は内側の編集でも変わった。印の値は、Scene の依存閉包（製品の snapshot と同じ除外）のうち main asset が `GameObject` の物と `ModelImporter` が import した物を path の序数順に並べ、鍵の値を連ねたハッシュ。repository の外の空の project の 1 つの batch（Content Directory の build 36 回）で、入れ子の内側・2 つの Scene が共有する prefab の編集が、焼き込む Scene だけを書き直し、変更なしと前の版へ戻した build は Scene を 1 つも書かず前回の出力と一致し、全ての build の後に `Assets` の下の全 file が build の前のバイトに戻り、印の文字列は出力に無く、印を書いた最後の build の出力は同じ source を印なしで build した出力と一致した（事実の文書「印の形と検知の鍵」）。
  - **上乗せの値と前提。** 規模の fixture（Scene 300・prefab 3,000）の何も変えていない build で、M の上乗せは印なしの build の最小（約 15.8 秒）の 4.75〜5.16%（5% の合否は未判定）。印の計算は約 110 ms で、上乗せの約 8 割は書き込み・戻し・2 回の `Refresh` だった。**この値は、印の計算と書き込みを製品の snapshot の後・adapter の `Refresh` の前に置き、その snapshot の閉包をそのまま使う場合の値である。** 閉包を `Refresh` の後に作り直す形では約 3 秒（割る値の約 2 割）が乗り、5% を大きく超える。印の `Refresh` を製品の `Refresh` とまとめる手順と、鍵の結果を Scene の間で使い回す手順は測っていない（事実の文書の同じ節）。
  - **Unity の build を呼ぶのは adapter 1 か所。** 製品で `BuildPipeline.BuildContentDirectory` を呼ぶのは `UnityContentDirectoryAdapter.Build`（`Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs`、173 行）だけ。順は `ValidateTarget` → `CleanupOrphans`（前回の異常終了で残った生成 folder を marker で見分けて消す）→ 生成 folder と marker の作成 → `AssetDatabase.Refresh()`（42 行）→ `try` の中で root の GUID の照合、`BuildContentRoot` の作成と import、build（100 行）→ `finally` で生成物を消す。`BuildContentOptions.None`。
  - **snapshot の閉包は adapter に届いている。** coordinator は `BuildContentProjection.Create(plan, snapshot)` で、選択 root ごとに snapshot の閉包（`ProjectedContent.Closure`、GUID と path の列）を持つ projection を作り、そのまま adapter に渡す。閉包は `AssetDependencySnapshotBuilder` が root ごとに `AssetDatabase.GetDependencies(path, true)` から `Assets/` の下の `.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref` でない file を集めた物で、`.prefab` も model の file も入る。製品の呼び手は SampleGame の `SampleGameContentBuild`（目的 build）と、host が作る上書きの build（S2-2 の `ContentOverlaySelection` で root を選ぶ）。どちらも snapshot を作ってから coordinator を呼び、その間に `Refresh` を呼ばない。
  - **build の入口は coordinator 1 つ。** `BuildContentCoordinator.Build`（208 行）が projection の検査、preflight、adapter、staging → final、outcome の順を持つ。adapter の例外は `BuildFailure` の issue と outcome になる。
  - **この repository の Scene に prefab instance は無い。** `unity/Assets` の Scene（668 件）に source prefab を持つ instance が無く、Scene の `.meta` の `userData` は全て空（2026-11-15 に base で確認）。Scene の `.meta` は `DefaultImporter` の形で、`userData:` は 1 行。
  - **Scene は text で保存される。** `ProjectSettings/EditorSettings.asset` の `m_SerializationMode: 2`（Force Text）。M は Scene 本体ではなく `.meta`（常に text）を書くので、この設定に依らない。
  - **build の間だけ source を変えて戻す前例が 2 つある。** adapter の生成 root（上記）と、SampleGame の `PlayerBuildProjectMutation`（Player build の間だけ Project の設定と生成 asset を変え、`Dispose` で戻し、marker で自分の残りを見分ける）。どちらも新しい asset を足して消す形で、利用者の既存の file を書き換えて戻す前例は無い。
  - **既存の不成立の記録:** 18 章 §4 の「既知の不成立（未解消）」は、印の方式までの間、Scene に prefab を置く project でこの入口を使う時は build の前に `BuildPipeline.CleanBuildCache()` が要ると書いている。
- UC ごとの利用者の入力:

  **UC-1 Content Directory を build する**（目的 build と上書きの build。呼び手は host の Editor code か CI）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | （新しい入力なし）既存の build の入力（plan、snapshot、contentSet、mode 等） | 既存のまま | 目的の「呼び手に新しい入力を求めない」 |

  機械が決めるもの（自動導出）: 印を書く Scene（root の Scene のうち、閉包に印の対象を持つ物）、印の値（§2.7）、書いた `.meta` の元のバイトと戻し、途中で落ちた build の残りの戻し。利用者の開発時の宣言・操作は無い（M は Scene に置き場を要らない）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Scene の `.meta` に、焼き込む prefab の版の印（`GetAssetDependencyHash` から作る値）を build の間だけ書く方式を製品の build 経路に入れた時、同じ `Library` の差分 build でも Scene に置いた prefab（入れ子を含む）の編集が目的 build と上書きの出力に入り、prefab が変わっていない Scene の再利用を失わず、検知が製品の snapshot の閉包を使い回す軽さに収まり、build の後の source が build の前のバイトに戻ると言えるか。

### 2.1 進める最低条件

**不変条件**（起きてはいけないこと）

- **I1 build の後に、build が書いた物を source に残さない。** build の成否にかかわらず、build が書いた Scene の `.meta` は build の始めのバイトに戻り、Unity の `AssetDatabase` も戻した版を見る。Scene の file 本体と、`Assets/`・`ProjectSettings/` の他の file は、build の前後で同じバイトである。build の間に build 以外が `.meta` の印の行以外を変えていた時は、印の行だけを元の形に戻し、他の行は触らずに Warning で知らせる。異常終了で戻せなかった印は、記録が残っていれば次の build で戻り、その build の後に元のバイトになる（記録が失われた印は、その Scene を root に含む次の build で戻る）。印が残っていることを理由に次の build を止めない。
  - 主張を崩しうる既存の経路: (a) 書いた後、`try` に入る前の処理（生成 folder の作成、marker の書き込み、製品の `Refresh`）が例外で止まり、戻しの処理に入らない（現在の adapter は `try` を 42 行の `Refresh` の後に始める）。(b) Unity の build 自体の異常終了・Editor の強制終了で `finally` が走らない。(c) 戻した `.meta` を `AssetDatabase` が import し直さず、次の操作（次の build の snapshot・利用者の Inspector）が印の版の `userData` を見る。(d) build の後、戻す前に Unity が asset を保存し直し（`SaveAssets` 等で importer を書き戻し）、元と違う版で `.meta` を上書きする。(e) 次の build の始めの「残りの戻し」が、利用者が後から変えた `.meta` を古いバイトで上書きする。(f) 書いている途中で 1 つの `.meta` の書き込みが失敗し（別の process が共有なしで開いている等）、それまでに書いた分が残る。(g) 書き込みと戻しが起こす Scene の再 import に、利用者の project の `AssetPostprocessor` が反応して別の source file を書く。(h) 異常終了の後に `Library` の記録が失われ（利用者が `Library` を消す）、次の build が印の付いた `.meta` を「元のバイト」として保存し、以後その版へ「戻し」続ける。(i) `finally` の片付けの 1 段（戻しの import、生成物の `DeleteAsset`）が例外を出し、残りの段が走らない。(j) 製品の `Refresh` が保留中の script の変更を compile し domain reload を起こすと、C# の `finally` が走らない（(b) と同じく次の build の始めの戻しで回復する）。(k) 開いている Editor で、`.meta` の書き込みが製品の `Refresh` より前に自動 refresh で import される（batch でしか測っていない）。(l) Scene を移動・改名した後に、記録の path が古いまま戻しが file を見つけられない。
  - 探索した境界: adapter と coordinator の build の手順、`PlayerBuildProjectMutation` の前例、S2-2b-0 の batch（戻しと戻し後の `Refresh` で `Assets` の下の全 file が元のバイト）、A2 のアーキテクチャゲートが挙げた (f)〜(i)、外部レビュー（PR #193）が挙げた (k)・(l)、主担当の点検で足した (j)。未確認: 開いている Editor（batch でない）での `.meta` の書き換えと戻しの観測、Unity の build が importer を保存し直すか、host の project の `AssetPostprocessor`（SampleGame 以外）。
- **I2 保存していない編集を壊さない。** build が印を書く Scene が Editor で開かれて未保存の変更を持つ時、build はどの file も書く前に止まり、未保存の変更はそのまま残る。印を書く Scene の `.meta` が読み取り専用の時も、どの file も書く前に止まる。
  - 主張を崩しうる既存の経路: 既存の build は、開いている Scene の未保存の変更を無視して保存済みの file を build する（書かないので無害だった）。印を書くと Scene の asset が import し直され、Editor が開いている Scene を disk から読み直すと未保存の変更が消えうる。複数の Scene のうち一部を書いた後に別の Scene で止まると、書いた分が残る（I1 (f)）。前回の異常終了で残った印の戻しは `.meta` を書いて import するので、検査より前に行うと、印が残った Scene を開いて未保存の変更を持たせた状態で、戻しの import が未保存の変更を消してから止まる（外部レビュー F1）。
  - 探索した境界: 同上。未確認: 開いている Scene の `.meta` を書き換えて import させた時に、Editor がその Scene を読み直すか・dialog を出すか（§5 の未知の操作経路）。
- **I3 印は出力に入らない。** 出力のどの file にも印の文字列が無く、印を入れた差分 build の Scene の file は、同じ source を cache を捨てて build した時の Scene の file と同じ sha256 である。
  - 主張を崩しうる既存の経路: `userData` が Scene の serialized file や他の file の中身に入る。Scene 以外の file の差（PR #180 の未統制の観察: script をまとめた file・URP `Lit`・`Resources/unity_builtin_extra` が build の組み合わせで別の hash になった。§2.8 U4）は印と無関係にも起きるので、本条件の判定に使わず record にだけ残す（A2 のアーキテクチャゲートの指摘 F11 で絞った）。
  - 探索した境界: 事実の文書の「build をまたぐ同じ file」、PR #180 の観察、S2-2b-0 の「印を書いた最後の build の出力は、同じ source を印なしで build した出力と Scene の file を含めて一致」。製品の経路（`BuildContentRoot` と root の identity）では未確認。

**成立条件**（起きなければならないこと）

- **S1 焼き込んだ prefab の編集が、同じ `Library` の差分 build の出力に入る。** 入れ子の prefab の内側を編集した時と、複数の Scene が共有する prefab を編集した時を含む。目的 build（`Full`）と上書きの build の両方。
  - 前提: snapshot を作る時点で、手元の prefab が Unity に import 済みであること（製品の snapshot の既存の前提。外で変えて import していない prefab は対象外。§1）。
- **S2 prefab が変わっていない Scene は作り直されない。** prefab を変えずに build し直した時、Scene の serialized file は 1 つも書かれず、prefab を前の版に戻した build は、その版で build した時と同じ Scene の file（同じ sha256）を出す。
- **S3 印を要らない Scene の file と `.meta` は、build の前後で一度も書かれない。** 閉包に印の対象（§2.7）を持たない Scene が該当する。
- **S4 検知は製品の snapshot の閉包をそのまま使う。** 印の計算は projection の root の閉包だけを入力にし、依存閉包を作り直さず（`GetDependencies` を呼ばず）、asset の file のバイトを読まない。印の書き込みは製品の `Refresh`（現在の 42 行）より前に行い、印のための `Refresh` を別に呼ばない（program §8 の S2-2b の gate。上乗せの値はこの置き方の下でだけ成り立つ）。

### 2.2 受け入れ条件

AC の観測は全て §5 の Unity バッチと EditMode test・単体 test で閉じる。「最終の版」は、その build の直前の手元の source の版をいう。

- **AC1（S1）入れ子と共有の prefab の編集が入る。** §5 の fixture で、build の列 B1〜B6 と B5' の各回の出力に含まれる各 Scene の `.cf` が、その回の最終の版の子の位置の float 列を全て持ち、前の版の float 列を持たない（B6 は上書きなので、出力に含まれる Scene だけを見る）。合格例: B2（内側の prefab だけ v2）で、外側の prefab の instance を置いた `SOuter` の `.cf` が内側の v2 の列を持つ。不合格例: B2 で、内側の prefab を外側の prefab 経由でだけ焼き込む `SOuter` が v1 の列を持つ。
- **AC2（S1）上書きにも入る。** B6 は、B5 と同じ `Library` で、`Outer` だけを v1 に戻した source から、編集した prefab をもとに `ContentOverlaySelection` が逆引きした root だけを上書きとして build する。`SOuter` の (`Outer` v1, `Inner` v2) は B2 と同じ組み合わせで、直前の B5（(v2, v2)）とは違う。上書きの出力の `SOuter` の `.cf` が (7,8,9) と (4,5,6) を持ち、(10,11,12) を持たず、`SOuter` の file の sha256 が B2 の `SOuter` と一致する（S2-2 の F-1 が、この経路で起きない。印が効かない実装は B5 の版を出す）。
- **AC3（S2）再利用。** Unity の `Library/BuildHistory/<build>/BuildContentTEP.json` で、`Incremental` の回だけを数える（`Full` は workspace を空にするので数えない）。prefab を変えていない回（B3）は Scene の serialized file を 1 つも書かない（`BuildPipeline::WriteSerializedFile` の event の数え方は S2-2b-0 と同じ）。B2 で書く Scene の serialized file は `SOuter` の分だけである。prefab を前の版に戻した回（B4）の各 Scene の file の sha256 が、同じ版で build した回（B1）と一致する。
- **AC4（S3）印を要らない Scene は書かれない。** fixture の対照の Scene `SPlain` の file と `.meta` は、全ての build の前後で sha256 と更新時刻が変わらない。
- **AC5（I1）build の後に source が戻る。** harness の各 build で、`coordinator.Build` が戻った直後（harness が `Refresh`・`ImportAsset` を呼ぶ前）に、fixture の各 Scene の `.meta` の sha256 が build の前に harness が記録した値と一致し、`AssetImporter.GetAtPath(<scene>).userData` が build の前の値と一致し、`Assets/`（fixture の folder の中の Scene とその `.meta` 以外を含む）と `ProjectSettings/` の全 file の sha256 が build の前と一致し、adapter の生成 folder（`Assets/OneStarMakerGenerated/BS2b` の下）が残っていない。各 Scene の file 本体の sha256 と更新時刻は変わらない。失敗経路（EditMode test。adapter の Unity の build の呼び出しを差し替える継ぎ目で確かめる。「元の値」は異常を起こす前に test が記録した値）: (a) 印を書いた後に build の呼び出しが失敗（継ぎ目が失敗を返す、または例外を投げる）した時も戻る。(b) 印を書いた後、build の呼び出しより前に止まった時（root の GUID の不一致を test が起こす）も戻る。(c) 印を書いたまま戻さずに終わった時（異常終了を模して、書いた後に戻しの処理を呼ばない）、次の build は止まらずに成功し、build の後にその `.meta` が元の値に戻る。次の build がその Scene を再び印の対象に含む場合（`SOuter` を root に含む）と、含まない場合（記録だけが残る）の両方で確かめる。対象に含む場合は、build の間の `.meta` の書き込みが今回の印の 1 回だけである（戻した中間のバイトを書かない）。(d) (c) の後に利用者がその `.meta` の印の行以外を変えていた時は、次の build は印の行だけを元の形に戻し、他の行を変えず、`Debug.LogWarning` で Scene の path を出して続ける（`LogAssert.Expect` で確かめる）。(e) 印を書く Scene が 2 つあり、2 つ目の `.meta` を test が `FileShare.None` で開いたまま build した時、1 つ目の `.meta` が元の値に戻り、build は `BuildFailure` を返す。(f) 記録が無く、`userData` が印の形の行を持つ `.meta`（`Library` を消した後を模す）の Scene を印の対象に含む次の build は、止まらずに成功し、build の後に file 全体の sha256 が印を書く前の値と一致し、Warning が出る。(g) 印を書いたまま戻さずに終わった後、その Scene を移動してから、移動後の Scene を印の対象に含む build をした時も、build は成功し、build の後に移動後の `.meta` が元の値に戻る。
- **AC6（I3）印は出力に入らない。** 全ての build の出力の file のバイトに、印の文字列の接頭辞が無い。B5 の出力の各 Scene の file が、`BuildPipeline.CleanBuildCache()` の後に同じ source で build した B5' の Scene の file と同じ sha256 である。Scene 以外の file の B5 と B5' の差は record に残すが、この AC の合否に使わない。合格例: `SOuter`・`SShared1`・`SShared2`・`SPlain` の file が B5 と B5' で同じ sha256。不合格例: 印を入れた `SOuter` の file だけが B5 と B5' で違う。
- **AC7（I2）未保存の編集と読み取り専用で止まる。** EditMode test で、(a) 印を書く Scene を Editor で開いて未保存の変更を持たせた時、(b) 印を書く Scene の `.meta` を読み取り専用にした時、(c) 印を書く Scene の `.meta` の `userData` が自分の印でない値を持つ時（自分の印の接頭辞で始まり形が違う値を含む）、build は `BuildFailure` の outcome を返し、その message が Scene の path を持ち、build の root のどの Scene の file と `.meta` も sha256 が変わらず、(a) の未保存の変更が Editor に残っている。止まるのは印を書く前で、差し替えた Unity の build の呼び出しは 0 回。印を書く Scene が 2 つあり、2 つ目だけが (a)〜(c) の時も、1 つ目の `.meta` は書かれない。(d) 前回の印が残った Scene（記録あり）を開いて未保存の変更を持たせた時も、build は同じく止まり、どの `.meta` も書かず（残った印も戻さない）、未保存の変更が残る。(e) `userData` の値が空白 2 つ以上・タブなど、§2.7 の空の形でない時も (c) と同じく止まる。
- **AC8（I1・I2）開いている変更の無い Scene。** 印を書く Scene を Editor で開いて変更を持たない時、build は成功し、build の後もその Scene が開かれたまま未保存の変更を持たず、`.meta` は AC5 のとおり戻る（EditMode test。継ぎ目が成功を返す）。この AC の経路は未確認（§5）。
- **AC8'（AC8 の代替）。** 印を書く Scene が Editor で開いていれば、変更の有無にかかわらず build は `BuildFailure` を返し、どの file も書かれず、差し替えた Unity の build の呼び出しは 0 回（EditMode test）。Phase B が AC8 の経路を確かめて不成立だった時だけ、AC8 に替えてこの AC を判定に使う。どちらを採ったかを §6 に記録する（U3 (b) への切り替え。A3 で合意済み）。
- **AC9（S4）閉包の使い回しと書き込みの時点。** 単体 test で、印の値が、渡した閉包の path の列と、path ごとに返す（印の対象か、鍵の値）だけから決まり、閉包の並びの順に依らない。Phase C が差分で次を確かめる。新しい 2 file と adapter の差分に `GetDependencies` の参照が無い。印の値の計算が asset の file のバイトを読まない（記録の置き場と `.meta` の読み書きは許す）。印の書き込みが製品の `Refresh` より前にあり、印を build に読ませるための別の `Refresh` が無い（戻しの `ImportAsset` は数えない）。検査の後から製品の `Refresh` までが `AssetDatabase.DisallowAutoRefresh`／`AllowAutoRefresh` の対で囲まれ、`finally` で戻る。
- **AC10（全体）非退行と範囲。** 全 EditMode 回帰が通る（XML の failed 0 と root result `Passed`）。差分は §3 の file と §5 の harness と文書だけで、`Scripts/Runtime/`、coordinator、SampleGame の source を変えない。

### 2.3 ここでは答えない問いと所有する後続

- prefab variant・model（FBX）・override を持つ instance を置いた Scene での実機の確認、実データ（SampleGame）での確認: S5（実データの接続）。印の規則は対象に含めるので、不成立なら S5 から本書の契約へ戻す入力になる。
- 規模の fixture での上乗せの測り直し（印の `Refresh` を製品の `Refresh` にまとめた置き方の値）: 2026-11-15 の owner 決定（U5）で本スライスでは測らない。所有 program 未定。
- Player の build の差分 build で同じ取りこぼしが起きるか: 所有 program 未定。program §8 の S3 の Player の非退行の入力として送る。
- 別の PC・`Library` の間の cache、CI の agent をまたぐ cache: S4。
- 外で変えて import していない prefab を build 前に import させること（snapshot の前の `Refresh`）: 所有 program 未定（製品の snapshot の前提の変更で、本スライスの対象外）。
- ロック方式の VCS と連携して `.meta` を書くこと: S5。
- script の code・field の既定値を変えた時の Scene の出力: 所有 program 未定（事実の文書の測っていないもの）。
- PR #180 の未統制の観察を事実の文書へ移すこと: 本スライスと切り離した小さな文書の変更（2026-11-15 の owner 決定、§2.8 U4）。本スライスは AC6 で印が他の file を変えないことだけを確かめる。

### 2.4 スライス分割の判断

- 契機: 高リスクの初回実機スライスで、独立して失敗しうる未知が複数ある（(1) 印の書き込みを製品の `Refresh` にまとめた時に Unity の build が印を読むか、(2) 開いている変更の無い Scene の `.meta` を書いて戻した時の Editor の振る舞い、(3) 製品の経路（`BuildContentRoot` 経由の root）で入れ子と複数の Scene に印が効くか）。
- 判断: 分割しない。(1) と (3) は AC1・AC3 で判定でき、(1) が不成立なら、書いた後に Scene ごとに import させる形への切り替えは S4 の置き方（上乗せの値の前提）に触れるので、B 適応にせず A 再開とする。(2) は AC8 で単独に判定でき、不成立時の代替を A3 で先に合意する。どれも同じ 1 つの機構の成否で、別の問いの判定に使う AC は無い。印の形と鍵の成否そのものは S2-2b-0 で製品の外で確かめ済みで、本スライスは製品の経路への組み込みだけを問う。

### 2.5 判定定義と停止規則

- GO: §2.1 の不変条件と成立条件が、§2.2 の AC で全て成り立つ。AC8 と AC8' はどちらか一方が成り立てば足りる（どちらを判定に使ったかは §6 の記録に従う）。
- NO-GO: どれかの AC が成り立たない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。

### 2.6 A3 後の例外承認・決定記録

- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

2026-11-15 の owner の回答（U3・U5）と A3 の採否（§5.1）を反映した版。

- **書く担当と時点:** `UnityContentDirectoryAdapter.Build` が、`ValidateTarget` と `CleanupOrphans` の後に、検査、残りの戻し、印の書き込みをこの順に行い、生成 folder の作成と製品の `AssetDatabase.Refresh()` より前に書き終え、その `Refresh` で Unity に読ませる。印のための `Refresh` を別に呼ばない。印の scope（`IDisposable`）は 1 つ目の `.meta` を書く前に開き、書き込みのループを scope の中に置く。検査の後から製品の `Refresh` までを `AssetDatabase.DisallowAutoRefresh` と `AllowAutoRefresh` の対で囲み（`finally` で戻す）、開いている Editor の自動 refresh が印の書き込みを製品の `Refresh` より前に import しないようにする。scope は生成 folder の作成・製品の `Refresh`・root の作成・build を囲み、どこで例外が出ても戻す（現在の `try` の開始位置を前へ広げる）。coordinator は変えない。目的 build と上書きの build の区別をしない（どちらも同じ adapter を通る）。
- **印を書く Scene:** projection の root のうち `.unity` の物で、その root の閉包（`ProjectedContent.Closure`）に印の対象を 1 つ以上持つ物。対象を持たない Scene の file と `.meta` には触れない。この選択は純粋な規則（§3 の `ScenePrefabStampRules`）に置く。
- **印の値:** root の閉包のうち、main asset の型が `GameObject` の物と、importer が `ModelImporter` の物を印の対象にし、path の序数順に「path と `AssetDatabase.GetAssetDependencyHash(path)` の値」を連ねた列の sha256 を 16 進で書く。閉包を作り直さず（`GetDependencies` を呼ばず）、asset の file のバイトを読まない。PC ごとに値が違っても、cache が当たらないだけで出力の正しさは変わらない。鍵の結果を Scene の間で使い回すかは B が決めてよい（同じ path の鍵を 1 回の build の中で 1 回だけ取る形は、値を変えない）。
- **全てを検査してから書く:** source へ書く操作と import の前に、今回書く全ての `.meta`（印を書く Scene と、次の build の始めの戻しの対象）について検査する。順は「検査 → 戻し → 印の書き込み」で、検査で止まる時は戻しも行わず、残った印は次に書き込める build が戻す。未保存の変更は Editor に load されている全ての Scene（active でないものを含む）から見る。印を書く Scene については、`.meta` の `userData` の行が 1 行で見つかり、その値が「元から空」「自分の残した印（戻せる）」「それ以外」のどれかを分類し、`.meta` が読み取り専用か、Scene が Editor で開かれて未保存の変更を持つか（AC8' を採った時は「開いているか」）を先に調べ、1 つでも止まる理由があれば、どの file も書かずに例外で止める（coordinator の既存の経路で `BuildFailure` になる。message に Scene の path と理由を書く）。`userData` が「それ以外」（自分の印でない値、複数行の値、行が無い）の `.meta` は止める理由に入れる。自分の残した印は止める理由にせず、下の「戻し」で戻してから今回の印を書く（止めると残った印が永久に戻らない。PR #193 の外部レビュー 2 巡目の F1）。自分の印の形は、固定の接頭辞・末尾の空白の有無の 1 文字・64 桁の 16 進がそろった値だけとし、接頭辞が同じでも形が違えば「それ以外」とする。空の形は `userData:` の直後が行末か、空白 1 つで行末の 2 つだけとし、空白 2 つ以上やタブは止める（印の行を元の形へ戻す規則がバイトで一意になる）。戻しの対象については、読み取り専用か、Editor で開かれて未保存の変更を持つかを調べる。
- **書き方:** `.meta` の text の `userData:` の行の値だけを印の文字列に置き換え、他の行と改行の形（LF・CRLF）は変えない。印の文字列は固定の接頭辞と、元の行の末尾の空白の有無を表す 1 文字と、値の 16 進からなり、英数字と `-` だけを使う（引用なしの YAML の scalar として有効で、Unity が書き直す理由を作らない）。書き込みは `Library` の下の一時 file に書いてから置き換え、途中で落ちても `.meta` が半端なバイトにならないようにする（一時 file を `Assets/` の下に置かない）。Scene を Unity の API で開いて保存しない。製品の `Refresh` で印を Unity が読まない（AC1 不成立）時に、書いた後に Scene ごとに `AssetDatabase.ImportAsset` で読ませる形へ替えるのは S4 に触れるので、B 適応にせず A 再開とする。
- **記録:** 書く前に、`.meta` ごとの元のバイトを `Library` の下の本スライス専用の場所に保存し、Scene の path・元の sha256・書いた後の sha256 を記録してから書く。記録は Scene の GUID も持ち、file 名は Scene の GUID から作り、path の長さを Scene の path に依らせない。戻す時は GUID から今の path を引き（`AssetDatabase.GUIDToAssetPath`）、Scene の移動・改名の後も戻せるようにする。記録の置き場は internal の引数で差し替えられ、test は一時 folder を使う。
- **戻し（build の後と、次の build の始めで同じ規則）:** Scene ごとに、今の `.meta` が (1) 書いた後の sha256 のままなら元のバイトに戻す。(2) 違うが `userData` の行が自分の印の形なら、その行だけを元の形（記録した末尾の空白の有無）に戻し、他の行は触らず、`Debug.LogWarning` で Scene の path を出す。(3) `userData` の行が自分の印の形でなければ触らず、Warning を出す。(4) file が無ければ Warning を出す。どの場合も、その Scene の処理が終わってから、その Scene の記録を消す（まとめて消さない）。戻した Scene は `AssetDatabase.ImportAsset` で Unity に import させる（0 回にはしない。`Refresh` は使わない）。次の build の始め（検査の後、印の書き込みの前）には、記録が残っている Scene に加えて、記録が無いのに `userData` の行が印の形を持つ root の Scene の `.meta`（`Library` が消された後）にも (2) を当てる。戻しの対象が今回の印の対象でもある Scene では、戻した後のバイトを file に書かず import もせず、それを今回の記録の「元のバイト」にして、file には今回の印の書き込みだけを行う（中間の書き込みと import を挟まない）。前の記録は今回の記録を保存してから消す。
- **片付けの段を互いに止めない:** `finally` の片付け（印の戻し、戻した Scene の import、生成物の `DeleteAsset`）は段ごとに例外を受け止め、最初の例外を保持して残りの段を走らせ、最後に保持した例外を投げ直す。段の順は B が決めてよい。
- **Unity の build の継ぎ目:** adapter は「`BuildContentDirectoryParameters` を受け取り、成否を返す」internal の継ぎ目で `BuildPipeline.BuildContentDirectory` を呼ぶ。`BuildReport` を読む処理は既定の実装の中に置き、test の差し替えは成功・失敗・例外を返せる。
- **開いている Scene（U3、2026-11-15 の owner 決定）:** 未保存の変更を持つ Scene は止める。変更の無い開いている Scene は書いて戻す（AC8）。AC8 が成り立たない時の代替は、印を書く Scene が開いていれば全て止める（AC8'）。印を書かない Scene は開いていても未保存でも止めない（file に触れないので壊さない）。
- **外に保存するハッシュ:** 持たない。build の後に元のバイトへ戻すので、印は毎回の build で書き直し、「変わった Scene だけ書く」目印は要らない。外の保存値を commit する担当も要らない。
- 本スライスは public な型・メソッドを増やさない。issue code も増やさない。

### 2.8 owner の決定と未決事項

- **決定済み（2026-11-14）:** cache を捨てない、検知を重くしない、build の後の source に何も残さない（3 条件）。印の形は M、検知の鍵は `GetAssetDependencyHash`（S2-2b-0 の判定を書き換えずに採った）。主な利用場面は実機へ転送する build をローカルで作ることと CI で、Editor Play は別経路。編集途中の Scene は考えなくてよい（未保存なら止めてよい）。ロック方式の VCS は対象外。
- **U1 目的・利用者の文言の承認:** §1 の 2 欄（利用場面の前提に合わせて書き直した版）を、2026-11-15 に owner が承認した。
- **U2 印の置き場を誰がいつ足すか:** M では Scene に置き場を要らないので、問いが消えた（閉じた）。
- **U3 開いている Scene:** 2026-11-15 に owner が (a) を選んだ。(a) **推奨:** 未保存の変更を持つ Scene だけ止め、変更の無い開いている Scene は書いて戻す。ローカルの build で、開いたままの Scene を閉じる手間を求めない。開いている Scene の `.meta` を書いた時の Editor の振る舞いは未確認で（§5）、不成立なら (b) に切り替える。(b) 印を書く Scene が開いていれば全て止める。経路が単純で確認が要らないが、ローカルの build のたびに対象の Scene を閉じる手間がかかる。
- **U4 PR #180 の未統制の観察を事実の文書へ移すか:** 2026-11-15 に owner が推奨案（本スライスと切り離した小さな文書の変更）を選んだ。本スライスの差分には入れない。観察は、事実の文書の「build の組み合わせで中身が変わる」の「2 種類だけ」（比べた 15 build の範囲）への反例になる（47 build、統制していない集計）。推奨: 本スライスと切り離した小さな文書の変更で、「2 種類だけ」を比べた範囲の文として残し、観察を「統制していない集計」と明記して足す。本スライスの問いは AC6 で実測するので、観察の移し先に依存しない。
- **U5 上乗せを本スライスで測り直すか:** 2026-11-15 に owner が (a) を選んだ。program の gate は、印を snapshot の後・製品の `Refresh` の前に置き、snapshot の閉包をそのまま使えることを確かめる条件を求め、5% の数値の合否は S2-2b-0 で未判定のまま owner が M を採った。(a) **推奨:** 測り直さず、構造の条件（S4・AC9）で gate を満たす。S2-2b-0 で測った置き方と同じで、印のための `Refresh` を製品の `Refresh` にまとめる分は上乗せを減らす向きに働くと見込む（測っていない）。規模の fixture（Scene 300・prefab 3,000、1 回の build 約 16 秒）を製品の経路で作り直す費用と、5% 付近の値を判定 C で読み切れない揺れ（印なしの build が約 15.7〜22.3 秒）を避ける。(b) 規模の fixture を製品の経路で build し、上乗せを高コスト証拠として測る。閾値（5% か別の値か）と回数を owner が決める。

## 3. 責務マップ

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `Scripts/Editor/Build/Content/ScenePrefabStampRules.cs`（新規） | 印の純粋な規則: 印を書く Scene の選択（root の path と、閉包の path ごとの（印の対象か、鍵）から）、印の値の組み立て、`.meta` の text の `userData` の行の検出・値の分類（元から空 / 自分の残した印 / それ以外）・置き換え・印の行の戻し、戻しの判断（元・書いた後・今の sha256 と行の形から） | 印の形・値・対象の規則が変わる時 | 状態なし | BCL だけ | internal | 単体 test（Unity・`AssetDatabase` なし） | build の adapter と同じ assembly の内部規則。Unity I/O と分け、規則を test できるようにする | 0 → 約 150 |
| `Scripts/Editor/Build/Content/ScenePrefabStamp.cs`（新規） | 1 回の build の印の I/O: 検査、記録と元のバイトの保存、書く、戻す、戻した Scene の import、次の build の始めの戻し。path から（印の対象か、鍵）を返す継ぎ目 1 つと、記録の置き場の internal の引数を持つ | 書き方・戻し方・Editor の状態の扱いが変わる時 | 1 回の build（`IDisposable` の scope）。記録は `Library` の下で次の build まで | `UnityEditor`（`AssetDatabase`・`EditorSceneManager`）、`ScenePrefabStampRules`、projection の型 | internal | EditMode test（一時 Scene と一時 prefab。Unity の build なし） | adapter と同じ寿命と依存。adapter に入れると adapter が 2 つの I/O（生成 root と利用者の `.meta`）を持つので分ける | 0 → 約 200 |
| `Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs` | 印の scope を `CleanupOrphans` の後・製品の `Refresh` の前に開き、build の後に閉じる。`CleanupOrphans` の後に、検査・残りの戻し・印の書き込みをこの順に呼ぶ。`finally` の片付けの段を互いに止めない。Unity の build を「parameters を受け取り成否を返す」internal の継ぎ目で呼ぶ | 印の時点の変更 | 既存 | 既存 + `ScenePrefabStamp` | internal（変わらず） | 既存 + AC5・AC7・AC8 の EditMode test | 既存 | 173 → 約 210 |
| `Tests/Editor/Build/ScenePrefabStampRulesTests.cs`（新規） | 純粋な規則の単体 test | — | — | — | — | — | 既存の `Tests/Editor/Build` | 0 → 約 180 |
| `Tests/Editor/Build/ScenePrefabStampEditorTests.cs`（新規） | AC5 (a)〜(g)・AC7 (a)〜(e)・AC8・AC8' の EditMode test | — | test ごとの一時 folder と一時の記録の置き場 | — | — | — | 同上 | 0 → 約 280 |
| `Tests/Editor/Spikes/PrefabStamp/`（新規、Phase D で削除） | AC1〜AC6 の実 build の harness（`[Explicit]`） | — | process ごとの record | — | — | — | 実 build を伴う確認は製品の test に残さない前例（S2-0〜S2-2b-0） | 0 → 約 400 |

- 行数警報: どれも 500 行未満。adapter の増加は約 21% で警報に当たらない。
- 継ぎ目は 3 つに限る: path から（印の対象か、鍵）を返す 1 つ、記録の置き場、Unity の build の呼び出し。閉包はデータ（projection）として渡すので継ぎ目を作らない。開いている Scene の状態は test が実際に Scene を開いて作るので継ぎ目を作らない。
- 依存の向きと asmdef は変えない（`OneStarMaker.Build.Content` の中だけ）。Runtime / Editor の境界も変えない。

## 4. 実装計画

- 順序: (1) `ScenePrefabStampRules` と単体 test。(2) `ScenePrefabStamp` と EditMode test。(3) adapter への接続。(4) harness。
- Phase B から Phase A へ差し戻す条件: §2.7 の外の書き方が要る時（例: Scene 本体を書く、印のための `Refresh` を足す、閉包を作り直す、coordinator や public な型を変える）。AC8 から AC8'（開いている Scene を全て止める）への切り替えは A3 で合意済みとし、差し戻さない。
- 対象外を維持する方法: `Scripts/Runtime/`、coordinator、SampleGame を変えない（AC10）。

## 5. テストとレビュー計画

- 単体テスト: `ScenePrefabStampRulesTests`（印を書く Scene の選択（`.unity` で対象を持つ root だけ）、`userData` の行の検出が 0・1・2 行と複数行の値と空でない値で止まる、置き換えが他の行と改行の形（LF・CRLF）と元の末尾の空白の有無を保ち戻しで往復する、印の文字列が英数字と `-` だけ、印の値が閉包の順に依らず path と鍵の値のどちらの変更でも変わり印の対象外の path で変わらない、空の形（`userData:` の直後が行末か空白 1 つ）だけを受け、空白 2 つ以上・タブを止める、`userData` の値の分類（元から空 / 自分の残した印 / 接頭辞だけ同じで形の違う値を含むそれ以外）、残った印を持つ Scene が今回も対象の時に戻した後のバイトを今回の元のバイトにする規則、戻しの判断 (1)〜(4)）。EditMode test の fixture に CRLF の `.meta` を 1 つ入れる。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Build`（C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）。別々の Unity process で、この順に:
  1. `pwsh tools/run-tests.ps1`（全 EditMode 回帰）。合格は XML の failed 0 と root result `Passed`（`step.json` は VFX の Inconclusive で常に `failed` なので使わない）。他の Unity を止めてから実行する。
  2. harness の第 1 process（fixture を作り、B1〜B6 と B5' を build し、record を書き、fixture を消す）。
  3. harness の第 2 process（record と `Library/BuildHistory` から AC1〜AC6 を計算し直す）。
  - 2 と 3 は XML の total が 1 以上で failed 0、record に終了行があることを harness が最後まで動いた確認とし、判定 C が record を AC ごとに読む。process の自然終了をゲートにしない（既知の終了 stall は `tools/Harness/README.md` の規則）。
- fixture と build の列（Phase B が名前を決めてよい）: 一時 folder に、内側の prefab `Inner`（子の位置 v1 (1,2,3) / v2 (4,5,6)）、`Inner` を nest した `Outer`（自分の子 v1 (7,8,9) / v2 (10,11,12)）、共有の `Shared`（v1 (13,14,15) / v2 (16,17,18)）。Scene は `SOuter`（`Outer` の instance）、`SShared1`・`SShared2`（どちらも `Shared` の instance）、対照の `SPlain`（prefab なし）。B1: 全て v1 で `Full`。B2: `Inner` だけ v2、`Incremental`。B3: 変更なし、`Incremental`。B4: `Inner` を v1 に戻す、`Incremental`。B5: `Inner`・`Shared`・`Outer` を v2、`Full`（目的 build の mode で prefab の編集が入ることを見る）。B6: B5 の後（B5' の前に行う）、`Outer` だけ v1 に戻した source から、`ContentOverlaySelection` で逆引きした root を、別の contentSet の上書きとして `Incremental` で build。B5': B6 で戻した `Outer` を v2 に戻し、`CleanBuildCache()` の後に B5 と同じ source を `Full` で build。期待値は fixture の source の値から導き、実装の出力から作らない。
- record が必ず持つ値: head、Unity の版、各 build の identity・mode・root、build の前と `coordinator.Build` の直後の各 Scene の file と `.meta` の sha256・更新時刻と `userData` の値、`Assets/`（fixture の中の Scene とその `.meta` 以外を含む）と `ProjectSettings/` の全 file の sha256、`coordinator.Build` の直後に生成 folder が残っているか、各出力の file の名前と sha256、各 Scene の `.cf` の float 列の有無、`BuildContentTEP.json` の該当値（Scene の serialized file を書いた event の数と対象）、終了行。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の 2 と 3。
- 発見 C の限定実行: harness が最後まで動く確認として 2 と 3 を各 1 回実行してよい（結果は判定に使わない）。
- 操作・実行時・目視条件の検証経路: 全て上の Unity バッチと EditMode test で閉じる。担当は Phase C の実行担当で、sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1 -Filter <完全名>` を起動する。初期状態は Editor を閉じ、active build target が StandaloneWindows64 の Player、作業ツリーが clean。目視は無い。証拠は record・XML・log を evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路:
  - 印の書き込みを製品の `Refresh` にまとめた時に Unity の Content Directory build が印を読むか（AC1・AC3）: 未確認。初回確認は Phase C の限定実行（Unity の build を伴うため。Phase A・B は build を行わない）。不成立時: Scene ごとに `ImportAsset` で読ませる形に替える。S4 に触れるので A 再開として A3 に戻す（§2.7）。
  - 開いている変更の無い Scene の `.meta` を書いて import させた時に、Editor がその Scene を読み直すか・dialog を出すか・未保存の扱いにするか（AC8）: 未確認。初回確認は Phase B（Editor の操作として、`DisallowAutoRefresh` の区間の中で、一時 Scene を開いた状態で `.meta` の `userData` を書いて import させ、戻して import させ、Scene が開かれたまま・未保存の変更なし・dialog なしかを観測する。Unity の build を伴わない）。不成立時: 印を書く Scene が開いていれば全て止める（U3 (b)、A3 で合意する代替）。
  - 開いている Editor で、`DisallowAutoRefresh` の区間の中で書いた `.meta` が製品の `Refresh` より前に import されないか（I1 (k)）: 未確認。初回確認は Phase B（上の AC8 の観測と同じ Editor の操作で、書いた直後と `Refresh` の後の `AssetImporter.userData` を見る）。不成立時: 区間の中で import される経路を特定してから、新しい事実の分類に従う。
  - 開いている Editor（batch でない）で書いて戻した `.meta` を Unity が保存し直さないか（I1 (d)）: 未確認。上の Phase B の確認と、AC5 の EditMode test（開いている Editor で走る）で観測する。
  - 成立済み: 実 build の harness を EditMode の `[Explicit]` test から別の Unity process で流す経路（S2-0〜S2-2）。`.cf` のバイト走査で float 列を探す観測と `BuildContentTEP.json` の読み取り（S2-2 と S2-2b-0）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（U5 の owner 決定で上乗せを測らない。build 7 回と `CleanBuildCache()` 1 回。`CleanBuildCache()` は実行した PC の `Library` の build cache を全て捨てるので、判定 C の実行担当はその後の build が遅くなることを受け入れる）。
- 判定 C の照合 script の方式: 判定 C の offline 実行（harness の第 2 process）。期待値は fixture の source の値から導く。機械向けの期待値 file は持たない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: 本 session（Anthropic）。A0 は program §8 の S2-2b の行、事実の文書の同じ節と「印の形と検知の鍵」、S2-2 の HANDOFF（commit `52612ede`）、PR #180 の commit `00119ac2` の観察、製品の `Editor/Build/Content` と `Editor/Build/Materialization` の source を入力とした。A1 の書き直し（2026-11-15）は base `980a69ad` で現況を取り直した。
- C' 用に予約した担当・モデル・ベンダー: なし。A2 は Anthropic の系列だけで行ったが、owner が依頼した凍結前の外部レビュー（PR #193、cursor の Grok 系）で Grok 系も Phase A に関与した。C' に使える系列は他に無い（Google 系は使えない）。
- 独立性の強化条件を満たせない場合の理由: Phase A で Anthropic と Grok の両系列が関与したため、C' は Phase B・C に関与していない担当で行い、`独立性制約あり` と記録する（Phase A に未関与の系列は残っていない）。

### 5.1 A2 の独立レビューと A3 の採否

- A2 入力版: commit `46b8ad3b`（HANDOFF だけ）。内容 sha256 は git の blob で固定される。
- 目的レビューの入力: §1 の目的と利用者の欄の原文。sha256 `a12a2095bfebdc4724c831105ef8fe7dc8d331f185f719ed2913e53ce0f71ceb`（目的欄の本文、LF、利用者欄の本文。UTF-8、BOM なし、末尾改行なし、1,120 bytes）。
- A2 独立レビュー（2026-11-15、3 件とも別の新しい subagent で、互いの回答を見せていない。担当は全て Anthropic の系列）:
  - R1 目的レビュー: 入力は上の原文だけ（ツールを使わせず、repository を読ませていない）。問いは定めの 2 つ。
  - R2 アーキテクチャゲート: 入力は A2 入力版の HANDOFF（`46b8ad3b` の写し）、`architecture-gates.md`、`AGENTS.md`、base の source。枠として「§3・§4 の変更は実装済みと仮定し、現コードに無いことを欠陥の根拠にしない」を指示し、不変条件を破る順序と AC の読み違いも求めた。
  - R3 A0 だけからの代替構成: 入力は A0 の抜粋（§1 の目的・利用者・対象外・現況・UC、owner の決定、program §8 の S2-2b の行、事実の文書の該当 2 節。初稿の §2〜§5 を含まない）だけ。R1 とは別の担当で、目的レビューのやり直し用の未関与の担当は残っている。
- A3 統合担当: 本 session（Anthropic、A1 と同じ主担当）。owner は A3 の進行を指示し、凍結は外部レビューの後とした（2026-11-15）。採否:

  | 指摘 | 内容 | 採否 | 理由・反映先 |
  |---|---|---|---|
  | R1 | 保証すべきこと（失敗・中断でも何も残さない、未保存の Scene を保存・破棄・再読込しない、止める時は理由の分かる失敗、呼び手を変えない） | 採用（既存の条件と照合） | I1・I2・AC5・AC7 が持つ。黙って古い出力が出る経路として R2 F10（`Packages/`）を対象外に明記した |
  | R1 | 作らないもの（新しい入力・手作業・cache 全消し・prefab を読む検知・source に残る副産物・別経路・目的 build と上書きで別の仕組み） | 採用（初稿に該当なし） | 記録は `Library` の下で source ではない。model（`ModelImporter`）を印の対象に含めるのは、Scene に置いた model の instance も prefab と同じく焼き込まれるからで、「prefab instance 以外へ広げる汎用機構」に当たらないと判断した |
  | R2 F1 | build の列で `Outer` が v2 にならず、AC2 が空振りしうる | 採用 | B5 で `Outer` も v2 にし、B6 で `Outer` だけ v1 に戻す。AC2 に float 列を具体的に書いた |
  | R2 F2 | 書き込みの途中で失敗した時の AC が無い | 採用 | §2.7 で scope を 1 つ目の書き込みの前に開く。AC5 (e) |
  | R2 F3 | 記録が失われると印が恒久的に残る | 採用 | 空の `userData` だけを対象にし（R3 の案 B）、記録が無くても印の行を元の形に戻す。I1 (h)・AC5 (f) |
  | R2 F4 | build の後に sha が書いた後と違う時の規則が無い | 採用 | 戻しの規則 (1)〜(4) と、Scene ごとに記録を消す順を §2.7 に書いた。I1 の文を「印の行だけを戻し、他の行は触らず知らせる」に絞った |
  | R2 F5 | `finally` の片付けの段が互いを止めうる | 採用 | §2.7「片付けの段を互いに止めない」。I1 (i) |
  | R2 F6 | `AssetPostprocessor` の副作用を AC5 が見ない | 採用 | I1 (g)。AC5 で `Assets/` と `ProjectSettings/` の全 file を照合する |
  | R2 F7 | 継ぎ目の形では AC8 と AC5 (b) を test にできない | 採用 | 継ぎ目を「parameters を受け取り成否を返す」に決めた。AC5 (b) は root の GUID の不一致で起こす |
  | R2 F8 | 閉包の継ぎ目は不要で、AC9 の EditMode 検査は同語反復 | 採用 | 閉包の継ぎ目を外し、継ぎ目を (印の対象か、鍵) の 1 つにまとめた。AC9 は単体 test と Phase C の差分の確認にした |
  | R2 F9 | AC8 の代替の AC が凍結されていない | 採用 | AC8' を足し、§2.5 で AC8 と AC8' のどちらかで足りると書いた |
  | R2 F10 | `Packages/` の prefab は検知されない | 採用 | §1 の対象外。harvest 先の「既知の不成立」に残す |
  | R2 F11 | AC の時点と範囲が曖昧（AC5 の「始め」、AC1 の「各 Scene」、mode、Warning の出口、AC6 の識別力） | 採用 | AC5 は harness が build の前に記録した値と `coordinator.Build` の直後で比べる。AC1 は出力に含まれる Scene。mode は §5 に書き、AC3 は `Incremental` の回だけを数える。Warning は `Debug.LogWarning` と `LogAssert.Expect`。I3・AC6 は Scene の file と印の文字列に絞り、他の file の差は record だけにした（対照の build B5'' を足す案は build を 1 回増やすので採らない） |
  | R2 F12 | 対象の Scene の選択を純粋な規則へ | 採用 | §3 の `ScenePrefabStampRules`（`ScenePrefabStampText` から改名）に移した |
  | R2 F13 | 記録の file 名と test の記録の置き場 | 採用 | file 名は Scene の GUID から作る。置き場を internal の引数で差し替える。file が無い時は Warning |
  | R2 F14 | 印の文字列の形と CRLF の実機確認 | 採用 | 印は英数字と `-` だけ。単体 test と EditMode test の fixture に CRLF の `.meta` を入れる |
  | R3 | 空の `userData` だけを対象にし、印の形を自己記述的にして行だけを戻せるようにする（案 B） | 採用 | F3・F4 の解消に使った。§1 の対象外に `userData` を持つ project を足した |
  | R3 | 書き込みを `Library` の下の一時 file から置き換える | 採用 | §2.7「書き方」 |
  | R3 | 戻した後の import を 0 回にしない | 採用（既存） | §2.7 に明記した |
  | R3 | 未保存の Scene は、印を書かない物も含めて止める | 不採用 | 印を書かない Scene の file には触れないので、その編集を壊す経路が無い。owner の決定は「止めてよい」で、止めることを求めていない。ローカルの build の手間を増やさない |
  | R3 | Editor の起動時（`[InitializeOnLoad]`）にも残りを戻す | 不採用 | 異常終了は稀で、次の build の始めに戻る。記録が失われても印の行を戻せる（F3）。新しい起動時の処理を増やさない。残った印を利用者が commit しうることは残存リスクとして受け入れる |
  | R3 | 契約の検査で `.meta` に印が無いことを grep する | 不採用 | 検査の仕組みを増やす。AC5 と次の build の戻しで足りる |
  | R3 | 入れ子を 2 段（`Inner`⊂`Mid`⊂`Outer`）にし、prefab の root も fixture に入れる | 不採用 | 2 段の入れ子は S2-2b-0 で測り済みで、本スライスの問いは製品の経路への組み込み。1 段で入れ子の経路を通る。prefab の root は S3 の対象外の判定で、規則は単体 test で確かめる |
  | R3 | 鍵の値を Scene の間で使い回す | 保留（B の選択） | §2.7 で B が決めてよいとした（値を変えない） |

- 凍結前の外部レビュー（2026-11-15、owner の依頼。PR #193 の cursor のコメント 2 件、担当は Grok 系の cursor-agent、入力は A3 統合版 `c02bc9d4` の HANDOFF 全文と base の source）と、主担当が Phase C・C' の観点で行った点検（H）の採否:

  | 指摘 | 内容 | 採否 | 理由・反映先 |
  |---|---|---|---|
  | X F1 | 残った印の戻しが未保存の検査より前に `.meta` を書いて import し、未保存の変更を消してから止まりうる（I2） | 採用 | §2.7 の順を「検査 → 戻し → 書き込み」にし、戻しの対象も検査する。load されている全 Scene の未保存を見る。止まる時は戻さない。I2 の経路、AC7 (d) |
  | X F2 | 開いている Editor では、印の書き込みが製品の `Refresh` より前に自動 refresh で import されうる（S4） | 採用 | 検査の後から製品の `Refresh` までを `DisallowAutoRefresh` の対で囲む。戻しの import は `ImportAsset` に固定。I1 (k)、AC9、§5 の未知の操作経路（Phase B で観測） |
  | X F3 | AC5 の照合範囲が F6 の採用より狭い（fixture の prefab と生成 folder を除いていた） | 採用 | AC5 で fixture の Scene 以外も照合し、生成 folder が残れば不合格。record も合わせた |
  | X F4 | AC9 の差分検査が記録の読み戻しと戻しの `Refresh` を不合格にする | 採用 | AC9 を「印の値の計算が asset のバイトを読まない（記録と `.meta` は許す）」「印を読ませる別の `Refresh` が無い（戻しの `ImportAsset` は数えない）」に直した |
  | X F5 | 記録の無い戻しがバイトで一意でない（空白 2 つ・タブ）、Scene の移動後に戻せない | 採用 | 空の形を 2 つに限り、他は止める（AC7 (e)）。AC5 (f) で file 全体の sha256 を見る。記録に GUID を持ち今の path を引く（I1 (l)、AC5 (g)） |
  | X | Grok 系が Phase A に関与し、C' の未関与ではなくなる | 採用（記録） | §5 の C' の予約を外し、C' は `独立性制約あり` で行うと書いた |
  | H1 | AC2 の「B6 の (`Outer` v1, `Inner` v2) はどの回も build していない」は誤りで、B2 と同じ組み合わせ | 採用 | AC2 を直し、B6 の `SOuter` の file が B2 と同じ sha256 であることを足した（正しい実装は B2 を再利用でき、印が効かない実装は B5 の版を出すので、F-1 の経路は捕まる） |
  | H2 | 記録の無い残りの戻しは今回の root の Scene しか見ないので、I1 の「次の build の始めに戻る」が言い過ぎ | 採用 | I1 の文を「記録が失われた印は、その Scene を root に含む次の build で戻る」に絞った。全 Scene の `.meta` を毎回走査する案は、build ごとの読みを増やすので採らない |
  | H3 | 製品の `Refresh` が script の compile と domain reload を起こすと `finally` が走らない | 採用（経路の記録） | I1 (j)。回復は異常終了と同じ次の build の始めの戻しで足り、新しい AC は足さない（AC5 (c) が同じ回復を確かめる） |

- 凍結前の外部レビュー 2 巡目（2026-11-15、PR #193 の owner のコメント。入力は `cc687196` の HANDOFF）と、指摘の周辺の点検（H）の採否:

  | 指摘 | 内容 | 採否 | 理由・反映先 |
  |---|---|---|---|
  | Y F1 | 残った印を持つ Scene が今回も印の対象だと、検査が空でない `userData` として止め、止まる時は戻さないので永久に戻らない（1 巡目 X F1 の修正で生じた順序の矛盾） | 採用 | §2.7 の検査で `userData` を「元から空 / 自分の残した印 / それ以外」に分け、それ以外だけを止める。AC5 (c)(f)(g) を、その Scene を今回も対象に含む build で成功し元に戻ると書き直した（記録あり・なしの両方） |
  | H4 | AC7 (c) の「空でない時に止まる」が自分の残した印も止める文のまま | 採用 | AC7 (c) を「自分の印でない値」に直し、接頭辞だけ同じで形の違う値は止めると書いた |
  | H5 | 戻しの対象が今回の印の対象でもある時、戻して import してから印を書くと、余分な書き込みと import が挟まる | 採用 | 戻した後のバイトを今回の記録の元のバイトにし、file には今回の印だけを書く。AC5 (c) で書き込みが 1 回であることを見る |
  | H6 | 印の形の判定（分類）が純粋な規則の責務と単体 test に無い | 採用 | §3 の `ScenePrefabStampRules` と §5 の単体 test に分類を足した |
  | H7 | §2.7 の「書く担当と時点」が戻しを `CleanupOrphans` と同じ時点に置いたまま（1 巡目の順の変更と食い違う） | 採用 | 「`CleanupOrphans` の後に、検査、残りの戻し、印の書き込み」に揃え、「戻し」の時点も「検査の後、印の書き込みの前」に直した |

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
