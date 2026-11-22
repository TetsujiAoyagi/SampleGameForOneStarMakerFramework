# Content Directory の build の出力と登録（実測の事実）

Unity の Content Directory（`BuildPipeline.BuildContentDirectory`）の build の出力と、その出力を `ContentLoadManager` に登録して load した時の挙動について、Unity の公開文書に書かれておらず、OSM の配布と保存の設計が前提にする事実を、実測で確かめた範囲だけ置く。OSM の設計の正本ではない（設計は [Architecture](../../unity/Assets/Docs/Architecture/)）。

**範囲:** Unity 6000.6.0f1、StandaloneWindows64、loose file（`BuildCompression.Uncompressed`、`BuildContentOptions.None`。`CleanBuildCache` を付けた build を含む）、1 台の PC と 1 つの `Library`、SampleGame を使わない小さな fixture。登録と load の事実は、さらに Editor の Play（Domain Reload 無効）に限る。Unity の版を上げたら、下の「測り直し」で確かめ直すか、この文書を直すか消す。

**project の Unity は 2026-11-17 に 6000.6.9f1 へ上げた。** この文書の実測は、版を別に書いた箇所を除いて 6000.6.0f1 のもので、6000.6.9f1 では測り直していない。6000.6.1f1〜6000.6.9f1 の release note のうち、この文書の事実に当たる修正は 2 件ある。6000.6.3f1 の「prefab だけを編集した後の差分 build で Scene が作り直されない」の修正（UUM-151103。下の「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」）と、6000.6.2f1 の「Content Directory の build が built-in resource の参照を解決する時、最初の content load で Player が落ちる」の修正（UUM-150973。この文書は Player を測っていない）。出力の形、build をまたぐ同じ file、2 つの directory の登録に当たる記載は release note に無い。記載が無いことは、挙動が変わっていないことの証明ではない。節に依拠して設計する後続のスライスは、その節を自分の A1 で 6000.6.9f1 で測り直す（割り当ては [program](../handoff/PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8）。「同じ file を持つ 2 つの directory の登録」と「同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）」の 2 節は、2026-11-18 に 6000.6.9f1 で測り直して同じだった（下の「6000.6.9f1 での上書きと登録の測り直し」）。下の「部分 checkout の上書き（6000.6.9f1）」は最初から 6000.6.9f1 で測った。下の「Editor の Play の入口からの上書き（6000.6.9f1）」も 6000.6.9f1 で、上の範囲と違い SampleGame の実 Editor を使い、Domain Reload 有効の Play を含む。

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
- **読まれる側（登録の順が base → 上書き）:** `SceneEdit` を、base の root から引いた id で要求しても上書きの root から引いた id で要求しても、上書きの版（v2）が出た。`EditedTex` も、両方の root の `Loadable` から load して v2 で、実体は 1 つだった。2 つの root の Scene の id も `LoadableObjectId` も等しかった（load は handle を取らない。Scene の id は Scene の GUID と同じ文字列で、Scene を別の folder へ移しても変わらなかった。下の「6000.6.9f1 での上書きと登録の測り直し」）。したがって、上書きを登録している間、要求する側は base の版を選べない。
- **class:** base だけにある Scene、上書きの Scene、base の root から `Loadable` で load した prefab の component は、`GetComponents` の位置ごとに正しい class の完全名と値になり、Missing は 0 だった。base の Scene と prefab の component は、上書きの script の一覧に無い script（base だけの probe）でも正しく解決された。
- **Scene の直接参照:** base だけにある Scene の Renderer が直接参照する material の `mainTexture`（編集した texture）は、base の版（v1）のままだった。共通の asset を編集した時、それを直接参照する Scene も上書きに入れないと、同じ Play の中で古い版と新しい版が混ざる。
- **実体の数:** base だけにある Scene と上書きの Scene を両方載せ、base の prefab と上書きの texture を `Loadable` で保持した時、両方の directory にある共通の texture・material・shader と、編集した texture・material は 2 つずつ、base だけにある prefab と texture は 1 つずつだった。base の Scene を先に載せた時と上書きの Scene を先に載せた時の 2 つの時点で同じ数だったが、Scene を load する順の効果は確かめていない。
- **寿命:** 上書きの Scene と base の Scene の unload と load し直し（間に `UnloadUnusedAssets`）を繰り返しても、保持している物は壊れず（`== null`、pixel の変化、material の参照の変化、class の変化のどれも無い）、Error も Exception も出なかった。上書きの Scene を load し直した時も、出た Scene は上書きの版だった（record の root の名前で確かめた。harness の判定の検出の範囲の外）。最後に上書き → base の順で解除し、登録は 0 件になった。
- **登録の順を逆にした時（上書き → base）:** どちらの root の id からも、`SceneEdit` と `EditedTex` は base の版（v1）が出た。載った Scene と prefab の class と値は正しく、Missing は 0 だった（この読みは「載った版の class と値で読む」と 2026-11-14 に owner が改めた語による。計画時の語は上書きの版が出ることを前提にしていた）。共通の asset と編集した texture の実体は 1 つずつだった。実体の数は登録の順で変わり（base → 上書きで 2、逆で 1）、一定ではない。
- **仕組みの推測（実測ではない）:** どちらの順でも、後から登録した directory の版が出た。公開文書の「登録の逆順に探索する」と合う。base の Scene の直接参照が base の版を読んだことは、上の節の推測（Scene の直接参照は Scene のある directory の中で解決される）と合う。設計上の保証ではなく、どちらの directory の file を読んだかを返す API は無い。
- **含意:** 上書きの版が読まれ、script の顔ぶれが違っても class が壊れなかったのは、登録の順が base → 上書きの時に限る（この fixture の範囲）。逆の順では上書きが効かない。

測っていないもの（結果を変えうる）: Player、別の PC・`Library` で build した base、実データ（SampleGame）、archive、3 つ以上の directory、上書きが 2 つ以上、上書きで script の code や field を変えた時、URP の `Lit` の variant、Scene に置いた prefab instance・nested prefab・sprite atlas・audio、Domain Reload 有効の Play、Scene を load した後に上書きを登録する順、Play の途中の上書きの解除、GPU 側の資源の重複。fixture は 1 つ。

## 製品の build 経路で作った上書きと、prefab を編集した後の差分 build

製品の `BuildContentCoordinator` で、編集した asset の集合から逆引きした root だけを上書きとして build した（2026-11-14、S2-2）。fixture は合成 1 つで、root は 6 つ（編集した texture を material 経由で直接参照する Scene、prefab の instance を置いた Scene、入れ子の prefab の instance を置いた Scene、Object の root の prefab、上書きに入らない Scene 2 つ）。prefab は variant でなく、override が無く、unpack していない instance。source を v1 で base を `Full` で build・publish し、source を v2 にしてから上書きを別の contentSet で build し、fixture の source を消した Editor の Play で base → 上書きの順に登録して読んだ。同じ観測を、root を load する順だけが違う 2 つの Unity process で採り、一致した。

- **成り立ったこと:** 逆引きは、実 `AssetDatabase` の閉包から、編集の影響を受ける 4 root を選び、上書きに入らない 2 root を含まなかった。上書きの build は base の directory と workspace の file を 1 つも変えなかった。編集した texture を直接参照する Scene と、Object の root の prefab は上書きの版で出た。上書きに入らなかった Scene は base の版で出て、上書きに入った Scene と base だけの Scene が同じ texture を直接参照すると、同じ Play に 2 つの版が出た。両方の root が直接参照する共有の texture の実体は 2 つ、base だけの root が使う texture は 1 つで、重複の報告と合った。
- **成り立たなかったこと:** Scene に置いた prefab の instance と、入れ子の prefab の instance は、上書きから読んでも v1 だった。上書きの directory の中で、その Scene の file は base と同じ sha256 で、v2 の値（子の位置の float 列）はどの file にも無かった。
- **原因（同じ `Library` の build cache）:** 次の 3 つの観察で切り分けた。base を build した process と別の Editor process で上書きを build しても v1。同じ `Library` で base を `Full` で build し直しても v1。`BuildPipeline.CleanBuildCache()` の後に上書きを build すると v2。Unity が `Library/BuildHistory/<build>/BuildContentTEP.json` に残す trace（`SerializeBuildInstructions` の `changedCount` / `unchangedCount`）では、prefab を編集した後の build で Scene の build metadata は作り直されたが、Scene の build instruction は変わらず、前回書いた serialized file が再利用された。build した Scene には prefab の instance が展開されて焼き込まれる（実行時の Scene は prefab を参照しない）ので、差分 build の判定が Scene の file だけを見ていると、prefab の編集を取りこぼす。上書きに限らず、prefab を編集した後に同じ `Library` で行う Content Directory の build 全般に起きる。build は成功し、Warning も出ない。
- **OSM を含まない最小の再現:** package を持たない project で、prefab（子の位置 (1,2,3)）の instance を置いた Scene を build → prefab を (4,5,6) にして build → `CleanBuildCache()` の後に build、の 3 回で、2 回目は (1,2,3) のまま（build instruction 4 個中、変更 0）、3 回目で (4,5,6) になった。
- **Unity の修正と 6000.6.9f1 での確認（2026-11-17）:** Unity は 6000.6.3f1 の release note に、Content Directory の build で、Scene が使う prefab だけを編集した後の差分 build がその Scene を作り直さない不具合の修正を載せている（Build System、UUM-151103。<https://unity.com/releases/editor/whats-new/6000.6.3f1>）。同じ形の OSM を含まない最小の再現 project を 6000.6.9f1 で開き、`CleanBuildCache()` を挟まずに 2 回 build した。prefab の子 Probe の位置を (3,4,5) に変えた 2 回目の出力に (3,4,5) が入った。確かめた範囲は、入れ子でない prefab 1 つ、Editor の build、`CleanBuildCache()` なしの 2 回の build だけで、入れ子の prefab、model、製品の build 経路（印を外した状態）、Player の build は確かめていない。この節と下の 2 節（印の形と検知の鍵、製品の build 経路での印）の実測は 6000.6.0f1 のもので、書き換えない。入れ子・共有・variant・model と製品の build 経路は、下の「6000.6.9f1 で印を外した製品の build 経路」で測り、印を製品から外した（S2-2c、PR #199）。
- **Scene に意味のない変更を入れた時:** 値を 1 つ変える、空の GameObject を足す、のどちらでも、その build は新しい版になったが、Scene を元のバイトに戻して build すると古い版に戻った。cache の鍵は Scene の file のバイトで、prefab の状態は鍵に入らない。
- **回避（最小の再現の project で成立）:** Scene に `EditorOnly` tag の GameObject を印の置き場として 1 つ置き、build の直前にその名前を、Scene が焼き込む prefab（`GetDependencies(scene, true)` の `.prefab`）の中身のハッシュに書き換え、build の後に Scene を元のバイトに戻す。prefab を v1 → v2 → v2 → v1 → v2 と進めて戻した 5 回の build で、全て正しい版が出た。2 回目は Scene の 1 個だけを作り直し、3〜5 回目は作り直し 0 で、v1 に戻した build は 1 回目と同じ出力を再利用した。印の名前は出力に無く、全 build の後の Scene は元のバイトと一致した。build のたびに印の object を新しく足す形は、object の fileID が毎回変わって Scene のバイトが毎回違い、正しいが cache が当たらなかった。
- **含意（6000.6.0f1）:** Scene に prefab の instance を置く project で差分 build の正しさを保つには、Scene のバイトを焼き込む prefab の版に結び付ける必要があった。印を source に残さない形と、prefab の file を読まない検知の鍵は、下の「印の形と検知の鍵」で測り、製品の build 経路に実装した（S2-2b、PR #193。下の「製品の build 経路での印」）。6000.6.9f1 では Unity の差分 build が自分で拾うので、印は外した（下の「6000.6.9f1 で印を外した製品の build 経路」、契約は [18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4）。

### 印の形と検知の鍵

印を source に残さない 2 つの形と、prefab の file を読まない検知の鍵を、1 回の batch で測った（2026-11-14、program §8 の S2-2b-0）。範囲: repository の外の package を持たない空の project、Unity 6000.6.0f1、1 つの Unity process の batch で Content Directory の build 36 回。小さい fixture は、2 段の入れ子の prefab（`Inner` を nest した `Mid` を、さらに nest した `Outer`）を置いた Scene、同じ prefab を置いた Scene 2 つ、`.obj` の model を置いた Scene、prefab を置かない Scene。規模の fixture は合成で、Scene 300（1 つ約 1 MB、prefab の instance 420 個）、prefab 3,000（3 分の 1 は入れ子）、material 10、texture は無い。

- **測った印の形:** どちらも build の直前に印の文字列を text で書き、build の後に元のバイトへ戻す。Scene を Unity の API で開いて保存しない。**M** は Scene の `.meta` の `userData` の行に書く（Scene 本体は書かない）。**F2** は Scene の file の末尾（最後の document の `SceneRoots`）に Unity が知らない key の行を 1 つ足し、戻しは file を元の長さに切り詰める。
- **印の値:** Scene ごとの依存閉包（製品の `AssetDependencySnapshotBuilder` と同じ除外をした `GetDependencies(scene, true)`）のうち、main asset が `GameObject` の物と `ModelImporter` が import した物を path の序数順に並べ、検知の鍵の値を連ねてハッシュにした。
- **成り立ったこと（M と F2 で同じ）:** 入れ子の内側の prefab、2 つの Scene が共有する prefab をそれぞれ編集した build は、編集した物を焼き込む Scene の serialized file だけを書き、他の Scene は書かなかった（書いた数は `BuildContentTEP.json` の `BuildPipeline::WriteSerializedFile` の event で数えた）。model を編集した build も、Scene のうちその model を置いた Scene だけを書き（他に書いたのは model 自身の出力）、他の Scene は書かなかった。これは書いた数の観察で、model の頂点は印なしでも出力に入る（下の「model の頂点」）ので、印が model の編集を検知した証拠ではない。変更なしの build と前の版へ戻した build は Scene を 1 つも書かず、出力はその版の前回の出力と名前・sha256 で一致した。全ての build の後、`Assets` の下の全ての file が build の前と同じバイトだった。印の文字列は出力に無く、印を書いた最後の build の出力は、同じ source を印なしで build した出力と Scene の file を含めて一致した。印による Error・Warning は無い。規模の fixture でも、source を変えない印ありの build は Scene を書かず、同じ出力を出した。
- **Scene の `.meta` も再利用の鍵に入る:** `.meta` の `userData` だけを変えた build で Scene は作り直され、元のバイトへ戻した build は前の出力を再利用した（M）。
- **検知の鍵:** `AssetDatabase.GetAssetDependencyHash`（public）は、prefab と model の編集で値が変わり、元のバイトへ戻すと最初の値に戻り、更新時刻だけを変えても変わらなかった。入れ子の外側の prefab の値は、外側の file を変えなくても内側の編集で変わった。prefab が参照する texture や material を変えても、prefab の値は変わらなかった（material の変更が file に落ちたことは確かめた）。`AssetDatabaseExperimental.LookupArtifact` の ID は全ての時点で `GetAssetDependencyHash` と同じ値だった（build の鍵には `GetAssetDependencyHash` を使った）。internal の `GetSourceAssetFileHash` は file のバイトにだけ追従し、外側の prefab の値は内側の編集で変わらなかった。
- **model の頂点:** `.obj` の頂点の編集は、印なしでも出力に入った（Mesh は Scene に焼き込まれない）。model の編集のうち階層と Transform は測っていない。
- **何も変えていない build への上乗せ（規模の fixture、各形 3 回）:** 印の計算、書き込み、`Refresh` の差、戻し、戻し後の `Refresh`、build の差を足し、印なしの変更なしの build の最小（約 15.8 秒）で割った。**M は 4.75〜5.16%、F2 は 4.79〜6.29%。** 印の計算は約 110 ms（0.7%）で、上乗せの約 8 割は書き込み・戻し・2 回の `Refresh` だった。M は build の差を除くと 4.75〜4.80% でほぼ一定、5% を超えた 2 回は build の差（53〜65 ms）の分だった。F2 は戻しと戻し後の `Refresh` が M より重い。印なしの変更なしの build は回によって約 15.7〜22.3 秒と揺れた（各 block の最初の回が約 6 秒遅い）。
- **上乗せの値の前提（この値を引く時は必ず付ける）:** 上の値は、印の計算と書き込みを製品の snapshot の後・adapter の `Refresh` の前に置き、その snapshot の閉包をそのまま使う場合の値である。閉包を `Refresh` の後に作り直す形では、閉包を作る所要（製品の snapshot の所要ではなく、`GetDependencies` と除外だけの所要。この batch で 2,954〜3,480 ms、割る値の 18.7〜22.1%）を足して読み直す。足すと両形とも 5% を大きく超える。
- **測っていない手順:** 印の `Refresh` を製品の `Refresh` とまとめる、Scene の間で鍵の結果を使い回す。上乗せの大半はこの側にある。
- **S2-2b への入力:** 判定は CONDITIONAL ACCEPT で、M の上乗せは 5% の合否が**未判定**（4.75〜5.16%）、F2 は**不合格**（4.79〜6.29%）だった。owner は 2026-11-14 に、この判定を書き換えずに、M と `GetAssetDependencyHash` の組を S2-2b の入力に採った。F2 は不合格の代替として残した（[program](../handoff/PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8）。この組の上乗せの値は、上の前提（印の計算と書き込みを製品の snapshot の後・adapter の `Refresh` の前に置き、その snapshot の閉包をそのまま使う）の下でだけ成り立つ。

測っていないもの（結果を変えうる）: Player の build、別の PC・`Library`、実データ（SampleGame）、texture を持つ規模の fixture、prefab variant・override を持つ instance・unpack した prefab、model の階層と Transform の編集、開いている Editor での build（測ったのは batch だけ）、build が途中で落ちた時の Scene の戻し、開いている Scene と読み取り専用の file、印の他の形（印の object を Scene に差し込む形、YAML のコメント行は公開文書が非対応と書く）、file の更新時刻で全バイトの読みを省く検知、`AssetPostprocessor` で prefab の保存を拾う検知、`BuildContentOptions.CleanBuildCache` が `CleanBuildCache()` と同じ効果か、cache を消した build の所要。

### 製品の build 経路での印

M と `GetAssetDependencyHash` の組を製品の `UnityContentDirectoryAdapter` に実装し、製品の `BuildContentCoordinator` で測った（2026-11-15、S2-2b、PR #193、Unity 6000.6.0f1、batch）。この印は S2-2c（PR #199）で製品から外した。この節は 6000.6.0f1 での記録として残す。印の計算と書き込みは製品の snapshot の閉包をそのまま使い、製品の `Refresh` の前に書いてその `Refresh` で読ませる（印のための `Refresh` は無い）。fixture は合成 1 つで、入れ子の prefab（`Inner` を nest した `Outer`）の instance を置いた Scene、共有の prefab `Shared` の instance を置いた Scene 2 つ、prefab を置かない対照の Scene。同じ `Library` で 7 回 build した: B1 全て v1 で `Full`、B2 `Inner` だけ v2 で `Incremental`、B3 変更なしで `Incremental`、B4 `Inner` を v1 に戻して `Incremental`、B5 全て v2 で `Full`、B6 `Outer` だけ v1 に戻した source から逆引きした root を上書きとして build、B5' `CleanBuildCache()` の後に B5 と同じ source で `Full`。版は子の位置の float 列を出力の `.cf` で探して読んだ。

- **入れ子と共有の prefab の編集が入った:** 7 回とも、出力の各 Scene はその回の手元の版の列だけを持った。B2 で、内側の prefab を外側の prefab 経由でだけ焼き込む Scene が (`Inner` v2, `Outer` v1) になった（印の無い時は v1 のままだった経路）。上書き（B6）の Scene も (`Inner` v2, `Outer` v1) で、Scene の file は B2 と同じ ContentHash だった。
- **差分 build の再利用が残った:** `Incremental` の回の `BuildPipeline::WriteSerializedFile` の event で、B2 は印の値が変わった Scene だけを書き、B3 は Scene を 1 つも書かなかった（書いたのは毎回新しい生成 root だけ）。B4 の 4 Scene の ContentHash は B1 と同じだった。
- **source が戻った:** 7 回とも、`coordinator.Build` が戻った直後に `Assets/` と `ProjectSettings/` の全 file が build の前と同じ sha256 で、各 Scene の `.meta` の `userData` は build の前と同じ（空）、生成 folder は残らなかった。対照の Scene の file と `.meta` は、14 回の前後とも sha256 と更新時刻が変わらなかった。
- **印は出力に入らなかった:** 7 回とも出力に印の接頭辞は無く、B5 と B5' の 4 Scene の file は同じ ContentHash だった。Scene 以外の file には B5 と B5' で差があり、印と無関係にも起きる（「build をまたぐ同じ file」）ので、ここでは判定に使っていない。
- **失敗経路（EditMode test、build の呼び出しを差し替えて）:** build の失敗・例外、build の前の例外、戻さずに終わった印の次の build での戻し（印の対象に含む時と、記録だけが残る時）、印の行以外を変えた `.meta` の部分的な戻しと Warning、2 つ目の `.meta` の書き込みの失敗、`Library` を消した後の印、Scene を移動した後の戻しで、build の後の `.meta` が元のバイトに戻った。開いている Scene・未保存の Scene・読み取り専用・自分の印でない `userData` では、どの file も書かずに止まった。
- **開いている Editor での観察（実 build ではない）:** `DisallowAutoRefresh` の区間の中で書いた `.meta` は、自動 refresh で import されず、明示の `Refresh` で import された。開いている Scene の `.meta` を書いて import すると、Scene は読み直されず未保存の変更も付かなかったが、次の Editor の更新で「The open scene(s) have been modified externally」の dialog が出て main thread が止まった（戻した後も同じ）。このため、開いている Scene を書く build は止める。`Refresh` が script の compile を起こしても、`Refresh` は compile を始めて戻り、直後の行と `finally` は走り、domain reload は C# のスタックが抜けた後に起きた（1 回の観察）。
- **測っていないもの（結果を変えうる）:** Player の build、別の PC・`Library`、実データ（SampleGame）、prefab variant・override を持つ instance・unpack した prefab、開いている Editor での実 build、Unity の build が importer を保存し直すか、host の project の `AssetPostprocessor`、上乗せの所要（owner の決定で測り直さず、上の「印の形と検知の鍵」の値の前提に合う置き方を構造で確かめた）。
- **測り直し:** harness は製品に残さず、git の commit `2bc0c108` の `unity/Assets/OneStarMaker/Tests/Editor/Spikes/PrefabStamp/` にある（PR #193）。その commit を別の worktree に取り出し、Editor を閉じ、active build target を StandaloneWindows64 の Player にして、次の 2 つを順に別々の Unity process で実行する。record は `TestResults/pst/<run id>/` に書かれ、第 2 process が record と写した build の履歴から判定の行を計算し直す。B5' の `CleanBuildCache()` は、その PC の `Library` の build cache を全て捨てる。process の自然終了を待たず、XML と record の終了行で完了を読む（[Harness README](../../tools/Harness/README.md) の既知の終了 stall）。

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.PrefabStamp.PrefabStampBuildProcess.Build
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.PrefabStamp.PrefabStampVerdictProcess.Verdict
```

### 6000.6.9f1 で印を外した製品の build 経路

印を外した製品の `UnityContentDirectoryAdapter` と `BuildContentCoordinator` で、同じ `Library` の差分 build を測った（2026-11-17、S2-2c、PR #199、Unity 6000.6.9f1、batch、active build target は StandaloneWindows64 の Player）。fixture は合成 1 つで、上の S2-2b の fixture に、`Shared` の variant `SharedVariant`（override なし）と `.obj` の model `Model` を足した: `SOuter`（`Inner` を nest した `Outer`）、`SShared1`・`SShared2`（`Shared`）、`SVariant`（`SharedVariant`）、`SModel`（`Model`）、対照の `SPlain`。同じ `Library` で 9 回 build した: B1 全て v1 で `Full`、B2 `Inner` だけ v2 で `Incremental`、B3 変更なしで `Incremental`（Scene を開かない）、B3o B3 の後に `SShared1` を開いて root の名前を変えた未保存の状態で `Incremental`、B4 `Inner` を v1 に戻して `Incremental`、B4b `Shared` と `Model` を v2 にして `Incremental`、B5 全て v2 で `Full`、B6 `Outer` だけ v1 に戻した source から逆引きした root を上書きとして build、B5' `CleanBuildCache()` の後に B5 と同じ source で `Full`。prefab の版は子の位置の float 列、model の版は版だけが持つ group の名前（`.obj` を `g` の行で分けると、子の GameObject がその名前で Scene に焼き込まれる。`o` の行で分けると importer が名前を捨てて子が `default` 1 つになる）を、出力の `.cf` で探して読んだ。

- **入れ子・共有・variant・model の編集が入った:** 9 回とも、出力の各 Scene はその Scene が焼き込む prefab・model の、その回の手元の版の識別値だけを持ち、他の版の識別値を持たなかった。B2 で `SOuter` が (`Inner` v2, `Outer` v1) になった（6000.6.0f1 で印なしに v1 のままだった経路）。B4b で `SShared1`・`SShared2`・`SVariant` が `Shared` v2、`SModel` が `Model` v2 になった。`SPlain` はどの識別値も持たなかった。
- **上書きにも入った:** B6 の逆引きの root は `SOuter` だけで、上書きの `SOuter` は (`Inner` v2, `Outer` v1) の列を持ち、file の sha256 は B2 の `SOuter` と一致した。
- **差分 build の再利用が残った:** `BuildContentTEP.json` の `BuildPipeline::WriteSerializedFile` の event で、B2 は Scene のうち `SOuter` だけを、B4b は `SShared1`・`SShared2`・`SVariant`・`SModel` の 4 つだけを書き、B3 は Scene を 1 つも書かなかった。B4 は Scene を書かず、各 Scene の file の sha256 は B1 と一致した。数え方の対象外だが、B3o・B6 も Scene を書かず、B5（`Full`）は `SOuter` だけを書いた（`Full` でも workspace の外の build cache は再利用される）。
- **clean な build と同じ:** B5 と B5' の各 Scene の file は同じ sha256 だった（Scene 以外の file の差は「build をまたぐ同じ file」のとおりで、判定に使っていない）。
- **source を書かなかった:** 9 回とも、`coordinator.Build` が戻った直後に `Assets/` と `ProjectSettings/` の全 file が build の前と同じ sha256 で、fixture の各 Scene の file と `.meta` の更新時刻は変わらず、生成 folder は残らなかった。`Library/OneStarMaker/ScenePrefabStamp/` は run の始めと同じ状態で、どの出力にも `osm-prefab-stamp-` は無かった。
- **開いている未保存の Scene（batch）:** B3o の build は成功し、`SShared1` の file と `.meta` は build の前と同じ sha256、build の後も開かれたまま `isDirty` で変更が残り、出力の `SShared1` は変えた名前を持たなかった（disk の版を焼き込む）。対話の Editor（batch でない）での実 build は確かめていない。
- **編集の帰属:** 各回で書いた asset の file の sha256 は harness が書いたバイトと一致し、その asset の `GetAssetDependencyHash` は直前の回と違い、書いていない file の sha256 は変わらなかった。
- **失敗経路（EditMode test、build の呼び出しを差し替えて）:** 開いて未保存の変更を持つ Scene の `.meta` を読み取り専用にした時、`userData` が空でない `.meta` の時も build は成功し、Scene の file と `.meta` は変わらず、未保存の変更は残った。build の失敗と例外では build が失敗し、生成 folder は残らず、source は変わらなかった。
- **別に観察したこと:** 同じ PC の別の回で、coordinator が staging の directory を final へ移す `Directory.Move` が Windows の access denied で失敗したことがある（harness の 1 回と、全 EditMode 回帰の既存の統合 test 2 件。同じ test を実行し直すと通った）。印の撤去と無関係な coordinator の publish の失敗で、原因は切り分けていない。
- **測っていないもの（結果を変えうる）:** Player の build、別の PC・`Library`・CI の agent をまたぐ cache、6000.6.3f1 より前の Unity で build したことのある `Library`（印の無い `.meta` を鍵にした古い Scene cache を持つことがあり、印を外すとその鍵に戻る。当たると build は成功したまま古い Scene を出す。回復はその `Library` で `CleanBuildCache()` を 1 回）、override を持つ instance・unpack した prefab・2 段以上の入れ子、model の Transform の編集、実データ（SampleGame）、対話の Editor での実 build、印が残った `.meta` の実環境での戻し。
- **測り直し:** harness は製品に残さず、git の commit `16dbc387` の `unity/Assets/OneStarMaker/Tests/Editor/Spikes/NoStamp/` にある（PR #199）。その commit を別の worktree に取り出し、Editor を閉じ、active build target を StandaloneWindows64 の Player にして、次の 2 つを順に別々の Unity process で実行する。record は `TestResults/nst/<run id>/` に書かれ、第 2 process が record と写した build の履歴から判定の行（`ac`、`model-identifier`）を計算し直す。B5' の `CleanBuildCache()` は、その PC の `Library` の build cache を全て捨てる。process の自然終了を待たず、XML と record の終了行で完了を読む（[Harness README](../../tools/Harness/README.md) の既知の終了 stall）。

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.NoStamp.NoStampBuildProcess.Build
```

```bash
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.NoStamp.NoStampVerdictProcess.Verdict
```

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

## 6000.6.9f1 での上書きと登録の測り直し

2026-11-18、Unity 6000.6.9f1、Active Build Target は StandaloneWindows64 の Player、Enter Play Mode は Domain Reload 無効で、上の 2 節の harness（下の「測り直し」）を git の commit から取り出して、各 filter を別々の Unity process で 1 回ずつ実行した（上書きの登録は 7 process、同じ file の登録は 3 process）。製品の code は変えていない。

- **上書きの登録:** record の観測の行は 5 つの観測の process で同じ値になり、6000.6.0f1 の節と全て一致した（登録の順が base → 上書きなら、どちらの root の id でも上書きの版。逆の順なら base の版。class は全て正しく Missing は 0。実体の数、寿命の 5 つの checkpoint での破損 0、build の出力で id が同じで中身が違う file が 3 つ）。Error / Exception の log は 0 件（build の段に Pipeline の設定の Warning が 2 件）。harness の `verdict` の行は `UNDECIDED` で、理由は下の「測り直し」が予告する補助の判定 code の既知の誤り（対照の順の class を上書きの版が出る前提で読む）。観測の値は上のとおり変わっていない。
- **同じ file を持つ 2 つの directory の登録:** harness の判定は `GO` で、答えは 6000.6.0f1 の節と全て一致した（`Loadable` からは実体 1、Scene の直接参照からは 2 つずつ、後から登録した側の解除が先に登録した側から `Loadable` で載せた物を Error なしに壊す）。第 1 process は XML と record の終了行を書いた後に終わらなかった（[Harness README](../../tools/Harness/README.md) の既知の終了 stall）ので、その process だけを強制終了し、答えは XML と record で読んだ。
- **移動した Scene:** 製品の coordinator で Scene 2 つの目的を `Full` build した後、`AssetDatabase.MoveAsset` で 1 つを下の folder へ移し、中の物の名前を変えて保存し、移動後の path を編集の集合にして製品の逆引きと上書きの build を行った。逆引きは移した Scene だけを選び、上書きの build は成功した。`LoadableSceneIdEditorUtility.CreateLoadableSceneId` の値は移動の前後で同じで、Scene の GUID と同じ文字列だった。base と上書きの root のその Scene の entry の id も等しく、base → 上書きの順に登録するとどちらの entry から読んでも上書きの版が載った。製品の登録の口（S3-2）の実機の test でも、移した Scene の base と上書きの root の id は等しかった。rename は測っていない。
- **native の登録と Play の終わり:** Play の中で native に 2 つ登録し、解除せずに Play を終えると、一覧（`ContentLoadManager.GetContentDirectories`）に 2 件残った。Domain Reload 無効でも有効でも同じだった。続けて script の reload（`EditorUtility.RequestScriptReload`）を起こすと、reload の後の一覧は 0 件だった。Domain Reload 有効では、domain reload は Play に入る時に起き、Play を終える時には起きなかった。残った 2 件は、一覧から取った handle の `UnregisterContentDirectory` で解除できた。

測っていないもの（結果を変えうる）: Play に入る時の domain reload が、前の Play で残った native の登録を消すか（script の reload が消すことだけを見た）。reload で static が消えた後の OS の file handle の扱い。Scene の rename、Scene 以外の asset の移動。Player、別の PC・`Library`。

## 部分 checkout の上書き（6000.6.9f1）

担当外の source の一部が手元に無い checkout で、製品の build 経路の上書きを作って base に重ねた（2026-11-21〜22、S3-4a、PR #202、Unity 6000.6.9f1、Active Build Target は StandaloneWindows64 の Player、Enter Play Mode は Domain Reload 無効）。fixture は合成 1 つで、Scene `A`（Full と Whitebox の 2 表現。Whitebox の Scene を以下 `AWhitebox`）と Scene `B` が texture `T` を Light の cookie として直接参照し、Scene `C` が material `M`（texture `U`）を使う。base は宣言の選ぶ `A`（Full）・`B`・`C` を `Full` で build・publish・install した。file を退かす時は `.meta` と一緒に `Assets` の外へ移して `AssetDatabase.Refresh` した。最初の観測は対象を限定した Editor 操作で取り、同じ答えを、製品の上書きの計画を通す test（下の「測り直し」）の判定の run で確かめた。

- **root の Scene の file が無い project の build:** `B` と `AWhitebox` を退かした project で、`A` だけの上書きを製品の coordinator で build でき、成功した。下の clean な checkout でも成功した。
- **上書きに入らなかった root の直接参照:** base と `A` だけの上書き（`T` を編集後の色）を base → 上書きの順に登録した Play で、`A` は上書きから載って（record は上書き）cookie の pixel は編集後の色、`B` は base から載って（record は base）cookie の pixel は base の build の時の色、`C` は base だった。上の「Scene の直接参照」と同じく、Scene の直接参照は Scene のある directory の中で解決されたと読める。
- **依存の file だけが無い時の `GetDependencies`:** `M` だけを退かすと、`AssetDatabase.GetDependencies(C, true)` は `C` 自身だけを返し、`M` の path も、`M` からしか届かない `U` も返さなかった。materialize は欠けを見つけず、手元の閉包が小さく見えるだけだった。依存の欠けを見分けるには、base の build の時点の閉包（publish の `sourceClosure`）の asset が手元で解決するかを見る必要がある。その記録の無い base では、依存の file だけが欠けた root は見分けられない。
- **退かしている間の AssetDatabase:** 退かしている間も、一度 import した `Library` の `AssetDatabase.GUIDToAssetPath` と `AssetPathToGUID` は、消えた asset の GUID と path の対応を返し続けた。手元で解決するかは file の有無で決める必要がある。materialize では、この状態の root は `UnresolvedRootGuid` ではなく `MissingRootAsset`（欠けを許す口では `RootFileMissing`）の欠けになる。退かして戻した `B`・`AWhitebox`・`M` の GUID と、`C` の依存は元のままだった。
- **一度も import していない `Library`（clean な checkout）:** 新しい worktree（`Library` の無い checkout）に、`B` と `AWhitebox` を除く fixture の file を `.meta` ごと写してから初めて import した。`B` と `AWhitebox` の GUID は `GUIDToAssetPath` が空を返し、写した file の GUID は元の checkout と同じだった。元の checkout が build・publish した base を install し、この checkout で計画すると、`T` が base と同じ内容なら上書き不要（作り直せない root も無い）、`T` を変えると `A` が上書き、`B` が作り直せない root になり、`A` だけの上書きを build でき、Play で `A` は手元の色、`B` は base の色だった。Unity の API で作った Scene の file は object の id が毎回変わって同じ内容を 2 回作れないので、2 つの checkout で同じ bytes と GUID にするには file を写す。

測っていないもの（結果を変えうる）: Player、別の PC の base（2 つの checkout は同じ PC）、実データ（SampleGame）、Domain Reload 有効の Play、上書きにある id の物を base の root から id で引いた時の版（上の節で上書きの版）、prefab や material を挟んだ参照の Play での版（`T` は Scene から直接参照した。`U` の編集は build までで Play では見ていない）、作り直せない root がどれくらいの頻度で生じるか。

測り直し: 1 つの checkout で file を退かす回は `unity/Assets/SampleGame/Tests/Editor/Build/OverlayFromLocalEditsIntegrationTests.cs` で、全 EditMode 回帰に入る（Content Directory の build を 5 回伴う）。clean な checkout の答えは同じ folder の `OverlayFromLocalEditsR8Tests` の `[Explicit]` の 2 つの test を、同じ head の 2 つの checkout で順に名前の filter で選んで走らせる（手順は test の class の comment）。後半の test は、初回の import を含む Unity process が XML を書いた後に終わらないことがある（[Harness README](../../tools/Harness/README.md) の既知の終了 stall）ので、XML で読む。

## Editor の Play の入口からの上書き（6000.6.9f1）

SampleGame の実 Editor で、Play の入口の経路 Built + local edits（[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」）を作る前と判定の時に、開いている Editor を `eval` で操作して確かめた（2026-11-22、S3-4b、PR #203、Unity 6000.6.9f1、1 台の PC、Active Build Target は StandaloneWindows64 の Player、各 1 回）。

- **複製の保存が書く物:** `Title` を開き、`Title` が参照する asset 1 つと参照しない material 1 つを Dirty にして `EditorSceneManager.SaveScene(scene, 複製の path, saveAsCopy: true)` を呼ぶと、2 つとも Dirty が倒れ、値を変えた material の file が書き換わった。Scene の保存の hook を止めても同じで、書いているのは `SaveScene` 自身である。`Assets/` の下の Dirty の object を `Resources.FindObjectsOfTypeAll` と `EditorUtility.IsPersistent`・`IsDirty` で挙げ、`EditorUtility.ClearDirty` で印を外してから複製し、`SetDirty` で戻すと、file は 2 つとも変わらず、Dirty と memory の値は残った。この挙げ方は 2 つの asset を過不足なく返した（`Assets/` の外では built-in と package の object が数百件 Dirty と出るので、path で絞る）。挙げる所要は、約 15,000 件の object から数 ms だった。
- **複製の保存と Scene の保存の hook:** 複製の準備（`PlaySnapshotSession.Prepare`）の中で、`EditorSceneManager.sceneSaved` が元の Scene（`Title` の元の path）と開始 Scene について発火した。`sceneSaved` を購読する hook（OSM の `SceneVolumeSaveHook`）は、複製の保存のたびに走る。体積の変わらない編集では source に差分は出なかった。
- **対話の Editor での build と開いている Scene:** `Title` の root の名前を変えて Dirty にし、複製を準備したまま、Play の外の main thread の同期呼び出し（`eval`）から製品の Content Directory の build（上書きでない `Incremental`）を起こした。前後で開いている Scene の path・Dirty（真のまま）・root の数・変えた名前は同じで、build の間に `sceneSaved`・`sceneOpened` は 0 回、保存を求める dialog は出なかった。判定の上書きの build の前後でも、`Title` の path と Dirty は同じだった。
- **複製の置き場と build:** 上の build の間、複製の置き場（`Assets/OsmPlaySnapshot/<session>/`）の file の SHA-256 は前後で同じで、preflight の閉包に置き場の path は無かった。
- **`Refresh` の直後:** `Assets/` の下に text file を書いて `AssetDatabase.Refresh()` を呼んだ直後、`EditorApplication.isCompiling`・`isUpdating` は偽で、同じ呼び出しのまま `AssetPathToGUID` と `GetDependencies` が引けた（`LoadAssetAtPath<TextAsset>` は null を返した）。script の追加は試していない。
- **script だけで変えた URP の material:** URP の `Lit` の material の `_BaseColor` だけを `Material.SetColor` で変えて `SetDirty` し、`AssetDatabase.SaveAssets()` で保存すると、Dirty は倒れたが file の `_Color` は古い値のままで、続く `Refresh()` の再 import で Dirty が真に戻った。もう一度保存すると `_Color` が `_BaseColor` に揃った値で書かれ、以後は Dirty に戻らなかった。shader の custom editor（`LitShader`）の `ValidateMaterial` を通してから保存すると（Inspector の編集と同じ）、1 回の保存でその最終の形になった。揃える処理は URP の `BaseShaderGUI.SetMaterialKeywords` にある。保存の時に URP は `_ScreenSpaceReflections*` の 2 行も同じ file に足した。
- **Domain Reload 有効の Play と登録:** Reload Domain を有効にして、製品の directory の Play → 停止 → Play を行うと、停止の後の Edit mode で `ContentLoadManager.GetContentDirectories()` は 0 件（製品が Play の終わりに解除した）、2 回目の Play の中で 1 件で、登録と lease の取り直しが成功した。base → 上書きの 2 層でも、上書きの Play → 停止 → 上書きの Play で 2 回とも登録が成功し、2 回目は前回の上書きを再利用した。製品が解除しなかった native の登録を、Play に入る時の reload が消すかは、ここでも測っていない。
- **SampleGame の部分 checkout:** 開いていない Scene の 1 つ（`Spring_Cell_1_5`）を `.meta` と一緒に `Assets` の外へ退かして `Refresh()` すると、Addressables の group の asset（`Default Local Group.asset`）が Dirty になり、次の保存で書かれた。その Scene と、手元にある別の Scene の両方が直接参照する material を編集して上書きの Play に入ると、手元の Scene は上書きから手元の値で、退かした Scene は base から base の build の時の値で載った（上の「部分 checkout の上書き」と同じ答えを、実データの 1 例で見た）。

測っていないもの（結果を変えうる）: Player、別の PC・`Library` の base、script の追加の後の `Refresh`、列挙に掛からない Dirty（Undo の履歴、sub-asset、Dirty の Scene、prefab の編集中、import の設定（`.meta`）の未適用の変更）、Play の間の script の compile。

## 確かめていないこと

- 増分 build cache の仕組みの全体（出力の file ごとの build instruction のハッシュで再利用を決め、Scene の鍵は Scene の file のバイトであることまでは、上の「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」で trace から読み、Scene の `.meta` のバイトも鍵に入ることを「印の形と検知の鍵」で観察した。他の asset の種類の鍵、cache の置き場と寿命は確かめていない）。
- 別の PC・`Library`・Unity の版・target の間、archive（`UseArchive`、圧縮）。
- 同じ file を持つ 2 つの directory を両方登録した時に、どちらの directory の file を読んでいるか（中身が同じ file では見分けられず、上の節の推測を実測で確かめていない。中身が違う file では、後から登録した側の版が出た。「同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）」）。
- 測っていないもの（結果を変えうる）: sprite atlas、共通物と固有物をまたぐ循環参照（6.6 は 1 file に合併する）、lightmap と lighting data（Scene に置いた prefab instance・nested prefab・prefab variant・`.obj` の model は、上の節で焼き込みと差分 build を測った）、asset の rename と Scene 以外の asset の移動（Scene の移動は上の節で測った）、script の code の変更、mesh・audio・FBX の sub-asset、import 設定と Graphics / URP の stripping 設定の違い。
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
