# 複数 package の発見と合成の実証（S2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2 の前半。分割は §2「スライス分割の判断」）
- status: `A`（A1 初稿。A2 入力版）
- branch: `claude/content-package-discovery`（PR の base は `develop`）
- implementation base commit: `f8ffd023`（`develop` の head、2026-11-11。§1 の実測はこの base）
- implementation head commit: `未固定`
- risk: `high`（program §8。複数 directory の native 合成の初回実機）
- owner: root（依頼者）
- created: 2026-11-11
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: program §5.3（配布粒度と結論）、§8（S2 の行と後続への移送）、§10 U6。実験 code は製品に残さず git 履歴に置く（S0-b と同じ）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§3.1・§4.1・§5.3・§9 G10 / G11 / G24・§10 U6）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）。本スライスは製品 code を変えないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B 同時実行制約の対象外
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A2（独立レビュー）。
  - 次の作業: §5「A2 の独立レビュー」の割当てで、本 commit の版をレビューする。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: A3 の凍結は owner の外部レビューの後。
  - 引き渡さないもの: 目的レビューには §1 の目的と利用者の原文だけを渡す。

## 1. 目的と対象外

- 目的: 1 回の build 計画から作った複数の正規 Content Directory（package）について、要求された Scene / Object がどの package に入っているかを、Unity に何も登録しないうちに build の出した索引だけで特定し、要求に必要な package だけを登録して読めるかを Unity 実機で確かめる。その時、ある package の unload・登録解除・失敗が別の package の Scene / Object を壊さず、別の package の複製や別の build の物が要求の成功として返らないことを確かめる。共有する asset がどの形なら合成できるかを実測で分け、成立しなければ配布粒度の縮退を人間が判断できる材料を残す。
- 利用者: OSMFramework の開発者（本スライスの結論から製品の索引、供給元の adapter、Runtime の接続を後続で書く人）と、OSM を使う host application の開発者（どの root をどの package に入れるかを宣言する人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続）:
  - 製品の索引形式、BuildSystem の合成 build の入口、publisher と manifest の変更: 後続 S2-b（本スライスの結論の後で A1。program §8 に行を足す）。
  - `AssetManagement.InstallContentDirectory` の複数化、`ContentRevisionGate` の複数登録、`ContentDirectorySession` の複数化、`SceneDirector` と owner 台帳との接続: S2-b / S3。
  - 実在の manifest から候補を作る adapter、install、LAN、要求をまたぐ pin: S3 / S4。
  - Player での成立: S2-b / S3（§2「成果の範囲」）。
  - 取消（G15。S0-b が 1 directory で確かめた）、Enter Play Mode の 4 設定の行列（G26。S0-b が確かめた）: 再実証しない。
  - 異なる build 同士の合成（program §8 の「後続の別課題」）: 拒否の確認だけを行う。
- 現況（base `f8ffd023`。path は `unity/Assets/OneStarMaker/Scripts/` から）:
  - **build:** `Editor/Build/Content/UnityContentDirectoryAdapter.cs` は 1 回の呼び出しで 1 directory を `BuildPipeline.BuildContentDirectory` で作る。root は生成した `BuildContentRoot` 1 件で、選択 root を `BuildContentEntry` で持つ（Scene は `LoadableSceneId`、Object は `Loadable<Object>`）。`BuildContentCoordinator` は build ごとに新しい identity（時刻 + GUID）を発番し、`ContentTransportPublisher` は `revision = identity` の `transport.json` を 1 directory に 1 つ書く。**複数 directory を束ねる単位は無い。** `BuildContentProjection` は root ごとの依存閉包（`BuildDependencyEntry`）を持ち、build 前に root と閉包の関係を調べられる。
  - **Unity の仕様（6000.6 の公開文書、2026-11-11 に確認）:** `BuildContentDirectoryParameters` は `compression` / `extraScriptingDefines` / `name` / `options` / `outputPath` / `rootAssetPaths` だけで、別の directory への依存を宣言する欄は無い。**1 回の build の中だけ重複を除き、build は自己完結する**（同じ asset を参照する 2 build はそれぞれ自分の複製を持つ）。文書が示す build をまたぐ参照は実行時の untracked reference（登録後の `GetRootAssets` 等）だけで、「asset を 1 つの build からだけ参照し、他からは untracked で辿る」のが重複回避の案内である。`Loadable` / `LoadableSceneId` の参照は build に閉包として含まれる。`LoadableObjectId` は登録中の directory 全体から解決され、検索は登録の逆順である。`Loadable.LoadAsync` に directory handle の指定は無い（handle を取るのは `GetRootAssets` だけ）。登録解除の前に、その directory から読んだ物の解放が要る。Editor の登録は Play 中だけで、type tree が要る。Editor の Play での `Loadable` の別 directory の複製の扱い、閉包にしか無い Object が id で見つかるかは文書に無い。
  - **S0-b の結論（program §5.2）:** native 層で 2 directory を直接登録し、同じ identity は片方を優先、同時 load、unload と再 load が成立した。ただし 2 directory の共有依存と、root が別 directory の閉包に居る場合は見ていない。実験 code は commit `94b0c4a6` の `Tests/Editor/Spikes/ContentDemandRouting/`（`FixtureFactory`、`RoutingBackend` 496 行、`RoundRunner` 系、`TempRootLease` 227 行、`ExperimentRecord` 161 行）で、Enter Play Mode の 4 設定の行列のために全件 1 回で約 89 分かかった。
  - **登録の製品側:** `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` は 2 件目の install を拒み、`Runtime/BuildContent/ContentRevisionGate.cs` は process に 1 登録だけを許す。native の port `IContentNativeDirectory`（`UnityContentDirectoryBackend.cs`）は 1 directory 単位で、`OneStarMaker.Tests.Editor` から internal で使える。`ContentDirectoryIndex`（130 行）は 1 root の中の `(logical key, representation, kind)` の索引と `Full` への fallback を持つ。
  - **S1 が S2 に残した入力:** 互換の基準の 1 候補分は S1 が定めた（`ContentCompatibilityRule`）。合成の欄（共同の build plan の出所、shared asset の所有と重複、同一 logical identity の衝突、索引の hash 関係）は S2。
  - Unity 上では何も確認していない。`tools/docs-audit.ps1` は commit の前に通す。
- UC ごとの利用者の入力（実行時の操作は無い。開発時の宣言を利用者の作業として数える）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| host が package を分ける | package 名と、それぞれに入れる root の一覧 | 依存閉包、閉包の重複の一覧、root の重複と閉包への混入の拒否、package ごとの identity、合成の revision | 分割は host の開発者が開発時に宣言。残りは build の時に自動導出 | program §5.3（単位を OSM が固定しない）。分割を機械が推測すると host の Region / Cell の知識が core に要る |
| 要求する | logical key、kind、表現（既存の `ContentRequest` の欄） | どの package か | 呼び出し側が要求時に / 索引から自動導出 | 目的の「要求された物がどの package か」。新しい入力は無い |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 1 つの build 計画から作った複数の package は、build の出す索引だけで要求の行き先を決め、必要な物だけを登録し、互いの複製・片方の解放・失敗に壊されずに合成できるか。できないなら、どの形の共有が原因か。
- 仮説（Phase C で検証する）: できる。単位は「合成」= 1 つの build 計画と 1 つの索引で、Stage I は package が 1 つの合成である。共有は 2 種類に分かれ、**serialized の直接参照は package ごとに複製され**（Unity の仕様どおり。合成の側で除去しない）、**logical key の要求は索引が別 package へ送る**（これが Unity の案内する untracked reference に当たる）ので、共有したい物は通常の package の root に置ける。危険は 1 つで、ある package の root が別の package の閉包にも居ると、`Loadable` は登録全体から解決されるので、索引の送り先と別の複製を掴みうる。実験は既定でこの組を build 前に拒否し（§3 の planner）、拒否を外した組で実際に何が起きるかを測る。
- 成果の範囲: GO は「Editor Play の native 層で、fixture の合成が成立する」ことだけを意味する。製品の 2 件 install、`SceneDirector` や owner 台帳との接続、Player、実在の publish 経路（transport.json は harness が製品の型で書く）、大きな実データでの性能は主張しない。
- どうすればこの問いに誤って YES を出せるか（C と C' はこれを探す）:
  - (a) 全 package を先に登録して読み、登録の順の偶然で正しい Scene が出る（要求した物だけの登録を見ていない。AC5）。
  - (b) marker を見ず Scene が load できただけを成功とする。別 package の複製でも load はできる（AC4、AC8）。
  - (c) 同じ build の同じ bytes で複製の区別がつかず、別の複製を掴んでも一致して見える（fixture の marker は package ごとに違う値にする。AC6、AC8）。
  - (d) 閉包の重複を「hash が同じだから共有」と読む。hash の一致は実行時の実体の共有を意味しない（AC6）。
  - (e) 拒否を外した組の観測を行わず、planner の拒否だけで「危険は無い」とする（AC8）。
  - (f) 登録解除が成功した事実だけを見て、解放前の Scene が実は壊れていないかを見ない（AC7、AC8、AC11）。
  - (g) 壊れた package の失敗を `try` の中だけで確認し、先に読んだ package の Scene の生存と登録の残りを見ない（AC10）。
  - (h) 索引を Unity の登録後に root から作る（Runtime の走査。U6 に反し、要求の前に全部の登録が要る）（AC2）。
  - (i) 別 build の成果物を同じ名前の revision で束ねて合成と呼ぶ（AC3、AC9）。
- 判定定義:
  - **GO:** §2.1 の不変条件と成立条件を §2.2 で全て確認でき、AC8 の組で I-P1 / I-P3 が破れない（拒否を緩められる）。
  - **CONDITIONAL ACCEPT（スパイクの定義済みの成功）:** AC8 以外は全て満たし、AC8 の組で I-P1 か I-P3 が破れる。かつ planner の拒否で fixture のその組が build 前に止まる。意味は「root と別 package の閉包の重なりを禁じる条件付きで成立」。この制約を実データで受け入れられるかは owner が S2-b の前に判断する（縮退の選択肢に含める）。
  - **NO-GO:** AC4 / AC5 / AC6 / AC7 / AC10 / AC11 のどれかが未達、または拒否しても fixture の組を止められない。program §5.3 により縮退（大きな chunk + 先読み、全 revision 取得）を owner に返す。
- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO / CONDITIONAL ACCEPT で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-P1 要求した物と別の複製や別の build を成功にしない（program I1、G10）:** 索引が package P を指す要求の結果は、P の build marker を持つ Scene / Object である。登録の順に依存しない。
  - 崩しうる経路: (a) `SceneManager.LoadSceneAsync(LoadableSceneId)` と `Loadable.LoadAsync` は directory を指定できず、登録の逆順で解決される。(b) `ContentDirectoryIndex` の `Full` への fallback が、別 package の `Full` を拾う（索引の表現の規則）。(c) 同じ triple が 2 package に入る（planner が拒否）。
  - 探索した境界: `UnityContentDirectoryBackend`、`ContentDirectoryIndex`、Unity の公開文書。未確認: 閉包にしか無い Object が id で見つかるか、複製のどちらを掴むか（AC8 で測る）。
- **I-P2 合成の単位の外の物を混ぜない（program §4.1、G24）:** 索引に載っていない package、manifest の hash が索引と違う package、同じ source path の hash が package 間で違う合成、target / engine / schema の違う package は合成として使えない。
  - 崩しうる経路: (a) `ContentDirectoryIndex.TryCreate` は自分の identity だけを見て、他の package との関係を見ない。(b) `ContentTransportManifest` に合成の欄が無い。(c) 2 回の build の間に source が変わる。
  - 探索した境界: build の port、manifest の型。未確認: なし。
- **I-P3 片方の解放が他方を壊さない（program I4、G11）:** ある package の Scene の unload や登録解除の後も、他の package の Scene / Object は読めて描画に必要な参照を保つ。登録解除は、その package から読んだ物を全て返した後だけ行う。
  - 崩しうる経路: (a) 解放の前の `UnregisterContentDirectory`（Unity は error を出す）。(b) `Loadable` が別 package の複製に解決された時、その package の登録解除で参照が消える。(c) `CompleteContentDirectoryPlayStop` は drain を待たない（S0-b の移送。harness が自分で drain する）。
  - 探索した境界: S0-b の `RoutingBackend` の順序、Unity の登録解除の文書。未確認: (b) の実挙動（AC8）。
- **I-P4 失敗が成功に化けず、他を巻き込まない（program §4.1 fail-closed）:** 欠損・破損した package の要求は失敗として返り、既に読んだ他の package は生きたままで、失敗した package の登録は残らず、復元後の再試行が成功する。
  - 崩しうる経路: (a) 登録の途中失敗の後始末（`ContentDirectorySession.RegisterCore` の rollback は 1 session の物）。(b) 登録は通り load で壊れる content file。(c) 索引が指す package が tier に無い時の「他の tier へ進む」と整合性の拒否の混同（program §3.1 の 10）。
  - 探索した境界: `RegisterCore`、`ContentAvailabilityStatus` の 4 値。未確認: 壊れた content file の検出時点（AC10 で測る）。
- **I-P5 要求していない package を登録しない（program I6、G02）:** 実験の各時点で、登録中の package は「その時点までに要求され、まだ返していない物の package」と一致する。
  - 崩しうる経路: (a) harness が開始時に全 package を登録する。(b) 依存の package を暗黙に登録する。
- **I-P6 製品を変えない（S0-b と同じ）:** §3 の新規 file と本書・program 以外の変更が無く、実験は assets・設定・登録の残りを残さない。
  - 崩しうる経路: (a) 既存 `.meta` の再 import の差分。(b) adapter の build option を変える。(c) `Assets/OneStarMakerGenerated` や Build Settings の残り。(d) Enter Play Mode の設定を戻さない。

**成立条件（起きなければならないこと）**

- **E1 合成の build:** 1 つの build 計画から、製品の adapter（無改変）で 2 件以上の正規 directory と 1 つの索引ができる。
- **E2 索引だけの発見:** 登録を 1 件もしていない状態で、索引の bytes から `(logical key, kind, representation)` の行き先の package が決まる。package が 1 つの合成も同じ形である（Stage I）。
- **E3 交互のロード:** 2 package の Scene を、どちらの登録順でも交互に load / unload / 再 load でき、marker が要求どおりである。
- **E4 共有の実測:** 直接参照の共有は package ごとの複製であることと、その量（disk、実行時の実体の数）が record に残り、logical key の共有は別 package の Object として読める。
- **E5 回収:** 実験の後、登録 0・token 0・Scene の残り 0 で、同じ Play セッションの次の round が残骸なしで始まる。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。「観測」とある項目は record に値があること自体を見る。

- **AC1（E1、I-P6）build:** fixture の 4 package（`a`、`b`、`shared`、`overlap`。§3）が無改変の `UnityContentDirectoryAdapter` で build され、各 directory に `BuildContentRoot` が 1 件ある。各 directory の file 一覧・合計 size・所要時間が record にある。
- **AC2（E2）発見（pure、登録なし）:** 索引の lookup は Unity の登録を呼ばずに、(1) 正しい package、(2) `NotInComposition`、(3) 表現の `Full` fallback（要求の表現が無い時だけ。別 package の別表現を黙って採らない）を返す。package 1 つの索引も同じ型で通る。2 つの tier の folder に同じ索引の bytes があれば同じ結果になり、bytes が違えば `CompositionMismatch` になる。package が tier に無い時は `PackageAbsent` で、整合性の拒否と区別される。
- **AC3（I-P2）合成の拒否（pure）:** 次はいずれも planner か索引の検証が拒否し、理由を区別して返す。同じ triple が 2 package。package の root が別 package の閉包に居る（既定）。同じ source path の hash が package 間で違う（G24）。target / engine / schema の不一致。索引の manifest hash と package の manifest の不一致。索引の改竄（`compositionRevision` は索引の canonical な内容の sha256 と一致する）。
- **AC4（E3、I-P1）交互のロード:** package `a` と `b` を、登録順 `a→b` と `b→a` の両方で、`A` load → `B` load → `A` unload → `A` 再 load → `B` unload の列を実行し、各 load の Scene の marker が要求の package と一致する。
- **AC5（I-P5）要求だけの登録:** AC4 の列を「要求の時に初めて登録し、その package の物が全て返ったら解除する」で実行し、各時点の登録集合（`ContentLoadManager.GetContentDirectories` の件数と harness の台帳）が I-P5 と一致する。`b` が未登録の時に `B` の load は成功しない（負の確認）。
- **AC6（E4、I-P3）直接参照の共有:** `A` と `B` が同じ prefab → material → texture を直接参照する。観測: 両方 load した時の実体の別（instance id の組、`Resources.FindObjectsOfTypeAll` の該当数）と package ごとの disk 上の増分（1 directory に入れた baseline との差）、`GetRuntimeMemorySizeLong`。`A` の unload の後も `B` の Renderer の material と texture が null でない。
- **AC7（E4、I-P3）logical key の共有:** `A` が load された状態で、索引の指す package `shared` の Object を logical key で load でき、その Object の marker が `shared` である。`a` を解除しても Object は有効で、`shared` の解除は Object の解放の後にだけ行う。
- **AC8（I-P1、I-P3）root と閉包の重なり（G10）:** planner の拒否を外す専用の経路で、`overlap` の root を `c` の Scene が直接参照する組を、登録順 2 通りで実行する。観測: `overlap` の key で load した Object の marker、登録の相手の解除の順と解除の後の Object の有効性、Unity の error log の有無。合否は判定定義の GO / CONDITIONAL ACCEPT を分ける。既定の planner はこの組を AC3 のとおり拒否する。
- **AC9（I-P2）同じ識別子の別 build（G24、G10）:** 同じ GUID の Scene を marker だけ変えて別の build にし、両方登録した時に、どちらが返るかを登録順 2 通りで観測する。索引はこの 2 つを 1 つの合成にしない（AC3）。観測は記録だけで、合否は記録の存在と AC3 で見る。
- **AC10（I-P4）失敗:** `A` を load した後で、(1) `b` の directory が無い、(2) `b` の content file を切り詰めた、(3) `b` の manifest の hash が索引と違う、の各場合に `B` を要求する。どれも成功せず、失敗の種別が record に残り、`A` の Scene は生きており、`b` の登録が残らない。元に戻した後の再試行で `B` が load できる。検出の時点（登録か load か）は観測する。
- **AC11（E5、I-P3）回収:** 各 round の終わりで、登録 0、live token 0、fixture の Scene の残り 0、想定外の Error log 0。Domain Reload 無効の Play 再入で次の round が残骸を検出しない。
- **AC12（program §4.1、U-S1-1）候補への写像（pure）:** 索引の結果から `ContentIdentity`（content set、`compositionRevision`、target、package の manifest sha256）と予約 key の事実を持つ `ContentCandidate` を作れ、S1 の `ContentCompatibilityRule` が互換と答える。content set の違う索引の候補は不採用になる。
- **AC13（I-P6）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書、program だけである。`pwsh tools/contract-audit.ps1` が合格する。実験の後に `Assets/OneStarMakerGenerated`、一時 root、Build Settings、Enter Play Mode の設定に変化が無い。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 製品の索引形式と BuildSystem の合成の入口、manifest と publisher | S2-b（新設） |
| 2 件目の install、revision lease、close の順序 | S2-b / S3 |
| `SceneDirector`・owner 台帳・`IAssetManagement` との接続、要求の直列化（同じ package の同時登録の合流） | S3 |
| Player での成立 | S2-b / S3 |
| tier の catalog（どの合成があるかの列挙）、install、prefetch、取得中の合流（G25） | S4 |
| 実データでの root と閉包の重なりの量（CONDITIONAL ACCEPT の時の判断材料） | S2-b の前に owner（§2.5 U-S2-2） |

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで独立して失敗しうる未知が 2 つある（Unity の native 合成の挙動と、製品の索引・build 入口の設計）。program §8 の S2 の問いを 1 スライスにせず、**分割する。** 本スライス（S2-a）は native の成立と共有の方針を確かめ、製品の形は結論の後の S2-b に送る。理由: 共有の方針の答えで索引の欄と planner の規則が変わるので、先に製品の形を凍結すると手戻りになる。代償は fixture と索引の試作を S2-b で作り直すことで、試作は pure の 2 file に限る。

### 2.5 未決事項（owner へ）

- **U-S2-1 分割:** 上記のとおり S2-a / S2-b に分けてよいか。推奨: 分ける。
- **U-S2-2 CONDITIONAL ACCEPT の扱い:** AC8 で危険が確認された時は、root と他 package の閉包の重なりを禁じる条件付きで進めるか、縮退かを owner が S2-b の前に決める。実データの量はその時に測る。本スライスで SampleGame は使わない（D10）。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentPackageDiscovery/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし。internal は既存の `InternalsVisibleTo`）に置く。寿命は 1 回の test、依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。予想の合計は約 1,400 行で、S0-b（約 3,500 行）から Enter Play の行列・snapshot・`SceneDirector` の harness を除いた。行数の警報は無い（最大 300 行）。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `CompositionPlan.cs` | package の分割の宣言と planner（triple の重複、root と閉包の重なり、source hash の差、拒否を外す専用の入口） | 150 | Unity 非依存の pure。AC3 |
| `CompositionIndex.cs` | 索引の試作（`compositionRevision`、content set、target、engine、schema、package ごとの identity と manifest sha256、entry の triple → package、重複の一覧）の JSON、検証、lookup、候補への写像 | 200 | pure。AC2、AC3、AC12。**製品の形の決定ではない** |
| `CompositionPureTests.cs` | 上の 2 file の EditMode test | 220 | `[Explicit]` なしで 1 回の filter で走る |
| `CompositionFixture.cs` | fixture の Scene / prefab / material / texture の生成と、adapter の呼び出しで package を build | 220 | S0-b の `FixtureFactory` の縮小 |
| `PackageRegistry.cs` | `IContentNativeDirectory` を package ごとに持ち、要求の時の登録、token の計数、解放後の解除、失敗の回収 | 220 | `IContentDirectoryBackend` を実装しない（install しないので所有の層を持たない）。**製品の候補ではない** |
| `CompositionRounds.cs` | Play の往復と AC4〜AC11 の列 | 260 | `[Explicit]` の EditMode test から Play に入る |
| `ExperimentRecord.cs` / `TempRootLease.cs` | JSONL の record、一時 root の予約と回収 | 130 / 130 | S0-b の縮小 |

fixture: Scene `A`（package `a`）、`B`（`b`）、`C`（`c`）、prefab `SharedDirect`（`A` と `B` の直接参照）→ material → 1 MiB の texture、Object `Keyed`（`shared` の root）、`Overlap`（`overlap` の root、`C` の直接参照）。marker は package ごとの値を持つ MonoBehaviour の text（`a`、`b` …）で Scene に置く。SampleGame を使わない（D10）。同じ asset から作った複製は bytes が同じで区別できないので、**AC8 と AC9 の組だけ**、同じ GUID の asset の marker を package の build ごとに書き換えて build する（build の間の書き換えは実験の手段で、既定の planner は source hash の差で拒否する。AC3）。AC6 の複製の別は instance id で見る。

## 4. 実装計画

- 順序: (0) §5 の未知の経路を確かめる最小の 1 round を先に作る。(1) pure 2 file と test。(2) fixture と build。(3) registry と round。(4) 全 round の実行は Phase C。
- 対象外を維持する方法: 製品 file を変えない（AC13）。record と build 出力は一時 root（`TestResults/` 下、path 予算内）に置き、git に入れない。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る、adapter の build option を変えないと fixture が build できない、Editor Play で 2 件目の登録が根本的にできない（S0-b と矛盾する）場合。
- B 適応の範囲: fixture の形、record の項目名、S0-b の部品の再利用の程度。

## 5. テストとレビュー計画

- 単体テスト: `CompositionPureTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery.CompositionPureTests`。
- 判定必須テスト（GO 候補 head）: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentPackageDiscovery`（Enter Play Mode の設定は Domain Reload 無効の 1 つに固定し、終わりに戻す）。XML の total が 1 以上で failed 0、root result `Passed`、record に全 round がある。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC13）、実験 test は通常の回帰に混ぜない。代替証拠: AC13、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 1 コマンドで閉じる（Unity バッチ、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64 Player。観測は record と XML。目視は無い。証拠は record・XML・log を evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: batch の EditMode test から Play を往復して directory を登録する経路は S0-b が成立させた（`RoundRunner`）。`[Explicit]` の filter 選択も同じ。**未確認:** 同じ round で 4 package を要求の時に登録する順序と、壊れた content file の検出時点。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録）。
- 人間の判断が必要な条件: なし（CONDITIONAL ACCEPT の後の方針は Phase D の後に owner）。
- 高コスト証拠: なし（1 回の実行で 1 時間を超えない見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定は record を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session。
- A2 の独立レビュー: A3 で記録する。
- C' 用に予約した担当: Phase A に関与していないベンダー（A3 で記録する）。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
