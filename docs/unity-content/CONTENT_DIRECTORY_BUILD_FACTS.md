# Content Directory の build の出力と登録（実測の事実）

Unity の Content Directory（`BuildPipeline.BuildContentDirectory`）の build の出力と、その出力を `ContentLoadManager` に登録して load した時の挙動について、Unity の公開文書に書かれておらず、OSM の配布と保存の設計が前提にする事実を、実測で確かめた範囲だけ置く。OSM の設計の正本ではない（設計は [Architecture](../../unity/Assets/Docs/Architecture/)）。

**範囲:** Unity 6000.6.0f1、StandaloneWindows64、loose file（`BuildCompression.Uncompressed`、`BuildContentOptions.None`。`CleanBuildCache` を付けた build を含む）、1 台の PC と 1 つの `Library`、SampleGame を使わない小さな fixture。登録と load の事実は、さらに Editor の Play（Domain Reload 無効）に限る。Unity の版を上げたら、下の「測り直し」で確かめ直すか、この文書を直すか消す。

## 出力の形

- 出力 directory の直下に、内容 hash を名前にした file が並ぶ: `.cf`（serialized object）、`.resS`（texture・mesh の stream）、manifest の `.json`、それと `BuildManifestHash.txt`（manifest の hash を書いた制御 file）。
- `Library/BuildHistory/<build>/ContentLayout.json`（Version 2）が、build の 1 回ごとに file の id（`.cfid`）、source asset の一覧、依存、内容 hash を持つ。`BuildManifestHash.txt` だけは layout に載らない。`Library/unity default resources` は内容 hash を持たない仮想の項目で、file ではない。
- `.resS` は layout の `BinaryArtifacts` にあり、`Category` が `contentfile` の項目の `ArtifactReferences` が、`Category` が `texture` 等の項目の `Index` を指す。
- **読める texture（`isReadable`）は `.resS` を持たなかった。** 1024×1024・RGB24・mipmap 無し・読める texture の `.cf` は 3,147,948 bytes（pixel の raw 3,145,728 bytes を上回る）で、その `contentfile` の項目に `ArtifactReferences` が無く、どの `contentfile` からも指されない `texture` の項目も無かった。layout の参照と size からは pixel が `.cf` の中にあると読める（`.cf` の bytes の中で位置を確かめてはいない）。読めない 256×256 の texture は `.cf` と `.resS` に分かれた。大きさと読める設定のどちらが効いたかは切り分けていない。

## build をまたぐ同じ file

別々に build した Content Directory の間で、共通の asset の content file は同じ id・同じ bytes になる。比べたのは、同じ root 集合の再 build（別の出力先、`name` と root の identity だけ別、別 process の `CleanBuildCache`、OSM の `BuildContentCoordinator` 経由）、root 集合の違う 6 対、依存先を変えた 3 回と元に戻した 1 回の、計 15 build。

- **同じになる:** texture、material、asset として参照される prefab、variant の無い自作 shader。root 集合と build の回によらない。
- **build の組み合わせで中身が変わる:** URP の `Lit` の shader と、build 内の全 MonoScript をまとめた file の 2 種類だけ。`Lit` が変わる理由は確かめていない（build 内の material の keyword で variant の集合が変わる、という仮説）。root の identity が違う build の間では、root の file と manifest も変わる（OSM の製品経路 `BuildContentCoordinator` は build ごとに identity を作り直すので、毎回変わる）。同じ root を別の出力先へ build し直した時は、比較から除外しており測っていない。
- **依存先を変えた時:** 比べた content file（manifest と `BuildManifestHash.txt` を除く）のうち、変わったのは書き換えた asset の file だけで、参照元の file の bytes は変わらなかった。manifest と `BuildManifestHash.txt` は内容が変われば変わる。UnityDataTools の形式文書は、その理由を「file 間の参照は相手の hash ではなく安定した id（`.cfid`）で持つ」と説明している（実測で確かめたのは bytes が変わらなかったことだけ）。
- **名前ではまとめられない:** `BuildManifestHash.txt` は同じ名前で、中身が build ごとに違う。まとめるのは sha256 で行う。それ以外の出力では、この fixture の全 build を通じて名前と sha256 の対応が一対一だった（同じ名前で sha256 が違う file も、同じ sha256 で名前が違う file も無かった）。名前に使われる hash は Unity の内容 hash で、sha256 そのものではない。

## 数値（fixture の比率で、実 project の代表値ではない）

| 項目 | bytes |
|---|---:|
| 別々の build `A` と `B` の合計 | 2,521,007 |
| 同じ 2 つの sha256 の和集合 | 1,574,899（946,108 減る） |
| `A` と `B` の root をまとめた 1 build | 1,374,182（和集合より 200,717 小さい） |
| 組み合わせで変わった file の延べ（shader 5 件 / script 4 件。下の注） | 1,679,044 / 7,984 |
| 依存先 1 つの変更で配り直す量（変更前の build に無い sha256 の file の合計。manifest と `BuildManifestHash.txt` を含む。texture は `A` で 48,488・`B` で 48,916 / material / prefab / 元に戻した時） | 48,488・48,916 / 6,764 / 8,544 / 0 |

- 「組み合わせで変わった file」は一意の file の量ではなく、root 集合の違う 6 対で違った file を対ごとに数えた延べ。内訳は `S`–`A` が shader 1 件、`S`–`B`・`S`–`AB`・`A`–`B`・`A`–`AB` が shader と script 各 1 件、`B`–`AB` が 0 件。
- OSM の製品経路で build した `P1` と `P2` を `ContentTransportPublisher.Publish` に通し、その `ContentTransportManifest.files` の `sha256` だけで、2 つの和集合を棚卸しと同じ 1,171,554 bytes に計算できた（`A` と `B` の和集合とは別の数。実行ごとに 1 byte 前後違うのは、毎回 identity が変わる root の file による）。

## 同じ file を持つ 2 つの directory の登録

別々に build した directory `A`・`B`（共通の texture・material・自作 shader・prefab が両方で同じ名前・同じ sha256 の file になる）を、同じ Play の中で `ContentLoadManager.RegisterContentDirectory` で両方登録した。登録の順は `A`→`B`・`B`→`A` の両方で、各 round を 2 回繰り返し、同じ値になった。「実体の数」は load の結果として区別できる `UnityEngine.Object` の数（相異なる `EntityId`）で、物理メモリの量ではない（texture の GPU 側の資源が共有されるかは測っていない）。

- **登録:** どちらの順でも 2 つ目の登録は有効な handle を返し、同じ名前の file の重複を知らせる Warning・Error は出なかった。
- **`Loadable` からの load:** 両方の root（handle ごとの `GetRootAssets`）の同じ prefab を load すると、prefab・material・texture とも実体は 1 つだった。両方の root の `LoadableObjectId` は等しい。
- **Scene の直接参照からの load:** `A` の Scene と `B` の Scene（どちらも Renderer が共通の material を直接参照する）を additive に load すると、material と texture の実体は 2 つずつだった。続けて `A` の root から prefab を load すると、その material は、登録の順 `A`→`B` では `A` の Scene の material と別の実体、`B`→`A` では同じ実体だった。
- **片方の登録解除:** 先に登録した側から `Loadable` で load した物を保持したまま、後から登録した側を `UnregisterContentDirectory` すると、保持している prefab・Renderer・material・texture の参照が解除の直後（同じ frame）に `== null` になった（`A`・`B` を入れ替えても同じ。両方から load して後から登録した側の物だけを先に Release した場合も同じ）。この時 Unity は Error も Exception も log に出さず、解除された directory は登録の一覧から消えた。逆に、後から登録した側から load した物は、先に登録した側を解除しても無傷だった。Scene の直接参照で載せた物は、もう片方の directory を解除しても無傷だった（もう片方の Scene を先に unload した場合も同じ）。どの場合も、解除の後に生き残った側の handle から同じ asset を load し直せた。
- **仕組みの推測（実測ではない）:** 公開文書は、登録中の directory を登録の逆順に探索すると言う。上の結果は、どちらの root から引いた `Loadable` も後から登録した directory の file に解決され、Scene の直接参照は Scene のある directory の中で解決された、と読むと説明できる。どちらの directory の file を読んだかを返す API は無い。
- **含意:** 中身が同じ file を各 directory に複製したまま両方を登録する形では、(1) Scene の直接参照の経路で同じ asset の実体が directory の数だけでき、(2) 後から登録した directory の登録解除が、先に登録した directory から `Loadable` で載せた物を黙って壊す。共有する asset を共有の directory に分けるか、登録の寿命を directory の間で結びつけるかは、Content の配布単位を設計する側が決める。

測っていないもの（結果を変えうる）: Player、archive、3 つ以上の directory、Scene に置いた prefab instance、sprite atlas、audio・video の `.resource`、片方の directory の file が disk から消えた時、Domain Reload 有効の Play、process をまたいだ再現（2 回の繰り返しは同じ Unity process の中）。中身が違う同じ id の file を両方登録した時は、次の節で測った。

## 同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）

取得した build（以下 base）に、手元で build した上書きを後から登録する形を模した（2026-11-13〜14）。fixture の source を v1 で build した directory を base、source を v2 に変えた後に root の一部（Scene `SceneEdit` と texture `EditedTex`）だけを build した directory を上書きとした。両方を同じ `Library` で build し、製品の `UnityContentDirectoryBackend` を無改変で 2 つ持って両方を登録してから load した。component の script には、base だけが使う物、上書きだけが使う物、両方が使う物の 3 つの `MonoBehaviour`（probe）を置き、base と上書きで使う script の顔ぶれを違えた。各観測は、それが Unity process の最初の Play になる回から採り（Domain Reload 無効の Play では、前の Play が読んだ物が残りうるため）、同じ観測を 5 つの process で照合して一致した。「実体の数」の意味は上の節と同じ。

- **build の出力:** script をまとめた file は、base と上書きで同じ id・違う sha256 だった。その file の layout の `SourceAssets` は、その build が使う script だけを持った（base は `BuildContentRoot`・base だけの probe・両方の probe、上書きは `BuildContentRoot`・両方の probe・上書きだけの probe）。編集した Scene と texture の file は同じ id・違う sha256 だった。編集していない共通の texture・material・自作 shader と、編集した texture を参照する material の file は、同じ名前・同じ sha256 だった。
- **読まれる側（登録の順が base → 上書き）:** `SceneEdit` を、base の root から引いた id で要求しても上書きの root から引いた id で要求しても、上書きの版（v2）が出た。`EditedTex` も、両方の root の `Loadable` から load して v2 で、実体は 1 つだった。2 つの root の Scene の id も `LoadableObjectId` も等しかった（Scene の id は path から作られ、load は handle を取らない）。したがって、上書きを登録している間、要求する側は base の版を選べない。
- **class:** base だけにある Scene、上書きの Scene、base の root から `Loadable` で load した prefab の component は、`GetComponents` の位置ごとに正しい class の完全名と値になり、Missing は 0 だった。base の Scene と prefab の component は、上書きの script の一覧に無い script（base だけの probe）でも正しく解決された。
- **Scene の直接参照:** base だけにある Scene の Renderer が直接参照する material の `mainTexture`（編集した texture）は、base の版（v1）のままだった。共通の asset を編集した時、それを直接参照する Scene も上書きに入れないと、同じ Play の中で古い版と新しい版が混ざる。
- **実体の数:** base だけにある Scene と上書きの Scene を両方載せ、base の prefab と上書きの texture を `Loadable` で保持した時、両方の directory にある共通の texture・material・shader と、編集した texture・material は 2 つずつ、base だけにある prefab と texture は 1 つずつだった。base の Scene を先に載せた時と上書きの Scene を先に載せた時の 2 つの時点で同じ数だったが、Scene を load する順の効果は確かめていない。
- **寿命:** 上書きの Scene と base の Scene の unload と load し直し（間に `UnloadUnusedAssets`）を繰り返しても、保持している物は壊れず（`== null`、pixel の変化、material の参照の変化、class の変化のどれも無い）、Error も Exception も出なかった。上書きの Scene を load し直した時も、出た Scene は上書きの版だった（record の root の名前で確かめた。harness の判定の検出の範囲の外）。最後に上書き → base の順で解除し、登録は 0 件になった。
- **登録の順を逆にした時（上書き → base）:** どちらの root の id からも、`SceneEdit` と `EditedTex` は base の版（v1）が出た。載った Scene と prefab の class と値は正しく、Missing は 0 だった（この読みは「載った版の class と値で読む」と 2026-11-14 に owner が改めた語による。計画時の語は上書きの版が出ることを前提にしていた）。共通の asset と編集した texture の実体は 1 つずつだった。実体の数は登録の順で変わり（base → 上書きで 2、逆で 1）、一定ではない。
- **仕組みの推測（実測ではない）:** どちらの順でも、後から登録した directory の版が出た。公開文書の「登録の逆順に探索する」と合う。base の Scene の直接参照が base の版を読んだことは、上の節の推測（Scene の直接参照は Scene のある directory の中で解決される）と合う。設計上の保証ではなく、どちらの directory の file を読んだかを返す API は無い。
- **含意:** 上書きの版が読まれ、script の顔ぶれが違っても class が壊れなかったのは、登録の順が base → 上書きの時に限る（この fixture の範囲）。逆の順では上書きが効かない。

測っていないもの（結果を変えうる）: Player、別の PC・`Library` で build した base、実データ（SampleGame）、archive、3 つ以上の directory、上書きが 2 つ以上、上書きで script の code や field を変えた時、URP の `Lit` の variant、Scene に置いた prefab instance・nested prefab・sprite atlas・audio、Domain Reload 有効の Play、Scene を load した後に上書きを登録する順、Play の途中の上書きの解除、GPU 側の資源の重複。fixture は 1 つ。

## 同じ `Library` での再 build の所要

同じ `Library` のまま、製品の publish と同じ条件で build を繰り返した時の所要を測った（2026-11-12〜13、各番 1 回ずつ）。条件: root の集合ごとに固定の出力先を毎回空にしてから書く、`name` と root asset は毎回新規、`Uncompressed`、1 build = 1 つの Unity process（batch）。所要は `BuildPipeline.BuildContentDirectory` の呼び出しだけで、Unity の起動と import を含まない。製品の `BuildContentCoordinator` は通していない。fixture は合成で、1024×1024 の読めない texture 300 枚、それを持つ URP `Lit` の material 300、Scene 100（各 Scene が material 20 を直接参照）。root の集合は X（Scene 0〜59）と Y（Scene 40〜99）で、出力はどちらも 660 file・約 142 MB。

| 番 | 操作 | 所要（秒） | 番 0 比 |
|---|---|---:|---:|
| 0 | X、`CleanBuildCache` | 336 | 100% |
| 1 | X、変更なし | 158 | 47% |
| 2 | X、texture 1 枚（Scene 4 つが参照）を変更 | 57 | 17% |
| 3 | Y、この `Library` で初回 | 250 | 74% |
| 4 | X、Y を挟んだ後 | 82 | 24% |
| 5 | Y、変更なし | 48 | 14% |
| 6 | X、`CleanBuildCache` | 437 | 130% |

- **変更の無い再 build は cache なしの build より短いが、0 にはならない。** 48〜158 秒（番 0 の 14〜47%）で、ばらつきが大きい。
- **別の root の集合を挟んでも、短さは失われなかった**（番 4・番 5）。
- **root の集合をまたいだ共有による短縮は小さかった。** 番 3 の出力は 308 file・68,062,048 bytes（bytes で 48%）が X の出力と同じ sha256 だが、所要は 74% だった。
- **texture 1 枚の変更で変わった file は 5 つ、799,353 bytes。** 変更の無い番 5 でも 3 file・98,038 bytes が変わる（毎回新しい identity の root と manifest の分と読める）ので、texture の分は 2 file・約 0.7 MB と読める。
- **読み方の限界:** `CleanBuildCache` の build 自体が 336 秒と 437 秒で 3 割違い、1 割単位の差は読めない。番 0 と番 1 の間は約 7 時間空いており、番 1 が他の変更なしの番より長い理由は切り分けていない。番 1 の出力と番 0 の出力の file の比較は取っていない。所要が短いことは「作り直していない」ことの証明ではない（時間から仕組みは読めない）。この PC・合成 fixture の範囲の数値で、実 project の絶対時間、別の PC、CI の agent をまたぐ cache は言えない。全ての番で、結果を書いた後に Unity を強制終了した（[Harness README](../../tools/Harness/README.md) の既知の native 終了 stall を待たないため）。
- **測り直し:** harness は git に残していない。上の条件と fixture で、EditMode の `[Explicit]` の test 1 件を 1 番として `pwsh tools/run-tests.ps1 -Filter <test の完全名>` で順に実行し、呼び出しの前後を stopwatch で測り、出力の file の sha256 を番ごとに控える。番 0 が 60 秒に満たない fixture では差が固定費に埋もれる。build 中に Burst の Error log が出ることがあり、test の枠組みに失敗と数えさせない設定が要る。

## 確かめていないこと

- 増分 build cache の仕組み（何を単位に、どこに持つか。上の節は所要の実測だけで、`CleanBuildCache` の build の log に cache を使わなかったことを示す行は無い）。
- 別の PC・`Library`・Unity の版・target の間、archive（`UseArchive`、圧縮）。
- 同じ file を持つ 2 つの directory を両方登録した時に、どちらの directory の file を読んでいるか（中身が同じ file では見分けられず、上の節の推測を実測で確かめていない。中身が違う file では、後から登録した側の版が出た。「同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）」）。
- 測っていないもの（結果を変えうる）: sprite atlas、共通物と固有物をまたぐ循環参照（6.6 は 1 file に合併する）、lightmap と lighting data、Scene に置いた prefab instance・nested prefab・prefab variant（instance は置いた側の file に焼き込まれる）、asset の rename と移動、script の code の変更、mesh・audio・FBX の sub-asset、import 設定と Graphics / URP の stripping 設定の違い。
- 実データ（SampleGame）での重複の量。

## 測り直し

実測の harness は製品に残さず、git の commit `f632707a` の `unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentFileIdentity/` にある（PR #177）。その commit を別の worktree に取り出し、Editor を閉じた状態で次の 3 つを順に、別々の Unity process で実行する。record は `TestResults/cfi/<run id>/` に書かれ、第 2 process が全 build の棚卸しから判定を計算し直す。

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.FileInventoryTests
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.IdentityBuilds.FirstProcess
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.IdentityBuilds.SecondProcess
```

2 つ目（13 build、約 6 分）は、XML を書いた後に Unity が終わらないことがある（[Harness README](../../tools/Harness/README.md) の「Unity終了時の既知native stallと調査の停止規則」）。その時は XML と record の終了行で読む。

登録の事実の harness は、git の commit `444651ed` の `unity/Assets/OneStarMaker/Tests/Editor/Spikes/SharedFileRegistration/` にある（PR #179）。同じく別の worktree に取り出し、Editor を閉じ、Enter Play Mode の Domain Reload を無効にした状態で、次の 3 つを順に、別々の Unity process で実行する。record は `TestResults/sfr/<run id>/record.jsonl` に書かれ、XML の合格は harness が最後まで動いたことだけを示す（答えは record の観測の行から読む）。

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationVerdictTests
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationBuilds.FirstProcess
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationRounds.SecondProcess
```

上書きの登録の事実の harness は、git の commit `a304963b` の `unity/Assets/OneStarMaker/Tests/Editor/Spikes/OverlayRegistration/` と、probe だけを持つ asmdef の `unity/Assets/OneStarMaker/Tests/Spikes/OverlayProbes/` にある（PR #182）。probe は test 制約の無い asmdef に置く（test 制約を持つ assembly は player の script の compile に入らず、build された component の script が解決されない）。同じく別の worktree に取り出し、Editor を閉じ、Active Build Target を StandaloneWindows64、Enter Play Mode の Domain Reload を無効にした状態で、次を順に、それぞれ別の Unity process で実行する。record は `TestResults/ovl/<run id>/` に process ごとに書かれ、XML の合格は harness が最後まで動いたことだけを示す（答えは record の観測の行から読む）。

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdictTests
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayBuilds.FirstProcess
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayRounds.ClassFirst
```

3 つ目と同じ形で、`OverlayRounds` の `LifetimeFirst`・`ReadFirst`・`ControlClassFirst`・`ControlReadFirst` を 1 つずつ別の process で続ける（最初に行う観測だけが違い、どれも 2 つ目の同じ出力を読む）。登録か load を Unity が拒否した時だけ、`BaseAloneFirst`・`OverlayAloneFirst` で単独の登録での成否を確かめる。補助の判定 code（`OverlayVerdict`）は、対照の順の class を上書きの版が出る前提で読むので、再利用する時は載った版の class と値で読むように直す。
