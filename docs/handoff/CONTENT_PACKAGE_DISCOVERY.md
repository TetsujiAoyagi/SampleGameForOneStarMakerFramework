# 複数 package の発見と合成の実証（S2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-a。分割は §2.4）
- status: `A`（**revision 1 の A3 の統合版、未凍結**。2026-11-12 に A2 の 3 件を反映した。owner の指示により凍結せず、外部レビューのためにこの版を commit して draft PR に出す（A3 凍結版ではない）。A2 入力版は commit `621a4252`。revision 0 は A3 の統合版で凍結せずに保留した（2026-11-11、commit `c7201de0`）。保留の理由だった Content Directory の前提を S2-0（PR #177）と S2-0b（PR #179）が確かめたので、その結論で仮説・共有の形・AC を改め、revision 1 として A2 からやり直す。改めた点は §1「revision 1 で改めた点」。外部レビュー用に commit した版は `2badbc71`。同日、owner 経由の相談への回答 E-01〜E-07 と、その追記への反証レビュー R-01〜R-10（§5.1）を作業ツリーに反映した。この追記は未 commit で、凍結の時に A3 凍結版として commit する）
- branch: `claude/content-package-discovery`（PR の base は `develop`）
- implementation base commit: `0b3e1cea`（`develop` の head、2026-11-12。§1 の現況はこの base。製品 code は revision 0 の base `f8ffd023` から変わっていない）
- implementation head commit: `未固定`
- risk: `high`（program §8。複数 directory の native 合成の初回実機）
- owner: root（依頼者）
- created: 2026-11-11（revision 1: 2026-11-12）
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（同じ id で中身の違う file の登録、登録の寿命の結合、logical key の共有の実測の事実と測り直し）、program §5.3（配布粒度と結論、共有の形の判定）、§8（S2-a の行と後続への移送）、§10 U6。実験 code は製品に残さず git 履歴に置く（S0-b・S2-0b と同じ）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§2.4・§3.1・§4.1・§5.3・§9 G10 / G11 / G24・§10 U6）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）。本スライスは製品 code を変えないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B 同時実行制約の対象外
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（凍結前の外部レビューと owner の裁定）。
  - 次の作業: 外部レビューの指摘を §5.1 の revision 1 の採否に同じ形で足し、§2.5 の U-S2-1〜U-S2-5 の owner の裁定を反映して、本書と program を A3 凍結版として commit する。外す物の一覧はここを正とする。U-S2-4 が「測らない」なら AC14、判定定義の CONDITIONAL ACCEPT (iii)、AC15 (2)、U-S2-2 (b) の AC14 (2) への言及、§3 の錨と AC14 の組の記述（表・fixture・行数）、§4 の B 適応の錨、§5 のコマンド 4 の AC14、I-P5 と AC11 の AC14 を外す。U-S2-4 の (1) だけ採らないなら (iii) を、(2) だけ採らないなら AC14 の stripping の組を、(3) だけ採らないなら AC14 (2) と U-S2-2 (b) の言及を外す。U-S2-5 が「測らない」なら AC15 と、§1 対象外・§2.3・I-P1・I-P5・AC11・§3・§5 の AC15 への言及を外す。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結前に Phase B を始めない。目的・利用者の 2 欄を変えたら目的レビューを 1 回やり直す（revision 0 で残した未関与の担当を使う）。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 1 回の build 計画から作った複数の正規 Content Directory（package）について、要求された Scene / Object がどの package に入っているかを、Unity に何も登録しないうちに build の出した索引だけで特定し、要求に必要な package だけを登録して読めるかを Unity 実機で確かめる。その時、ある package の unload・登録解除・失敗が別の package の Scene / Object を壊さず、別の package の複製や別の build の物が要求の成功として返らないことを確かめる。共有する asset がどの形なら合成できるかを実測で分け、成立しなければ配布粒度の縮退を人間が判断できる材料を残す。
- 利用者: OSMFramework の開発者（本スライスの結論から製品の索引、供給元の adapter、Runtime の接続を後続で書く人）と、OSM を使う host application の開発者（どの root をどの package に入れるかを宣言する人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続）:
  - 製品の索引形式、BuildSystem の合成 build の入口、publisher と manifest の変更、file の id を build の何から取るか（`ContentLayout.json` は Unity の内部形式）: 後続 S2-b（本スライスの結論の後で A1。program §8）。
  - `AssetManagement.InstallContentDirectory` の複数化、`ContentRevisionGate` の複数登録、`ContentDirectorySession` の複数化、`SceneDirector` と owner 台帳との接続: S2-b / S3。
  - 実在の manifest から候補を作る adapter、install、LAN、要求をまたぐ pin、片方の directory の file が disk から消えた時にどちらの file を読むか（AC15 が観測する 2 列を除く）: S3 / S4。
  - Player での成立、shader の variant が描画で正しいか（画素の確認）: S2-b / S3（§2「成果の範囲」）。
  - 取消（G15。S0-b が 1 directory で確かめた）、Enter Play Mode の 4 設定の行列（G26。S0-b が確かめた）: 再実証しない。
  - 中身が同じ file を複製した 2 directory の登録の挙動（実体の数、後から登録した側の解除が先に登録した側の `Loadable` の物を壊すこと）: S2-0b が確かめた。再実証しない。
  - 異なる build 計画同士の合成（program §8 の「後続の別課題」）: 索引の拒否（AC3）だけを確かめる。
- 現況（base `0b3e1cea`。path は `unity/Assets/OneStarMaker/Scripts/` から）:
  - **build:** `Editor/Build/Content/UnityContentDirectoryAdapter.cs` は 1 回の呼び出しで 1 directory を `BuildPipeline.BuildContentDirectory` で作る。root は生成した `BuildContentRoot` 1 件で、選択 root を `BuildContentEntry` で持つ（Scene は `LoadableSceneId`、Object は `Loadable<Object>`）。`BuildContentCoordinator` は build ごとに新しい identity（時刻 + GUID）を発番し、`ContentTransportPublisher` は `revision = identity` の `transport.json` を 1 directory に 1 つ書き、その `files` に各 file の `sha256` を持つ。**複数 directory を束ねる単位は無い。** `BuildContentProjection` は root ごとの依存閉包（`BuildDependencyEntry`）を持ち、build 前に root と閉包の関係を調べられる。
  - **Unity の仕様（6000.6 の公開文書、2026-11-11 に確認）:** `BuildContentDirectoryParameters` は `compression` / `extraScriptingDefines` / `name` / `options` / `outputPath` / `rootAssetPaths` だけで、別の directory への依存を宣言する欄は無い。1 回の build の中だけ重複を除き、build は自己完結する（同じ asset を参照する 2 build はそれぞれその asset の file を持つ）。文書が示す build をまたぐ参照は実行時の untracked reference（登録後の `GetRootAssets` 等）だけで、「asset を 1 つの build からだけ参照し、他からは untracked で辿る」のが重複を避ける案内である。`Loadable` / `LoadableSceneId` の参照は build に閉包として含まれる。`LoadableObjectId` は登録中の directory 全体から解決され、検索は登録の逆順である。`Loadable.LoadAsync` に directory handle の指定は無い（handle を取るのは `GetRootAssets` だけ）。登録解除の前に、その directory から読んだ物の解放が要る。Editor の登録は Play 中だけで、type tree が要る。文書は「各 build が file を持つ」とは言うが、「build をまたいで共有できない」とは言っていない。
  - **S2-0 の結論（PR #177、program §5.3、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)）:** この PC・Unity 6000.6.0f1・StandaloneWindows64・loose file・fixture の範囲で、別々に build した directory の間で共通の texture・material・prefab・variant の無い自作 shader の content file は **同じ id（`.cfid`）・同じ bytes** になり、sha256 でまとめられる。**build の組み合わせで中身が変わるのは URP の `Lit` の shader と、build 内の全 MonoScript をまとめた file の 2 種類だけ**で、これは **同じ id のまま bytes が変わる**（以下「N file」）。名前（Unity の内容 hash）ではまとめられず、sha256 でまとめる。file の id と依存は `Library/BuildHistory/<build>/ContentLayout.json`（Version 2、Unity の内部形式）にある。
  - **S2-0b の結論（PR #179、program §2.4、同じ事実の文書）:** 中身が同じ file を持つ 2 directory を同じ Play で両方登録できる（重複の警告は出ない）。`Loadable` からの load は実体 1、Scene の直接参照からの load は実体 2（directory ごと）。**先に登録した側から `Loadable` で載せた物を保持したまま後から登録した側を登録解除すると、保持している物が Error の log なしに同じ frame で `== null` になる。** 後から登録した側の物と Scene の直接参照の物は、もう片方の解除で壊れない。推測（実測ではない）: `Loadable` は後から登録した directory の file に解決され、Scene の直接参照は Scene のある directory の中で解決される。S2-0b の fixture は URP の `Lit` と MonoBehaviour を意図的に使っていない（N file を持たない）。**中身の違う同じ id の file を両方登録した時は測っていない。**
  - **既存の build 履歴の読み取り（2026-11-12。Unity を起動せず、この PC の `Library/BuildHistory` に残る S2-0・S2-0b と統合 test の 47 build の `ContentLayout.json` を集計した。統制した比較ではない）:** script の束の file（id は全 build で同じ）は、build に入る script の集合 4 通りに hash 4 通りが 1 対 1 で、同じ集合の 33 build は同じ hash だった（集合が同じなら bytes が同じ、という読みと合う）。`Lit` は 3 通り（171,796 / 192,468 / 371,644 bytes）で、S2-0 の対ごとの差（`S`≠`A`≠`B`、`B`=`AB`）は「build 内の使用の和で variant の集合が決まる」と読める（未確認）。**N file は 2 種類に閉じていない:** `Resources/unity_builtin_extra` の file（id `0000000000000000f000000000000000.cfid`）が、UI の Scene（`UIScene`、EventSystem）を含む統合 test の 1 build だけ別の hash（575,932 bytes。他の 46 build は 99,452 bytes）だった。`ProjectSettings/GraphicsSettings.asset` の Lightmap / Fog / Instancing の stripping は 3 つとも値 0（Lightmap と Fog は Automatic）で、build に入る Scene によって `Lit` の variant が変わりうる（未確認）。本スライスは N file を種類の一覧ではなく「同じ id で sha256 が違う file」として観測する（AC1）。
  - **S0-b の結論（program §5.2）:** native 層で 2 directory を直接登録し、同じ identity は片方を優先、同時 load、unload と再 load が成立した。実験 code は commit `94b0c4a6`。
  - **実験 code の前例:** S2-0b の harness（commit `444651ed` の `Tests/Editor/Spikes/SharedFileRegistration/`。`RegistrationFixture` 256 行、`RegistrationBuilds` 444 行、`RegistrationRounds` 669 行、`RegistrationVerdict` 380 行、test 509 行）は、fixture の fileID と GUID を固定し、build（第 1 process）と登録の round（第 2 process）を別の Unity process に分け、record の観測の行から判定を計算し直す。build を伴う batch test は XML の完成後に Unity が終わらないことがある（program §8 の S2-0 の移送、`tools/Harness/README.md` の既知の native 終了 stall）。
  - **登録の製品側:** `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` は 2 件目の install を拒み、`Runtime/BuildContent/ContentRevisionGate.cs` は process に 1 登録だけを許す。native の port `IContentNativeDirectory`（`UnityContentDirectoryBackend.cs`）は 1 directory 単位で、`OneStarMaker.Tests.Editor` から internal で使える。`ContentDirectoryIndex`（130 行）は 1 root の中の `(logical key, representation, kind)` の索引と `Full` への fallback を持つ。閉包の実体は `GetDependencies` の結果で、`.meta`、`.cs`、`Assets/` 外を除く。
  - **S1 が S2 に残した入力:** 互換の基準の 1 候補分は S1 が定めた（`ContentCompatibilityRule`）。合成の欄（共同の build plan の出所、shared asset の所有と重複、同一 logical identity の衝突、索引の hash 関係）は S2。
- revision 1 で改めた点（revision 0 から。目的・利用者の 2 欄は変えていない）:
  - 仮説の「serialized の直接参照は package ごとに複製され、合成の側で除去しない」（F2）は、S2-0b により **そのままでは候補にならない**と分かった（解除の順序で黙って壊れる）。実体の数と解除の破れは S2-0b の事実を引用し、測り直さない（下の owner の想定により F2 は主の形から外れた）。
  - **どの共有の形でも避けられない N file**（同じ id で build ごとに中身が変わる `Lit` と script の束）を合成の前提条件として新たに測る（AC8 を改めた）。`Lit` の material か MonoBehaviour を持つ package を 2 つ合成すれば必ず起き、F1・F3 でも起きる。
  - revision 0 の AC8 の「root と別 package の閉包の重なり」は、同じ GUID の asset を build ごとに書き換える合成の列（同じ id で中身の違う file の直接の観測）として AC8 に残した。revision 0 の AC9（同じ GUID の Scene の別 build）は、異なる build 計画の合成で本スライスの問いの外なので、AC3 の拒否だけにして実機の列を外した。
  - 索引に package ごとの file の id と sha256 を持たせ、package 間の重なり（同じ id。bytes が同じか違うか）を build の時に計算する（AC2・AC3）。登録の寿命の結合はこの重なりだけで決める（U6: Runtime で走査しない）。
  - **owner の想定する構成（2026-11-12、owner の発言）に合わせて主の形を F3 にした。** 想定は「Scene で使う resource はその Scene の folder の下に、共通で使う物は共通の folder にまとめる。texture だけをまとめる等の型ごとの束ね方はしない」「Scene からの直接参照より、resource を独立して build して参照する形を主とする」。これにより package は Scene の folder 単位と共通の folder 単位になり、Scene は共通の物を key で要求する（F3）。共通の物を Scene が直接参照すると Scene の package の閉包に複製が入るので、既定の planner の「root が別 package の閉包に居る」の拒否がこの構成を守る。F2 / F2'（Scene から共通の物への直接参照の複製）は主の形でないので測らず、revision 0 の AC6（複製と実体の数、結合の対照）を外した。F3 で避けられない危険は、共通の package と Scene の package が N file で重なり、key で読んだ共通の物の script・shader が別の package の file に解決されることで、AC7・AC8 がこれを測る。
- UC ごとの利用者の入力（実行時の操作は無い。開発時の宣言を利用者の作業として数える）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| host が package を分ける | package 名と、それぞれに入れる root の一覧（owner の想定では Scene の folder と共通の folder が単位。folder から宣言を導くかは S2-b） | 依存閉包、閉包の重複の一覧、root の重複と閉包への混入の拒否、package ごとの identity、build 計画の実行 id、合成の revision、package ごとの file の id と sha256、package 間の重なり、登録の寿命の結合 | 分割は host の開発者が開発時に宣言。残りは build の時に自動導出 | program §5.3（単位を OSM が固定しない）。分割を機械が推測すると host の Region / Cell の知識が core に要る。結合は build の出力から決まるので宣言を増やさない |
| 要求する | logical key、kind、表現（既存の `ContentRequest` の欄） | どの package か、登録と解除の時点 | 呼び出し側が要求時に / 索引から自動導出 | 目的の「要求された物がどの package か」。新しい入力は無い |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 1 つの build 計画から作った複数の package（Scene の folder の package と共通の folder の package）は、build の出す索引だけで要求の行き先を決め、必要な物だけを登録し、共通の物を key で要求する形（F3）で、N file・片方の解放・失敗に壊されずに合成できるか。できないなら、どの経路（Scene の直接参照 / `Loadable` の load / 解除）が原因で、登録の寿命の結合で防げるか。
- 仮説（Phase C で検証する）: Scene の直接参照は Scene のある package の中で解決されるので、N file があっても Scene は自分の package の script と shader で読める。`Loadable` は後から登録した package の file に解決されるので、N file の違う package を後から登録すると、先の package の Object（key で読んだ共通の物を含む）が別の package の script の束と shader で読まれうる（script の欠落、別の variant 集合）。読めても、後から登録した package の解除で壊れうる（S2-0b の中身の同じ file と同じ形）。後者は、索引の重なりで解除を延ばす結合（後から登録した package の解除を、先に登録した重なる package が全て解除されるまで延ばす）で防げる。
- 共有の形と前提（実測の前に固定する。判定 C が各行に `成立` / `不成立` / `未確定` と条件・根拠の record を付ける。未確定を不成立に数えない）:

| 形 | 内容 | 見る AC | 成立した時の代償 |
|---|---|---|---|
| N（前提） | `Lit` の material と package 固有の MonoBehaviour を持つ package を両方登録し、各 package の物が自分の script と shader で読め、片方の解除で他方が壊れない。Scene の直接参照の経路と `Loadable` の経路を分けて判定する | AC4（Scene）、AC8（両方） | なし |
| F1 Scene の folder の中の共有 | 同じ Scene の folder の root 同士の共有を同じ package に入れ、1 build の重複除去に任せる | AC5 | package が Scene の folder の大きさになる |
| **F3 共通の package を key で要求（主の形）** | 共通の物を共通の package の root に置き、Scene の package は直接参照せず（planner が閉包への混入を拒否）、実行時に key で要求する | AC7、AC8 | 内容の作り方を key の要求へ寄せる。N file の重なりで結合が要れば、解除が遅れる |
| F2 Scene から共通の物を直接参照し複製 | 別 package の root が同じ asset を直接参照し、両方を登録する | なし | `不成立`（S2-0b の引用。測り直さない）。複製に登録の寿命の結合を足す形（F2'）は、主の形でないので測らない（§2.3） |

- 成果の範囲: GO は「Editor Play の native 層で、fixture の合成が成立する」ことだけを意味する。`Loadable` field と Scene の id の package 間の重なりは本スライスの結論によらず既定で拒否する（§2.3）。製品の 2 件 install、`SceneDirector` や owner 台帳との接続、Player、shader の variant が描画で正しいこと（本スライスは script の有無、shader の実体と `isSupported` までを見る）、実在の publish 経路（transport.json は harness が製品の型で書く）、大きな実データでの性能、fixture の 3 package（`a`・`b`・`common`）を超える数の package は主張しない。record の先頭に Unity の版、platform、Editor Play であることを書き、未検証の組み合わせへ結論を広げない。
- どうすればこの問いに誤って YES を出せるか（C と C' はこれを探す）:
  - (a) 全 package を先に登録して読み、登録の順の偶然で正しい Scene が出る（AC5）。
  - (b) marker を見ず Scene が load できただけを成功とする（AC4、AC8）。
  - (c) fixture の 2 package で N file が実際には違わず、N の列が何も試していない（AC1 の校正）。
  - (d) 複製の区別がつかず、別の複製を掴んでも一致して見える。書き換えが build に反映されていない（校正 round。AC1、AC8）。
  - (e) 結合の効果を、結合しない対照で壊れることを示さずに主張する（AC8）。
  - (f) 登録解除が成功した事実だけを見て、残った側の Scene / Object が壊れていないかを見ない（AC7、AC8、AC11）。
  - (g) 壊れた package の失敗を `try` の中だけで確認し、先に読んだ package の生存と登録の残りを見ない（AC10）。
  - (h) 索引や重なりを Unity の登録後に root や disk から作る（U6 に反する）（AC2）。
  - (i) 別 build の成果物を同じ名前の revision で束ねて合成と呼ぶ（AC3）。
  - (j) AC4 の合格を「`Loadable` の経路でも N file が安全」と読む。AC4 が見るのは Scene の直接参照の経路で、`Loadable` の経路は AC8 にある。
  - (k) script の欠落を、component の参照が null でないこと（Unity の偽 null）で見落とす（AC4、AC8。`== null` で判定する）。
- 観測の語（AC4・AC7・AC8・AC10 の各列に当てる。X-01・X-03）:
  - **load の破れ:** load した Scene / Object の固有の MonoBehaviour が具象型の `GetComponent` で `== null`、`GetComponents` に null の項目（missing script）がある、marker が別の package の物、material の shader が `== null`。
  - **解決の共有:** N file の違う 2 package の物が同じ shader の実体（`EntityId`）を持つ、または AC8 (2) で handle の `GetRootAssets` / `LoadableObjectId` が別の package の marker を返す。どちらかの package の物が別の package の file に解決された印で、それだけでは壊れていない。
  - **解除の破れ:** 解除の後の観測（同じ frame、次の frame、`Resources.UnloadUnusedAssets` と新規の load の後）で、残る側の物・Renderer・material・shader・固有の MonoBehaviour のどれかが `== null` になる、または観測の中で `MissingReferenceException` 等の例外が出る。観測の中の例外は harness の不備ではなく破れとして record する（観測の各段を `== null` で区切り、例外は段ごとに捕まえて記録する）。
  - 同じ step で 2 回再現する Editor の crash は、未判定ではなく、その列の破れの観測とする。
- 判定定義（列挙に当たらない結果は NO-GO。環境の不調と harness の不備は判定せず未判定として B へ戻し、record の欠落で AC の観測が無い時も未判定とする）:
  - **GO:** §2.1 と §2.2 を全て満たし、AC4・AC7・AC8 の全ての列で、load の破れが無く、結合なしの列でも解除の破れが無く、解決の共有も無い。
  - **CONDITIONAL ACCEPT（スパイクの定義済みの成功）:** §2.1 と §2.2 を全て満たし、AC4・AC7・AC8 の全ての列で load の破れが無く、AC7・AC8 の結合ありの列で解除の破れが無いが、(i) 結合なしの列に解除の破れがある、または (ii) 解決の共有がある（壊れていなくても、一方の package の物が他方の `Lit` の variant 集合と script の束で動いているので、fixture の偶然に依存しうる）。意味は「F3 は、索引の重なりで登録の解除の順を結びつける条件で成立する。解決の共有があれば、variant 集合の正しさは未確認のまま」。record に (i)・(ii) のどちらかを書く。受け入れるか、§2.5 U-S2-4 の N file を揃える方式へ進むかは owner が S2-b の前に決める（U-S2-2）。
  - **CONDITIONAL ACCEPT (iii)（U-S2-4 (1) が「採る」の時だけ）:** 揃えない組（`a`・`b`・`common`）の `Loadable` の経路の列（AC7 と、AC8 (1) の prefab の列）に load の破れか結合ありの解除の破れがあり、それに伴う I-P1・I-P3 の未達を除いて §2.1 と §2.2 を全て満たす。かつ、AC14 で比較の対象の file が全ての対で同じ sha256 に揃い、揃えた組の AC14 (1)(2) の全ての列で load の破れが無く、結合ありの列で解除の破れが無い（揃えた組が I-P1・I-P3 を満たす）。Scene の直接参照の経路（AC4 と AC8 (1) の Scene の列）に load の破れがある時は (iii) にしない。意味は、揃えた組の結合なしの列の結果で 2 つに分けて record に書く: 結合なしでも解除の破れが無ければ「F3 は、全 package の N file を揃え、bytes の違う重なりを build の時に拒否する条件で成立する」、結合なしに解除の破れがあれば「揃え、かつ登録の解除の順を結びつける条件で成立する」。record に、揃った組の名前と stripping の設定（Project の設定のままか、Custom の組か）を必ず書く。
  - **NO-GO:** 上記以外。例: (iii) に当たらない load の破れ（script の欠落、shader の欠落）、結合ありの列でも解除の破れ、AC2・AC3・AC5・AC10・AC11・AC13 の未達。program §5.3 により縮退を owner に返す。縮退の判断材料として、Scene の直接参照の経路（AC4 と AC8 (1) の Scene の列）の成否と、AC14 を実施していればその結果を record から報告する。
- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO / CONDITIONAL ACCEPT で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-P1 要求した物と別の複製・別の package の N file・別の build を成功にしない（program I1、G10）:** 索引が package P を指す要求の結果は、P の build marker を持ち、P に入っている script の component を持つ Scene / Object である。登録の順と load の順に依存しない。AC15 (2) は別の directory の同じ id の file での成立を見る観測で、本条の判定に使わない。
  - 崩しうる経路: (a) `SceneManager.LoadSceneAsync(LoadableSceneId)` と `Loadable.LoadAsync` は directory を指定できず、登録の逆順で解決される。(b) `ContentDirectoryIndex` の `Full` への fallback が、別 package の `Full` を拾う（索引の表現の規則）。(c) 同じ triple が 2 package に入る（planner が拒否）。(d) N file（同じ id で中身の違う `Lit` と script の束）が後から登録した package の物に解決される（S2-0b の推測を中身の違う file に当てた場合）。(e) 同じ GUID の asset が 2 package で別の bytes になる（build の間の書き換え。既定の planner が source hash の差で拒否）。
  - 探索した境界: `UnityContentDirectoryBackend`、`ContentDirectoryIndex`、Unity の公開文書、S2-0・S2-0b の事実。未確認: (d) の実挙動（AC4、AC8 で測る）、shader の variant の描画上の正しさ（§2.3）。
- **I-P2 合成の単位の外の物を混ぜない（program §4.1、G24）:** 索引に載っていない package、manifest の hash が索引と違う package、同じ source の hash が package 間で違う合成、target / engine / schema の違う package、別の実行の build を束ねた物は合成として使えない。
  - 崩しうる経路: (a) `ContentDirectoryIndex.TryCreate` は自分の identity だけを見て、他の package との関係を見ない。(b) `ContentTransportManifest` に合成の欄が無い。(c) 2 回の build の間に source が変わる（hash は package の build ごとに取り、全 build の後・索引の生成前に再照合する。比較するのは閉包の `Assets/` 下の file とその `.meta`）。(e) N file の源（`Packages/packages-lock.json`、`Library/ScriptAssemblies` のコンパイル済み assembly、`ProjectSettings/GraphicsSettings.asset` と使用中の URP の asset）が build の間に変わる（実行の fingerprint としてこれらの sha256 を最初の build の前に取り、全 build の後と第 2・第 3 process の開始時に再照合する）。(d) 同じ source の別の実行の成果物を 1 つの索引に束ねる（identity に build 計画の実行 id を含め、索引に記録して照合する）。
  - 探索した境界: build の port、manifest の型。未確認: なし。
- **I-P3 片方の解放が他方を壊さない（program I4、G11）:** ある package の Scene の unload や登録解除の後も、他の package の Scene / Object は読めて描画に必要な参照（script、material、shader、texture）を保ち、続けて新規の load ができる。登録解除は、その package から読んだ物を全て返した後だけ行う。
  - 崩しうる経路: (a) 解放の前の `UnregisterContentDirectory`（Unity は error を出す）。(b) **後から登録した package の解除が、先に登録した package から `Loadable` で載せた物を黙って壊す**（S2-0b が中身の同じ file で実測。索引の重なりによる結合で塞ぐ）。(c) N file が後から登録した package の物に解決された後、その package の解除で script・shader が消える。(d) `CompleteContentDirectoryPlayStop` は drain を待たない（S0-b の移送。harness が自分で drain する）。
  - 探索した境界: S0-b の `RoutingBackend` の順序、S2-0b の round、Unity の登録解除の文書。未確認: (c) の実挙動（AC7、AC8）。
- **I-P4 失敗が成功に化けず、他を巻き込まない（program §4.1 fail-closed）:** 欠損・破損した package の要求は失敗として返り、既に読んだ他の package は生きたままである。その要求で新たに登録した package は貸し出しが 0 の時に解除され（結合で解除が延びる時は延びた先で解除され）、既に貸し出しのある package の別の root の失敗では解除しない。復元後の再試行が成功する。
  - 崩しうる経路: (a) 登録の途中失敗の後始末（`ContentDirectorySession.RegisterCore` の rollback は 1 session の物）。(b) 登録は通り load で壊れる content file。(c) 索引が指す package が tier に無い時の「他の tier へ進む」と整合性の拒否の混同（program §3.1 の 10）。(d) 失敗した package の登録が、他の package と N file で重なり、結合の規則で解除が延びたまま残る。
  - 探索した境界: `RegisterCore`、`ContentAvailabilityStatus` の 4 値。未確認: 壊れた content file の検出時点（AC10 で測る）。
- **I-P5 要求していない package を登録しない（program I6、G02）:** 全ての `RegisterContentDirectory` の呼び出しが要求元（要求の id か結合の理由）を持ち、要求元の無い呼び出しは破れとする。AC5・AC7・AC10 の各 step の完了時と登録の直前に、登録中の package の path の集合（harness の台帳と `GetContentDirectories` の両方）が「その時点までに要求され、まだ返していない物の package」と、結合で解除を延ばしている package の和と一致する。延ばしている package は record に理由（どの package との重なりか）を持つ。AC8・AC14 (1)・AC15 は登録の状態を先に作る専用の round で、本条の対象外とする（AC14 (2) は AC7 と同じく本条の対象）。
  - 崩しうる経路: (a) harness が開始時に全 package を登録する。(b) 依存の package を暗黙に登録する。(c) 結合の規則が、重なりの無い package の解除まで延ばす。
- **I-P6 製品を変えない（S0-b・S2-0b と同じ）:** §3 の新規 file と本書・program 以外の変更が無く、実験は assets・設定・登録の残りを残さない。
  - 崩しうる経路: (a) 既存 `.meta` の再 import の差分。(b) adapter の build option を変える。(c) `Assets/OneStarMakerGenerated` や Build Settings の残り。(d) Enter Play Mode の設定を戻さない。(e) fixture に使う既存の MonoBehaviour の型や URP の設定を変える。

**成立条件（起きなければならないこと）**

- **E1 合成の build:** 1 つの build 計画から、製品の adapter（無改変）で 2 件以上の正規 directory と 1 つの索引ができ、package 間の N file が実際に違う（同じ id、違う sha256）。
- **E2 索引だけの発見:** 登録を 1 件もしていない状態で、索引の bytes から `(logical key, kind, representation)` の行き先の package と、package 間の重なり（同じ id の file、bytes が同じか違うか）が決まる。package が 1 つの合成も同じ形である（Stage I）。
- **E3 交互のロード:** 2 package の Scene を、どちらの登録順でも交互に load / unload / 再 load でき、marker と script が要求どおりである。
- **E4 共有の実測:** 共有の形の表の各行の判定に要る観測が record に残り、N file の解決（どちらの package の script・shader か）と、結合の有無による解除の結果が分かり、共通の package の物は key で別 package の Object として読める。
- **E5 回収:** 実験の後、登録 0・token 0・Scene の残り 0 で、同じ Play セッションの次の round が残骸なしで始まる。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id と head を持ち、各 round に終了行がある（無い round は未判定）。XML の合格は harness が最後まで動いたことだけを示し、答えは record の観測の行から読む（S2-0b と同じ）。fixture は既定の合成（Scene の folder の package `a` は root が `A`・`A2`・`PrefabA`、`b` は `B`・`PrefabB`、共通の folder の package `common` は `Keyed`）と、同じ GUID の asset を build ごとに書き換える専用の組（`p` と `q` が `Twin` を root に持つ）に分ける（§3）。

- **AC1（E1、I-P6）build と校正:** 既定の合成と専用の組の各 package が無改変の `UnityContentDirectoryAdapter` で build され、各 directory に `BuildContentRoot` が 1 件ある。各 directory の file 一覧（id、sha256、size）・合計 size・所要時間と、同じ asset の `LoadableObjectId` の値が record にある。**`a`・`b`・`common` の全ての対で、`Lit` の shader と script の束の file が同じ id・違う sha256 である**ことを record で確かめる。それ以外に同じ id で sha256 の違う file があれば、id と source asset とともに record に書く（`Resources/unity_builtin_extra` の file を含む。あっても未判定にはしない）。adapter に渡した build option と実行の fingerprint（I-P2 (e)）を record の先頭に書く（違わなければ N の列は何も試していないので未判定とし、B が fixture を直す）。校正 round で各 package を単独で登録し、索引の entry と root の entry が過不足なく一致し、marker と script が build の値である（Play の前に source の marker は第 3 の値 `source` に戻し、その値の観測は破れとする）。
- **AC2（E2）発見（pure、登録なし）:** lookup は索引の bytes と要求だけで完結し、呼び出しの間 Unity の登録が 0 件であることを record に残す。結果は「正しい package 1 つ」か、理由付きの `NotInComposition` / `Ambiguous` / 索引の破損で、先頭一致で成功しない。表現は要求の triple → 無ければ索引全体の `Full` の順で決める（`(K, Whitebox)→q`、`(K, Full)→p` の時に `q` が手元に無ければ `PackageAbsent` で、`p` の `Full` へ進まない）。package 1 つの索引も同じ型で通る。手元の package の集合は引数で渡す（実 tier の読み取りは S4）。canonical な内容が同じ索引 2 つは同じ結果で、canonical な内容の違う索引は `DifferentComposition` になる（比較は `compositionRevision` で行い、空白や並びだけの違いは同じ索引とする）（整合性の拒否ではなく、次の供給元へ進んでよい状態）。索引から package 間の重なり（同じ id で bytes が同じ / 違う）を引け、重なりの無い package 同士は結合の対象にならない。
- **AC3（I-P2）合成の拒否:** 次はいずれも拒否され、理由 code が区別される。同じ triple が 2 package。同じ GUID が別の triple で別 package に入る。root が別 package の閉包に居る（GUID で比べ、root 自身の項目を含む。既定）。同じ source の hash が package 間で違う。build の後・索引の生成前の再照合で hash が変わった。target / engine / schema の不一致。build 計画の実行 id の不一致（同じ source の別の実行の組）。索引の manifest hash と package の manifest の不一致。登録後の `BuildIdentity` と索引の不一致。索引の改竄（`compositionRevision` は索引の canonical な内容の sha256）。**build 後の漏れの検査:** 各 package の `ContentLayout.json` の source asset の一覧に、別の package の root（`common` の `Keyed` 等）が現れない（nested prefab、prefab variant、Scene に置いた prefab instance を経由した焼き込みも、layout の一覧で検出する）。現れたら AC3 の拒否と同じ理由 code で止める。**実機の負の確認:** 書き換えない同じ bytes の `p` + `q` は実際の閉包で既定の planner に重なりの code で止まり、`Loadable<Object>` field の参照が planner の入力に現れる（現れなければ、`Loadable` の重なりを既定で拒否できないので NO-GO）。
- **AC4（E3、I-P1、N の Scene の経路）交互のロード:** `a` と `b` を登録順 2 通りで、`A` load → `B` load → `A` unload → `A` 再 load → `B` unload → `B` 再 load → 全て unload の列を実行し、各 load の Scene の marker が要求の package と一致し、Scene の固有の MonoBehaviour が `== null` でなく marker の field を読め、Renderer の material の shader が null でなく `isSupported` で、続けての新規 load が成功する。`A` の material と `B` の material の shader の実体（`EntityId`）と、想定外の Error / Warning log を記録する。
- **AC5（I-P5、F1）要求だけの登録:** 要求の時に初めて登録し、その package の貸し出しが 0 になったら解除する（結合で延ばす時は理由を record に書く）列を、`A` 先と `B` 先の両方で実行する。`A` load → `A2` load → `A` unload の時点では `a` は登録のままで、`A2` unload で解除される（`A` と `A2` は同じ package の中の共有で、F1 の観測）。`B` の load は、登録前と解除後の 2 点で native の直接 load でも成功しない。`b` の directory を disk から外した状態で `A` の要求が成功する。
- **AC7（E4、I-P1、I-P3、F3）共通の package を key で要求:** `A` と `B` が読まれた状態で、それぞれ `common` の `Keyed`（共通の folder の prefab → material（`Lit`）→ texture と、共通用の固有の MonoBehaviour を持つ）を key で要求する。要求の順を 2 通りで行う: (i) `Keyed` を先に要求して `common` を登録し、その後に `A`・`B` を要求する（`Keyed` の load は `a`・`b` の登録より前）。(ii) `A`・`B` を要求した後に `Keyed` を要求する（`Keyed` の load は `a`・`b` の登録より後）。どちらも round の開始時に package を登録しない（I-P5）。各列で、`Keyed` の marker が `common` で、固有の MonoBehaviour が `== null` でなく、material の shader が null でなく `isSupported` で、`Keyed` から Instantiate した物を `A` の Scene に置ける。続けて、**結合なしの列**（各 package を貸し出し 0 で即座に解除）と **結合ありの列**（索引の重なりで後から登録した package の解除を延ばす）の 2 通りで、`a` の解除、`b` の解除、`common` の貸し出し 2 のうち 1 件の返却の後に、残る物（`Keyed` の instance、`B` の Scene）の同じ項目を、観測の語の 3 時点（同じ frame、次の frame、`Resources.UnloadUnusedAssets` と新規の load の後）で記録する。harness は Instantiate した物を破棄してから返却する（貸し出しの中に数える）。解除の前の解放を破る列は実行しない（Unity の文書が禁じる）。`A` の Scene の source は `Keyed` を直接参照せず、planner が `a` の閉包に `Keyed` が入らないことを確かめた後に build する（AC3）。
- **AC8（I-P1、I-P3、G10、N の両経路）同じ id で中身の違う file:** 2 つの部分を、最後の別の Play セッション（第 3 process）で実行する。
  - (1) N file の列: `a` と `b` を登録順 2 通りで両方登録し、各 package の root の `PrefabA`・`PrefabB`（package の固有の MonoBehaviour と、keyword の違う `Lit` の material を持つ）を `Loadable` で load する。load の順も 2 通り（先に登録した側が先 / 後）。各 prefab の固有の MonoBehaviour が `== null` でないか、marker、material の shader の null と `isSupported`、両 prefab の shader の実体が同じか、想定外の Error / Warning log を記録する。続けて、結合なしと結合ありの 2 通りで片方ずつ解除し、残る側の prefab の同じ項目を観測の語の 3 時点で記録する。列の間は登録 0 と `Resources.UnloadUnusedAssets` で回収し、前の列と同じ shader の `EntityId` が出たら再利用として record に残す（その列の解決の共有の判定は未判定）。同じ項目を、`A`・`B` の Scene（直接参照の経路）でも記録する。結合なしの列で破れが出なければ、結合の効果は「不要」と記録する（感度の対照ではなく観測の結果）。
  - (2) 合成の列: 同じ GUID の `Twin` の marker を package の build ごとに `p` / `q` に書き換えて build し、登録順 2 × load の経路 2（各 handle の `GetRootAssets` / `LoadableObjectId`）で、返る marker と、片方ずつの解除の前後の有効性と Error log を記録する。想定内の log は B が実行前に列挙して record の先頭に書く。
  - GO / CONDITIONAL ACCEPT / NO-GO は AC7 とともに判定定義で読む。(2) は解決の規則を直接見る観測で、単独では合否を決めない（(1) と AC7 の破れの説明に使う）。
- **AC10（I-P4）失敗:** `A` を load した後で、(1) `b` の directory が無い、(2) `b` の manifest の hash が索引と違う（事前の検証で止まる列）、(3) `b` の Scene が読む content file を全て切り詰める（事前の検証を迂回して native に届く列）、の各場合に `B` を要求する。どれも成功せず失敗の種別が record に残り、`A` の marker・script・Renderer の参照が有効で、`b` の登録は残らない（結合で延ばした場合は、`a` の解除の後に残らない）。元に戻した後の再試行が成功する。検出の時点（登録か load か）は観測する。(4) 貸し出しのある package の別の root の load 失敗では解除しないことを、fake の `IContentNativeDirectory` で `PackageRegistry` の EditMode test として確かめる。(5) 結合の規則を同じ fake で確かめる: 重なりのある package の解除の延長、重なりの無い package を延ばさないこと、延長は登録の回（登録ごとの連番）で比べ循環しないこと、延びている package への再要求は同じ登録を再利用して延長を取り消すこと（同じ path を 2 重に登録しない）、失敗した package は延ばさずに解除すること（その時の `a` の物の有効性は (1)〜(3) で観測する）。(1)〜(3) の壊す操作は build 出力の複製に対して行い、第 1 process の出力を変えない。
- **AC11（E5、I-P3）回収:** AC8・AC14・AC15 を除く各 round の終わりで、登録 0（harness と `GetContentDirectories`）、live token 0、fixture の Scene の残り 0、想定外の Error log 0。Domain Reload 無効の Play 再入で次の round がそれらの残りを検出しない。AC8・AC14・AC15 の残りは同じ項目を記録し、合否は判定定義に従う（AC15 は判定に使わない）。
- **AC12（program §4.1、U-S1-1）候補への写像（pure）:** 索引の結果から `ContentIdentity`（content set、`compositionRevision`、target、package の manifest sha256）と予約 key の事実を持つ `ContentCandidate` を作れ、S1 の `ContentCompatibilityRule` が互換と答える。content set の違う索引の候補は不採用になる。
- **AC13（I-P6）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書、program だけである。`pwsh tools/contract-audit.ps1` が合格する。実験の前後で `git status --porcelain` が一致し、`Assets/OneStarMakerGenerated`、一時 root、Build Settings、Enter Play Mode の設定、URP の設定に変化が無い。

- **AC14（U-S2-4、owner の回答待ち）N file を揃える方式:** 錨の root を `a`・`b`・`common` の全てに入れた別の組 `a'`・`b'`・`common'` を第 1 process で build し、全ての対で、比較の対象の file が全て同じ sha256 になるか（`Lit`、script の束、それ以外）を record で確かめる（AC1 と同じ読み方）。比較の対象は、2 つ以上の package の layout に同じ id で現れる file のうち、package ごとに生成する `BuildContentRoot` に由来する file を除いた物とし、除いた id は record に書く。錨と宣言した root に限り、専用の入口で次の検査の例外にする: 同じ triple が 2 package、root が別 package の閉包に居る、build 後の漏れの検査、I-P1 (c)。錨への要求は索引の lookup の対象にしない（錨は harness が handle から直接読む）。錨でない root には AC3 の拒否と負の確認がそのまま効く。揃わない時は、違った対と file の id・source asset を record に残し、Lightmap と Fog の stripping を Custom、Instancing を Keep All にした同じ組をもう 1 回だけ build して比べる（残す mode は B が選んで record に書く。この組は自分の fingerprint を build の直前に取り、I-P2 (e) の再照合は組ごとの fingerprint に対して行う。設定は build の成否によらず直後に戻し、復元を record に書く。AC13 の前後の一致は変えない）。それでも揃わなければその事実を記録して列を回さない。揃った時だけ、第 3 process で、揃った方の組（Project の設定のままの組が揃えばそれ、揃わなければ Custom の組）の `a'`・`b'`・`common'` を使い、開始時にその組の fingerprint と照合して、(1) AC8 (1) と同じ列、(2) AC7 の要求の順 (i)(ii) と結合なし・結合ありの解除の列を回す（(2) は、共通の package を先に登録して `Keyed` を先に load して保持した時に、後から登録した Scene の package の解除で壊れないかを見る）。判定定義では CONDITIONAL ACCEPT (iii) にだけ使い、それ以外は U-S2-2 の判断材料として報告する。
- **AC15（U-S2-5、owner の回答待ち）file の欠けた directory:** build 出力の複製で 2 つの列を第 3 process の最後に回す（AC10 と同じく第 1 process の出力は変えない）。どちらの列も、AC10 (3) と同じく事前の検証を通さず `NativeProbe` で native に直接登録する（複製は manifest の hash が合わなくなるので、registry を通すと Unity に届かない）。想定内の log は B が実行前に列挙して record の先頭に書く。(1) `a` の複製から `A2` の Scene だけが使う file（`ContentLayout.json` の依存で `A` の閉包に入らない file。0 件ならその事実を記録して列を回さない）を消して単独で登録し、登録が通るか、`A` の load と marker・script、`A2` の要求が失敗として返るかを記録する（1 回の build の directory を、取得した部分だけで使えるかの材料）。(2) AC14 が揃った時だけ、`a'` の複製から `common'` と同じ id・同じ sha256 の file を消し、`common'` を登録して `Keyed` を load して保持した状態で登録して、`A` と `PrefabA` の load が別の directory の同じ id の file で成立するか、`a'` の解除の後に `common'` の物が残るかを記録する（重なる file を Scene の package から抜く形の材料）。どちらも判定定義には使わず、観測と Error log を U-S2-2 の判断材料として報告する。

AC6 と AC9 は revision 0 の番号で、revision 1 では欠番にする（AC6 の Scene から共通の物への直接参照の複製は主の形でなく、S2-0b が不成立を確かめた。AC9 の同じ GUID の Scene の別 build は異なる build 計画の合成で問いの外。拒否は AC3）。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 製品の索引形式と BuildSystem の合成の入口、manifest と publisher、tier の実読み取りの形、file の id を `ContentLayout.json`（Unity の内部形式）から取るか | S2-b（新設） |
| 2 件目の install、revision lease、close の順序、登録の寿命の結合を製品のどこが持つか（S0-d が機構 (i) を選んだので、S0-b の移送の「(ii) を製品にする別スライス」は切られない。所有は本スライスの結論の後に決める） | S2-b か S3 |
| `SceneDirector`・owner 台帳・`IAssetManagement` との接続、要求の直列化（同じ package の同時登録の合流） | S3 |
| 読み込み途中の相手への unload・解除（G15） | S3 |
| Player での成立、shader の variant が描画で正しいか（画素） | S2-b / S3 |
| 4 つ以上の package、片方の directory の file が disk から消えた時にどちらの file を読むか（AC15 が観測する 2 列を除く。AC15 は判定に使わない材料で、製品の規則は決めない） | S4（S2-0b の移送と同じ） |
| `Loadable` field と Scene の id の package 間の重なりを許すこと | 要る host が現れた時の後続（本スライスは既定で拒否する） |
| Scene から共通の物を直接参照する複製に登録の寿命の結合を足す形（F2'）、Scene の直接参照での実体の数の代償 | 要る host が現れた時の後続（owner の想定では主の形でない。F2 のままは S2-0b で不成立） |
| tier の catalog（どの合成があるかの列挙）、install、prefetch、取得中の合流（G25） | S4 |
| 配布と保存を sha256 でまとめる実装、revision をまたぐ file の共有 | S2-b か S4（program §8 の S2-0 の移送） |
| 実データでの root と閉包の重なり、共有の連結成分の大きさ、N file の違いの頻度（F1 と CONDITIONAL ACCEPT の判断材料） | S2-b の前に owner（§2.5 U-S2-2）。読み取りの集計で足り、本スライスの合否にしない |

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで独立して失敗しうる未知が複数ある。

- **S2-a / S2-b の分割（revision 0 から維持）:** Unity の native 合成の挙動と、製品の索引・build 入口の設計は独立に失敗しうる。program §8 の S2 の問いを 1 スライスにせず、**分割する。** 本スライス（S2-a）は native の成立と共有の方針を確かめ、製品の形は結論の後の S2-b に送る。理由: 共有の方針の答えで索引の欄と planner の規則が変わる（revision 1 では結合のための file の id が索引に加わった）ので、先に製品の形を凍結すると手戻りになる。代償は fixture と索引の試作を S2-b で作り直すことで、試作は pure の 2 file に限る。S2 全体の確定は S2-b の Phase D で、それまで S3 / S4 の Phase B を始めない（program §8）。
- **N file の測定を別のスパイクに分けるか（revision 1 で検討）:** N file の挙動（AC4・AC8）、結合の効果と共通の package の key の要求（AC7・AC8）、発見と要求だけの登録（AC2・AC5）はそれぞれ別に失敗しうる。**分けない。** 理由: これらは同じ fixture・同じ build・同じ登録の harness を使い、build を伴う実行は 1 回で数十分かかる（S2-0b）。N file の答えが結合の規則（何で重なりと見るか）と CONDITIONAL ACCEPT の意味を決めるので、分けても後のスパイクが前の結論を入力に同じ harness を作り直すことになる。発見（AC2・AC3・AC12）は pure で Unity の未知を持たない。分けた場合の利点（N file の事実が先に出る）は、判定定義で AC ごとに `成立` / `不成立` を独立に記録することで得る。owner が分けたい場合は §2.5 U-S2-3。

### 2.5 未決事項（owner へ）

- **U-S2-1 分割と gate の語（revision 0 から）:** S2-a / S2-b に分け、program §8 の S2 の gate を次のとおり読み替えてよいか（program の S2-a・S2-b の行に反映済み）。「異なる build 同士の合成は後続」は「異なる build **計画**」と読む（1 package = 1 build なので、元の語のままでは gate が自己矛盾する）。「共有依存」は「§2 の共有の形の表の判定」と読む（Unity が追跡する package 間の依存は作れない）。推奨: 分ける、読み替える。
- **U-S2-2 CONDITIONAL ACCEPT の扱い:** 判定定義の CONDITIONAL ACCEPT（結合の条件で成立、または解決の共有あり）の時に、製品の registry に結合を持たせて進めるか、U-S2-4 の N file を揃える方式に進むか、縮退かを owner が S2-b の前に決める。実データの量はその時に測る。本スライスで SampleGame は使わない（D10）。判断の時に見る点（E-05〜E-07）: (a) N file は、揃えても揃えなくても全ての package の対で重なるので、結合は「先に登録した package が残る限り、後から登録した package を解除しない」に退化する。常駐する `common` がある構成では、Scene の package の登録は `common` の解除まで残る（貸し出しは返るので Scene 固有の物は解放される。残るのは登録と、その directory の file を消せない・差し替えられないこと）。(b) AC7 と AC14 (2) で、共通の package を先に登録して先に load した列が結合なしでも壊れず、逆の順の列だけが壊れるなら、「共通を先に登録し、共有する物を先に load して保持する」運用の規則が結合の代わりの候補になる。(c) 製品の既定として、bytes の違う重なりを build の時（索引の生成）に拒否する案。本スライスの索引は重なりの計算（AC2）まで持つので、拒否の規則は S2-b で決められる。(d) 縮退の形は「全 root を 1 回の build にして 1 directory を登録し、package は取得の単位（file の部分集合）にする」で、N file・重なり・解除の順の問題が全て消える。成否は AC15 (1) が材料になる。
- **U-S2-4 N file を揃える方式を AC14 として測るか（2026-11-12 に Claude が提案、owner の回答待ち）:** 全ての package に同じ「錨」の root（fixture の全 MonoBehaviour の型を持つ prefab と、`a`・`b`・`common` の material の keyword を全て含む shader variant collection か material の組）を入れ、`Lit` と script の束を全 package で同じ bytes にできるかを AC1 と同じ方法で確かめ、できれば AC8 (1) と同じ列を回す。揃えば S2-0b の中身の同じ file の状況（実体 1、解除の順だけが問題）になる見込み（仮説）。代償は、script や keyword を 1 つ足すと全 package を build し直すこと。推奨: 測る（AC1 の build に 1 組足すだけで、F3 が CONDITIONAL ACCEPT 以下だった時の次の手の材料になる）。owner が測らないと決めた時に外す物は §0 の引継ぎ票の一覧に従う。**2026-11-12 の追記（E-03〜E-05）:** 既存の build 履歴の読み取り（§1 現況）は、script の束が集合で決まるという読みと合うので、揃う見込みは上がった。あわせて次の 3 点を裁定する。(1) 揃えない組が load で壊れても、揃えた組が成立すれば CONDITIONAL ACCEPT (iii) とする（今の判定定義では NO-GO になり、揃えた組の結果を結論に使えない）。(2) 揃わない時に、stripping を Custom にした組を 1 回だけ build する（Project の設定を build の間だけ変えて戻す）。(3) 揃えた組で AC7 の列も回す（AC14 (2)）。推奨: 3 点とも採る。代償は第 1 process の build が 1〜2 組、第 3 process の列が約 2 倍に増えること（§5 の 1 時間の見込みを超えたら記録する）。
- **U-S2-5 file の欠けた directory を AC15 として測るか（2026-11-12、E-07）:** 縮退の形（1 回の build + 部分取得）と、重なる file を Scene の package から抜く形の成否は、どちらも「file の欠けた directory を Unity が登録・load できるか」で決まり、未実測である（後者の一部は program §8 で S4 に送ってある）。推奨: 測る（AC10 の複製を壊す仕組みを使う 2 列で、判定には使わない。U-S2-2 の選択肢 (c)(d) の材料が同じ実行で揃う）。測らないなら AC15 を外し、S4 の移送のままにする。
- **U-S2-3 N file のスパイクを先に切るか:** §2.4 のとおり分けない案を推奨する。分けるなら、AC1 の N file の校正・AC4・AC8 (1) と AC7 の解除の列を S2-0c（normal）として先に測り、本書は残りで A1 からやり直す。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentPackageDiscovery/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし。`OneStarMaker.Build.Content` と `OneStarMaker.Runtime` を既に参照し、両者の `AssemblyInfo.cs` に `OneStarMaker.Tests.Editor` 向けの `InternalsVisibleTo` がある）に置く。寿命は 1 回の test。第 1・第 2・第 3 process の間は一時 root の record と build 出力で渡し、メモリ上の状態は共有しない。依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。予想の合計は下の表の和で約 3,300 行（AC14・AC15 を測る場合。測らなければ約 3,100 行）で、S2-0b（約 2,260 行）の fixture の固定・process の分割・record の再計算を流用する。最大は `CompositionPureTests.cs` の 400 行で、行数の警報は無い（process ごとに file を分けた。G-03）。

pure と Unity の境界: `CompositionPlan`・`CompositionIndex`・`CompositionVerdict` は Unity の API を呼ばない。閉包（`BuildContentProjection` と `AssetDatabase.GetDependencies`）、source の hash、実行の fingerprint、`ContentLayout.json` の bytes は `CompositionFixture` が集めて data として渡す。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `CompositionPlan.cs` | package の分割の宣言と planner（triple と GUID の重複、root と閉包の重なり、source hash と fingerprint の差と再照合、build 計画の実行 id、書き換えを許す専用の入口） | 230 | pure。AC3 |
| `CompositionIndex.cs` | 索引の試作（`compositionRevision`、content set、target、engine、schema、実行 id、package ごとの identity と manifest sha256、file の id と sha256、package 間の重なり、entry の triple → package）の JSON、検証、lookup（手元の package の集合を引数に取る）、候補への写像、`ContentLayout.json` の解析（file の id、sha256、source asset の一覧、漏れの検査）。**製品の形の決定ではない** | 300 | pure。AC2、AC3、AC12 |
| `CouplingRule.cs` | 結合の判定（入力: 索引の重なり、登録の回の連番、貸し出し → 出力: 今解除してよい package と延ばす package と理由）。S2-b へ移送する中核の規則なので registry の実行から分けた（G-04） | 100 | pure。AC10 (5) |
| `CompositionVerdict.cs` | record の観測の行から、観測の語（load の破れ・解決の共有・解除の破れ）と AC ごとの結果と判定定義を計算する（S2-0b の `RegistrationVerdict` と同じ役。G-02） | 300 | pure。判断担当は結果の行と観測の行を突き合わせる |
| `CompositionPureTests.cs` | 上の 4 file と fake native の `PackageRegistry` の EditMode test | 400 | `[Explicit]` なし。通常の全 EditMode 回帰にも入る（Unity 非依存で速く、Phase D で削除するので許容する。「通常の回帰に混ぜない」のは Play を伴う `[Explicit]` の test だけ。G-06） |
| `CompositionFixture.cs` | fixture の Scene / prefab / material / texture の生成（fileID と GUID の固定は S2-0b の方式）、adapter の呼び出しで package を build、閉包・hash・fingerprint・layout の収集 | 320 | S2-0b の `RegistrationFixture` の拡張 |
| `PackageRegistry.cs` | `IContentNativeDirectory` を package ごとに持ち、要求の時の登録（要求元の記録）、token の計数、`CouplingRule` に従う解除、失敗の回収 | 200 | `IContentDirectoryBackend` を実装しない（install しないので所有の層を持たない）。**製品の候補ではない** |
| `NativeProbe.cs` | port に無い native の直接呼び出し（`ContentLoadManager.GetContentDirectories`、`LoadableObjectId` での load、handle ごとの `GetRootAssets`）と、観測の語の 3 時点の観測（`== null` の段と例外の捕捉） | 160 | registry の外に置き、registry は port だけを使う（G-04） |
| `CompositionBuilds.cs` | 第 1 process: fixture の生成、全 package の build、索引、漏れの検査、fingerprint、build 出力の sha256 の record | 260 | `[Explicit]` |
| `CompositionRounds.cs` | 第 2 process: 開始時の照合（build 出力の sha256、実行 id、head、fingerprint。一致しなければ未判定）、校正、AC4・AC5・AC7・AC10・AC11 の列 | 380 | `[Explicit]`。Play に入る |
| `CompositionNFileRounds.cs` | 第 3 process: 開始時の照合、AC8・AC14・AC15 の列（AC14 (2) は `CompositionRounds` の AC7 の列を組の引数で再利用する） | 340 | `[Explicit]`。Play に入る |
| `ExperimentRecord.cs` / `TempRootLease.cs` | JSONL の record、一時 root の予約と回収 | 140 / 130 | S2-0b・S0-b の縮小 |

fixture（SampleGame を使わない。D10）:

- 既定の合成は owner の想定する folder の構成に倣う（AC14 の組 `a'`・`b'`・`common'` は同じ構成に錨の root を足した物）: `Scenes/A/`（Scene `A`・`A2` と、その folder の prefab `PrefabA` → material → texture。package `a`）、`Scenes/B/`（Scene `B` と `PrefabB` → material → texture。package `b`）、`Common/`（prefab `Keyed` → material → 1 MiB の texture。package `common`）。共通の物は prefab・material・texture の組で置き、型ごとに束ねない。`A` と `A2` は `Scenes/A/` の material を直接参照する（F1。同じ package の中の共有）。Scene は `Common/` の物を直接参照しない。
- N file の作り方: `A` 側・`B` 側・`Keyed` の material は URP の `Lit` を使い、全ての対で keyword の組を変える（例: `A` は emission、`B` は alpha clip、`Keyed` は無し）。package 固有の MonoBehaviour は、既存の `OneStarMaker.Runtime` の internal sealed の型で、`Initialize` を呼ばない限り何もしない次の 3 つに固定する（G-01）: `a` は `AssetReleaseOnDestroy`、`b` は `VfxOwnerDestroyNotifier`、`common` は `UpdaterDriver`。Runtime で abstract でない MonoBehaviour は 4 つだけで、残る `UICommon` は OnDestroy で例外を設定しうるので使わない。3 つとも serialized な text を持たないので、marker は GameObject の名前に置き、script の有無は具象型の `GetComponent` と `GetComponents` の null の項目（missing script）で見る。Editor の asmdef の型は Play で component にできないので使わない。新しい script は作らない（作る必要が出たら Phase A へ戻す）。
- 専用の組: Object `Twin`（`p` と `q` の root）。**AC8 (2) の組だけ**、同じ GUID の `Twin` の marker を package の build ごとに書き換えて build し、書き換えの後に校正 round で build の値を確かめる（build の間の書き換えは実験の手段で、既定の planner は source hash の差で拒否する。AC3）。

## 4. 実装計画

- 順序: (0) §5 の未知の経路を確かめる最小の 1 round を先に作る（N file が `a`・`b` で実際に違うことを、build と layout の読み取りだけで確かめる）。(1) pure 2 file と test。(2) fixture と build。(3) registry と round。(4) 全 round の実行は Phase C。
- 対象外を維持する方法: 製品 file を変えない（AC13）。record と build 出力は一時 root（`TestResults/cpd/` 下、path 予算内）に置き、git に入れない。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る、adapter の build option を変えないと fixture が build できない、Editor Play で 2 件目の登録が根本的にできない（S0-b と矛盾する）、§3 の 3 つの MonoBehaviour の型が fixture に使えず新しい script が要る場合。
- B 適応の範囲: fixture の形、`Lit` の keyword の選び方、錨の root の作り方（AC14）、record の項目名、S2-0b の部品の再利用の程度。

## 5. テストとレビュー計画

- 単体テスト: `CompositionPureTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery.CompositionPureTests`。
- 判定必須テスト（GO 候補 head）: 次の 4 つを順に、別々の Unity process で実行する（Enter Play Mode の設定は Domain Reload 無効の 1 つに固定し、各 process の終わりに戻す）。
  1. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery.CompositionPureTests`
  2. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery.CompositionBuilds`（build と索引）
  3. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery.CompositionRounds`（校正、AC4・AC5・AC7・AC10・AC11）
  4. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery.CompositionNFileRounds`（AC8・AC14・AC15）
  各 XML の total が 1 以上で failed 0、root result `Passed`、record に全 round の終了行があり、record の実行 id と head が当該 head を指す。**process の自然終了をゲートにしない**（XML の完成と record の終了行で読む。build を伴う process の終了 stall は既知。`tools/Harness/README.md`）。Enter Play Mode の設定の復元は XML を書く前に終え、復元の結果を record に書く（X-11）。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC13）、実験 test は通常の回帰に混ぜない。代替証拠: AC13、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 4 コマンドで閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64 Player。観測は record と XML。目視は無い。証拠は record・XML・log を evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: batch の EditMode test から Play を往復して directory を登録する経路と、build と round を別 process に分ける経路は S0-b・S2-0b が成立させた。`[Explicit]` の filter 選択も同じ。**未確認:** 同じ round で複数 package を要求の時に登録する順序、壊れた content file の検出時点、既存の Runtime の MonoBehaviour を置いた fixture が build と Editor Play の load で script を保つか（N file の校正の前提）。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録）。§4 の (0) は B が build と layout の読み取りだけで行い、Play の round は含めない。
- 人間の判断が必要な条件: なし（CONDITIONAL ACCEPT の後の方針は Phase D の後に owner）。
- 高コスト証拠: なし（4 process の合計で 1 時間を超えない見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定は record を AC ごとに読む（S2-0b と同じく、第 2・第 3 process の判定 code が record の観測の行から AC ごとの結果を書き、判断担当はその行と観測の行を突き合わせる）。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: revision 0 と同じく Claude の session。
- C' 用に予約した担当: Phase A に関与していない Cursor 経由のベンダー（`cursor-agent` は AGENTS.md の契約どおり Grok 系だけ）。revision 1 の A2 でも Grok を使わない。

### 5.1 A2 の独立レビューと A3 の採否

#### revision 1

入力版は commit `621a4252` の本書（git の blob の内容 sha256 `573daa2d73abc7728426126c1775703630cfb5a7292f1fa5e9bf29880691d6f9`）。全て未関与の新規 session で、互いの指摘と revision 0 の A2 の指摘を見せていない（入力版の §5.1 に revision 0 の表があり、各担当には対象外と伝えた）。目的・利用者の 2 欄の sha256 は再計算で revision 0 と同じ `146d77b0b9e905f9cb16147c78d4a98b3f30a6bbedb13d92fb6212ad35d89f44` だったので、目的レビューはやり直さず、revision 0 の P-01〜P-38 を revision 1 と突き合わせた（新しい指摘は無い。P-05 の「推移的な依存」は、F3 の共通の package も key の要求なので不採用のまま）。

| レビュー | 担当 | 入力 | 結果 |
|---|---|---|---|
| アーキテクチャゲート（G） | Claude の新規 subagent | 入力版、`architecture-gates.md`、関係 source（read-only）、S2-0b の harness | blocker なし、major 4、minor 2 |
| 反例（X） | Claude の新規 subagent（枠固定: §3・§4 は実装済みと仮定） | 入力版、事実の文書 | blocker 1、major 6、minor 4 |
| A0 のみの代替（ALT） | Claude の新規 subagent | program と事実の文書、owner の folder の構成の発言。制約: program §8 の S2-a・S2-b の行に分割の案がある | ALT-01〜ALT-06 |

owner の入力（A2 の最中、2026-11-12）: folder の構成（Scene の folder と共通の folder、型ごとに束ねない）と、resource を独立して build して参照する形を主とする意向。主の形を F3 にし、F2 の測定を外した（§1「revision 1 で改めた点」。A2 入力版に反映済み）。続けて owner が「texture だけまとめても一緒では、どういう方法がよいか」と問い、Claude が N file を揃える方式を提案した（U-S2-4、回答待ち）。

A3 の分類（採用 / 一部 / 不採用）。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| X-01 誤解決の定義と U-S2-2 の不整合 | 採用 | §2 に観測の語（load の破れ・解決の共有・解除の破れ）を置き、判定定義を語で書き直した。U-S2-2 を新しい CONDITIONAL ACCEPT に揃えた |
| X-02 解決の共有での偶然の GO | 採用 | 解決の共有は GO から外し CONDITIONAL ACCEPT (ii) にした |
| X-03 観測の例外と再現する crash | 採用 | 観測の中の例外は破れ、同じ step で 2 回再現する crash は破れの観測（§2 の観測の語） |
| X-04 AC7 の登録順と I-P5 | 採用 | AC7 を要求の順 (i)(ii) に書き直し、I-P5 の検査を AC7・AC10 に広げた |
| X-05 延びた登録への再要求、循環 | 採用 | AC10 (5) と `CouplingRule`（登録の回の連番、再要求は再利用して取り消す、失敗した package は延ばさない） |
| X-06 N file の源の変化 | 採用 | I-P2 (e) に実行の fingerprint を足し、AC1 の record と第 2・第 3 process の開始時の照合に入れた |
| X-07 遅れた破れと列の間の再利用 | 採用 | 観測の 3 時点、列の間の回収と `EntityId` の再利用の記録 |
| X-08 一時的な登録 | 採用 | I-P5 で全ての登録の呼び出しに要求元 |
| X-09 AC1 の対 | 採用 | 全ての対 |
| X-10 索引の比較 | 採用 | canonical な内容（`compositionRevision`）で比べる |
| X-11 build option と設定の復元 | 採用 | option と fingerprint を record の先頭に、設定の復元を XML の前に |
| G-01 MonoBehaviour の型 | 採用 | 3 つの型を名指しで固定し、marker を GameObject の名前に、script の有無を `GetComponent` と missing script で見る（§3）。型を確かめた: 3 つとも `Initialize` を呼ばない限り何もしない |
| G-02 判定の計算の置き場 | 採用 | pure の `CompositionVerdict.cs`、layout の解析は `CompositionIndex` |
| G-03 行数と非分割の理由 | 採用 | process ごとに file を分けた（`CompositionBuilds` / `CompositionRounds` / `CompositionNFileRounds`） |
| G-04 結合と native I/O の混在 | 採用 | pure の `CouplingRule.cs`、native の直接呼び出しは `NativeProbe.cs` |
| G-05 壊す操作の対象と process 間の照合 | 採用 | 複製を壊す。第 2・第 3 process の開始時に sha256・実行 id・head・fingerprint を照合 |
| G-06 pure test と回帰、入力収集の境界 | 採用 | 回帰に入ることを明記して許容。pure と Unity の境界を §3 に書いた |
| ALT-01 問いを N file の寿命の結合に絞る | 既に計画の内容 | revision 1 の問いと同じ |
| ALT-02 直接参照の禁止と独立の build | 一部 | 直接参照の禁止は planner と AC3 の漏れの検査で採用。「1 つの build 計画」は宣言と実行 id の単位で、package ごとに別の `BuildContentDirectory` の呼び出し（独立の build）なので変えない |
| ALT-03 offline の漏れ検査を先のスライスに | 一部 | 漏れの検査（`ContentLayout.json` の source asset の一覧）を AC3 に入れた。Unity の未知を持たないのでスライスは分けない（§2.4） |
| ALT-04 最小の実機観測 | 既に計画の内容 | AC7・AC8。root でない共有物を key で引くことは問いの外（`Keyed` は root） |
| ALT-05 P2 共有の階層、P3、P4 | 一部 | P3 は結合として採用済み、P4 は縮退の先。P2 の階層は後続（§2.3 の 4 つ以上の package） |
| ALT-06 危険の一覧 | 採用 | 偽 null（§2 (k)）、nested prefab 等の漏れ（AC3）、build の時期の差（I-P2 (e)）、範囲（成果の範囲）、終了の判定（§5）は反映済みか今回反映した。型と層の先取りは spike の試作に限る（§3） |

#### revision 1 の A3 の途中: owner 経由の相談への回答（E）

2026-11-12。owner が外部レビュー用に書いた相談文（構成、実測した現象、想定する壊れ、割り切ること）への回答として、Claude の新規 session が本書・program・事実の文書と既存の build 履歴を読んで出した。A1 の主担当と同じベンダーなので、A2 の独立レビューの件数には数えない。入力は commit `2badbc71` の本書。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| E-01 N file は種類の一覧で閉じない（`Resources/unity_builtin_extra` の file が UI を含む build で別の hash） | 採用 | §1 現況、AC1（同じ id で sha256 の違う file を種類によらず record する） |
| E-02 script の束は build に入る script の集合で決まる、`Lit` は使用の和で決まる、という読みが既存の履歴と合う | 採用 | §1 現況（裏づけであり実証ではない。実証は AC14） |
| E-03 stripping が Automatic なので、錨だけでは `Lit` が揃わない可能性がある | owner の裁定待ち（U-S2-4 (2)） | AC14 の「揃わない時の 1 回」 |
| E-04 揃えない組が load で壊れると NO-GO（縮退）になり、揃えた組が成立しても結論に使えない。owner の相談文は揃えない組の壊れを想定している | owner の裁定待ち（U-S2-4 (1)） | 判定定義の CONDITIONAL ACCEPT (iii) |
| E-05 N file は全ての対で重なるので、結合は「先に登録した package が残る限り解除しない」に退化する。先に load して保持する運用で足りるかを揃えた組でも見る | owner の裁定待ち（U-S2-4 (3)） | U-S2-2 (a)(b)、AC14 (2) |
| E-06 製品の既定は、bytes の違う重なりを build の時に拒否する形が最も安い（実行時の挙動に頼らない） | S2-b の入力 | U-S2-2 (c)。本スライスの AC は変えない |
| E-07 「1 回の build + 部分取得」の縮退と、重なる file を抜く形の成否は、file の欠けた directory の登録で決まる | owner の裁定待ち（U-S2-5） | AC15 |

追記への反証レビュー（R）: Claude の新規 subagent（E の session とは別のモデル。枠固定: §3・§4 は実装済みと仮定）。入力は E の追記の差分（未 commit の版）と本書の全文。blocker 2、major 7、minor 1。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| R-01 (iii) が「§2.1 と §2.2 を全て満たし」と両立しない | 採用 | (iii) の除外の範囲を明示した |
| R-02 AC14・AC15 の残りと log が AC11 の未達を通じて NO-GO を作る | 採用 | AC11 の除外、AC15 の想定内の log |
| R-03 I-P5・I-P1 と AC14 (1)・AC15 | 採用 | I-P5 の対象外、I-P1 の注記 |
| R-04 錨の例外が triple だけでは索引を作れない | 採用 | AC14 の例外の一覧と lookup の扱い |
| R-05 Custom の組と fingerprint の再照合、どの組で列を回すか | 採用 | AC14（組ごとの fingerprint、失敗時の復元、揃った方の組）、(iii) の record |
| R-06 (i)(ii) の「全ての列」の範囲 | 採用 | AC4・AC7・AC8 に限定 |
| R-07 (iii) の意味の文と Scene の経路の除外 | 採用 | 意味を結合なしの列の結果で 2 つに分け、AC8 (1) の Scene の列を除外に足した |
| R-08 package ごとに違って当然の file が「全て同じ」を崩す | 採用 | AC14 の比較の対象の定義（推測に基づく指摘。除いた id を record する） |
| R-09 AC15 の実行経路と対象 | 採用 | native への直接登録、`Keyed` の保持、layout からの導出と 0 件の扱い |
| R-10 外す条件・行数・番号・範囲・commit の食い違い | 採用 | §0 の引継ぎ票を一覧の正にした。§3 の行数は表の和に直した（E の前から表の和と本文が合っていなかった） |

#### revision 0（保留した統合版。参照用）

入力版は commit `cdc8d6c8` の本書（内容 sha256 `8c5ffc22281924f0d89a8ab393006db55a8fc4254f074b23b4bfa786e6251457`）。全て未関与の新規 session で、互いの指摘を見せていない。

| レビュー | 担当 | 入力 | 結果 |
|---|---|---|---|
| アーキテクチャゲート（G） | Codex CLI（read-only） | 入力版と関係 source の写し | blocker なし、major 2 |
| 反例（X） | Claude の新規 subagent（枠固定: §3・§4 は実装済みと仮定） | 入力版 | 計 27 |
| 目的（P） | Claude の新規 subagent | 目的・利用者の 2 欄の原文だけ（sha256 `146d77b0b9e905f9cb16147c78d4a98b3f30a6bbedb13d92fb6212ad35d89f44`） | 38 項 |
| A0 のみの代替（ALT） | 目的レビューの担当が続けて担当 | program（A0）。制約: 入力版の program §8 の S2 行に「S2-b に分ける案」の 1 句があり、分割の推奨は完全には独立でない | 2 分割を推奨 |

revision 0 の A3 の分類（採用 / 一部 / 不採用）。revision 1 は採用した内容を引き継ぎ、F2 と AC8・AC9 に関わる行は revision 1 で置き換えた（§1「revision 1 で改めた点」）。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| G-01 分割と program の gate | 採用 | program §8 に S2-a / S2-b の案を反映。裁定は U-S2-1 |
| G-02 AC2 の tier の責務 | 採用 | 手元の package の集合を引数にし pure に限定。実 tier は S4（AC2、§3） |
| X-01 判定の網羅、X-02 破れの定義 | 採用 | 判定定義を全域にし、未判定を分けた |
| X-03 CA の覆い隠し、X-04 CA の条件 | 採用 | 判定定義、AC3 の実機の負の確認 |
| X-05 load の順、X-07 `C` の参照の観測 | 採用 | AC8 の列（revision 1 で N file と合成の列に置き換え） |
| X-06 `Loadable` / Scene の組 | 一部 | `Loadable` の重なりは既定で拒否し AC3 で負の確認。Scene の id の組は既定拒否のまま（§2.3） |
| X-08 校正、X-09 AC6 の閾値と両方向 | 採用 | AC1、AC6（revision 1 で AC6 を外し、実体の数は S2-0b の引用、両方向は AC7・AC8 の解除の列へ） |
| X-10 AC4 の主張 | 採用 | AC4 の列を延ばし §2 (j) に明記 |
| X-11 閉包と収録の一致 | 採用 | AC3 の実機の負の確認 |
| X-12 GUID、X-13 hash の時点、X-14 実行 id | 採用 | AC3、I-P2 |
| X-15 AC5 の自明、X-16 I-P5 の範囲、X-21 負の確認の時点 | 採用 | I-P5、AC5 |
| X-17 2 要求者 | 採用 | AC7 |
| X-18 Instantiate の残り | 一部 | Instantiate を貸し出しに数える列は採用。解除の前の解放を破る列は Unity の文書が禁じるので実行しない |
| X-19 失敗の解除、X-20 native への到達 | 採用 | I-P4、AC10 |
| X-22 AC8 の残骸 | 採用 | AC8 を最後の別 process へ、AC11 の扱いを分けた |
| X-23 record の結びつき、X-24 fixture の文言、X-25 Full の順、X-26 `DifferentComposition`、X-27 `git status` | 採用 | §2.2、§3 |
| P-01、P-03、P-04、P-06、P-07、P-11、P-13、P-16、P-18、P-19 | 採用 | AC1、AC2、AC5、AC6、判定定義、成果の範囲、共有の形の表 |
| P-02、P-08、P-12、P-14、P-21、P-22〜P-38 | 既に計画の内容 | 追加なし（P-15・P-17・P-20 は harvest の内容） |
| P-05 推移的な依存 | 不採用 | 合成に package 間の依存は無い（F2 は複製、F3 は呼び手の要求） |
| P-09 故意の欠陥の注入 | 一部 | 校正 round と AC3 の拒否で足り、判定器の mutation は作らない（revision 1 では AC7・AC8 の結合なしの列が対照になる） |
| P-10 直積 | 不採用 | 読み込み途中は S3（G15）。両方向で足りる |
| ALT-11 F1〜F3 | 採用 | §2 の表 |
| ALT-13 (1) id の一致 | 採用 | AC1 の record |
| ALT-18・ALT-19 gate の語 | 採用 | U-S2-1 |
| ALT-20 複数 install の所有 | 採用 | §2.3 |

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
