# 上書きの build と登録契約（S2-2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2。program の S2 を §2.5 の判断で S2-2（本書）と S2-3（目的 build の索引、A1 未着手）に分けた）
- status: `A`（**A2 入力版。** 2026-11-14 に owner が §1 の目的・利用者の文言と §2.8 の U1〜U8 を担当の推奨どおり承認した。A2 は未実施、未凍結）
- branch: `claude/content-overlay-build`（PR の base は `develop`）
- implementation base commit: `66564116`（`develop` の head、2026-11-14。§1 の現況はこの base）
- implementation head commit: `未固定`
- risk: `high`（公開 API の追加、製品の build 経路で root の一部だけを build する初回の実機）
- owner: root（依頼者）
- created: 2026-11-14
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4（上書きの build と逆引き）、[13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「Unity 6.6 Content Directories の実証済み境界」（§2.3 の登録契約）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（製品の経路で作った上書きの実測）、program §5.3・§8
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§5.3「当面の方針」、§8 の S2 の行と S2-0〜S2-1 の移送、§9 G10 / G11）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本。X6）。本スライスは `Scripts/Runtime/` を変えないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない制約の対象外
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2 の独立レビュー担当（3 件。§5）。
  - 次の作業: この版を入力に、目的レビュー、A0 だけからの代替構成、アーキテクチャゲート兼不変条件を破る順序の探索を、互いの指摘を見せずに行う。主担当が A3 で採否を統合する。
  - 引き継ぐ commit: この票を含む commit（A2 入力版。SHA は tree の外で渡す）。
  - 必要な権限: なし（文書と source の読み取りだけ）。
  - 停止規則: 目的・利用者の文言を A2 の後に変えたら、目的レビューをやり直す。owner の指示があるまで凍結しない。
  - 引き渡さないもの: レビュー担当どうしの指摘。目的レビューには §1 の 2 欄の原文以外を渡さない。

## 1. 目的と対象外

- 目的: 取得した目的 build を base にして Editor で Play する時に、手元で編集した asset の集合を渡すだけで、その編集の影響を受ける Scene と asset の全部を編集後の版で読ませる「上書き」の Content Directory を、製品の build 経路で作れるようにする。渡した編集が上書きに反映されなかった場合は、その編集と理由が記録から分かる。あわせて、base と上書きを登録する側が守る契約（登録と解除の順、同じ id の物の扱い、重ねてよい組み合わせ）を、実測で確かめた範囲を添えて定める。
- 利用者: host application の Editor code を書く人（上書きの build を呼ぶ入口を作る）と、base と上書きを Runtime に登録する実装を書く人（契約を読む）。編集する開発者が直接操作する入口は、このスライスでは作らない。開発者に求める入力は、編集した asset の集合だけにする。
- 目的・利用者の文言の経緯: 上の 2 欄は、program §8 の S2 の行（2026-11-14 に owner が縮めた版）から本書で初めて書いた文である。2026-11-14 に owner が A2 の前にこの文言を承認した（§2.8 U1）。A2 の目的レビューは、この 2 欄の本文だけをそのまま見る。
- 対象外:
  - `Scripts/Runtime/` の変更。`AssetManagement` が base と上書きの 2 つを持つ実装、record の由来、`ContentRevisionGate` の扱いは S3。
  - 要求した Scene を含む目的 build を選ぶ索引（S2-3）。
  - 編集した asset の集合をどこから得るか（Project の選択、VCS の変更一覧）、menu、SampleGame の宣言での上書きの入口、実データでの確認（S5）。
  - 上書きの配布。上書きは作った PC の Editor の Play だけで使い、publish しない。
  - Player、script の code・field の変更、sprite atlas、audio、lightmap、URP `Lit` の variant の見た目、Play の途中での上書きの作り直し・解除、上書きが 2 つ以上（§2.4）。
- 現況（base `66564116`）:
  - **上書きの形は実測済みで、作り方は未実装。** S2-1 は、`BuildPipeline.BuildContentDirectory` を harness が直接呼んで base と上書きを作り、製品の `UnityContentDirectoryBackend` を 2 つ持って登録した。登録の順が base → 上書きの時だけ上書きの版が出て、base だけにある Scene が直接参照する編集した texture は base の版のままだった（[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）」）。製品の build 経路（`BuildContentCoordinator`）で root の一部だけを build したことは無い。
  - **build の入力は plan と snapshot の対。** `BuildContentCoordinator.Build`（`Editor/Build/Content/BuildContentCoordinator.cs`）は `BuildPlan` と `BuildMaterializationSnapshot` を受け、`BuildContentProjection.Create` が「snapshot の候補は全て plan の selected か excluded に現れる」ことを要求する（欠けは `MissingCandidate`）。root ごとの依存閉包は `snapshot.Dependencies`（`BuildDependencySnapshot`: root の GUID・path と閉包の entry）にあり、`AssetDependencySnapshotBuilder` が `AssetDatabase.GetDependencies(path, true)` から作る。閉包に入るのは `Assets/` の下で、`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref` でない file だけ（`IsContent`）。
  - **plan を作り直す public な道が無い。** `BuildPlan` のコンストラクタは internal で、friend は test の assembly と `SampleGame.DependOnAll.Editor` だけ。`OneStarMaker.Build.Content` は `OneStarMaker.Build.Selection` を参照するが friend ではない（[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の X6 と同じ根）。
  - **directory は自分が何の上書きかを持たない。** `BuildContentRoot` は build identity・target・entry・任意の宣言（名前・表現の主値・runtime 既定値）だけを持つ。preflight（`reports/<identity>/preflight.json`）は selected・excluded・roots・閉包の和集合・requirements を持ち、root ごとの閉包は持たない。
  - **publish は Full の build だけを受ける。** `ContentTransportPublisher.Publish` は outcome の `buildMode` が `Full` でなければ拒む。上書きかどうかは見ない。
  - **登録は 1 つだけ。** `AssetManagement.InstallContentDirectory` は 2 件目を `InvalidOperationException` で拒む。本スライスは変えない。
  - **workspace は contentSet ごと。** coordinator は `work/<target>/<contentSet>` を再利用し、`Incremental` は前の出力を残す。base と同じ contentSet で上書きを build すると、base の目的の workspace に上書きの出力が混ざる。
- UC ごとの利用者の入力:

  **UC-1 編集を上書きにして build する**（呼び手は host の Editor code。開発者から見れば「編集を確かめる Play の前の 1 回」）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | 編集した asset の path の集合 | 利用者が実行時に渡す（出所は呼び手が決める。§2.8 U3） | 目的の「編集した asset の集合を渡すだけ」そのもの。唯一の利用者の入力 |
  | 目的の宣言から作った plan と snapshot（bootstrap 等の合成を終えた物） | 開発時の宣言から自動導出（呼び手が既存の入口で作る） | 上書きを base と同じ目的の内側に収める。利用者は選ばない |
  | base の identity（contentSet・revision・target・manifest の digest） | 自動導出（呼び手が、これから登録する base から読む） | 作った時と別の base に重ねない契約（§2.3 C5）の材料 |
  | 上書きの contentSet（workspace の名前） | 呼び手の既定値（base と別の名前） | base の workspace に上書きの出力を混ぜない |
  | 出力先の root、build の mode | 既定値（coordinator の既存の規則） | — |

  機械が決めるもの: 上書きに入れる root、root ごとの理由、当たらなかった編集とその理由、実体が重複する共有 asset の一覧、build identity。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **目的 build 1 つを base にした時、編集した asset の集合から製品の build 経路で上書きを作り、base → 上書きの順に登録した Editor の Play で、渡した編集の影響を受ける全ての root が編集後の版で出て、古い版が混ざらないと言えるか。**
- 誤って YES を出す道（各 AC はこれを塞ぐ形にする）:
  - (a) 観測が directory ではなく `AssetDatabase` の source を読む。Editor の Play で source が編集後なら、何も上書きしなくても全部が編集後に見える。→ Play の前に fixture の source を消し、E に入れなかった編集（対照）が base の版で出ることを同じ Play で確かめる（AC7）。
  - (b) 絞らずに全 root を上書きに入れる。漏れは無いが、上書きではなく作り直しである。→ 影響を受けない root が上書きの entry に無いこと（AC2・AC5）と、base の版で出ること（AC7）。
  - (c) 逆引きの単体テストが、期待と同じ手で作った閉包だけを見る。→ 実 `AssetDatabase` から作った snapshot で prefab instance と nested prefab を通す（AC2）。
  - (d) 編集前と編集後が観測値で区別できない fixture。→ build の process が、各 asset の v1 と v2 の観測値が違うことを record に残す（AC5 の前提 P）。
  - (e) Domain Reload 無効の Play で、前の Play が読んだ物が残る。→ 各観測は、それが process の最初の Play になる回から採る（S2-1 の移送）。
  - (f) 重複の報告が常に空でも通る。→ 報告に出た物は実体 2、出なかった物は実体 1 の両方向を見る（AC8）。

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 渡された編集について、古い版を黙って残さない。** plan の selected の root のうち、手元の依存閉包に E（渡された編集の集合）の file を 1 つでも含む root が、上書きから外れることは無い。
  - 主張を崩しうる経路: (1) 閉包の出所。`IsContent` が落とす file（`.cs` 等、`Assets/` の外、`Packages/` の下）は閉包に現れないので、E に入っても root に届かない。(2) plan を後から合成する経路。SampleGame の `ComposeBootstrap` と `Bs4FixtureComposer` は selected に候補を足す。合成の前の plan を渡すと、足された root が判定から漏れる。(3) E の path の形。`.meta` だけの変更（import 設定）、folder、`\` 区切り、削除済みの file、rename の前の path。(4) 弱い参照。`Loadable` や logical key で引く先は別の root なので、その root 自身の閉包で判定される。root でない asset を実行時に名前で引く経路は build に入らない既存の制約で、本スライスは変えない。
  - 探索した境界: `Editor/Build/Materialization` の閉包の作り方、`Editor/Build/Content` の projection と adapter、SampleGame の合成 2 箇所。未確認: path の大文字小文字が違う入力（Windows の VCS が返しうる）、`Packages/` の下の content を参照する root。
- **I-2 渡された編集を黙って捨てない。** E の各 path は、「どの root を上書きに入れた理由になったか」か「当たらなかった理由」のどちらかとして、結果と記録に必ず現れる。
  - 経路: I-1 の (1)(3) と、どの selected の root の閉包にも無い file（その目的が使わない asset）。
- **I-3 上書きは、渡された plan の内側から出ない。** 上書きの root は plan の selected の部分集合で、logical key・stable key・physical key・表現は元と同じ。plan に無い root を足さず、除外済みの候補を戻さない。
  - 経路: 絞る処理が snapshot の候補（除外済みを含む）から拾う実装、表現の tag を引き直す実装。
- **I-4 上書きを使わない build を変えない。** 上書きの情報を渡さない build の preflight・outcome・directory と、publish の受理条件は、本スライスの前後で同じである。上書きの build は publish できない。
  - 経路: `BuildContentRequest` の既存のコンストラクタ 3 つ、`ContentReportFile` を読む既存の読み手（判定 script、Artifacts の collect、Player build の検証）、`ContentTransportPublisher` の `buildMode` だけの検査（`Full` で作った上書きは今の検査を通る）。

**成立条件（起きなければならないこと）**

- **S-1** 絞った plan が `BuildContentProjection` を通り、製品の coordinator が上書きの directory を作る。その root の entry は、逆引きが選んだ root と一致する。
- **S-2** base → 上書きの順に登録した Editor の Play で、影響を受ける root が全て編集後の版で出る。影響の経路は、直接参照（Scene → material → texture）、Scene に置いた prefab instance、nested prefab、Object の root（prefab）の 4 つ。
- **S-3** 同じ Play で、影響を受けない root は base の版で出て、E に入れなかった編集は base の版のまま出る。
- **S-4** 重複の報告が実測と合う。報告に出た共有 asset は実体が 2 つ、base だけの root しか使わない asset は 1 つ。
- **S-5** preflight に、base の identity、E、root ごとの理由、当たらなかった編集と理由、重複する共有 asset が残る。

### 2.2 受け入れ条件

fixture は SampleGame を使わない合成 1 つ（program D10）。custom の script を持たず、版は組み込みの値（texture の pixel、子の GameObject の名前）で読む。root は 6 つ: `SceneDirect`（material 経由で `EditedTex` を直接参照し、`SharedTex` も使う）、`SceneInstance`（`EditedPrefab` の instance を置く）、`SceneNested`（`InnerPrefab` を nest した `OuterPrefab` の instance を置く）、`PrefabRoot`（Object の root。`EditedTex` を直接参照）、`SceneClean`（`SharedTex` と `CleanOnlyTex` だけ）、`SceneUnlisted`（`UnlistedTex` を直接参照）。v2 で変えるのは `EditedTex`・`EditedPrefab`・`InnerPrefab`・`UnlistedTex`。E は `EditedTex`・`EditedPrefab`・`InnerPrefab` の 3 つで、`UnlistedTex` は入れない（対照）。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | I-1・I-2 | 単体。表の入力（plan、閉包、E）に対し、上書きの root の集合、root ごとの理由、当たらなかった編集を返す。**失敗経路:** E に `.cs`、存在しない path、folder、`Assets/` の外の path、どの root の閉包にも無い asset を入れると、それぞれ別の理由で「当たらなかった編集」に出て、例外にも無言にもならない。`X.png.meta` は `X.png` として判定する。E が空、または当たる root が 0 の時は「上書き不要」を返し、空の plan を作らない |
| AC2 | I-1 | 実機。fixture の実 `AssetDatabase` から `AssetDependencySnapshotBuilder` で作った snapshot に E を当て、上書きの root が `SceneDirect`・`SceneInstance`・`SceneNested`・`PrefabRoot` の 4 つで、`SceneClean` と `SceneUnlisted` を含まない |
| AC3 | I-3・S-1 | 単体。絞った plan の selected は元の部分集合で、各候補の 3 つの key は元と同じ。元の除外は理由ごと残り、絞りで外れた候補は新しい理由 code の除外になる。絞った plan と元の snapshot の対が `BuildContentProjection` で issue 0。**失敗経路:** selected に無い stable key を指定した絞りは例外で拒む |
| AC4 | I-4 | 単体。上書きの情報を渡さない request の preflight・outcome は、追加の欄が空である以外は従来と同じで、既存の `BuildContentReportTests`・`BuildContentFullModeTests` が変更なしで通る。**失敗経路:** 上書きの build の結果を `ContentTransportPublisher.Publish` に渡すと、`Full` であっても publish 先を作る前に拒む |
| AC5 | S-1 | 実機。製品の coordinator で base（6 root）と上書き（AC2 の 4 root）を、別の contentSet で build する。Play で上書きの directory から読んだ `BuildContentRoot` の entry の logical key が AC2 の 4 つと一致する。**前提 P:** build の process が、変えた 4 つの asset の v1 と v2 の観測値が互いに違うことを record に残す。P が成り立たない回は未判定 |
| AC6 | S-2 | 実機。fixture の source を消した Editor の Play で、base → 上書きの順に登録し、4 つの root を load する。`SceneDirect` と `PrefabRoot` の texture、`SceneInstance` と `SceneNested` の子の名前が、全て v2。Error と Exception の log が 0 |
| AC7 | S-3 | AC6 と同じ Play。`SceneClean` は載り、`SceneUnlisted` の texture は v1。**これが v2 なら、観測が directory を読んでいないので全 AC を未判定にする** |
| AC8 | S-4 | AC6 と同じ Play。`SceneDirect` と `SceneClean` を両方載せた時、`SharedTex` の実体（相異なる `EntityId`）は 2、`CleanOnlyTex` は 1。逆引きの重複の報告は `SharedTex` を含み、`CleanOnlyTex` と `EditedTex` を含まない |
| AC9 | S-5・I-2 | 単体と実機の record。上書きの preflight の追加の欄に、渡した base の identity、E の 3 path、root ごとの理由（`SceneNested` の理由は `InnerPrefab`）、重複する共有 asset がある |
| AC10 | 範囲 | 差分が §3 の file だけで、`Scripts/Runtime/` と asmdef の `references` に変更が無い。実機の test は `[Explicit]` で通常の回帰に入らない。build の process の前後で `git status` と Build Settings・active target・Enter Play Mode の設定が同じで、fixture が残らない |

AC6〜AC8 は、root を load する順だけが違う 2 つの process（Scene を先、Object を先）で採り、同じ値になること。食い違えば未判定にして owner へ返す。

### 2.3 登録契約（このスライスが定める文。Phase D で §13 へ移す）

各文の根拠を角括弧で示す。「禁止」は、測っていないので契約で塞ぐという意味で、後続が測れば解ける。

- **C1 順序:** base を先に、上書きを後に登録する。逆の順は登録しない。［S2-1 の実測。逆では base の版が出た。owner 決定 2026-11-14］
- **C2 個数:** 登録するのは base 1 つと、上書き 0 か 1 つ。編集が増えたら、和集合の E で上書きを作り直す。［上書き 2 つ以上は未測定のため禁止］
- **C3 時点:** 両方の登録を、最初の Scene と asset の load より前に終える。［load の後の登録は未測定のため禁止］
- **C4 同じ id の物:** 上書きの登録中は、上書きにある id の物の base の版を選べない。要求する側は、どちらの root から引いても上書きの版を得る。［S2-1 の実測。owner 決定 2026-11-14 で既定の制約として受け入れ］
- **C5 重ねてよい組み合わせ:** 上書きは、作った時に渡された base の identity と同じ base にだけ重ねる。target と宣言の名前も一致すること。合わなければ上書きを登録せず、作り直しを求める。［本スライスが記録を作る。照合の実装は S3］
- **C6 解除:** Play の終わりまで両方を保持し、上書き → base の順に解除する。Play の途中で上書きだけを解除・差し替えしない。［S2-1 の実測は Play の終わりの解除だけ。途中は未測定のため禁止］
- **C7 範囲:** 上書きは Editor の Play だけで使う。Player の構成に上書きは無く、リリースは 1 つの目的 build だけを載せる。［program §5.3「当面の方針」。Player は未測定］
- **C8 実体の重複:** 上書きの root と base の root の両方が直接参照する共有 asset は、実体が 2 つになる。状態を持つ asset（例: 参照の同一性で共有する ScriptableObject）は、上書きの Scene と base の Scene の間で別の実体になる。上書きの build は、その一覧を記録に出す。［実体の数は S2-1 と本スライスの実測。状態を持つ asset の扱いは §2.8 U6］
- **C9 測った設定:** 上の実測は Domain Reload 無効の Play、同じ PC・同じ `Library` で作った base と上書きの範囲である。［Domain Reload 有効は S3、別の PC の base は S4］

### 2.4 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 要求した Scene を含む目的 build を、BuildSystem が出す索引で選べるか | S2-3（program §8、§10 U6） |
| `AssetManagement` が base と上書きを持つ実装、C1・C3・C5・C6 の強制、record の由来、Scene を load した後の登録、Play の途中の作り直し、Domain Reload 有効の Play | S3 |
| 別の PC・`Library` で build した base と手元の上書きの組み合わせ。配布と保存を hash でまとめる実装 | S4 |
| 編集した asset の集合の出所、menu、SampleGame の宣言と実データでの上書き（sprite atlas、audio、lightmap、URP `Lit` の variant の見た目を含む） | S5（program §9 G20） |
| script の code や field を変えた時の base の Scene と prefab の読まれ方 | S3 の A1 が、測るか別の調査にするかを決める |
| Player での上書き | 持たない（C7）。要るなら owner が program を改める |

program が「S2 の A1」へ送った入力の引き取り: 複数 package の合成の可否（S1 から）は、目的 build 同士の合成を主にしない方針（program §5.3）の下で、base と上書きの組だけを本書が答える（plan の出所は同じ宣言の手元の plan、共有 asset は重複を許して報告、同じ id の衝突は C4）。共有依存の置き場（S2-0b から）は、共有 package に分けず、C6 の寿命の結合と C8 の重複の受け入れで答える。prefab instance と nested prefab（S2-1 から）は AC6 で測る。上書きが 2 つ以上は C2、Player は C7 で契約として塞ぐ。sprite atlas・audio・`Lit` の variant・script の変更は上の表へ送る（理由は §2.8 U5）。

### 2.5 スライス分割の判断

- 契機: program の S2 の行は 3 つを持つ（目的 build を選ぶ索引、上書きの登録契約、逆引きの道具）。索引は「どの build を取るか」、残りの 2 つは「取った build に何を重ねるか」で、変更理由・触る file・証拠の種類が別である。索引は publish の出力（`ContentTransportPublisher`、transport manifest）と pure な選択で閉じ、実機の未知が無い。上書きの側は、製品の経路での部分 build と Play の実機の証拠が要り、独立に失敗しうる。
- 判断: **分割する。** S2-2（本書）が上書きの build・逆引き・登録契約を、S2-3 が索引を持つ。互いの成果物を入力にしないので並行できる（逆引きは手元の plan だけで決まり、base の索引を読まない）。
- 本書の内側はこれ以上分けない。逆引き（pure）だけを先に出すと、Unity の依存の取り方と build の出力が合うこと（AC2・AC6）を確かめないまま契約を書くことになり、問いに答えられない。

### 2.6 判定定義と停止規則

- 判定定義: **GO** は、AC1〜AC10 が全て成り立ち、判定必須テスト（§5）が通ること。**NO-GO** は、AC6 か AC7 が不成立（影響を受ける root に古い版が出る、または対照が編集後の版で出る）で、fixture や harness の不備ではないと確かめられた時。その場合は、逆引きの規則（閉包の取り方）か上書きの形のどちらが崩れたかを記録して owner へ返す（program §8 の「不成立なら縮退判断を owner へ」）。スパイクではないので CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の `references` を足さない（足す必要が出たら Phase A へ戻す）。
- 新規・編集する Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。Editor の code を Runtime の assembly に置かない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する（harness の観測を含む）。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない。
- repo 相対 path は `tools/path-budget.json` の予算（150 文字）に収める。
- coordinator の出力先は `artifacts/bs2b` か `TestResults/bs3-fixture` の下だけ。harness は後者を使う。
- build の target は StandaloneWindows64 の Player 固定（`UnityContentDirectoryAdapter.ValidateTarget`）。
- 機械向けの記録（record、layout の写し、期待値）を git に入れない。

### 2.8 owner の決定と未決事項

A1 の終わりに担当が推奨を付けて owner へ出した U1〜U8 は、**2026-11-14 に owner が全て推奨どおり決めた。** 以下は決まった内容である。

- **U1 目的と利用者の文言。** §1 の 2 欄を承認した。A2 の目的レビューはこの原文だけを読む。
- **U2 分割。** §2.5 のとおり S2 を S2-2 と S2-3 に分ける。
- **U3 編集した asset の集合を誰が決めるか。** 集合は引数で受けるだけにする（出所は S5）。機械が base の manifest の `sourceFiles` と手元の sha256 の差から決める案は、利用者の入力が 0 になり、渡し忘れによる漏れも無くなるが、program D4（sha 差分で適用対象を決めない）と当たり、手元の checkout が base より古いだけの file も上書きに入る。差分から決めるかは S5 の A1 で owner が決める。本書の保証は「渡された集合に対して漏れない」までである（AC7 がその境界を実測で示す）。
- **U4 逆引きの対象。** program の語は「上書きに入れる Scene」だが、plan の selected の root 全部（Object の root を含む）を対象にし、AC6 の `PrefabRoot` で測る。Object の root も、編集した asset を直接参照していれば base の版を読むと推測される（S2-1 は base の Scene で実測、Object の root は未測定）。
- **U5 測る・送るの仕分け。** 測る: prefab instance、nested prefab（逆引きの合否に直結する）。送る: sprite atlas・audio・lightmap（build の出力の形が別で、合成 fixture を増やすより S5 の実データで見る方が安い）、URP `Lit` の variant（構造の観測では判定できず描画の証拠が要る。上書きの Scene は自分の directory の中で shader を直接参照するので、directory をまたぐ解決は起きない見込み）、script の変更（base と上書きの build の間に compile を挟む多 process の harness が要る）。契約で塞ぐ: 上書き 2 つ以上、Player。
- **U6 状態を持つ共有 asset の重複。** C8 のとおり、上書きの Scene と base の Scene が同じ ScriptableObject を直接参照すると、実体が別になる。program の方針「開発では重複を許す」は texture 等の量の話として決められており、同一性に依る共有が切れることは明示されていなかった。本書は一覧を記録に出すだけにし、止める・広げる（その asset を使う root を全て上書きに入れる）の規則は足さない。実害が出る host が現れた時に、記録の一覧を材料に後続で決める。
- **U7 実機の harness の扱い。** S2-0b・S2-1 と同じく Phase D で消して git 履歴に残す。常設するのは単体テストだけ（製品の逆引きと記録の回帰はそれで閉じる）。
- **U8 上書きと base の結びつけ（C5）。** 上書きの記録に base の identity を必須で持たせる。無いと、base を取り直した後の古い上書きが黙って重なる。

未決事項: なし（owner の判断が要るもの）。A2 のアーキテクチャゲートに見てもらう設計上の選択が 2 つある: plan を絞る道を `BuildPlan` の public な method にするか friend を足すか（§3）、上書きの記録を preflight の追加の欄にするか別の file にするか。

## 3. 責務マップ

製品の変更は `Editor/Build` の 2 つの assembly に閉じる。`Scripts/Runtime/` は変えない。path は `unity/Assets/OneStarMaker/` からの相対。

| file | 新規 / 変更 | 責務と変更理由 | 所有・寿命、依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---:|
| `Scripts/Editor/Build/Selection/Results/BuildPlan.cs` | 変更 | selected の一部だけを残した plan を返す method を足す。外れた候補は新しい理由の除外にする。plan を外から作り直す public な道が無いため | immutable な値。依存なし（`noEngineReferences`） | public method 1 つ | Unity なしの単体 | 42 → 約 70 |
| `Scripts/Editor/Build/Selection/Results/BuildExclusionReasonCode.cs` | 変更 | 除外の理由を 1 つ足す（末尾。絞りで外れた） | — | enum の値 1 つ | 同上 | 38 → 約 41 |
| `Scripts/Editor/Build/Content/ContentOverlaySelection.cs` | 新規 | 逆引き。plan・snapshot・E を受け、絞った plan、root ごとの理由、当たらなかった編集と理由、重複する共有 asset を返す。policy だけで I/O を持たない | 呼び出しごとの immutable な結果。依存は Selection と Materialization の公開の型 | public（host の Editor code が呼ぶ） | Unity と `AssetDatabase` なしの単体 | 約 220 |
| `Scripts/Editor/Build/Content/BuildContentReport.cs` | 変更 | `BuildContentRequest` に任意の上書きの記録（base の identity と逆引きの結果）を足し、preflight と outcome の追加の欄に書く | request の寿命。既存の読み手は未知の欄を無視する | コンストラクタ 1 つと property 1 つ、記録の型 1 つ | 単体（JSON の形） | 174 → 約 225 |
| `Scripts/Editor/Build/Content/BuildContentCoordinator.cs` | 変更 | 上書きの記録を report へ渡すだけ。順序と出力先の規則は変えない | — | なし | 既存の単体 | 188 → 約 195 |
| `Scripts/Editor/Build/Content/ContentTransportPublisher.cs` | 変更 | outcome が上書きの build なら publish を拒む | — | なし | 単体（失敗経路） | 143 → 約 152 |
| `Tests/Editor/Build/ContentOverlaySelectionTests.cs` | 新規 | AC1・AC3・AC9 の単体 | — | — | 通常の EditMode 回帰 | 約 320 |
| `Tests/Editor/Build/BuildContentReportTests.cs`・`BuildContentFullModeTests.cs` | 変更 | AC4 の追加（既存の test は変えない） | — | — | 同上 | 各 +約 40 |
| `Tests/Editor/Spikes/OverlayBuild/`（4 file） | 新規、Phase D で削除（§2.8 U7） | fixture の生成と削除、build の process、Play の round 2 つ、観測から AC の成否を読む pure な写像とその test。S2-1 の harness（commit `a304963b`）から fixture の固定 GUID と marker、record、round の枠、log の捕捉、実体の数え方を写して縮める | `[Explicit]`。`TestResults/` の下だけに書く | — | Unity の build と Play | 計 約 1,200 |

- 行数の警報: `BuildPlan.cs` は 50% を超えて増える。分けない。足すのは plan 自身の不変条件（selected と excluded が候補を過不足なく分ける）を保ったまま部分集合を作る 1 つの操作で、plan の外に置くと internal のコンストラクタを開く必要がある。
- 逆引きを `Editor/Build/Content` に置く理由: 閉包（Materialization）と plan（Selection）の両方を読むのはこの assembly だけで、結果を渡す先（`BuildContentRequest`）も同じ assembly にある。新しい folder と asmdef は作らない（呼び手が 1 つの段階で層を先に作らない）。
- 逆引きと build を束ねる入口の class は作らない。呼び手は「逆引き → 上書き不要なら終わり → `BuildContentRequest` に結果を載せて既存の coordinator」の 2 手を自分で呼ぶ。束ねる class は、呼び手が 2 つになった時に S5 が決める。
- 中核の単体テスト: 逆引きは、test が friend として作る plan と snapshot（既存の `BuildContentProjectionTests` と同じ作り方）だけで検証できる。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序: (1) `BuildPlan` の絞りと理由 code、その単体。(2) `ContentOverlaySelection` と単体（AC1・AC3）。(3) request・report・coordinator・publisher の追加と単体（AC4・AC9）。(4) harness（fixture、build の process、round、写像）。(1)〜(3) は Unity の build なしで閉じ、Editor の compile 確認までを Phase B が行う。(4) の実行は Phase C。
- Phase B から Phase A へ差し戻す条件: `Scripts/Runtime/` の変更が要る。asmdef の `references` か friend の追加が要る。絞った plan が `BuildContentProjection` を通らず、snapshot の側も作り直す必要がある。上書きの root に、元の plan に無い候補を足す必要が出た。fixture の形（6 root と 4 つの変更）では AC2 の集合が作れない。
- 対象外を維持する方法: AC10 の差分の検査。menu と SampleGame の file を足さない。`BuildContentRoot` の serialized な形を変えない。

## 5. テストとレビュー計画

- 単体テスト: `ContentOverlaySelectionTests`、`BuildContentReportTests`・`BuildContentFullModeTests` の追加分、harness の写像の test。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Build`（C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）。それぞれ別の Unity process で、この順に実行する:
  1. `pwsh tools/run-tests.ps1`（空 filter の全 EditMode 回帰）。合格は XML の failed 0 と root result `Passed` で読む（`step.json` は `-nographics` の VFX 25 件が Inconclusive になり常に `failed` なので使わない）。
  2. harness の build の process（`[Explicit]` の test 1 つを `-Filter` で指定）。base と上書きを build し、AC2・AC5・AC9 の材料と前提 P を record に書き、fixture の source を消す。
  3. harness の round 2 つ（`[Explicit]` の `[UnityTest]`。Scene を先に load する回と Object を先に load する回）を、別々の process で。どちらも 2 の出力を読む。
  - 2 と 3 は、各 XML の total が 1 以上で failed 0、record に終了行があることを「harness が最後まで動いた」確認とし、その後に判定 C が record を AC ごとに読む。**process の自然終了をゲートにしない**（XML の完成後に Unity が終わらない既知の終了 stall は `tools/Harness/README.md` の規則に従う）。test の完全名は Phase B が §6 に書く。
- 全 EditMode 回帰の適用除外: なし（製品の code を変える）。
- 統合・Unity テスト: 上の 2 と 3。SampleGame の実 build を伴う既存の統合 test は、1 に含まれる範囲で通ること。
- 操作・実行時・目視条件の検証経路: 全て上の Unity バッチで閉じる。担当は Phase C の実行担当で、sandbox 外の承認済み経路で起動する。初期状態は、Editor を閉じ、active build target が StandaloneWindows64 の Player、Enter Play Mode の Domain Reload が無効、作業ツリーが clean。観測は record・XML・log で、目視は無い。証拠は record・XML・log・preflight と outcome の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通結果:
  - 成立済み（S2-0b・S2-1）: EditMode の `[UnityTest]` から Play に入り、製品の `UnityContentDirectoryBackend` を 2 つ持って 2 つの directory を登録する経路。source を消した Editor の Play で directory の物を読む経路。
  - **凍結前の事実確認（2026-11-14 に実施、Unity 6000.6.0f1 の Editor、主担当）:** `AssetDatabase.GetDependencies(path, true)` は、prefab instance を置いた Scene に対してその prefab の asset を返し、その prefab が nest する内側の prefab も返した。外側の prefab に対しても内側の prefab を返した。再帰なし（`false`）では、Scene から内側の prefab は返らない（製品の `UnityAssetDatabaseGateway` は `true` で呼ぶ）。確かめ方: 一時 folder に、空の `Inner.prefab`、それを nest した `Outer.prefab`、`Outer` の instance を 1 つ置いた Scene を Editor の API で作り、問い合わせの後に folder ごと消した（作業ツリーに残りが無いことを `git status` で確かめた）。**既存の asset では確かめられなかった:** この repository の `unity/Assets` の Scene（668 件）と prefab には、source prefab を持つ instance が 1 つも無い。したがって prefab instance と nested prefab の実測は、本スライスの合成 fixture が初めてになる。範囲: 空の GameObject だけの prefab で、prefab variant、override を持つ instance、Scene の中で unpack した物は確かめていない。
  - 未確認。初回確認は Phase C: 製品の coordinator に、selected の一部を除外へ移した plan を渡した build が成功すること（理由: Unity の build は Phase C の責任。確認地点: 判定必須の 2。不成立時: 基盤不備なら B、絞りの形で作れないなら Phase A）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（build 2 回、Unity の起動は 4 回の見込み）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。harness の写像の出力は補助とする。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当: 本 session（Claude、Anthropic）。A0 は program §5.3「当面の方針」、§8 の S2 の行と移送、事実の文書を入力とした。
- A2 独立レビュー: 未実施。予定は 3 件（目的レビュー、A0 だけからの代替構成、アーキテクチャゲート兼不変条件を破る順序の探索）。担当は起こす時に決めて実績を書く。
- A3 統合担当・採否: 未実施。
- C' 用に予約した担当: A2 で使わないベンダーを 1 系列残す。
- 独立性の強化条件を満たせない場合の理由: 該当が出た時に書く。

## 6. Phase B 実装結果

未着手。

### 動作確認

未着手（§5 のとおり `対象なし` の見込み）。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
