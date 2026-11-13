# 上書きの build と登録契約（S2-2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2。program の S2 を §2.5 の判断で S2-2（本書）と S2-3（目的 build の索引、A1 未着手）に分けた）
- status: `A`（**A3 の統合版、未凍結。** 2026-11-14 に A2 の 3 件、凍結前の外部レビューの 1 巡目（draft PR #186）、主担当の見直しを反映した（§5.1）。owner の指示により凍結していない。A2 入力版は commit `cf31a6f2`、外部レビューの 1 巡目の対象は commit `288cf7bd`）
- branch: `claude/content-overlay-build`（PR の base は `develop`）
- implementation base commit: `66564116`（`develop` の head、2026-11-14。§1 の現況はこの base）
- implementation head commit: `未固定`
- risk: `high`（公開 API の追加、製品の build 経路で root の一部だけを build する初回の実機）
- owner: root（依頼者）
- created: 2026-11-14
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4（上書きの build と逆引き、preflight と outcome の追加の欄の名前）、[13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「Unity 6.6 Content Directories の実証済み境界」（§2.3 の登録契約）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（製品の経路で作った上書きの実測）、program §5.3・§8
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§5.3「当面の方針」、§8 の S2 の行と S2-0〜S2-1 の移送、§9 G10 / G11）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本。X6）。本スライスは `Scripts/Runtime/` を変えないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない制約の対象外
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: owner による採否と凍結の指示（必要なら外部レビューの 2 巡目）。その後に Phase A の主担当が A3 凍結版を切る。
  - 次の作業: owner が凍結を指示したら、status と本票を凍結版に改めて commit し、Phase B を起こす。外部レビューの 2 巡目を行うなら、指摘を §5.1 に採否つきで足す。
  - 引き継ぐ commit: この票を含む commit（SHA は PR の状態ブロックと起動指示で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: owner の指示があるまで凍結しない。凍結の前に Phase B を始めない。目的・利用者の文言を変えたら、目的レビューをやり直す。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 取得した目的 build を base にして Editor で Play する時に、手元で編集した asset の集合を渡すだけで、その編集の影響を受ける Scene と asset の全部を編集後の版で読ませる「上書き」の Content Directory を、製品の build 経路で作れるようにする。渡した編集が上書きに反映されなかった場合は、その編集と理由が記録から分かる。あわせて、base と上書きを登録する側が守る契約（登録と解除の順、同じ id の物の扱い、重ねてよい組み合わせ）を、実測で確かめた範囲を添えて定める。
- 利用者: host application の Editor code を書く人（上書きの build を呼ぶ入口を作る）と、base と上書きを Runtime に登録する実装を書く人（契約を読む）。編集する開発者が直接操作する入口は、このスライスでは作らない。開発者に求める入力は、編集した asset の集合だけにする。
- 目的・利用者の文言の経緯: 上の 2 欄は、program §8 の S2 の行（2026-11-14 に owner が縮めた版）から本書で初めて書いた文である。2026-11-14 に owner が A2 の前にこの文言を承認した（§2.8 U1）。A2 の目的レビューは、この 2 欄の本文だけをそのまま見た。A2 の後に文言は変えていない。
- 目的の「記録」の読み（A3 で固定。§5.1 G-05）: 逆引きが常に返す結果と、上書きを build した時の preflight の両方を指す。上書きが要らないと分かって build しなかった時は、結果だけが記録である。
- 対象外:
  - `Scripts/Runtime/` の変更。`AssetManagement` が base と上書きの 2 つを持つ実装、record の由来、`ContentRevisionGate` の扱いは S3。
  - 要求した Scene を含む目的 build を選ぶ索引（S2-3）。
  - 編集した asset の集合をどこから得るか（Project の選択、VCS の変更一覧）、menu、SampleGame の宣言での上書きの入口、実データでの確認（S5）。
  - 上書きの配布。上書きは作った PC の Editor の Play だけで使い、publish しない。
  - Player、script の code・field の変更、sprite atlas、audio、lightmap、URP `Lit` の variant の見た目、prefab variant、override を持つ prefab instance、Scene の中で unpack した prefab、root の移動と rename、Play の途中での上書きの作り直し・解除、上書きが 2 つ以上（§2.4）。
- 現況（base `66564116`）:
  - **上書きの形は実測済みで、作り方は未実装。** S2-1 は、`BuildPipeline.BuildContentDirectory` を harness が直接呼んで base と上書きを作り、製品の `UnityContentDirectoryBackend` を 2 つ持って登録した。登録の順が base → 上書きの時だけ上書きの版が出て、base だけにある Scene が直接参照する編集した texture は base の版のままだった（[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）」）。製品の build 経路（`BuildContentCoordinator`）で、編集から選んだ root だけを build したことは無い。
  - **build の入力は plan と snapshot の対。** `BuildContentCoordinator.Build`（`Editor/Build/Content/BuildContentCoordinator.cs`）は `BuildPlan` と `BuildMaterializationSnapshot` を受け、`BuildContentProjection.Create` が「snapshot の候補は全て plan の selected か excluded に現れる」ことを要求する（欠けは `MissingCandidate`）。root ごとの依存閉包は `snapshot.Dependencies`（`BuildDependencySnapshot`: root の GUID・path と閉包の entry）にあり、`AssetDependencySnapshotBuilder` が `AssetDatabase.GetDependencies(path, true)` から作る。閉包に入るのは `Assets/` の下で、`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref` でない file だけ（`IsContent`。Materialization の internal）。
  - **adapter は root だけを Unity に渡す。** `UnityContentDirectoryAdapter` は生成した root の asset 1 つを `rootAssetPaths` に渡し、依存は Unity が build の時の source から集める。OSM の閉包は build の入力を絞らない。
  - **plan を作り直す public な道が無い。** `BuildPlan` のコンストラクタは internal で、friend は test の assembly 2 つ（`OneStarMaker.Tests.Editor`・`SampleGame.Tests.Editor`）と `SampleGame.DependOnAll.Editor` だけ。`OneStarMaker.Build.Content` は `OneStarMaker.Build.Selection` を参照するが friend ではない（[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の X6 と同じ根）。
  - **directory は自分が何の上書きかを持たない。** `BuildContentRoot` は build identity・target・entry・任意の宣言（名前・表現の主値・runtime 既定値）だけを持つ。preflight（`reports/<identity>/preflight.json`）は selected・excluded・roots・閉包の和集合・requirements を持ち、root ごとの閉包は持たない。preflight は projection の成否より前に書かれ、失敗した build でも残る。
  - **publish は Full の build だけを受ける。** `ContentTransportPublisher.Publish` は outcome の `buildMode` が `Full` でなければ拒む。selected が何であるかは見ない。
  - **登録は 1 つだけ。** `AssetManagement.InstallContentDirectory` は 2 件目を `InvalidOperationException` で拒む。本スライスは変えない。
  - **workspace は contentSet ごと。** coordinator は `work/<target>/<contentSet>` を再利用し、`Incremental` は前の出力を残したまま `content/<identity>` へ複写する。
  - **この repository の Scene に prefab instance は無い。** `unity/Assets` の Scene（668 件）と prefab に、source prefab を持つ instance が 1 つも無い（§5 の凍結前の事実確認）。事実の文書は、Scene に置いた prefab instance・nested prefab・prefab variant を未測定とし、instance は置いた側の file に焼き込まれると書いている。
- UC ごとの利用者の入力:

  **UC-1 編集を上書きにして build する**（呼び手は host の Editor code。開発者から見れば「編集を確かめる Play の前の 1 回」）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | 編集した asset の path の集合 | 利用者が実行時に渡す（出所は呼び手が決める。§2.8 U3） | 目的の「編集した asset の集合を渡すだけ」そのもの。唯一の利用者の入力 |
  | 目的の宣言から作った plan と snapshot（bootstrap 等の合成を終えた物） | 開発時の宣言から自動導出（呼び手が既存の入口で作る） | 上書きを base と同じ目的の内側に収める。利用者は選ばない |
  | base の identity（contentSet・revision・target・manifest の digest） | 自動導出（呼び手が、これから登録する base から読む） | 作った時と別の base に重ねない契約（§2.3 C6）の材料 |
  | 上書きの contentSet（workspace の名前） | 呼び手の既定値。base の contentSet と同じ名前は製品が拒む | base の workspace に上書きの出力を混ぜない |
  | 出力先の root | 既定値（coordinator の既存の規則） | — |

  機械が決めるもの: 上書きに入れる root、root ごとの理由、当たらなかった編集とその理由、実体が重複する共有 asset の一覧、build identity、build の mode（`Full` に固定）。

  呼び手の義務: (1) 逆引きが「上書き不要」を返した時（E が空、またはどの root にも当たらない）は build しない。その時、結果の「当たらなかった編集」を利用者へ出すのは呼び手である（preflight は作られない）。(2) 逆引きは、渡された snapshot の時点の閉包で判定する。snapshot を作ってから上書きの build が終わるまで source を変えないことは、既存の build と同じく呼び手の前提である。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **目的 build 1 つを base にした時、編集した asset の集合から製品の build 経路で上書きを作り、base → 上書きの順に登録した Editor の Play で、渡した編集の影響を受ける全ての root が、上書きを build した時点の手元の版で出て、上書きに入らなかった root は base の版で出ると言えるか。**（渡していない手元の編集は、上書きに入った root からは手元の版、入らなかった root からは base の版で出る。§2.3 C10。program §8 の S2-2 の行と同じ文）
- 誤って YES を出す道（各 AC はこれを塞ぐ形にする）:
  - (a) 観測が directory ではなく `AssetDatabase` の source を読む。Editor の Play で source が編集後なら、何も上書きしなくても全部が編集後に見える。→ Play の前に fixture の source を消し、base だけの root しか参照しない canary が base の版で出ることを同じ Play で確かめる（AC7）。
  - (b) 絞らずに全 root を上書きに入れる。漏れは無いが、上書きではなく作り直しである。→ 影響を受けない root が上書きの entry に無いこと（AC2・AC5）と、base の版で出ること（AC7）。
  - (c) 逆引きの単体テストが、期待と同じ手で作った閉包だけを見る。→ 実 `AssetDatabase` から作った snapshot で prefab instance と nested prefab を通し、期待は fixture の作者が書いた root の名前で持つ（AC2）。
  - (d) 編集前と編集後が観測値で区別できない fixture、または base が既に編集後の source から作られている。→ base の build の直前に v1、上書きの build の直前に v2 を、変えた asset の全部で record に残す（AC5 の前提 P1）。
  - (e) Domain Reload 無効の Play で、前の Play が読んだ物が残る。→ 各観測は、それが process の最初の Play になる回から採る（S2-1 の移送）。
  - (f) 重複の報告が常に空でも通る。→ 報告に出た物は実体 2、出なかった物は実体 1 の両方向を見る（AC8）。
  - (g) 編集した asset を root として `Loadable` で引いて版を読む。探索の順で、上書きを後に登録していれば必ず編集後が出る。→ 版は、root が直接参照する物から読む（AC6）。
  - (h) prefab の版を読む値が、置いた側（Scene、外側の prefab）に書いてある。instance の override や外側の prefab の変更として v2 の値があると、上書きの build が内側の prefab を解決しなくても v2 に見える。→ v2 で変える source file を固定し、instance に override が無いことを build の前に record に残す（AC5 の前提 P2）。

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 渡された編集について、古い版を黙って残さない。** plan の selected の root のうち、手元の依存閉包に E（渡された編集の集合）の file を 1 つでも含む root が、上書きから外れることは無い。
  - 主張を崩しうる経路: (1) 閉包の出所。`IsContent` が落とす file（`.cs` 等、`Assets/` の外、`Packages/` の下）と、`GetDependencies` が返さない依存（shader の include 等の import 時の依存。未確認）は閉包に現れないので、E に入っても root に届かない。(2) plan と snapshot の組。SampleGame の `ComposeBootstrap` と `Bs4FixtureComposer` は selected に候補を足す。合成の後の plan と合成の前の snapshot を渡すと、足された root に閉包が無く、当たらないと判定される。(3) E の path の形。`.meta` だけの変更（import 設定）、`\` 区切り、大文字小文字だけ違う path（Windows の VCS が返しうる）、folder、削除済みの file、rename の前の path。(4) 弱い参照。logical key で引く先は別の root なので、その root 自身の閉包で判定される。host の asset が `Loadable` の field を持つ場合の閉包は未確認（この repository の製品 code で `Loadable` を持つのは `BuildContentRoot` だけ）。(5) 時点。snapshot を作った後に source が変わると、閉包が古くなる。
  - 塞ぎ方: (2) は、selected の root に閉包がちょうど 1 つ対応しない入力を例外にする。(3) は、区切りと `.meta` を正規化し、閉包との突き合わせで大文字小文字を無視する。(1) と (3) の残りは I-2 の「当たらなかった編集」に必ず出す。(5) は呼び手の前提として UC-1 に書いた（既存の build と同じ）。
  - 探索した境界: `Editor/Build/Materialization` の閉包の作り方、`Editor/Build/Content` の projection と adapter、SampleGame の合成 2 箇所。
- **I-2 渡された編集を黙って捨てない。** E の各 path は、「どの root を上書きに入れた理由になったか」か「当たらなかった理由」のどちらかとして、逆引きの結果に必ず現れる。build した時は preflight にも現れる。
  - 経路: I-1 の (1)(3) と、どの selected の root の閉包にも無い file。E の全部が当たらない時は build が走らず preflight が無い（§1 の「記録」の読みと呼び手の義務）。
- **I-3 上書きは、渡された plan の内側から出ない。** 上書きの root は plan の selected の部分集合で、logical key・stable key・physical key・表現は元と同じ。plan に無い root を足さず、除外済みの候補を戻さない。記録にある root と、実際に build する root は同じ集合である。
  - 経路: 絞る処理が snapshot の候補（除外済みを含む）から拾う実装、表現の tag を引き直す実装、絞った plan・snapshot・記録を別々の引数で渡せる request（呼び手が friend で plan に候補を足してから渡す、逆引きに使ったのと別の snapshot を渡す）。
  - 塞ぎ方: 上書きの request は、逆引きの結果 1 つを受けて、plan と snapshot の両方をそこから取る。plan と snapshot を別に受ける引数を持たない。
- **I-4 上書きを使わない build を変えず、部分 build を配らせない。** 上書きの情報を渡さない build の preflight・outcome は、追加の欄が既定値である以外は本スライスの前後で同じで、directory と publish の受理条件は変わらない。上書きの build と、selected を絞っただけの build は、どちらも publish できない。
  - 経路: `BuildContentRequest` の既存のコンストラクタ 3 つ、`ContentReportFile` を読む既存の読み手（判定 script、Artifacts の collect、Player build の検証）、`ContentTransportPublisher` の `buildMode` だけの検査（`Full` で作った上書きは今の検査を通る）、public な絞りで作った plan を記録なしで `Full` で build する呼び手、上書きの印を「base の identity が空でない」で読む実装（空の identity の記録が上書きでなく見える）。
  - 塞ぎ方: outcome に明示の印を書く。publisher は印に加えて、preflight の excluded に絞りの理由 code があれば拒む。base の identity の型は空の欄を例外で拒む。
- **I-5 上書きの build は base を変えない。** 上書きの build は、base の directory と、base の目的の workspace に書かない。
  - 経路: 上書きを base と同じ contentSet で build すると同じ workspace を使う。`content/<identity>` は build ごとに別名で、既存を上書きしない（coordinator の既存の検査）。
  - 塞ぎ方: 記録の base の contentSet と request の contentSet が同じなら、build の前に拒む。

**成立条件（起きなければならないこと）**

- **S-1** 絞った plan が `BuildContentProjection` を通り、製品の coordinator が上書きの directory を作る。その root の entry は、逆引きが選んだ root と一致する。
- **S-2** base → 上書きの順に登録した Editor の Play で、影響を受ける root が全て編集後の版で出る。影響の経路は、直接参照（Scene → material → texture）、Scene に置いた prefab instance、nested prefab、Object の root（prefab）の 4 つ。prefab の経路の証拠は、variant が無く、override が無く、unpack していない instance に限る。
- **S-3** 同じ Play で、上書きに入らなかった root は base の版で出る。渡していない手元の編集は、上書きに入った root からは手元の版で出る（C10）。
- **S-4** 重複の報告が実測と合う。報告に出た共有 asset は実体が 2 つ、base だけの root しか使わない asset は 1 つ（どちらも `Assets/` の下の asset について）。
- **S-5** build した時の preflight に、base の identity、E、root ごとの理由、当たらなかった編集と理由、重複する共有 asset が残り、outcome に上書きの印が残る。

### 2.2 受け入れ条件

fixture は SampleGame を使わない合成 1 つ（program D10）。custom の script を持たず、版は組み込みの値（texture の pixel、子の Transform の位置）で読む。

- root は 6 つ: `SceneDirect`（別々の material 経由で `EditedTex`・`SharedTex`・`UnlistedTex` を直接参照）、`SceneInstance`（`EditedPrefab` の instance を置く）、`SceneNested`（`InnerPrefab` を nest した `OuterPrefab` の instance を置く）、`PrefabRoot`（Object の root。`EditedTex` を直接参照）、`SceneClean`（`SharedTex` と `CleanOnlyTex` だけ）、`SceneUnlisted`（`UnlistedTex` と `CanaryTex` だけ）。どの root も参照しない `UnusedTex` を置く。
- v2 で変える source file は `EditedTex`・`EditedPrefab`・`InnerPrefab`・`UnlistedTex`・`CanaryTex` の 5 つだけ。prefab の v2 は、`EditedPrefab` と `InnerPrefab` の中の子（prefab の root ではない GameObject）の Transform の位置を変える。`SceneNested` の観測点は、`InnerPrefab` の下にだけある子の位置にする。
- E は `EditedTex`・`EditedPrefab`・`InnerPrefab`・`UnusedTex` の 4 つ。`UnlistedTex`（上書きの root と base だけの root の両方が参照する）と `CanaryTex`（base だけの root しか参照しない）は入れない。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | I-1・I-2 | 単体。表の入力（plan、閉包、E）に対し、上書きの root の集合、root ごとの理由（その root の閉包にある E の path。root 自身を含む）、当たらなかった編集（理由 code つき）を返す。path は `\` を `/` に直し、`X.meta` を `X` に読み替え、その後に同じになった path を 1 件にまとめる。理由 code は 2 つで、先に (1) を判定する: (1) `Assets/` の外（接頭辞は `IsContent` と同じ ordinal の `Assets/`）、(2) どの selected の root の閉包にも現れない。大文字小文字を無視するのは、(1) を通った後の閉包との突き合わせだけである。**失敗経路:** `.cs`、存在しない path、folder、未使用の asset は (2)、`Packages/` の下の path と `assets/foo.png` は (1) として出て、例外にも無言にもならない。`Assets/` の後ろの大文字小文字だけ違う path は当たる。`X.png` と `X.png.meta` の両方は 1 件になる。E が空、または当たる root が 0 の時は、plan を持たない「上書き不要」の結果を返し、当たらなかった編集はその結果に残る。selected の root に閉包がちょうど 1 つ対応しない入力は例外で拒む |
| AC2 | I-1 | 実機。fixture を v2 にした後、実 `AssetDatabase` から `AssetDependencySnapshotBuilder` で作った snapshot に E を当て、上書きの root が `SceneDirect`・`SceneInstance`・`SceneNested`・`PrefabRoot` の 4 つで、`SceneClean` と `SceneUnlisted` を含まない |
| AC3 | I-3・S-1 | 単体。plan の絞りは、selected を指定の部分集合にし、各候補の 3 つの key を変えず、元の除外を理由ごと残し、外れた候補を新しい理由 code の除外にする。絞った plan と元の snapshot の対が `BuildContentProjection` で issue 0。上書きの request は逆引きの結果から plan と snapshot を取り、別の plan や snapshot を渡す引数を持たない。**失敗経路:** selected に無い stable key の指定、空の集合の指定、「上書き不要」の結果を request に渡すことは、どれも `ArgumentException` で拒む |
| AC4 | I-4・I-5 | 単体。上書きの情報を渡さない request の preflight・outcome は、追加の欄が既定値（上書きでない印、空の配列）である以外は従来と同じで、既存の `BuildContentReportTests`・`BuildContentFullModeTests` が変更なしで通る。**失敗経路:** (1) 上書きの build の結果を `ContentTransportPublisher.Publish` に渡すと、publish 先を作る前に拒む。(2) outcome に印が無くても、preflight の excluded に絞りの理由 code があれば拒む。(3) 上書きの request の contentSet が base の contentSet と同じなら、report を書く前に `ArgumentException` で拒む。(4) base の identity の 4 欄のどれかが空なら、記録の型が例外で拒む |
| AC5 | S-1・I-5 | 実機。製品の coordinator で base（6 root、`Full`）を build して publish し、fixture を v2 にしてから上書き（AC2 の 4 root）を別の contentSet で build する。Play で上書きの directory から読んだ `BuildContentRoot` の entry の logical key が AC2 の 4 つと一致する。base の `content/<identity>` と base の workspace の全 file の名前と sha256 が、上書きの build の前後で同じ。**前提 P1:** 変えた 5 つの asset の source の観測値が、base の build の直前に v1、上書きの build の直前に v2。**前提 P2:** v1 と v2 の間で sha256 が変わった fixture の source file は上の 5 つだけ（Scene 6 つ、`OuterPrefab`、`PrefabRoot`、material は同じ）で、`SceneInstance` の `EditedPrefab` の instance、`SceneNested` の `OuterPrefab` の instance、`OuterPrefab` の中の `InnerPrefab` の instance に、既定のもの以外の override が無い。P1 と P2 は build の process が record に残し、成り立たない回は未判定 |
| AC6 | S-2 | 実機。fixture の source を消した Editor の Play で、base → 上書きの順に登録し、4 つの root を load する。logical key は §2.3 C5 の規則（上書きの entry を先）で引く。版は root が直接参照する物から読む: `SceneDirect` と `PrefabRoot` の Renderer の material の `EditedTex` の pixel、`SceneInstance` と `SceneNested` の instance の子の Transform の位置。全て v2。登録の開始から、上書き → base の解除の完了までの間、Error と Exception の log が 0 |
| AC7 | S-3 | AC6 と同じ Play。(1) `SceneClean` は載る。(2) canary: `SceneUnlisted` の `CanaryTex` は v1。(3) `UnlistedTex` は、`SceneDirect` の側が v2、`SceneUnlisted` の側が v1（§2.3 C10 の「同じ Play に 2 つの版」）。**読み分け:** (2) が v2 なら、観測が directory を読んでいないので全 AC を未判定にする。(2) が v1 で `SceneUnlisted` の側の `UnlistedTex` が v2 なら、base の直接参照が上書きの版へ解決しており、S-3 の不成立である。`SceneDirect` の側の `UnlistedTex` が v1 なら、C10 の不成立である。`SceneUnlisted` が上書きの entry にあれば、AC2・AC5 の不成立である |
| AC8 | S-4 | AC6 と同じ Play。`SceneDirect` と `SceneClean` を両方載せた時、`SharedTex` の実体は 2、`CleanOnlyTex` は 1（実体は相異なる `EntityId` で数える。S2-1 と同じ数え方）。重複の報告は、「上書きの root の閉包の和集合」と「上書きに入らなかった selected の root の閉包の和集合」の共通部分である。報告は `SharedTex` と `UnlistedTex` を含み、`CleanOnlyTex`・`CanaryTex`・`EditedTex` を含まない |
| AC9 | S-5・I-2 | 単体と実機の record。上書きの preflight の追加の欄に、渡した base の identity（harness では、publish した base の contentSet・revision・target と transport manifest の sha256）、E の 4 path、root ごとの理由（`SceneNested` の理由は `InnerPrefab`）、当たらなかった編集（`UnusedTex`、理由は (2)）、重複する共有 asset がある。outcome に上書きの印がある |
| AC10 | 範囲 | 差分が §3 の file と、その `.meta`（新しい folder の `.meta` を含む）だけで、`Scripts/Runtime/`、asmdef の `references`、`AssemblyInfo.cs` の friend に変更が無い。実機の test は `[Explicit]` で通常の回帰に入らない。build の process と各 round の process の前後で `git status` と Build Settings・active target・Enter Play Mode の設定が同じで、fixture が残らない |

AC6〜AC8 は、root を load する順だけが違う 2 つの process（Scene を先、Object を先）で採り、版の観測値、実体の数、entry の集合が同じになること。

### 2.3 登録契約（このスライスが定める文。Phase D で §13 へ移す）

各文の根拠を角括弧で示す。「禁止」は、測っていないので契約で塞ぐという意味で、後続が測れば解ける。「設計上の規則」と書いた文（C5）は実測ではないので、問いへの YES の根拠に数えない。

- **C1 順序:** base を先に、上書きを後に登録する。逆の順は登録しない。［S2-1 の実測。逆では base の版が出た。owner 決定 2026-11-14］
- **C2 個数:** 登録するのは base 1 つと、上書き 0 か 1 つ。編集が増えたら、和集合の E で上書きを作り直す。［上書き 2 つ以上は未測定のため禁止］
- **C3 時点:** 両方の登録を、最初の Scene と asset の load より前に終える。［load の後の登録は未測定のため禁止］
- **C4 同じ id の物:** 上書きの登録中は、上書きにある id の物の base の版を選べない。要求する側は、どちらの root から引いても上書きの版を得る。［S2-1 の実測。owner 決定 2026-11-14 で既定の制約として受け入れ］
- **C5 logical key の解決:** logical key は両方の root の entry から引く。同じ logical key が両方にあれば上書きの entry を採り、上書きに無ければ base の entry を採る。［設計上の規則。上書きにだけある root（手元で足した物）と、移動・rename で id が変わった Scene を、上書きの版に解決するために要る。id が同じ物は C4 のとおりどちらから引いても同じ（S2-1 の実測）。移動・rename した root の実測は S3］
- **C6 重ねてよい組み合わせ:** 上書きは、作った時に渡された base の identity と同じ base にだけ重ねる。target と宣言の名前も一致すること。合わなければ上書きを登録せず、作り直しを求める。［本スライスが記録を作る。照合の実装は S3］
- **C7 解除:** Play の終わりまで両方を保持する。解除の前に、どちらから載せた Scene と asset も全て離し、上書き → base の順に解除する。base を先に解除しない。Play の途中で上書きだけを解除・差し替えしない。［S2-1 の実測は Play の終わりの上書き → base の解除。S2-0b の実測では、後から登録した側の解除が、先に登録した側から `Loadable` で載せた物を Error なしに壊した。途中の解除と base を先に解除する順は未測定のため禁止］
- **C8 範囲:** 上書きは Editor の Play だけで使う。Player の構成に上書きは無く、リリースは 1 つの目的 build だけを載せる。［program §5.3「当面の方針」。Player は未測定］
- **C9 実体の重複:** 上書きの root と base の root の両方が直接参照する共有 asset は、実体が 2 つになる。状態を持つ asset（例: 参照の同一性で共有する ScriptableObject）は、上書きの Scene と base の Scene の間で別の実体になる。上書きの build は、`Assets/` の下の該当する asset の一覧を記録に出す。［実体の数は S2-1 と本スライスの実測。package の shader 等は一覧に出ない。GPU 側の資源の重複は未測定。状態を持つ asset の扱いは §2.8 U6］
- **C10 上書きの中身:** 上書きの root は、build の時の手元の版で作られる。E に無い手元の編集も、上書きの root が直接参照していれば編集後の版で入る。したがって、E が手元の編集の全部でない時、同じ Play に同じ asset の 2 つの版が出うる（上書きの root からは手元の版、base の root からは base の版）。［build の仕組み（adapter は root だけを渡す）と、本スライスの実測（AC7）］
- **C11 測った設定:** 上の実測は Domain Reload 無効の Play、同じ PC・同じ `Library` で作った base と上書きの範囲である。prefab の証拠は、variant が無く、override が無く、unpack していない instance に限る。［Domain Reload 有効は S3、別の PC の base は S4、prefab の残りの形は S5］

### 2.4 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 要求した Scene を含む目的 build を、BuildSystem が出す索引で選べるか | S2-3（program §8、§10 U6） |
| `AssetManagement` が base と上書きを持つ実装、C1・C3・C5・C6・C7 の強制、record の由来、Scene を load した後の登録、Play の途中の作り直し、Domain Reload 有効の Play、移動・rename した root の実測 | S3 |
| preflight と outcome の追加の欄を読む public な入口（C6 の照合が使う）。本スライスは欄の名前を §18 に書くまで | S3 |
| 別の PC・`Library` で build した base と手元の上書きの組み合わせ。配布と保存を hash でまとめる実装 | S4 |
| 編集した asset の集合の出所、menu、SampleGame の宣言と実データでの上書き（sprite atlas、audio、lightmap、URP `Lit` の variant の見た目を含む） | S5（program §9 G20） |
| prefab variant、override を持つ instance、Scene の中で unpack した prefab を編集した時に、上書きの版が出るか | S5（実データか、足りなければ合成 fixture で） |
| base の manifest の `sourceFiles` と手元の sha256 の差を、決め手ではなく「E の外にずれがあれば build しない」検査として使う案（§5.1 X-01） | S5 の A1（§2.8 U3 と一緒に owner が決める） |
| `GetDependencies` が返さない依存（shader の include、ShaderGraph の subgraph 等）と、それを base の `ContentLayout.json` の依存と突き合わせて洗う方法 | S5（実データで見る時） |
| 作り直すたびに増える上書きの `content/<identity>`・`reports/<identity>`・workspace を誰がいつ消すか | S5 |
| script の code や field を変えた時の base の Scene と prefab の読まれ方 | S3 の A1 が、測るか別の調査にするかを決める |
| Player での上書き | 持たない（C8）。要るなら owner が program を改める |

program が「S2 の A1」へ送った入力の引き取り: 複数 package の合成の可否（S1 から）は、目的 build 同士の合成を主にしない方針（program §5.3）の下で、base と上書きの組だけを本書が答える（plan の出所は同じ宣言の手元の plan、共有 asset は重複を許して報告、同じ id の衝突は C4・C5）。共有依存の置き場（S2-0b から）は、共有 package に分けず、C7 の寿命の結合と C9 の重複の受け入れで答える。prefab instance と nested prefab（S2-1 から）は AC6 で測る。上書きが 2 つ以上は C2、Player は C8 で契約として塞ぐ。sprite atlas・audio・`Lit` の variant・script の変更は上の表へ送る（理由は §2.8 U5）。

### 2.5 スライス分割の判断

- 契機: program の S2 の行は 3 つを持つ（目的 build を選ぶ索引、上書きの登録契約、逆引きの道具）。索引は「どの build を取るか」、残りの 2 つは「取った build に何を重ねるか」で、変更理由・触る file・証拠の種類が別である。索引は publish の出力（`ContentTransportPublisher`、transport manifest）と pure な選択で閉じ、実機の未知が無い。上書きの側は、製品の経路での部分 build と Play の実機の証拠が要り、独立に失敗しうる。
- 判断: **分割する**（2026-11-14 に owner が承認。§2.8 U2）。S2-2（本書）が上書きの build・逆引き・登録契約を、S2-3 が索引を持つ。互いの成果物を入力にしないので並行できる（逆引きは手元の plan だけで決まり、base の索引を読まない）。
- 本書の内側はこれ以上分けない。逆引き（pure）だけを先に出すと、Unity の依存の取り方と build の出力が合うこと（AC2・AC6）を確かめないまま契約を書くことになり、問いに答えられない。

### 2.6 判定定義と停止規則

- 判定定義:
  - **GO:** AC1〜AC10 が全て成り立ち、判定必須テスト（§5）が通る。
  - **NO-GO（owner へ返す）:** 前提 P1・P2 と canary（AC7 の (2)）が成り立っている回で、実機の観測が次のどれかに当たる。上書きの root の集合が期待と違う（AC2・AC5）。影響を受ける root に古い版が出る（AC6）。base の直接参照が上書きの版へ解決する、または上書きの root が渡していない編集を手元の版で持たない（AC7 の (3)）。実体の数が期待と違う（AC8）。どの条件と、逆引きの規則・上書きの形・契約のどれが崩れたかを記録して返す（program §8 の「不成立なら縮退判断を owner へ」）。
  - **未判定（B へ戻す）:** 前提 P1 か P2 が成り立たない。canary が v2。どちらも fixture か harness の不備である。
  - **未判定（owner へ返す）:** 2 つの process の値が食い違った。
  - **上のどれにも当たらない不成立**（単体の AC1・AC3・AC4・AC9、範囲の AC10、全 EditMode 回帰）は、実装の欠陥として通常の差し戻しで B へ戻す。
  - スパイクではないので CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の `references` と `InternalsVisibleTo` を足さない（足す必要が出たら Phase A へ戻す）。
- 新規・編集する Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。Editor の code を Runtime の assembly に置かない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する（harness の観測を含む）。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない。
- repo 相対 path は `tools/path-budget.json` の予算（150 文字）に収める。
- coordinator の出力先は `artifacts/bs2b` か `TestResults/bs3-fixture` の下だけ。harness は後者を使う。
- build の target は StandaloneWindows64 の Player 固定（`UnityContentDirectoryAdapter.ValidateTarget`）。
- 上書きの build の mode は `Full` に固定する。`Incremental` は前の上書きの file を directory に残し、それが load に効くかは未測定である。
- plan の絞りと、その除外の理由 code の名前は、上書きを知らない語にする（Selection は上書きの語彙を持たない）。
- 機械向けの記録（record、layout の写し、期待値）を git に入れない。

### 2.8 owner の決定と未決事項

A1 の終わりに担当が推奨を付けて owner へ出した U1〜U8 は、**2026-11-14 に owner が全て推奨どおり決めた。** 以下は決まった内容である。U3 と U4 は、A2 の指摘で説明の誤りを直した（決定そのものは変えていない。§5.1 G-03・G-14）。

- **U1 目的と利用者の文言。** §1 の 2 欄を承認した。
- **U2 分割。** §2.5 のとおり S2 を S2-2 と S2-3 に分ける。
- **U3 編集した asset の集合を誰が決めるか。** 集合は引数で受けるだけにする（出所は S5）。機械が base の manifest の `sourceFiles` と手元の sha256 の差から決める案は、利用者の入力が 0 になり、渡し忘れによる漏れも無くなるが、program D4（sha 差分で適用対象を決めない）と当たり、手元の checkout が base より古いだけの file も上書きに入る。差分から決めるかは S5 の A1 で owner が決める。本書の保証は「渡された集合に対して漏れない」までである。E が手元の編集の全部でない時に何が起きるかは C10 に書いた（訂正: 初稿は「E に入れなかった編集は base の版のまま出る」と書いたが、上書きに入った root からは編集後の版が出る）。
- **U4 逆引きの対象。** program の語は「上書きに入れる Scene」だが、plan の selected の root 全部（Object の root を含む）を対象にし、AC6 の `PrefabRoot` で測る。base だけにある Object の root が編集した asset を直接参照する場合も、base の版を読むと推測される（S2-1 が測ったのは base だけにある Scene と、上書きに入れた Object の root。訂正: 初稿は「Object の root は未測定」と広く書いた）。
- **U5 測る・送るの仕分け。** 測る: prefab instance、nested prefab（逆引きの合否に直結する。variant・override・unpack の無い形に限る）。送る: sprite atlas・audio・lightmap（build の出力の形が別で、合成 fixture を増やすより S5 の実データで見る方が安い）、URP `Lit` の variant（構造の観測では判定できず描画の証拠が要る。上書きの Scene は自分の directory の中で shader を直接参照するので、directory をまたぐ解決は起きない見込み）、script の変更（base と上書きの build の間に compile を挟む多 process の harness が要る）。契約で塞ぐ: 上書き 2 つ以上、Player。
- **U6 状態を持つ共有 asset の重複。** C9 のとおり、上書きの Scene と base の Scene が同じ ScriptableObject を直接参照すると、実体が別になる。program の方針「開発では重複を許す」は texture 等の量の話として決められており、同一性に依る共有が切れることは明示されていなかった。本書は一覧を記録に出すだけにし、止める・広げる（その asset を使う root を全て上書きに入れる）の規則は足さない。実害が出る host が現れた時に、記録の一覧を材料に後続で決める。
- **U7 実機の harness の扱い。** S2-0b・S2-1 と同じく Phase D で消して git 履歴に残す。常設するのは単体テストだけ（製品の逆引きと記録の回帰はそれで閉じる）。
- **U8 上書きと base の結びつけ（C6）。** 上書きの記録に base の identity を必須で持たせる。無いと、base を取り直した後の古い上書きが黙って重なる。

- **U9 問いの文。** 外部レビュー R-02 のとおり、初稿の問いの後半「古い版が混ざらない」は、C10 と AC7 が要求する観測（同じ Play に 2 つの版）と同時に YES と言えなかった。§2 の問いと program §8 の S2-2 の行を、C10 と同じ意味の文に改めた。目的と利用者の欄は変えていない。**2026-11-14 に owner がこの文を承認した**（凍結の指示ではない）。

未決事項: なし。

## 3. 責務マップ

製品の変更は `Editor/Build` の 2 つの assembly に閉じる。`Scripts/Runtime/` は変えない。path は `unity/Assets/OneStarMaker/` からの相対。

| file | 新規 / 変更 | 責務と変更理由 | 所有・寿命、依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---:|
| `Scripts/Editor/Build/Selection/Results/BuildPlan.cs` | 変更 | selected の一部だけを残した plan を返す method を足す。外れた候補は新しい理由の除外にする。plan を外から作り直す public な道が無いため | immutable な値。依存なし（`noEngineReferences`） | public method 1 つ | Unity なしの単体 | 42 → 約 75 |
| `Scripts/Editor/Build/Selection/Results/BuildExclusionReasonCode.cs` | 変更 | 除外の理由を 1 つ足す（末尾。絞りで外れた） | — | enum の値 1 つ | 同上 | 38 → 約 41 |
| `Scripts/Editor/Build/Selection/AssemblyInfo.cs` | 変更（コメントだけ） | 「本番の plan 生成経路や公開 API は増やさない」という既存のコメントが、上の method で事実と合わなくなるので直す。friend は足さない | — | なし | — | 10 → 約 10 |
| `Scripts/Editor/Build/Content/ContentOverlaySelection.cs` | 新規 | 逆引きとその結果。plan・snapshot・E を受け、絞った plan、使った snapshot、root ごとの理由、当たらなかった編集と理由 code、重複する共有 asset を返す。base の identity の型と、request に載せる記録の型もここに置く。policy だけで I/O を持たない | 呼び出しごとの immutable な結果。依存は Selection と Materialization の公開の型 | public: 逆引きの入口、結果、root ごとの理由、当たらなかった編集、理由の enum（2 値）、base の identity | Unity と `AssetDatabase` なしの単体 | 約 280 |
| `Scripts/Editor/Build/Content/BuildContentReport.cs` | 変更 | `BuildContentRequest` に、逆引きの結果と base の identity を受けるコンストラクタを足す（plan と snapshot は結果から取り、mode は `Full`）。preflight に上書きの詳細、outcome に上書きの印を書く | request の寿命。既存の読み手は未知の欄を無視する | コンストラクタ 1 つと property 1 つ | 単体（JSON の形） | 174 → 約 225 |
| `Scripts/Editor/Build/Content/BuildContentCoordinator.cs` | 変更 | 上書きの記録を report へ渡し、base と同じ contentSet の上書きを build の前に拒む。順序と出力先の規則は変えない | — | なし | 既存の単体と追加 | 188 → 約 200 |
| `Scripts/Editor/Build/Content/ContentTransportPublisher.cs` | 変更 | outcome の上書きの印か、preflight の excluded の絞りの理由 code があれば、publish を拒む | — | なし | 単体（失敗経路） | 143 → 約 158 |
| `Tests/Editor/Build/ContentOverlaySelectionTests.cs` | 新規 | AC1・AC3・AC9 の単体 | — | — | 通常の EditMode 回帰 | 約 380 |
| `Tests/Editor/Build/BuildContentReportTests.cs`・`BuildContentFullModeTests.cs` | 変更 | AC4 の追加（既存の test は変えない） | — | — | 同上 | 各 +約 50 |
| `Tests/Editor/Spikes/OverlayBuild/OverlayBuildFixture.cs` | 新規、Phase D で削除（§2.8 U7） | fixture の生成（固定 GUID、v1 と v2）、所有の marker、残りの回収と削除、前提 P1・P2 の材料の採取 | `[Explicit]` の test から呼ぶ。`TestResults/` の下と、marker を持つ fixture の folder だけに書く | — | Unity / `AssetDatabase` の I/O | 約 380 |
| `Tests/Editor/Spikes/OverlayBuild/OverlayBuildProcess.cs` | 同上 | `[Explicit]` の test `Build`。base の build と publish、v2、逆引き、上書きの build、record、fixture の source の削除 | 同上 | — | Unity の build の I/O | 約 300 |
| `Tests/Editor/Spikes/OverlayBuild/OverlayBuildRounds.cs` | 同上 | `[Explicit]` の `[UnityTest]` 2 つ（`ScenesFirst`・`ObjectFirst`）。登録、load、観測、解除、log の捕捉、record | 同上 | — | Play を伴う orchestration。判断は下の写像に置く | 約 380 |
| `Tests/Editor/Spikes/OverlayBuild/OverlayBuildVerdict.cs`・`OverlayBuildVerdictTests.cs` | 同上 | 観測（文字列・数値・真偽）から、AC ごとの成否と §2.6 の分類を返す pure な写像と、その単体。S2-1 の `OverlayVerdict` の「対照の順は上書きの版が出る前提」は持ってこない | — | — | 写像の単体は通常の EditMode 回帰 | 計 約 300 |

- 行数の合計: 製品は約 400 行（新規の `ContentOverlaySelection.cs` が約 280、既存 file への追加が約 115）、常設の単体テストが約 480 行、Phase D で消す harness が約 1,350 行。harness は S2-1 の harness（commit `a304963b`）から fixture の固定 GUID と marker、record、round の枠、log の捕捉、実体の数え方を写して縮める。
- 行数の警報: `BuildPlan.cs` は 50% を超えて増える。分けない。足すのは plan 自身の不変条件（selected と excluded が候補を過不足なく分ける）を保ったまま部分集合を作る 1 つの操作で、plan の外に置くと internal のコンストラクタを開く必要がある。
- plan を絞る道を public な method にした理由（§5.1 G-01）: friend を足すと、Selection の internal の全部（不正な plan も作れるコンストラクタ）を Content に開き、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の後続が目指す「`InternalsVisibleTo` を使わない」と逆を向く。plan を絞らずに request の側で root だけ減らす形は、preflight の `selected` を build した root として読む既存の読み手と食い違う。
- 上書きの記録を preflight と outcome の追加の欄にした理由（§5.1 G-07）: 別の file にすると「file が無い = 上書きでない」と読まれ、欠けた時に publish が通る。publisher は既に outcome を正として読み、無ければ拒む。preflight は失敗した build でも残る。
- 逆引きを `Editor/Build/Content` に置く理由: 結果を渡す先（`BuildContentRequest`）が同じ assembly にある。新しい folder と asmdef は作らない（呼び手が 1 つの段階で層を先に作らない）。
- `ContentOverlaySelection.cs` を分けない理由: E の path の正規化、逆引き、重複の報告は、同じ閉包を 1 回読んで出る 1 つの結果で、依存もテスト方法も同じである。§2.8 U6 の後続で重複に規則が足される時に、その規則を別の file に出す。
- 逆引きと build を束ねる入口の class は作らない。呼び手は「逆引き → 上書き不要なら終わり → 結果を載せた request を既存の coordinator へ」の 2 手を自分で呼ぶ。plan・snapshot・記録が食い違う道は、request が結果 1 つを受ける形で塞ぐ（I-3）。
- 中核の単体テスト: 逆引きは、test が friend として作る plan と snapshot（既存の `BuildContentProjectionTests` と同じ作り方）だけで検証できる。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序: (1) `BuildPlan` の絞りと理由 code、その単体。(2) `ContentOverlaySelection` と単体（AC1・AC3）。(3) request・report・coordinator・publisher の追加と単体（AC4・AC9）。(4) harness（fixture、build の process、round、写像）。(1)〜(3) は Unity の build なしで閉じ、Editor の compile 確認までを Phase B が行う。(4) の実行は Phase C。
- Phase B から Phase A へ差し戻す条件: `Scripts/Runtime/` の変更が要る。asmdef の `references` か friend の追加が要る。絞った plan が `BuildContentProjection` を通らず、snapshot の側も作り直す必要がある。上書きの root に、元の plan に無い候補を足す必要が出た。理由 code を 2 つより増やさないと I-2 を満たせない（存在の判定などの I/O が要る）。fixture の形（6 root、5 つの変更、override の無い instance）では AC2 の集合か前提 P2 が作れない。
- 対象外を維持する方法: AC10 の差分の検査。menu と SampleGame の file を足さない。`BuildContentRoot` の serialized な形を変えない。

## 5. テストとレビュー計画

- 単体テスト: `ContentOverlaySelectionTests`、`BuildContentReportTests`・`BuildContentFullModeTests` の追加分、`OverlayBuildVerdictTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Build`（C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）。それぞれ別の Unity process で、この順に実行する:
  1. `pwsh tools/run-tests.ps1`（空 filter の全 EditMode 回帰）。合格は XML の failed 0 と root result `Passed` で読む（`step.json` は `-nographics` の VFX 25 件が Inconclusive になり常に `failed` なので使わない）。他の Unity を止めてから実行する（実 build を伴う既存の統合 test は、他の Unity が動いていると失敗した実績がある。program §8「BuildSystem 側の入力」）。
  2. `-Filter OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlayBuildProcess.Build`。base を build して publish し、fixture を v2 にして上書きを build し、record を書き、fixture の source を消す。
  3. `-Filter OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlayBuildRounds.ScenesFirst` と、同 `.ObjectFirst` を、別々の process で。どちらも 2 の出力を読む。
  - 2 と 3 は、各 XML の total が 1 以上で failed 0、record に終了行があることを「harness が最後まで動いた」確認とし、その後に判定 C が record を AC ごとに読む。**process の自然終了をゲートにしない**（XML の完成後に Unity が終わらない既知の終了 stall は `tools/Harness/README.md` の規則に従う）。Phase B が test の名前を変えた時は、§6 に対応を書く。
- record が必ず持つ値（形式は Phase B が決めてよい。file は git に入れない）:
  - build の process: head、Unity の版、target、Enter Play Mode の設定。fixture の source file ごとの sha256（v1 と v2）。前提 P1 の観測値（5 つの asset、base の build の直前と上書きの build の直前）。前提 P2 の override の有無（3 つの instance）。逆引きの結果（root、理由、当たらなかった編集、重複）。base と上書きの identity、preflight と outcome の写し。base の `content/<identity>` と workspace の file の名前と sha256（上書きの build の前後）。終了行。
  - round の process: 登録の順と結果、両方の root の entry の logical key、root ごとの観測値（AC6・AC7 の各点）、実体の数（AC8）、発生順の log の全件（種類を問わない）、解除の順と結果、終了行。
- 全 EditMode 回帰の適用除外: なし（製品の code を変える）。
- 統合・Unity テスト: 上の 2 と 3。
- 発見 C の限定実行: 発見 C は、harness が最後まで動くことの確認として、上の 2 と 3 を各 1 回、理由と範囲を記録して実行してよい（Phase B は Unity の build と Play を実行できないので、harness の不備が判定 C で初めて見つかるのを避ける。S2-1 では判定 C からの差し戻しが起きた）。この実行の結果は判定に使わない。
- 操作・実行時・目視条件の検証経路: 全て上の Unity バッチで閉じる。担当は Phase C の実行担当で、sandbox 外の承認済み経路で起動する。初期状態は、Editor を閉じ、active build target が StandaloneWindows64 の Player、Enter Play Mode の Domain Reload が無効（repository の設定のまま）、作業ツリーが clean。観測は record・XML・log で、目視は無い。証拠は record・XML・log・preflight と outcome の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通結果:
  - 成立済み（S2-0b・S2-1）: EditMode の `[UnityTest]` から Play に入り、製品の `UnityContentDirectoryBackend` を 2 つ持って 2 つの directory を登録する経路。source を消した Editor の Play で directory の物を読む経路。
  - 成立済み（既存の製品の経路）: 除外を持つ plan を coordinator に渡す build（宣言からの build は除外を持つのが通常の形）。`Full` の build の publish（既存の `BuildContentFullModeTests`）。
  - **凍結前の事実確認（2026-11-14 に実施、Unity 6000.6.0f1 の Editor、主担当）:** `AssetDatabase.GetDependencies(path, true)` は、prefab instance を置いた Scene に対してその prefab の asset を返し、その prefab が nest する内側の prefab も返した。外側の prefab に対しても内側の prefab を返した。再帰なし（`false`）では、Scene から内側の prefab は返らない（製品の `UnityAssetDatabaseGateway` は `true` で呼ぶ）。確かめ方: 一時 folder に、空の `Inner.prefab`、それを nest した `Outer.prefab`、`Outer` の instance を 1 つ置いた Scene を Editor の API で作り、問い合わせの後に folder ごと消した（作業ツリーに残りが無いことを `git status` で確かめた）。**既存の asset では確かめられなかった:** この repository の `unity/Assets` の Scene（668 件）と prefab には、source prefab を持つ instance が 1 つも無い。したがって prefab instance と nested prefab の実測は、本スライスの合成 fixture が初めてになる。範囲: 空の GameObject だけの prefab で、prefab variant、override を持つ instance、Scene の中で unpack した物は確かめていない（§2.4 で S5 へ送った）。
  - 未確認。初回確認は Phase C: prefab instance と nested prefab を置いた Scene を製品の coordinator で build し、Play で load できること（理由: Unity の build と Play は Phase C の責任。確認地点: 上の発見 C の限定実行と、判定必須の 2 と 3。不成立時: 基盤不備なら B、fixture の形で作れないなら Phase A）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（build 2 回、Unity の起動は 4 回の見込み）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。`OverlayBuildVerdict` の出力は補助とする。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当: 本 session（Claude、Anthropic）。A0 は program §5.3「当面の方針」、§8 の S2 の行と移送、事実の文書を入力とした。
- A2 独立レビュー（実施 2026-11-14。互いの指摘を見せていない。(1)〜(3) は全て Anthropic の系列）:
  - (1) 目的レビュー: 本 session が起動した新しい subagent（Claude Opus、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `526a59d2a2504c83e9304d60e5e96fda96f545b5c8aa1e32435af77c6b94cba4`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。
  - (2) A0 だけからの代替構成: (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は program §5.3・§6・§8・§9 の G10 / G11、事実の文書、製品の `Editor/Build` と `Runtime/BuildContent` の source。本書は読んでいない。
  - (3) アーキテクチャゲート兼、各不変条件を破る順序・読み違い・誤って YES を出す道の探索: 本 session が起動した別の新しい subagent（Claude Fable、Anthropic）。入力は A2 入力版の本書（内容 sha256 `78d175dc237e3eba28ac4e14390138d69f43b738b7383fd45e13a5f958392cd3`、commit `cf31a6f2`）と、アーキテクチャゲート、事実の文書、program、製品の source、asmdef、既存の test、AGENTS.md。枠は「計画が書いている code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。
  - (4) 凍結前の外部レビュー（draft PR #186、2026-11-14、1 巡目）: cursor の background agent（Grok、xAI。A2 とは別ベンダー）。対象は commit `288cf7bd` の本書。観点は PR 本文の 4 つ（誤って YES を出す道、判定の形、範囲、規模）。Unity は起動していない。
- A3 統合担当・採否: 本 session（§5.1）。**未凍結。** 目的と利用者の欄は A1 の後に変えていない（目的レビューのやり直しは無い）。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー。凍結前の外部レビューが Grok 系を使ったので、C' に Grok 系を使う時は、Phase A に関与した系列として独立性の制約を §8 に記録する。
- 独立性の強化条件を満たせない場合の理由: A2 の (1)〜(3) は 1 つのベンダーの 2 つのモデルで行った（A1 の主担当は (3) と同じモデルの別 session）。別ベンダーの目は (4) で入れた。C' の充足は起こす時に確かめて記録する。

### 5.1 A2 の独立レビューと A3 の採否

**(1) 目的レビュー**（「何を保証すべきか」11 件、「何を作らないべきか」9 件）

| ID | 回答 | 採否と反映 |
|---|---|---|
| P-01 | 影響を受ける物を漏れなく入れる、渡した編集は反映か不反映（理由つき）に必ず分かれる、製品と同じ build 経路、base の版との結びつけ、契約の明示、実測した範囲と確かめていない範囲の分離 | 初稿にあった（I-1・I-2・S-1・C6・§2.3 の角括弧） |
| P-02 | 影響を受けない物は base から読み、不要な物を上書きに入れない | 初稿にあった（AC2・AC5・AC7）。S-3 の語を G-03 で直した |
| P-03 | 上書きと base の間で参照が切れない | **採用。** logical key の解決の規則を C5 として足した。上書きにだけある root の load は 1 つの directory の中の load で、実機の fixture には足さない（単体の AC1 が「root 自身が E にある」を見る） |
| P-04 | 不反映の理由は、文ではなく種別で読める | **採用。** 理由を code（enum、2 値）にした（AC1、§3） |
| P-05 | 上書きの build が base を書き換えない・汚さない | **採用。** I-5 を足し、同じ contentSet の拒否（AC4）と base の directory と workspace の sha256 の前後一致（AC5）で見る |
| P-06 | 契約が禁じる組み合わせを検出できる | 一部採用。本書は C6 の照合の材料（記録）までを作る。強制は S3（§2.4） |
| P-07 | 作らない物: 開発者の入口、影響範囲を指定させる入力、編集の自動検出、別の build 経路、Runtime の登録の実装、多段の上書き、推測での補正、base の取得、Player への一般化 | 初稿と一致。初稿が作る物のうち、要らないと指摘された物は無い |

**(2) A0 だけからの代替構成**

| ID | 提案 | 採否と理由 |
|---|---|---|
| X-01 | base の `sourceFiles` と手元の sha256 を比べ、E の外にずれがあれば build しない | 不採用。U3 の owner の決定の範囲である。checkout が base と違う revision の時は常に止まり、差分が事実上の決め手になる。S5 の A1 の入力として送った（§2.4） |
| X-02 | 上書きの記録を別の file（`overlay.json`）に置く | 不採用。G-07 の理由（欠けた時に publish が通る） |
| X-03 | Selection に internal の factory を置き、Content を friend にする | 不採用。G-01 の理由 |
| X-04 | 逆引き → 縮小 → build を束ねる public な入口 | 一部採用。入口の class は作らないが、request が逆引きの結果 1 つから plan と snapshot を取る形にした（I-3） |
| X-05 | 上書きの build は `Full` | **採用**（§2.7、G-08） |
| X-06 | 期待は OSM の閉包と別の出所から持つ（作者の topology、base の `ContentLayout.json` の依存の逆引き） | 一部採用。作者の topology は AC2 が持つ。layout との突き合わせは判定に要らないので足さず、`GetDependencies` が返さない依存を洗う手段として S5 へ送った（§2.4） |
| X-07 | 誤って合格にする道: 編集した asset を `Loadable` だけで見る、名前で版を見分ける | **採用。** §2 の (g) と AC6（root が直接参照する物から読む）。prefab の版は子の名前ではなく Transform の位置で読む |
| X-08 | 契約に、解除の前に載せた物を全て離すことを入れる。base を先に解除する順と GPU 側の資源は実測なし | **採用**（C7・C9） |
| X-09 | SampleGame の入口を本スライスの最後に足す | 不採用。program D10 と §9 G20 のとおり S5 |

**(3) アーキテクチャゲートと反証の探索**（15 件。高 3、中 8、低 4）

| ID | 重要度 | 指摘 | 採否と反映 |
|---|---|---|---|
| G-01 | 高 | plan を絞る道は public な method が良い。ただし絞った plan と記録を別々に渡せると、記録に無い root が上書きに入る（I-3）。public な絞りで作った plan を記録なしで `Full` で build すると、root の欠けた directory が publish を通る（I-4） | **採用。** request は逆引きの結果 1 つを受ける。publisher は preflight の excluded の理由 code でも拒む。名前は上書きを知らない語にする（I-3・I-4、AC3・AC4、§2.7、§3） |
| G-02 | 高 | 「存在しない path」「folder」「どの閉包にも無い asset」は、I/O なしでは区別できない。`.cs` の判定の正本 `IsContent` は Materialization の internal | **採用。** 理由 code を、文字列と閉包だけで決まる 2 つに減らした。`IsContent` を複製しない（AC1、§4 の差し戻し条件） |
| G-03 | 高 | 「E に入れなかった編集は base の版のまま出る」は一般には成り立たない。上書きに入った root は、E に無い手元の編集も編集後の版で含む | **採用。** S-3・AC7・U3 の語を直し、C10 を足した。fixture の `SceneDirect` に `UnlistedTex` を持たせ、同じ Play に 2 つの版が出ることを実測する |
| G-04 | 中 | I-1 を破る順序: 大文字小文字だけ違う path、合成の後の plan と合成の前の snapshot、import 時の依存 | **採用。** 大文字小文字を無視、閉包の無い root は例外（I-1、AC1）。import 時の依存は S5 へ送った（§2.4） |
| G-05 | 中 | E が全部当たらないと build が走らず、編集と理由がどの記録にも残らない | **採用。** 目的の「記録」の読みを §1 で固定し、UC-1 に呼び手の義務を書いた。writer は足さない |
| G-06 | 中 | 同じ contentSet で上書きを build すると base の workspace に出力が残る。印を「identity が空でない」で読むと空の記録が通る | **採用**（I-5、I-4、AC4） |
| G-07 | 中 | 詳細は preflight、publish を拒む印は outcome の欄。別の file にしない。S3 が読む入口の所有を書く | **採用**（§3、§2.4、harvest 先） |
| G-08 | 中 | harness は空の workspace で測るが、実際の作り直しでは前の上書きの file が directory に残る | **採用。** 上書きの build を `Full` に固定した（§2.7、§3） |
| G-09 | 中 | Scene の id は path から作られる。移動・rename した Scene は base の root の entry から引くと base の版が出る見込み | **採用。** C5 の規則を足し、移動と rename は対象外にして実測を S3 へ送った（§1、§2.4） |
| G-10 | 中 | AC7 と判定定義が、同じ観測に別の判定を出す | **採用。** AC7 に読み分けを書き、判定定義に未判定と戻し先を書いた（§2.6。R-03 でさらに分けた） |
| G-11 | 中 | AC9 が「当たらなかった編集」を見ない。publish していない base に manifest の digest が無い | **採用。** E に `UnusedTex` を足し、harness は base を publish する（AC5・AC9） |
| G-12 | 低 | log の窓、理由の優先、`.meta` の重複、空の集合の絞り、I-4 と AC4 の語、AC10 の範囲、AC2 の時点 | **採用**（AC1・AC2・AC3・AC6・AC10、I-4） |
| G-13 | 低 | base が既に編集後の source から作られていると、上書きが効かなくても通る | **採用。** 前提 P1 を build の直前の観測に強めた（AC5、§2 の (d)） |
| G-14 | 低 | 既存の前提の誤り: 「閉包と plan の両方を読むのは Content だけ」、Selection の `AssemblyInfo.cs` のコメント、U4 の語、重複の報告は `Assets/` の下だけ、初回なのは除外を持つ plan ではなく prefab instance の build | **採用**（§3、U4、S-4・C9、§5 の未確認の欄） |
| G-15 | 低 | `ContentOverlaySelection` を分けない理由、増える成果物の清掃の所有、公開の型の数え上げ、記録の型の置き場 | **採用**（§3、§2.4） |

**(4) 凍結前の外部レビュー（1 巡目、対象 `288cf7bd`）**（6 件。高 2、中 2、低 2。範囲と規模は「過不足なし」）

| ID | 重要度 | 指摘 | 採否と反映 |
|---|---|---|---|
| R-01 | 高 | prefab の版を読む Transform の位置が Scene や外側の prefab に書いてあると、上書きの build が内側の prefab を解決しなくても v2 に見える。entry の名前の一致では埋まらない | **採用。** §2 の (h) と前提 P2 を足した。v2 で変える source file を固定し、instance に override が無いことを build の前に record に残す。`SceneNested` の観測点は `InnerPrefab` の下にだけある子にした（§2.2、AC5、§2.6） |
| R-02 | 高 | 問いの「古い版が混ざらない」は、AC7 と C10 が要求する観測（同じ Play に 2 つの版）と同時に YES と言えない。program §8 の行も同じ | **採用。** §2 の問いと program §8 の S2-2 の行を、C10 と同じ意味の同じ文に改めた。owner が 2026-11-14 に承認した（§2.8 U9） |
| R-03 | 中 | 判定の 3 分類に入らない観測がある: 上書きの root の直接参照だけが古い、base だけの root が v2（directory を読んでいないのか、base の参照が上書きへ解決したのか分からない）、実体の数の違い。単体の不成立の戻し先も無い | **採用。** canary（`CanaryTex`）を、両方が参照する `UnlistedTex` と分けた。AC7 の読み分けと §2.6 の分類（NO-GO、未判定の 2 種、通常の差し戻し）を書き直した |
| R-04 | 中 | variant、override、unpack が未確認のまま、対象外にも送り先にも無い。C5 は設計上の規則なので、YES の根拠に数えないと書く | **採用**（§1 の対象外、S-2、C11、§2.4、§2.3 の前置き、U5） |
| R-05 | 低 | AC1 の「`Assets/` の外」と「大文字小文字だけ違う path は当たる」が、`assets/foo.png` で割れる | **採用。** 接頭辞は ordinal、大小の無視は閉包との突き合わせだけ、と AC1 に書いた |
| R-06 | 低 | §1 の friend の列挙が `SampleGame.Tests.Editor` を落としているように読める | **採用。** 3 つを名前で書いた（§1 の現況） |
| R-07 | — | 規模: §3 を足すと製品は約 400 行で、PR 本文の「約 200 行」と合わない。S2-1 の `OverlayVerdict` の「対照の順は上書きの版が出る前提」は写さない | **採用。** §3 に合計を書き、PR 本文を直す。写さないことを §3 の表に書いた |

**(5) 主担当の見直し（Phase C と C' で読みが割れそうな箇所。2026-11-14）**

| ID | 見つけた点 | 反映 |
|---|---|---|
| S-01 | request が plan だけを結果から取ると、逆引きに使ったのと別の snapshot を渡せて、記録の理由と build の入力が食い違う | request は plan と snapshot の両方を結果から取る（I-3、AC3、§3） |
| S-02 | 逆引きの判定は snapshot の時点の閉包で、build までに source が変わると古くなる。誰の責任かが無かった | I-1 の経路 (5) と UC-1 の呼び手の義務に書いた |
| S-03 | AC6 が、logical key をどちらの root の entry から引くかを言っていなかった | C5 の規則で引くと書いた（AC6） |
| S-04 | I-5 の「workspace に書かない」を見る実機の観測が無かった。同じ contentSet と「上書き不要」の結果の拒み方（例外か、失敗の result か）が無かった | AC5 に workspace の前後一致、AC3・AC4 に `ArgumentException` を書いた |
| S-05 | 重複の報告の定義が文に無く、実装と照合できなかった。`UnlistedTex` も報告に出る | AC8 に定義と期待を書いた |
| S-06 | harness の file を「4 file」とだけ書き、`.meta` と新しい folder の `.meta` が AC10 の範囲に入るかが割れた。test の名前が凍結されていなかった | §3 に file を名前で書き、AC10 に `.meta` を入れ、§5 に test の完全名を書いた |
| S-07 | record が何を持てば AC を読めるかが無かった。全回帰は他の Unity が動いていると失敗した実績がある。harness の不備が判定 C で初めて見つかる | §5 に record が必ず持つ値、他の Unity を止めること、発見 C の限定実行を書いた |

保留: なし。確認できなかった推測として残るもの: import 時の依存を `GetDependencies` が返さないこと（G-04）、前の上書きの file が load に効くか（G-08）、移動した Scene の id（G-09）、override を持つ instance と variant の読まれ方（R-04）。どれも本書では契約か対象外で塞ぎ、実測は後続へ送った。

## 6. Phase B 実装結果

未着手

### 動作確認

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
