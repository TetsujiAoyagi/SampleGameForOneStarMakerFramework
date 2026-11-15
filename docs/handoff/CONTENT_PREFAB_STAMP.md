# Scene に焼き込む prefab の版の印（S2-2b）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2b）
- status: `A`（A1 完了。M と `GetAssetDependencyHash` の前提で書き直し、2026-11-15 に owner が U1 を承認し U3・U4・U5 に答えた。この版が A2 入力版。A2 は未着手）
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
- 引継ぎ票（2026-11-15、A2 へ）:
  - 次の Phase・役割: Phase A の A2（主担当が独立レビューを依頼し、記録する）。この票を含む commit が A2 入力版で、その SHA は起動指示で渡す（§5.1 にも、この commit より後の記録で書く）。レビューは 3 件で、互いの指摘を見せない。(1) 目的レビュー: §1 の目的と利用者の欄の原文だけを渡す（入力の sha256 は §5.1）。問いは定めの 2 つに固定する。(2) アーキテクチャゲート: A2 入力版の HANDOFF 全文。(3) 高リスクの代替構成: 初稿を見せず A0（§1 の現況・対象外・UC、§2.8 の決定済み、program §8 の S2-2b の行、事実の文書「印の形と検知の鍵」）だけから代替構成を出す。(1) の回答を記録した後に同じ担当へ A0 を渡して兼ねてよく、兼ねたら目的レビューのやり直し用に未関与の担当を 1 件残す。担当は Anthropic の系列から選び、Grok 系（cursor-agent）は C' に予約して A2 に使わない。全件の回答を §5.1 に記録したら A3（採否の分類と凍結）へ進む。
  - 読む範囲: Phase A の定めどおり全節。
  - 開始時の照合: 渡された SHA と HEAD が一致し、作業ツリーが dirty でなく、HEAD の HANDOFF にこの票がある。implementation head 欄は `未固定`（Phase A の内部）。ブランチ `claude/content-prefab-stamp`、作業ツリー `D:\repositories\unity\SampleGameForOneStarMakerFramework`。新しい worktree を切らない。
  - 必要な権限: なし（A2 は Unity を起動しない）。外部のレビュー担当を使う場合は、その CLI の起動だけ。
  - 引き渡さないもの: レビュー担当の間で互いの指摘を渡さない。目的レビューには原文以外を渡さない。
  - 停止規則: A2 の記録と A3 の採否は作業ツリーに置き、commit しない（次の Phase A の commit は A3 凍結版）。
  - 未決: なし（U1・U3・U4・U5 は 2026-11-15 に owner が回答。U4 は本スライスの外の文書変更）。

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

- **I1 build の後に、build が書いた物を source に残さない。** build の成否にかかわらず、build が書いた Scene の `.meta` は build の始めのバイトに戻り、Unity の `AssetDatabase` も戻した版を見る。Scene の file 本体は書かない。
  - 主張を崩しうる既存の経路: (a) 書いた後、`try` に入る前の処理（生成 folder の作成、marker の書き込み、製品の `Refresh`）が例外で止まり、戻しの処理に入らない（現在の adapter は `try` を 42 行の `Refresh` の後に始める）。(b) Unity の build 自体の異常終了・Editor の強制終了で `finally` が走らない。(c) 戻した `.meta` を `AssetDatabase` が import し直さず、次の操作（次の build の snapshot・利用者の Inspector）が印の版の `userData` を見る。(d) build の後、戻す前に Unity が asset を保存し直し（`SaveAssets` 等で importer を書き戻し）、元と違う版で `.meta` を上書きする。(e) 次の build の始めの「残りの戻し」が、利用者が後から変えた `.meta` を古いバイトで上書きする。(f) 書いている途中で 1 つの `.meta` の書き込みが失敗し、それまでに書いた分が残る。
  - 探索した境界: adapter と coordinator の build の手順、`PlayerBuildProjectMutation` の前例、S2-2b-0 の batch（戻しと戻し後の `Refresh` で全 file が元のバイト）。未確認: 開いている Editor（batch でない）での `.meta` の書き換えと戻しの観測、Unity の build が importer を保存し直すか。
- **I2 保存していない編集を壊さない。** build が印を書く Scene が Editor で開かれて未保存の変更を持つ時、build はどの file も書く前に止まり、未保存の変更はそのまま残る。印を書く Scene の `.meta` が読み取り専用の時も、どの file も書く前に止まる。
  - 主張を崩しうる既存の経路: 既存の build は、開いている Scene の未保存の変更を無視して保存済みの file を build する（書かないので無害だった）。印を書くと Scene の asset が import し直され、Editor が開いている Scene を disk から読み直すと未保存の変更が消えうる。複数の Scene のうち一部を書いた後に別の Scene で止まると、書いた分が残る（I1 (f)）。
  - 探索した境界: 同上。未確認: 開いている Scene の `.meta` を書き換えて import させた時に、Editor がその Scene を読み直すか・dialog を出すか（§5 の未知の操作経路）。
- **I3 印は Scene の file の再利用の鍵だけを変え、出力の他の file を変えない。** 印を入れた差分 build の出力は、同じ source の cache を捨てた build の出力と、build ごとに作り直す file（root、manifest、`BuildManifestHash.txt`）を除いて、名前と sha256 が全て一致する。出力のどこにも印の文字列が無い。
  - 主張を崩しうる既存の経路: `userData` が Scene の serialized file や他の file の中身に入る。PR #180 の未統制の観察（script をまとめた file・URP `Lit`・`Resources/unity_builtin_extra` が build の組み合わせで別の hash になった。§2.8 U4）が、印と無関係に B5 と B5' の差に出る。
  - 探索した境界: 事実の文書の「build をまたぐ同じ file」、PR #180 の観察、S2-2b-0 の「印を書いた最後の build の出力は、同じ source を印なしで build した出力と Scene の file を含めて一致」。製品の経路（`BuildContentRoot` と root の identity）では未確認。

**成立条件**（起きなければならないこと）

- **S1 焼き込んだ prefab の編集が、同じ `Library` の差分 build の出力に入る。** 入れ子の prefab の内側を編集した時と、複数の Scene が共有する prefab を編集した時を含む。目的 build（`Full`）と上書きの build の両方。
  - 前提: snapshot を作る時点で、手元の prefab が Unity に import 済みであること（製品の snapshot の既存の前提。外で変えて import していない prefab は対象外。§1）。
- **S2 prefab が変わっていない Scene は作り直されない。** prefab を変えずに build し直した時、Scene の serialized file は 1 つも書かれず、prefab を前の版に戻した build は、その版で build した時と同じ Scene の file（同じ sha256）を出す。
- **S3 印を要らない Scene の file と `.meta` は、build の前後で一度も書かれない。** 閉包に印の対象（§2.7）を持たない Scene が該当する。
- **S4 検知は製品の snapshot の閉包をそのまま使う。** 印の計算は projection の root の閉包だけを入力にし、依存閉包を作り直さず（`GetDependencies` を呼ばず）、asset の file のバイトを読まない。印の書き込みは製品の `Refresh`（現在の 42 行）より前に行い、印のための `Refresh` を別に呼ばない（program §8 の S2-2b の gate。上乗せの値はこの置き方の下でだけ成り立つ）。

### 2.2 受け入れ条件

AC の観測は全て §5 の Unity バッチと EditMode test・単体 test で閉じる。「最終の版」は、その build の直前の手元の source の版をいう。

- **AC1（S1）入れ子と共有の prefab の編集が入る。** §5 の fixture で、build の列 B1〜B6 の各回の出力で、各 Scene の `.cf` が、その回の最終の版の子の位置の float 列を全て持ち、前の版の float 列を持たない。合格例: B2（内側の prefab だけ v2）で、外側の prefab の instance を置いた Scene の `.cf` が内側の v2 の列を持つ。不合格例: B2 で、内側の prefab を外側の prefab 経由でだけ焼き込む Scene が v1 の列を持つ。
- **AC2（S1）上書きにも入る。** B6 は、B5 と同じ `Library` で、編集した prefab から `ContentOverlaySelection` が逆引きした root だけを上書きとして build する。上書きの出力の Scene の `.cf` が最終の版の列を持つ（S2-2 の F-1 が、この経路で起きない）。
- **AC3（S2）再利用。** Unity の `Library/BuildHistory/<build>/BuildContentTEP.json` で、prefab を変えていない回（B3）は Scene の serialized file を 1 つも書かない（`BuildPipeline::WriteSerializedFile` の event の数え方は S2-2b-0 と同じ）。B2 で書く Scene の serialized file は `SOuter` の分だけである。prefab を前の版に戻した回（B4）の各 Scene の file の sha256 が、同じ版で build した回（B1）と一致する。
- **AC4（S3）印を要らない Scene は書かれない。** fixture の対照の Scene `SPlain` の file と `.meta` は、全ての build の前後で sha256 と更新時刻が変わらない。
- **AC5（I1）build の後に source が戻る。** 全ての build の後、fixture の各 Scene の `.meta` の sha256 が build の始めと一致し、`AssetImporter.GetAtPath(<scene>).userData` が build の始めの値と一致する。各 Scene の file 本体の sha256 と更新時刻は変わらない。失敗経路（EditMode test。adapter の Unity の build の呼び出しを差し替える継ぎ目で確かめる）: (a) 印を書いた後に build が例外で失敗した時も戻る。(b) 印を書いた後、`try` に入る前の処理が例外で止まった時も戻る。(c) 印を書いたまま戻さずに終わった時（異常終了を模して、書いた後に戻しの処理を呼ばない）、次の build の始めに戻る。(d) (c) の後に利用者がその `.meta` を別の内容に変えていた時は、次の build はその file を上書きせず、Warning で Scene の path を出して続ける。
- **AC6（I3）他の file を変えない。** B5 の出力と、`BuildPipeline.CleanBuildCache()` の後に同じ source で build した B5' の出力が、root・manifest・`BuildManifestHash.txt` を除いて、名前と sha256 で全て一致する。全ての build の出力の file のバイトに、印の文字列の接頭辞が無い。合格例: 印を入れた Scene の file が B5 と B5' で同じ sha256。不合格例: script をまとめた file だけが B5 と B5' で違う（印と無関係の差でも、この AC は不合格として記録し、原因の切り分けは判定 C の範囲で行う）。
- **AC7（I2）未保存の編集と読み取り専用で止まる。** EditMode test で、(a) 印を書く Scene を Editor で開いて未保存の変更を持たせた時、(b) 印を書く Scene の `.meta` を読み取り専用にした時、build は `BuildFailure` の outcome を返し、その message が Scene の path を持ち、build の root のどの Scene の file と `.meta` も sha256 が変わらず、(a) の未保存の変更が Editor に残っている。止まるのは印を書く前で、差し替えた Unity の build の呼び出しは 0 回。印を書く Scene が 2 つあり、2 つ目だけが (a) か (b) の時も、1 つ目の `.meta` は書かれない。
- **AC8（I1・I2）開いている変更の無い Scene。** 印を書く Scene を Editor で開いて変更を持たない時、build は成功し、build の後もその Scene が開かれたまま未保存の変更を持たず、`.meta` は AC5 のとおり戻る。この AC の経路は未確認（§5）。不成立なら §2.7 の代替（印を書く Scene が開いていれば全て止める）に切り替えることを A3 で合意する（§2.8 U3）。
- **AC9（S4）閉包の使い回しと書き込みの時点。** 単体 test で、印の値が、渡した閉包の path の列と、path ごとに返す鍵の値・印の対象かの判定だけから決まり、閉包の並びの順に依らない。印の I/O の class は、閉包の取得・鍵の取得を差し替えられる継ぎ目を持ち、EditMode test で `GetDependencies` 相当の呼び出しが 0 回であることを確かめる。adapter の差分で、印の書き込みが製品の `Refresh` より前にあり、印のための `Refresh` が無いことを Phase C が確かめる。
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

- GO: §2.1 の不変条件と成立条件が、§2.2 の AC で全て成り立つ。
- NO-GO: どれかの AC が成り立たない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。

### 2.6 A3 後の例外承認・決定記録

- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

2026-11-15 の owner の回答（U3・U5）を反映した版。

- **書く担当と時点:** `UnityContentDirectoryAdapter.Build` が、`ValidateTarget` と `CleanupOrphans`（と残りの戻し）の後、生成 folder の作成と製品の `AssetDatabase.Refresh()` より前に印を書き、その `Refresh` で Unity に読ませる。印のための `Refresh` を別に呼ばない。印を書いてから戻すまでを 1 つの `try`／`finally`（または `IDisposable` の scope）で囲み、生成 folder の作成・製品の `Refresh`・root の作成・build のどこで例外が出ても戻す（現在の `try` の開始位置を前へ広げる）。coordinator は変えない。目的 build と上書きの build の区別をしない（どちらも同じ adapter を通る）。
- **印を書く Scene:** projection の root のうち `.unity` の物で、その root の閉包（`ProjectedContent.Closure`）に印の対象を 1 つ以上持つ物。対象を持たない Scene の file と `.meta` には触れない。
- **印の値:** root の閉包のうち、main asset の型が `GameObject` の物と、importer が `ModelImporter` の物を印の対象にし、path の序数順に「path と `AssetDatabase.GetAssetDependencyHash(path)` の値」を連ねた列の sha256 を 16 進で書く。閉包を作り直さず（`GetDependencies` を呼ばず）、asset の file のバイトを読まない。PC ごとに値が違っても、cache が当たらないだけで出力の正しさは変わらない。鍵の結果を Scene の間で使い回すかは B が決めてよい（同じ path の鍵を 1 回の build の中で 1 回だけ取る形は、値を変えない）。
- **全てを検査してから書く:** 印を書く全 Scene について、`.meta` の `userData` の行が 1 行で見つかるか、`.meta` が読み取り専用か、Scene が Editor で開かれて未保存の変更を持つか（AC8 の代替に切り替えた時は「開いているか」）を先に調べ、1 つでも止まる理由があれば、どの file も書かずに例外で止める（coordinator の既存の経路で `BuildFailure` になる。message に Scene の path と理由を書く）。
- **書き方:** `.meta` の text の `userData:` の行の値だけを印の文字列（固定の接頭辞、例 `OSM.PrefabStamp:` + 値）に置き換え、他の行と改行の形は変えない。既存の `userData` に値があっても置き換える（build の後に元のバイトへ戻す）。`userData` が複数行の値を持つなど、1 行で置き換えられない形は止める理由に入れる。Scene を Unity の API で開いて保存しない。製品の `Refresh` で印を Unity が読まない（AC1 不成立）時に、書いた後に Scene ごとに `AssetDatabase.ImportAsset` で読ませる形へ替えるのは S4 に触れるので、B 適応にせず A 再開とする。
- **戻しと残りの戻し:** 書く前に、`.meta` ごとの元のバイトを `Library` の下の本スライス専用の場所に保存し、Scene の path・元の sha256・書いた後の sha256 を記録してから書く。build の後（成否と例外を問わず）、file が書いた後の sha256 のままなら元のバイトに戻し、戻した Scene を Unity に import させ（`ImportAsset` か `Refresh` かは B が選ぶ）、記録を消す。次の build の始め（`CleanupOrphans` と同じ時点）に記録が残っていれば、file が書いた後の sha256 なら戻し、元の sha256 なら記録だけ消し、どちらでもなければ（利用者が変えた）file を触らず Warning を出して記録を消す（残った印は `userData` の違いだけで、次の build が書き直すので出力の正しさに影響しない）。
- **開いている Scene（U3、2026-11-15 の owner 決定）:** 未保存の変更を持つ Scene は止める。変更の無い開いている Scene は書いて戻す（AC8）。AC8 が成り立たない時の代替は、印を書く Scene が開いていれば全て止める。
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
| `Scripts/Editor/Build/Content/ScenePrefabStampText.cs`（新規） | 印の純粋な規則: `.meta` の text の `userData` の行の検出と置き換え、印の値の組み立て（path・鍵の値の列から）、残りの戻しの判断（元・書いた後・今の sha256 から） | 印の形と値の規則が変わる時 | 状態なし | BCL だけ | internal | 単体 test（Unity・`AssetDatabase` なし） | build の adapter と同じ assembly の内部規則。Unity I/O と分け、規則を test できるようにする | 0 → 約 100 |
| `Scripts/Editor/Build/Content/ScenePrefabStamp.cs`（新規） | 1 回の build の印の I/O: 対象の Scene の選択、検査、元のバイトと記録の保存、書く、戻す、残りの戻し。閉包・鍵・印の対象の判定・開いている Scene の状態を取る継ぎ目 | 書き方・戻し方・Editor の状態の扱いが変わる時 | 1 回の build（`IDisposable` の scope）。記録は `Library` の下で次の build まで | `UnityEditor`（`AssetDatabase`・`EditorSceneManager`）、`ScenePrefabStampText`、projection の型 | internal | EditMode test（一時 Scene と一時 prefab。Unity の build なし） | adapter と同じ寿命と依存。adapter に入れると adapter が 2 つの I/O（生成 root と利用者の `.meta`）を持つので分ける | 0 → 約 200 |
| `Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs` | 印の scope を `CleanupOrphans` の後・製品の `Refresh` の前に開き、build の後に閉じる。残りの戻しを `CleanupOrphans` の時点で呼ぶ。test が Unity の build の呼び出しを差し替えられる internal の継ぎ目 | 印の時点の変更 | 既存 | 既存 + `ScenePrefabStamp` | internal（変わらず） | 既存 + AC5・AC7・AC9 の EditMode test | 既存 | 173 → 約 200 |
| `Tests/Editor/Build/ScenePrefabStampTextTests.cs`（新規） | 純粋な規則の単体 test | — | — | — | — | — | 既存の `Tests/Editor/Build` | 0 → 約 150 |
| `Tests/Editor/Build/ScenePrefabStampEditorTests.cs`（新規） | AC5 (a)〜(d)・AC7・AC8・AC9 の EditMode test | — | test ごとの一時 folder | — | — | — | 同上 | 0 → 約 250 |
| `Tests/Editor/Spikes/PrefabStamp/`（新規、Phase D で削除） | AC1〜AC6 の実 build の harness（`[Explicit]`） | — | process ごとの record | — | — | — | 実 build を伴う確認は製品の test に残さない前例（S2-0〜S2-2b-0） | 0 → 約 400 |

- 行数警報: どれも 500 行未満。adapter の増加は約 16% で警報に当たらない。adapter の `try` の開始位置を前へ広げる変更は、既存の生成物の掃除の順を変えない（`finally` の中で印の戻しを先に、生成物の削除を後に行うか、その逆かは B が決めてよい。どちらも I1 を満たす）。
- 依存の向きと asmdef は変えない（`OneStarMaker.Build.Content` の中だけ）。Runtime / Editor の境界も変えない。

## 4. 実装計画

- 順序: (1) `ScenePrefabStampText` と単体 test。(2) `ScenePrefabStamp` と EditMode test。(3) adapter への接続。(4) harness。
- Phase B から Phase A へ差し戻す条件: §2.7 の外の書き方が要る時（例: Scene 本体を書く、印のための `Refresh` を足す、閉包を作り直す、coordinator や public な型を変える）。AC8 の代替（開いている Scene を全て止める）への切り替えは A3 で合意済みとし、差し戻さない。
- 対象外を維持する方法: `Scripts/Runtime/`、coordinator、SampleGame を変えない（AC10）。

## 5. テストとレビュー計画

- 単体テスト: `ScenePrefabStampTextTests`（`userData` の行の検出が 0・1・2 行と複数行の値、置き換えが他の行と改行の形（LF・CRLF）を変えない、既存の値を置き換える、印の値が閉包の順に依らず path と鍵の値のどちらの変更でも変わり印の対象外の path で変わらない、残りの戻しの 3 つの判断）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Build`（C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）。別々の Unity process で、この順に:
  1. `pwsh tools/run-tests.ps1`（全 EditMode 回帰）。合格は XML の failed 0 と root result `Passed`（`step.json` は VFX の Inconclusive で常に `failed` なので使わない）。他の Unity を止めてから実行する。
  2. harness の第 1 process（fixture を作り、B1〜B6 と B5' を build し、record を書き、fixture を消す）。
  3. harness の第 2 process（record と `Library/BuildHistory` から AC1〜AC6 を計算し直す）。
  - 2 と 3 は XML の total が 1 以上で failed 0、record に終了行があることを harness が最後まで動いた確認とし、判定 C が record を AC ごとに読む。process の自然終了をゲートにしない（既知の終了 stall は `tools/Harness/README.md` の規則）。
- fixture と build の列（Phase B が名前を決めてよい）: 一時 folder に、内側の prefab `Inner`（子の位置 v1 (1,2,3) / v2 (4,5,6)）、`Inner` を nest した `Outer`（自分の子 v1 (7,8,9) / v2 (10,11,12)）、共有の `Shared`（v1 (13,14,15) / v2 (16,17,18)）。Scene は `SOuter`（`Outer` の instance）、`SShared1`・`SShared2`（どちらも `Shared` の instance）、対照の `SPlain`（prefab なし）。B1: 全て v1 で `Full`。B2: `Inner` だけ v2。B3: 変更なし。B4: `Inner` を v1 に戻す。B5: `Inner` と `Shared` を v2。B6: B5 の後（B5' の前に行う）、`Outer` だけ v1 に戻した source から、`ContentOverlaySelection` で逆引きした root を上書きとして build。B5': `CleanBuildCache()` の後に B5 と同じ source（B6 で戻した `Outer` を v2 に戻してから）。期待値は fixture の source の値から導き、実装の出力から作らない。
- record が必ず持つ値: head、Unity の版、各 build の identity・mode・root、build の前後の各 Scene の file と `.meta` の sha256・更新時刻と `userData` の値、各出力の file の名前と sha256、各 Scene の `.cf` の float 列の有無、`BuildContentTEP.json` の該当値（Scene の serialized file を書いた event の数と対象）、終了行。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の 2 と 3。
- 発見 C の限定実行: harness が最後まで動く確認として 2 と 3 を各 1 回実行してよい（結果は判定に使わない）。
- 操作・実行時・目視条件の検証経路: 全て上の Unity バッチと EditMode test で閉じる。担当は Phase C の実行担当で、sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1 -Filter <完全名>` を起動する。初期状態は Editor を閉じ、active build target が StandaloneWindows64 の Player、作業ツリーが clean。目視は無い。証拠は record・XML・log を evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路:
  - 印の書き込みを製品の `Refresh` にまとめた時に Unity の Content Directory build が印を読むか（AC1・AC3）: 未確認。初回確認は Phase C の限定実行（Unity の build を伴うため。Phase A・B は build を行わない）。不成立時: Scene ごとに `ImportAsset` で読ませる形に替える。S4 に触れるので A 再開として A3 に戻す（§2.7）。
  - 開いている変更の無い Scene の `.meta` を書いて import させた時に、Editor がその Scene を読み直すか・dialog を出すか・未保存の扱いにするか（AC8）: 未確認。初回確認は Phase B（Editor の操作として、一時 Scene を開いた状態で `.meta` の `userData` を書いて import させ、戻して import させ、Scene が開かれたまま・未保存の変更なし・dialog なしかを観測する。Unity の build を伴わない）。不成立時: 印を書く Scene が開いていれば全て止める（U3 (b)、A3 で合意する代替）。
  - 開いている Editor（batch でない）で書いて戻した `.meta` を Unity が保存し直さないか（I1 (d)）: 未確認。上の Phase B の確認と、AC5 の EditMode test（開いている Editor で走る）で観測する。
  - 成立済み: 実 build の harness を EditMode の `[Explicit]` test から別の Unity process で流す経路（S2-0〜S2-2）。`.cf` のバイト走査で float 列を探す観測と `BuildContentTEP.json` の読み取り（S2-2 と S2-2b-0）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（U5 の owner 決定で上乗せを測らない。build 7 回と `CleanBuildCache()` 1 回。`CleanBuildCache()` は実行した PC の `Library` の build cache を全て捨てるので、判定 C の実行担当はその後の build が遅くなることを受け入れる）。
- 判定 C の照合 script の方式: 判定 C の offline 実行（harness の第 2 process）。期待値は fixture の source の値から導く。機械向けの期待値 file は持たない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: 本 session（Anthropic）。A0 は program §8 の S2-2b の行、事実の文書の同じ節と「印の形と検知の鍵」、S2-2 の HANDOFF（commit `52612ede`）、PR #180 の commit `00119ac2` の観察、製品の `Editor/Build/Content` と `Editor/Build/Materialization` の source を入力とした。A1 の書き直し（2026-11-15）は base `980a69ad` で現況を取り直した。
- C' 用に予約した担当・モデル・ベンダー: A2 を Anthropic の系列だけで行い、Grok 系（cursor-agent）を C' に予約する案（A2 を始める時に確定する）。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 の担当を決めた時に書く）。

### 5.1 A2 の独立レビューと A3 の採否

- A2 入力版: この節を含む commit（SHA は起動指示で渡し、この commit より後の記録で書く）。
- 目的レビューの入力: §1 の目的と利用者の欄の原文。sha256 `a12a2095bfebdc4724c831105ef8fe7dc8d331f185f719ed2913e53ce0f71ceb`（目的欄の本文、LF、利用者欄の本文。UTF-8、BOM なし、末尾改行なし、1,120 bytes）。
- A2 独立レビュー: 未着手。
- A3 統合担当・採否: 未着手。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
