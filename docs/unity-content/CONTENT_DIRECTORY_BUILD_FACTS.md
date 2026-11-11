# Content Directory の build の出力（実測の事実）

Unity の Content Directory（`BuildPipeline.BuildContentDirectory`）の build の出力について、Unity の公開文書に書かれておらず、OSM の配布と保存の設計が前提にする事実を、実測で確かめた範囲だけ置く。OSM の設計の正本ではない（設計は [Architecture](../../unity/Assets/Docs/Architecture/)）。

**範囲:** Unity 6000.6.0f1、StandaloneWindows64、loose file（`BuildCompression.Uncompressed`、`BuildContentOptions.None`。`CleanBuildCache` を付けた build を含む）、1 台の PC と 1 つの `Library`、SampleGame を使わない小さな fixture。Unity の版を上げたら、下の「測り直し」で確かめ直すか、この文書を直すか消す。

## 出力の形

- 出力 directory の直下に、内容 hash を名前にした file が並ぶ: `.cf`（serialized object）、`.resS`（texture・mesh の stream）、manifest の `.json`、それと `BuildManifestHash.txt`（manifest の hash を書いた制御 file）。
- `Library/BuildHistory/<build>/ContentLayout.json`（Version 2）が、build の 1 回ごとに file の id（`.cfid`）、source asset の一覧、依存、内容 hash を持つ。`BuildManifestHash.txt` だけは layout に載らない。`Library/unity default resources` は内容 hash を持たない仮想の項目で、file ではない。
- `.resS` は layout の `BinaryArtifacts` にあり、参照する content file の id と `Category`（`texture` 等）で対応づけられる。

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

## 確かめていないこと

- 増分 build cache の関与（`CleanBuildCache` の build の log に、cache を使わなかったことを示す行が無い）。
- 別の PC・`Library`・Unity の版・target の間、archive（`UseArchive`、圧縮）。
- 同じ file を持つ 2 つの directory を両方登録した時の、メモリ上の実体の数・`Loadable` の解決先・片方の登録解除の影響。
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
