# 複数 package の発見と合成の実証（S2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-a。分割は §2.4）
- status: `A`（A3 の統合版、**未凍結・保留**。2026-11-11 に A2 の 4 件を反映した。A2 入力版は revision 0、commit `cdc8d6c8`。同日、Content Directory の前提（build をまたぐ共通物の file の同一性、同じ file を持つ 2 directory のメモリ上の扱い）を別の計画で先に確かめることを owner が決め、本書はその結果まで保留する。この版の commit は owner の指示による保留の記録で、A3 凍結版ではない）
- branch: `claude/content-package-discovery`（PR の base は `develop`）
- implementation base commit: `f8ffd023`（`develop` の head、2026-11-11。§1 の実測はこの base）
- implementation head commit: `未固定`
- risk: `high`（program §8。複数 directory の native 合成の初回実機）
- owner: root（依頼者）
- created: 2026-11-11
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: program §5.3（配布粒度と結論、F1〜F3 の判定、Unity の挙動と順序の制約、host の宣言の規則）、§8（S2-a の行と後続への移送）、§10 U6。実験 code は製品に残さず git 履歴に置く（S0-b と同じ）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§3.1・§4.1・§5.3・§9 G10 / G11 / G24・§10 U6）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）。本スライスは製品 code を変えないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B 同時実行制約の対象外
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5。A3 の統合版は未 commit の作業ツリーにあり、外部レビューには owner がその版を渡す）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: 保留。再開の条件は、Content Directory の前提を確かめる別の計画の結論が出ること。再開時は、その結論で本書の仮説・F1〜F3・AC を見直し（前提が崩れれば Phase A を新しい revision で再開）、owner の外部レビューと §2.5 の裁定を経て A3 を凍結する。
  - 次の作業: 再開時に、外部レビューの指摘を §5.1 と同じ形で採否へ足し、§2.5 の裁定を反映して、本書と program を A3 凍結版として commit する。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結前に Phase B を始めない。目的・利用者の 2 欄を変えたら目的レビューを 1 回やり直す。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 1 回の build 計画から作った複数の正規 Content Directory（package）について、要求された Scene / Object がどの package に入っているかを、Unity に何も登録しないうちに build の出した索引だけで特定し、要求に必要な package だけを登録して読めるかを Unity 実機で確かめる。その時、ある package の unload・登録解除・失敗が別の package の Scene / Object を壊さず、別の package の複製や別の build の物が要求の成功として返らないことを確かめる。共有する asset がどの形なら合成できるかを実測で分け、成立しなければ配布粒度の縮退を人間が判断できる材料を残す。
- 利用者: OSMFramework の開発者（本スライスの結論から製品の索引、供給元の adapter、Runtime の接続を後続で書く人）と、OSM を使う host application の開発者（どの root をどの package に入れるかを宣言する人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続）:
  - 製品の索引形式、BuildSystem の合成 build の入口、publisher と manifest の変更: 後続 S2-b（本スライスの結論の後で A1。program §8）。
  - `AssetManagement.InstallContentDirectory` の複数化、`ContentRevisionGate` の複数登録、`ContentDirectorySession` の複数化、`SceneDirector` と owner 台帳との接続: S2-b / S3。
  - 実在の manifest から候補を作る adapter、install、LAN、要求をまたぐ pin: S3 / S4。
  - Player での成立: S2-b / S3（§2「成果の範囲」）。
  - 取消（G15。S0-b が 1 directory で確かめた）、Enter Play Mode の 4 設定の行列（G26。S0-b が確かめた）: 再実証しない。
  - 異なる build 計画同士の合成（program §8 の「後続の別課題」）: 拒否の確認だけを行う。
- 現況（base `f8ffd023`。path は `unity/Assets/OneStarMaker/Scripts/` から）:
  - **build:** `Editor/Build/Content/UnityContentDirectoryAdapter.cs` は 1 回の呼び出しで 1 directory を `BuildPipeline.BuildContentDirectory` で作る。root は生成した `BuildContentRoot` 1 件で、選択 root を `BuildContentEntry` で持つ（Scene は `LoadableSceneId`、Object は `Loadable<Object>`）。`BuildContentCoordinator` は build ごとに新しい identity（時刻 + GUID）を発番し、`ContentTransportPublisher` は `revision = identity` の `transport.json` を 1 directory に 1 つ書く。**複数 directory を束ねる単位は無い。** `BuildContentProjection` は root ごとの依存閉包（`BuildDependencyEntry`）を持ち、build 前に root と閉包の関係を調べられる。
  - **Unity の仕様（6000.6 の公開文書、2026-11-11 に確認）:** `BuildContentDirectoryParameters` は `compression` / `extraScriptingDefines` / `name` / `options` / `outputPath` / `rootAssetPaths` だけで、別の directory への依存を宣言する欄は無い。**1 回の build の中だけ重複を除き、build は自己完結する**（同じ asset を参照する 2 build はそれぞれ自分の複製を持つ）。文書が示す build をまたぐ参照は実行時の untracked reference（登録後の `GetRootAssets` 等）だけで、「asset を 1 つの build からだけ参照し、他からは untracked で辿る」のが重複回避の案内である。`Loadable` / `LoadableSceneId` の参照は build に閉包として含まれる。`LoadableObjectId` は登録中の directory 全体から解決され、検索は登録の逆順である。`Loadable.LoadAsync` に directory handle の指定は無い（handle を取るのは `GetRootAssets` だけ）。登録解除の前に、その directory から読んだ物の解放が要る。Editor の登録は Play 中だけで、type tree が要る。Editor の Play での `Loadable` の別 directory の複製の扱い、閉包にしか無い Object が id で見つかるかは文書に無い。
  - **S0-b の結論（program §5.2）:** native 層で 2 directory を直接登録し、同じ identity は片方を優先、同時 load、unload と再 load が成立した。ただし 2 directory の共有依存と、root が別 directory の閉包に居る場合は見ていない。実験 code は commit `94b0c4a6` の `Tests/Editor/Spikes/ContentDemandRouting/`（`FixtureFactory`、`RoutingBackend` 496 行、`RoundRunner` 系、`TempRootLease` 227 行、`ExperimentRecord` 161 行）で、Enter Play Mode の 4 設定の行列のために全件 1 回で約 89 分かかった。
  - **登録の製品側:** `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` は 2 件目の install を拒み、`Runtime/BuildContent/ContentRevisionGate.cs` は process に 1 登録だけを許す。native の port `IContentNativeDirectory`（`UnityContentDirectoryBackend.cs`）は 1 directory 単位で、`OneStarMaker.Tests.Editor` から internal で使える。`ContentDirectoryIndex`（130 行）は 1 root の中の `(logical key, representation, kind)` の索引と `Full` への fallback を持つ。閉包の実体は `GetDependencies` の結果で、`.meta`、`.cs`、`Assets/` 外を除く。
  - **S1 が S2 に残した入力:** 互換の基準の 1 候補分は S1 が定めた（`ContentCompatibilityRule`）。合成の欄（共同の build plan の出所、shared asset の所有と重複、同一 logical identity の衝突、索引の hash 関係）は S2。
  - Unity 上では何も確認していない。
- UC ごとの利用者の入力（実行時の操作は無い。開発時の宣言を利用者の作業として数える）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| host が package を分ける | package 名と、それぞれに入れる root の一覧 | 依存閉包、閉包の重複の一覧、root の重複と閉包への混入の拒否、package ごとの identity、build 計画の実行 id、合成の revision | 分割は host の開発者が開発時に宣言。残りは build の時に自動導出 | program §5.3（単位を OSM が固定しない）。分割を機械が推測すると host の Region / Cell の知識が core に要る |
| 要求する | logical key、kind、表現（既存の `ContentRequest` の欄） | どの package か | 呼び出し側が要求時に / 索引から自動導出 | 目的の「要求された物がどの package か」。新しい入力は無い |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 1 つの build 計画から作った複数の package は、build の出す索引だけで要求の行き先を決め、必要な物だけを登録し、互いの複製・片方の解放・失敗に壊されずに合成できるか。できないなら、どの形の共有が原因か。
- 仮説（Phase C で検証する）: できる。単位は「合成」= 1 つの build 計画と 1 つの索引で、Stage I は package が 1 つの合成である。共有の形は下の F1〜F3 に分かれ、serialized の直接参照は package ごとに複製され（Unity の仕様どおり。合成の側で除去しない）、logical key の要求は索引が別 package へ送る（Unity の案内する untracked reference に当たる）。危険は 1 つで、ある package の root が別の package の閉包にも居ると、`Loadable` は登録全体から解決されるので、索引の送り先と別の複製を掴みうる。実験は既定でこの組を拒否し（§3 の planner）、拒否を外した組で実際に何が起きるかを測る。
- 共有の形（実測の前に固定する。判定 C が各形に `成立` / `不成立` / `未確定` と条件・根拠の record を付ける。未確定を不成立に数えない）:

| 形 | 内容 | 見る AC | 成立した時の代償 |
|---|---|---|---|
| F1 同じ package に置く | 共有する root を同じ package に入れ、1 build の重複除去に任せる | AC6 | package が共有の連結成分まで大きくなる |
| F2 複製 | 別 package の root が同じ asset を直接参照し、各 package が複製を持つ | AC6、AC8 | 重複の disk とメモリ。同じ asset が別の実体になる |
| F3 logical key の共有 | 共有する Object を別 package の root に置き、実行時に key で要求する | AC7 | 内容の作り方を key の要求へ寄せる。解除の順序を呼び手が守る |

- 成果の範囲: GO は「Editor Play の native 層で、fixture の合成が成立する」ことだけを意味する。**AC8 の GO が緩めるのは serialized の直接参照の Object の重なりだけ**で、`Loadable` field と Scene の id の重なりは本スライスの結論によらず既定で拒否する（§2.3）。製品の 2 件 install、`SceneDirector` や owner 台帳との接続、Player、実在の publish 経路（transport.json は harness が製品の型で書く）、大きな実データでの性能は主張しない。record の先頭に Unity の版、platform、Editor Play であることを書き、未検証の組み合わせへ結論を広げない。
- どうすればこの問いに誤って YES を出せるか（C と C' はこれを探す）:
  - (a) 全 package を先に登録して読み、登録の順の偶然で正しい Scene が出る（AC5）。
  - (b) marker を見ず Scene が load できただけを成功とする（AC4、AC8）。
  - (c) 複製の区別がつかず、別の複製を掴んでも一致して見える。書き換えが build に反映されていない（校正 round。AC1、AC8）。
  - (d) 閉包の重複を「hash が同じだから共有」と読む（AC6）。
  - (e) 拒否を外した組の観測を行わず、planner の拒否だけで「危険は無い」とする（AC8）。
  - (f) 登録解除が成功した事実だけを見て、残った側の Scene が壊れていないかを見ない（AC6、AC7、AC8、AC11）。
  - (g) 壊れた package の失敗を `try` の中だけで確認し、先に読んだ package の生存と登録の残りを見ない（AC10）。
  - (h) 索引を Unity の登録後に root から作る（U6 に反する）（AC2）。
  - (i) 別 build の成果物を同じ名前の revision で束ねて合成と呼ぶ（AC3、AC9）。
  - (j) AC4 の合格を「登録順に依存しない」と読む。AC4 の Scene は片方の package にしか無いので、示すのは交互の寿命の成立だけで、順序依存の証拠は AC8・AC9 にある。
- 判定定義（列挙に当たらない結果は NO-GO。環境の不調と harness の不備は判定せず未判定として B へ戻し、record の欠落・Editor の crash で AC の観測が無い時も未判定とする）:
  - **GO:** §2.1 と §2.2 を全て満たし、かつ AC8 の全ての列で、key の load が成功して marker が `overlap`、`C` の参照の marker が `c`、双方の解除の後の有効性が保たれ、想定外の Error log が 0 である。
  - **CONDITIONAL ACCEPT（スパイクの定義済みの成功）:** AC8 以外の AC を全て満たし、AC8 の破れが **key の解決の破れ（別の複製を掴む、load の失敗、解除で Error log や無効化が出る）だけ**で、`C` の直接参照の束縛の破れが無く、AC6 の両方向の確認に合格し、書き換えない同じ bytes の組が実際の閉包で既定の planner に**重なりの理由 code で**止まる（AC3）。意味は「root と別 package の閉包の重なりを禁じる条件付きで成立」。受け入れるかは owner が S2-b の前に決める（§2.5 U-S2-2）。
  - **NO-GO:** 上記以外。例: 直接参照の束縛が別 package の複製になる、拒否が重なりを止められない（`Loadable` の参照が planner の入力に出ない等）、AC2〜AC7 / AC10 / AC11 の未達。program §5.3 により縮退（大きな chunk + 先読み、全 revision 取得）を owner に返す。
- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO / CONDITIONAL ACCEPT で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-P1 要求した物と別の複製や別の build を成功にしない（program I1、G10）:** 索引が package P を指す要求の結果は、P の build marker を持つ Scene / Object である。登録の順と load の順に依存しない。
  - 崩しうる経路: (a) `SceneManager.LoadSceneAsync(LoadableSceneId)` と `Loadable.LoadAsync` は directory を指定できず、登録の逆順で解決される。(b) `ContentDirectoryIndex` の `Full` への fallback が、別 package の `Full` を拾う（索引の表現の規則）。(c) 同じ triple が 2 package に入る（planner が拒否）。
  - 探索した境界: `UnityContentDirectoryBackend`、`ContentDirectoryIndex`、Unity の公開文書。未確認: 閉包にしか無い Object が id で見つかるか、複製のどちらを掴むか（AC8 で測る）。
- **I-P2 合成の単位の外の物を混ぜない（program §4.1、G24）:** 索引に載っていない package、manifest の hash が索引と違う package、同じ source の hash が package 間で違う合成、target / engine / schema の違う package、別の実行の build を束ねた物は合成として使えない。
  - 崩しうる経路: (a) `ContentDirectoryIndex.TryCreate` は自分の identity だけを見て、他の package との関係を見ない。(b) `ContentTransportManifest` に合成の欄が無い。(c) 2 回の build の間に source が変わる（hash は package の build ごとに取り、全 build の後・索引の生成前に再照合する。比較するのは閉包の `Assets/` 下の file とその `.meta` で、`Assets/` 外は見ない）。(d) 同じ source の別の実行の成果物を 1 つの索引に束ねる（identity に build 計画の実行 id を含め、索引に記録して照合する）。
  - 探索した境界: build の port、manifest の型。未確認: なし。
- **I-P3 片方の解放が他方を壊さない（program I4、G11）:** ある package の Scene の unload や登録解除の後も、他の package の Scene / Object は読めて描画に必要な参照を保ち、続けて新規の load ができる。登録解除は、その package から読んだ物を全て返した後だけ行う。
  - 崩しうる経路: (a) 解放の前の `UnregisterContentDirectory`（Unity は error を出す）。(b) `Loadable` が別 package の複製に解決された時、その package の登録解除で参照が消える。(c) `CompleteContentDirectoryPlayStop` は drain を待たない（S0-b の移送。harness が自分で drain する）。
  - 探索した境界: S0-b の `RoutingBackend` の順序、Unity の登録解除の文書。未確認: (b) の実挙動（AC6、AC8）。
- **I-P4 失敗が成功に化けず、他を巻き込まない（program §4.1 fail-closed）:** 欠損・破損した package の要求は失敗として返り、既に読んだ他の package は生きたままである。その要求で新たに登録した package は貸し出しが 0 の時に解除され、既に貸し出しのある package の別の root の失敗では解除しない。復元後の再試行が成功する。
  - 崩しうる経路: (a) 登録の途中失敗の後始末（`ContentDirectorySession.RegisterCore` の rollback は 1 session の物）。(b) 登録は通り load で壊れる content file。(c) 索引が指す package が tier に無い時の「他の tier へ進む」と整合性の拒否の混同（program §3.1 の 10）。
  - 探索した境界: `RegisterCore`、`ContentAvailabilityStatus` の 4 値。未確認: 壊れた content file の検出時点（AC10 で測る）。
- **I-P5 要求していない package を登録しない（program I6、G02）:** AC5 の round の各 step の完了時と、登録の直前に、登録中の package の path の集合（harness の台帳と `GetContentDirectories` の両方）が「その時点までに要求され、まだ返していない物の package」と一致する。AC6・AC8・AC9 は両方の登録が前提の専用の round で、本条の対象外とする。
  - 崩しうる経路: (a) harness が開始時に全 package を登録する。(b) 依存の package を暗黙に登録する。
- **I-P6 製品を変えない（S0-b と同じ）:** §3 の新規 file と本書・program 以外の変更が無く、実験は assets・設定・登録の残りを残さない。
  - 崩しうる経路: (a) 既存 `.meta` の再 import の差分。(b) adapter の build option を変える。(c) `Assets/OneStarMakerGenerated` や Build Settings の残り。(d) Enter Play Mode の設定を戻さない。

**成立条件（起きなければならないこと）**

- **E1 合成の build:** 1 つの build 計画から、製品の adapter（無改変）で 2 件以上の正規 directory と 1 つの索引ができる。
- **E2 索引だけの発見:** 登録を 1 件もしていない状態で、索引の bytes から `(logical key, kind, representation)` の行き先の package が決まる。package が 1 つの合成も同じ形である（Stage I）。
- **E3 交互のロード:** 2 package の Scene を、どちらの登録順でも交互に load / unload / 再 load でき、marker が要求どおりである。
- **E4 共有の実測:** F1〜F3 の各形の判定に要る観測が record に残り、複製の量（disk、実行時の実体の数）が分かり、logical key の共有は別 package の Object として読める。
- **E5 回収:** 実験の後、登録 0・token 0・Scene の残り 0 で、同じ Play セッションの次の round が残骸なしで始まる。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id と head を持ち、各 round に終了行がある（無い round は未判定）。fixture は既定の合成（`a` は root が `A`・`A2`、`b` は `B`、`shared` は `Keyed`）と、planner の拒否を外す専用の組（`c` は `C`・`C2`、`overlap` は `Overlap`）に分ける（§3）。

- **AC1（E1、I-P6）build:** 既定の合成と専用の組の各 package が無改変の `UnityContentDirectoryAdapter` で build され、各 directory に `BuildContentRoot` が 1 件ある。各 directory の file 一覧・合計 size・所要時間と、同じ asset の `LoadableObjectId` の値が record にある。校正 round で各 package を単独で登録し、索引の entry と root の entry が過不足なく一致し、marker が build の値である（Play の前に source の marker は第 3 の値 `source` に戻し、その値の観測は破れとする）。
- **AC2（E2）発見（pure、登録なし）:** lookup は索引の bytes と要求だけで完結し、呼び出しの間 Unity の登録が 0 件であることを record に残す。結果は「正しい package 1 つ」か、理由付きの `NotInComposition` / `Ambiguous` / 索引の破損で、先頭一致で成功しない。表現は要求の triple → 無ければ索引全体の `Full` の順で決める（`(K, Whitebox)→q`、`(K, Full)→p` の時に `q` が手元に無ければ `PackageAbsent` で、`p` の `Full` へ進まない）。package 1 つの索引も同じ型で通る。手元の package の集合は引数で渡す（実 tier の読み取りは S4）。同じ bytes の索引 2 つは同じ結果で、bytes の違う索引は `DifferentComposition` になる（整合性の拒否ではなく、次の供給元へ進んでよい状態）。
- **AC3（I-P2）合成の拒否:** 次はいずれも拒否され、理由 code が区別される。同じ triple が 2 package。同じ GUID が別の triple で別 package に入る。root が別 package の閉包に居る（GUID で比べ、root 自身の項目を含む。既定）。同じ source の hash が package 間で違う。build の後・索引の生成前の再照合で hash が変わった。target / engine / schema の不一致。build 計画の実行 id の不一致（同じ source の別の実行の組）。索引の manifest hash と package の manifest の不一致。登録後の `BuildIdentity` と索引の不一致。索引の改竄（`compositionRevision` は索引の canonical な内容の sha256）。**実機の負の確認:** 書き換えない同じ bytes の `c` + `overlap` は実際の閉包で既定の planner に重なりの code で止まり、`C2` の `Loadable<Object>` の参照が planner の入力に現れる（現れなければ NO-GO の「止められない」に当たる）。
- **AC4（E3）交互のロード:** `a` と `b` を登録順 2 通りで、`A` load → `B` load → `A` unload → `A` 再 load → `B` unload → `B` 再 load → 全て unload の列を実行し、各 load の Scene の marker が要求の package と一致し、続けての新規 load が成功する。
- **AC5（I-P5）要求だけの登録:** 要求の時に初めて登録し、その package の貸し出しが 0 になったら解除する列を、`A` 先と `B` 先の両方で実行する。`A` load → `A2` load → `A` unload の時点では `a` は登録のままで、`A2` unload で解除される。`B` の load は、登録前と解除後の 2 点で native の直接 load でも成功しない。`b` の directory を disk から外した状態で `A` の要求が成功する。
- **AC6（E4、I-P3、F1 / F2）直接参照の共有:** `A`・`A2`・`B` が同じ prefab → material → texture を直接参照する。package 内（`A` と `A2`）の実体の数は 1、package 間（`A` と `B`）は 2 以上（1 なら未達）。instance id の組、`Resources.FindObjectsOfTypeAll` の該当数、`GetRuntimeMemorySizeLong`、1 directory に入れた baseline との disk の差が record にある。登録順 2 × 解除の向き 2（`a` を先に unload・解除 / `b` を先）の各場合に、残る側の Renderer の material と texture が null でなく、残る側から新規の load ができる。
- **AC7（E4、I-P3、F3）logical key の共有:** `A` と `B` が読まれた状態で、それぞれ `shared` の `Keyed` を key で要求する。`Keyed` の marker は `shared` で、`Keyed` から Instantiate した物を `A` の Scene に置いても `a` の解除の後に有効である。貸し出し 2 のうち 1 件の返却では `shared` は登録のままで、Object は有効である。harness は Instantiate した物を破棄してから返却する（貸し出しの中に数える）。解除の前の解放を破る列は実行しない（Unity の文書が禁じる）。
- **AC8（I-P1、I-P3、G10、F2）root と閉包の重なり:** 専用の組を、AC9 とともに最後の別の Play セッションで実行する。同じ GUID の `Overlap` の marker を package の build ごとに `overlap` / `c` に書き換えて build する。登録順 2 × load の順 2（key が先 / `C` が先）の 4 列で、key の Object の marker、`C` の参照の marker、双方の解除の前後の有効性、Error log を記録する。`C2`（`Loadable<Object>` field が `Overlap` を指す）は登録順 2 で同じ項目を記録する。GO / CONDITIONAL ACCEPT / NO-GO は判定定義で読む。想定内の log は B が実行前に列挙して record の先頭に書く。
- **AC9（I-P2、G24、G10）同じ識別子の別 build:** 同じ GUID の Scene を marker だけ変えて 2 回 build し、両方を登録して、どちらが返るかを登録順 2 通りで観測する。合否は record の存在と AC3 の拒否で見る。
- **AC10（I-P4）失敗:** `A` を load した後で、(1) `b` の directory が無い、(2) `b` の manifest の hash が索引と違う（事前の検証で止まる列）、(3) `b` の Scene が読む content file を全て切り詰める（事前の検証を迂回して native に届く列）、の各場合に `B` を要求する。どれも成功せず失敗の種別が record に残り、`A` の marker と Renderer の参照が有効で、`b` の登録は残らない。元に戻した後の再試行が成功する。検出の時点（登録か load か）は観測する。(4) 貸し出しのある package の別の root の load 失敗では解除しないことを、fake の `IContentNativeDirectory` で `PackageRegistry` の EditMode test として確かめる。
- **AC11（E5、I-P3）回収:** AC8・AC9 を除く各 round の終わりで、登録 0（harness と `GetContentDirectories`）、live token 0、fixture の Scene の残り 0、想定外の Error log 0。Domain Reload 無効の Play 再入で次の round がそれらの残りを検出しない。AC8・AC9 の残りは同じ項目を記録し、合否は判定定義に従う。
- **AC12（program §4.1、U-S1-1）候補への写像（pure）:** 索引の結果から `ContentIdentity`（content set、`compositionRevision`、target、package の manifest sha256）と予約 key の事実を持つ `ContentCandidate` を作れ、S1 の `ContentCompatibilityRule` が互換と答える。content set の違う索引の候補は不採用になる。
- **AC13（I-P6）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書、program だけである。`pwsh tools/contract-audit.ps1` が合格する。実験の前後で `git status --porcelain` が一致し、`Assets/OneStarMakerGenerated`、一時 root、Build Settings、Enter Play Mode の設定に変化が無い。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 製品の索引形式と BuildSystem の合成の入口、manifest と publisher、tier の実読み取りの形 | S2-b（新設） |
| 2 件目の install、revision lease、close の順序（S0-d が機構 (i) を選んだので、S0-b の移送の「(ii) を製品にする別スライス」は切られない。所有は本スライスの結論の後に決める） | S2-b か S3 |
| `SceneDirector`・owner 台帳・`IAssetManagement` との接続、要求の直列化（同じ package の同時登録の合流） | S3 |
| 読み込み途中の相手への unload・解除（G15） | S3 |
| Player での成立 | S2-b / S3 |
| `Loadable` field と Scene の id の重なりを許すこと | 要る host が現れた時の後続（本スライスは既定で拒否する） |
| tier の catalog（どの合成があるかの列挙）、install、prefetch、取得中の合流（G25） | S4 |
| 実データでの root と閉包の重なり、共有の連結成分の大きさ（F1 と CONDITIONAL ACCEPT の判断材料） | S2-b の前に owner（§2.5 U-S2-2）。読み取りの集計で足り、本スライスの合否にしない |

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで独立して失敗しうる未知が 2 つある（Unity の native 合成の挙動と、製品の索引・build 入口の設計）。program §8 の S2 の問いを 1 スライスにせず、**分割する。** 本スライス（S2-a）は native の成立と共有の方針を確かめ、製品の形は結論の後の S2-b に送る。理由: 共有の方針の答えで索引の欄と planner の規則が変わるので、先に製品の形を凍結すると手戻りになる。代償は fixture と索引の試作を S2-b で作り直すことで、試作は pure の 2 file に限る。S2 全体の確定は S2-b の Phase D で、それまで S3 / S4 の Phase B を始めない（program §8）。A0 だけから代替構成を出した担当も、同じ 2 分割を推奨した（§5.1 の制約を併記）。

### 2.5 未決事項（owner へ）

- **U-S2-1 分割と gate の語:** S2-a / S2-b に分け、program §8 の S2 の gate を次のとおり読み替えてよいか（作業ツリーの program に案を反映済み）。「異なる build 同士の合成は後続」は「異なる build **計画**」と読む（1 package = 1 build なので、元の語のままでは gate が自己矛盾する）。「共有依存」は「§2 の F1〜F3 の判定」と読む（Unity が追跡する package 間の依存は作れない）。推奨: 分ける、読み替える。
- **U-S2-2 CONDITIONAL ACCEPT の扱い:** AC8 で危険が確認された時は、root と他 package の閉包の重なりを禁じる条件付きで進めるか、縮退かを owner が S2-b の前に決める。実データの量はその時に測る。本スライスで SampleGame は使わない（D10）。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentPackageDiscovery/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし。internal は既存の `InternalsVisibleTo`）に置く。寿命は 1 回の test、依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。予想の合計は約 1,600 行で、S0-b（約 3,500 行）から Enter Play の行列・snapshot・`SceneDirector` の harness を除いた。行数の警報は無い（最大 350 行）。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `CompositionPlan.cs` | package の分割の宣言と planner（triple と GUID の重複、root と閉包の重なり、source hash の差と再照合、build 計画の実行 id、拒否を外す専用の入口） | 180 | Unity 非依存の pure。AC3 |
| `CompositionIndex.cs` | 索引の試作（`compositionRevision`、content set、target、engine、schema、実行 id、package ごとの identity と manifest sha256、entry の triple → package、重複の一覧）の JSON、検証、lookup（手元の package の集合を引数に取る）、候補への写像 | 220 | pure。AC2、AC3、AC12。**製品の形の決定ではない** |
| `CompositionPureTests.cs` | 上の 2 file と fake native の `PackageRegistry` の EditMode test | 240 | `[Explicit]` なしで 1 回の filter で走る |
| `CompositionFixture.cs` | fixture の Scene / prefab / material / texture の生成と、adapter の呼び出しで package を build | 220 | S0-b の `FixtureFactory` の縮小 |
| `PackageRegistry.cs` | `IContentNativeDirectory` を package ごとに持ち、要求の時の登録、token の計数、解放後の解除、失敗の回収 | 220 | `IContentDirectoryBackend` を実装しない（install しないので所有の層を持たない）。**製品の候補ではない** |
| `CompositionRounds.cs` | Play の往復、校正 round、AC4〜AC11 の列（AC8・AC9 は最後の別セッション） | 350 | `[Explicit]` の EditMode test から Play に入る |
| `ExperimentRecord.cs` / `TempRootLease.cs` | JSONL の record、一時 root の予約と回収 | 130 / 130 | S0-b の縮小 |

fixture（SampleGame を使わない。D10）: 既定の合成は Scene `A`・`A2`（package `a`）、`B`（`b`）、prefab `SharedDirect`（3 つの Scene の直接参照）→ material → 1 MiB の texture、Object `Keyed`（`shared` の root）。専用の組は Scene `C`（`Overlap` の直接参照）・`C2`（`Loadable<Object>` field が `Overlap` を指す）（package `c`）、Object `Overlap`（`overlap` の root）。marker は Scene では MonoBehaviour、Object では自身の component の text に置く。同じ asset から作った複製は bytes が同じで区別できないので、**AC8 と AC9 の組だけ**、同じ GUID の asset の marker を package の build ごとに書き換えて build し、書き換えの後に校正 round で build の値を確かめる（build の間の書き換えは実験の手段で、既定の planner は source hash の差で拒否する。AC3）。AC6 の複製の別は instance id で見る。

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
- 操作・実行時・目視条件の検証経路: 全て上の 1 コマンドで閉じる（XML の total と failed に加え、record の実行 id が当該 head を指し、全 round に終了行があること。Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64 Player。観測は record と XML。目視は無い。証拠は record・XML・log を evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: batch の EditMode test から Play を往復して directory を登録する経路は S0-b が成立させた（`RoundRunner`）。`[Explicit]` の filter 選択も同じ。**未確認:** 同じ round で複数 package を要求の時に登録する順序と、壊れた content file の検出時点。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録）。
- 人間の判断が必要な条件: なし（CONDITIONAL ACCEPT の後の方針は Phase D の後に owner）。
- 高コスト証拠: なし（1 回の実行で 1 時間を超えない見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定は record を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: A3 と同じ session（Claude）。
- C' 用に予約した担当: Phase A に関与していない Cursor 経由のベンダー（`cursor-agent` は AGENTS.md の契約どおり）。

### 5.1 A2 の独立レビューと A3 の採否

入力版は commit `cdc8d6c8` の本書（内容 sha256 `8c5ffc22281924f0d89a8ab393006db55a8fc4254f074b23b4bfa786e6251457`）。全て未関与の新規 session で、互いの指摘を見せていない。

| レビュー | 担当 | 入力 | 結果 |
|---|---|---|---|
| アーキテクチャゲート（G） | Codex CLI（read-only） | 入力版と関係 source の写し | blocker なし、major 2 |
| 反例（X） | Claude の新規 subagent（枠固定: §3・§4 は実装済みと仮定） | 入力版 | 計 27 |
| 目的（P） | Claude の新規 subagent | 目的・利用者の 2 欄の原文だけ（sha256 `146d77b0b9e905f9cb16147c78d4a98b3f30a6bbedb13d92fb6212ad35d89f44`） | 38 項 |
| A0 のみの代替（ALT） | 目的レビューの担当が続けて担当 | program（A0）。制約: 入力版の program §8 の S2 行に「S2-b に分ける案」の 1 句があり、分割の推奨は完全には独立でない | 2 分割を推奨 |

目的レビューのやり直しに使える未関与の担当は残している（目的・利用者の 2 欄は A3 で変えていないので、やり直しは発生していない）。

A3 の分類（採用 / 一部 / 不採用）。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| G-01 分割と program の gate | 採用 | program §8 に S2-a / S2-b の案を作業ツリーで反映。裁定は U-S2-1 |
| G-02 AC2 の tier の責務 | 採用 | 手元の package の集合を引数にし pure に限定。実 tier は S4（AC2、§3） |
| X-01 判定の網羅、X-02 破れの定義 | 採用 | 判定定義を全域にし、未判定を分けた |
| X-03 CA の覆い隠し、X-04 CA の条件 | 採用 | 判定定義、AC3 の実機の負の確認 |
| X-05 load の順、X-07 `C` の参照の観測 | 採用 | AC8 を 4 列にし必須項目を固定 |
| X-06 `Loadable` / Scene の組 | 一部 | `C2` の `Loadable` だけ AC8 に足し、GO の意味を直接参照に限定。Scene の id の組は既定拒否のまま（§2.3） |
| X-08 校正、X-09 AC6 の閾値と両方向 | 採用 | AC1、AC6、AC8 |
| X-10 AC4 の主張 | 採用 | AC4 の列を延ばし §2 (j) に明記 |
| X-11 閉包と収録の一致 | 採用 | AC3 の実機の負の確認 |
| X-12 GUID、X-13 hash の時点、X-14 実行 id | 採用 | AC3、I-P2 |
| X-15 AC5 の自明、X-16 I-P5 の範囲、X-21 負の確認の時点 | 採用 | I-P5、AC5 |
| X-17 2 要求者 | 採用 | AC7 |
| X-18 Instantiate の残り | 一部 | Instantiate を貸し出しに数える列は採用。解除の前の解放を破る列は Unity の文書が禁じるので実行しない |
| X-19 失敗の解除、X-20 native への到達 | 採用 | I-P4、AC10 |
| X-22 AC8 の残骸 | 採用 | AC8・AC9 を最後の別セッションへ、AC11 の扱いを分けた |
| X-23 record の結びつき、X-24 fixture の文言、X-25 Full の順、X-26 `DifferentComposition`、X-27 `git status` | 採用 | §2.2、§3 |
| P-01、P-03、P-04、P-06、P-07、P-11、P-13、P-16、P-18、P-19 | 採用 | AC1、AC2、AC5、AC6、判定定義、成果の範囲、F1〜F3 の表 |
| P-02、P-08、P-12、P-14、P-21、P-22〜P-38 | 既に計画の内容 | 追加なし（P-15・P-17・P-20 は harvest の内容） |
| P-05 推移的な依存 | 不採用 | 合成に package 間の依存は無い（F2 は複製、F3 は呼び手の要求） |
| P-09 故意の欠陥の注入 | 一部 | 校正 round と AC3 の拒否で足り、判定器の mutation は作らない |
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
