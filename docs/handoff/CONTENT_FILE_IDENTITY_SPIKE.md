# build をまたぐ content file の同一性の実証

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §5.3 の Stage II の前提の確認。§8 の S2-a より前に置く）
- status: 発見 C 4 巡目で阻害する指摘 0、判定 C 3 回目待ち（head `bb4efe77`）を `B` で修正済み、発見 C の確認待ち（A3 凍結版 revision 0 は commit `d1174709`、A2 入力版は commit `179012dd`、採否の経緯は §5.1）
- branch: `claude/content-file-identity-spike`（PR の base は `develop`）
- implementation base commit: `f8ffd023`（`develop` の head、2026-11-11。§1 の実測はこの base）
- implementation head commit: f632707a2896df58046ce1868790fec357d8a909
- risk: `normal`（製品 code を変えない。build だけで、Play も登録もしない）
- owner: root（依頼者）
- created: 2026-11-11
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: program §2.4「Unity の複数 Content Directory」と §5.3（build をまたぐ file の同一性、組み合わせで変わる file の種類、重複の数値、Q3 の要否）、§8（本スライスの行と S2-a の再開条件）。実験 code は製品に残さず git 履歴に置く（S0-b と同じ）。program の本文は Phase A では変えない（保留中の S2 のブランチが §8 を編集済みで、同じ行の衝突を避ける）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§2.4、§5.3、D10）。保留中の S2 計画 `CONTENT_PACKAGE_DISCOVERY.md` はブランチ `claude/content-package-discovery` にあり、本ブランチには無い（本スライスの結論が再開条件）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 判定 C 2 回目（head `bb4efe77`）の bundle はローカル固定済み（未昇格）、判断担当の別 process で取得と閲覧を確認済み。取得案内は進行役が repo 外の Storage 引渡し先に置いた。1 回目（head `0b72f0ff`）の bundle は差し戻しで無効
- C' blind bundle の取得案内・状態: 1 巡目（head `bb4efe77`）の bundle はローカル固定済み（未昇格）、C' の担当が取得した写しで閲覧を確認済み。取得案内は進行役が repo 外の Storage 引渡し先に置いた。差し戻しで head が変わるので判定には流用しない
- 引継ぎ票:
  - 次の Phase・役割: 判定 C 3 回目（GO 候補 head は implementation head 欄の commit）。その後 C' 2 巡目。判断担当と実行担当はそれぞれ新しい session。
  - 次の作業: §5 の判定必須テスト 3 コマンドを実行担当が順に別 process で実行し、新しい判定 evidence bundle に固定する。判断担当は §2 の判定定義を record の行から AC ごとに読む。旧 head の証拠は流用しない。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。
  - 必要な権限: 判定 C の Unity バッチテストは sandbox 外の承認済み経路。evidence bundle は Artifact Storage の `evidence stage`（ローカル固定）。
  - 停止規則: §4 の「Phase B から Phase A へ差し戻す条件」と、§2 の判定定義（未知の file・読めない欄・root の同居は Phase A へ）に従う。
  - 引き渡さないもの: C' には §7 の発見 C・判定 C の結論・指摘と、§6 の差し戻し修正の記述を渡さない（C' 用の取得案内は所見と分けて渡す）。

## 1. 目的と対象外

- 目的: Unity の Content Directory を別々に build した時、共通の asset（shader、material、texture、prefab）の content file が build をまたいで同じ名前・同じ bytes になるか、共通物が他の object と同じ file にまとめられて build の組み合わせで変わることがあるか、依存先の asset を変えた時に参照元の file まで変わるかを、Unity 6000.6 の実際の build の出力だけで確かめる。その結果から、複数の Content Directory の配布と保存を file の hash で 1 つにまとめられるかを owner が判断できる数値と、同じ file を持つ 2 つの directory を登録した時のメモリと解決の確認（次の段）が要るかの判断材料を残す。
- 利用者: OSMFramework の開発者（複数の Content Directory に分けて必要な分だけ配る計画を、この結論で続行・改訂・縮退のどれにするか決める owner と、後続で配布と索引を書く人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続）:
  - **問い 3（登録とメモリ）:** 同じ file を持つ 2 directory を両方登録した時に Unity がメモリに 1 回載せるか、`Loadable` がどちらの複製に解決されるか、片方の登録解除が他方を壊すか。本スライスの結論で要否を決める（§2.3、§2.5 U-2）。
  - 配布・保存を hash で 1 つにまとめる製品の実装（transport、cache、索引）: S2-b / S4。本スライスは数値と、製品の現在の形で足りない点の列挙までを出す。
  - 別の PC・別の `Library`・別の Unity の版・別の target の間の同一性: 後続（CI と手元で file を共有する時に要る。§2.3）。
  - archive（`UseArchive`、圧縮）での同一性: 製品の adapter は `Uncompressed` と `None` で loose file を出すので見ない。
  - 実データ（SampleGame）での重複の量: 後続（D10。本スライスは fixture）。
- 現況（base `f8ffd023`）:
  - **問いの出所:** 保留中の S2 計画は「build は自己完結し、同じ asset を参照する 2 build はそれぞれ複製を持つ」を前提に、共有の形を F1〜F3 に分けていた。owner は「複製が同じ bytes なら配布と保存は hash で 1 つにできるはずで、それは OSM の配布側の仕事ではないか」と指摘し、前提の確認を先に置いた。
  - **Unity の公開文書（6000.6、2026-11-11 に確認）:** 1 回の build の中だけ重複を除き、別の build とは asset を共有しない（各 build が複製を持つ）。build の出力は `.cf`（serialized object）、`.resS`（texture・mesh の stream）、`.resource`（audio・video）、manifest で、**各 file は自分の内容の hash で名付けられる。** 重複回避の頁は「複数の build に同じ file 名が現れたら、それは build をまたいで重複した内容である」と書く。登録中の directory は登録の逆順で探索される。**書いていないこと:** 決定性の保証、何を hash するか、object を file に分ける粒度、同じ file を持つ 2 directory を登録した時の扱い。
  - **Unity の UnityDataTools の形式文書（6.6 / 6.7 を扱う。内部形式で変わりうると明記）:** 通常は source asset ごとに 1 つの content file になる。Scene は 1 file で、参照先の asset は別 file。**build 内の全 MonoScript は 1 つの file にまとまる。** 6.6 は循環参照を 1 file に合併する。file 間の参照は相手の hash ではなく安定した id（`.cfid`）で、文書は理由を「相手の hash を埋めると 1 property の変更が build 全体へ波及するため」と述べる。`Library/BuildHistory/<build>/ContentLayout.json` が file ごとの id、source asset、依存、内容 hash を持つ。
  - **手元の build 履歴の読み取り（2026-11-11。統制していない観察で、判定の証拠ではない）:** この PC の `unity/Library/BuildHistory` に `ContentLayout.json`（Version 2）が 50 件ある。直近 12 件を隣同士で比べた。(a) 同じ root 集合を続けて build した 3 組は、共通の id 48・48・211 件の内容 hash が全て一致した。(b) root 集合の違う build の間では、共通の id の大半が一致し、**一致しなかったのは 3 種類だけ**だった: `Resources/unity_builtin_extra`、script をまとめた file（source asset の一覧が build ごとに違う）、URP の `Lit.shader`。texture・material・prefab・Scene で不一致は無かった。source が build の間に変わったかは統制していない。shader が変わる理由（variant の集合が build 内の material で決まる、等）は未確認。
  - **製品の build:** `Editor/Build/Content/UnityContentDirectoryAdapter.cs` は生成した `BuildContentRoot` 1 件を root に、`name = identity`（時刻 + GUID）、`Uncompressed`、`BuildContentOptions.None` で build する。root は build ごとに identity が違うので、root 自身の file は build ごとに必ず変わる。
  - **製品の配布:** `ContentTransportManifest.files` は file ごとに `path`・`size`・`sha256` を持つ。cache（`ContentCacheStore`）は revision 単位で、revision をまたいで同じ file を 1 つにする仕組みは無い。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力: `なし`（開発時の宣言も実行時の操作も無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 別々に build した Content Directory の間で、共通の asset の content file は同じ名前・同じ bytes になるか。ならない file があるなら、それはどの種類で、どれだけの量か。
- 仮説（Phase C で検証する）: texture・material・prefab・variant の無い shader の file は、root 集合と build の回によらず同じになる。依存先を変えても参照元の file は変わらない。build の組み合わせで変わる file は、root 自身、manifest、script をまとめた file、built-in の file、variant を持つ shader に限られる。
- 成果の範囲: 結論は「この PC、Unity 6000.6.0f1、StandaloneWindows64、loose file、fixture」に限る。record の先頭にそれを書き、別の PC・版・target・実データへ広げない。
  - **結論は配布と保存を hash でまとめられるかに限る。** GO / CONDITIONAL ACCEPT は「同じ file が複数の directory に居る」ことを意味し、実行時にそれがどう扱われるか（問い 3）と、保留中の S2-a の F1〜F3 が要らなくなるかは、問い 3 の結果まで未決である。
  - build を作り直す必要があるか（依存先が変われば、それを含む directory は build し直さないと新しい複製を持たない）と、作り直した結果のうち配り直す file がどれか、は別の問いで、本スライスが測るのは後者である。
  - **測らないもの（結果を変えうる。record と harvest にこの一覧を付ける）:** sprite atlas（packing が build 内の sprite の集合で決まる）、共通物と固有物をまたぐ循環参照（6.6 は 1 file に合併する）、lightmap と lighting data、Scene に置いた prefab instance・nested prefab・prefab variant（instance は置いた側の file に焼き込まれる。本スライスが見る prefab は asset として参照される prefab）、asset の rename と移動、script の code の変更（型情報を持つ file への波及）、mesh・audio・FBX の sub-asset、import 設定と Graphics / URP の stripping 設定の違い。
  - **AC6 の bytes は fixture の比率で、実 project の代表値ではない。**
- どうすればこの問いに誤って YES を出せるか（C と C' はこれを探す）:
  - (a) 2 回目の build が Unity の増分 cache の出力をそのまま返し、「同じ source で同じ」が自明になる（AC2 の `CleanBuildCache` と別 process）。
  - (b) file 名だけを比べる。名前が内容の hash だという主張そのものを確かめていない（AC1 の sha256）。
  - (c) fixture が小さく、`.resS` も variant も script の差も出ない（AC1 の fixture の条件）。
  - (d) 「共通」のつもりの asset が build ごとに別の GUID の複製になっている（AC1 の source の記録）。
  - (e) 比べた 2 build が実は同じ root 集合で、別の組み合わせになっていない（AC3 の固有 file の存在）。
  - (f) 違った file を帰属を確かめずに「root か manifest だから」と除く（AC4 の全件帰属）。
  - (g) 変更 round の書き換えが build に届いておらず、「参照元は変わらない」が自明になる（AC5 の校正）。
  - (h) 製品の adapter を通さない build だけで結論し、`name = identity` や root の違いが共通物に漏れる場合を見ない（AC2 の製品経路）。
  - (i) 同じ process の中の比較だけで決定性を主張する（AC2）。
- 判定に使う語（実測の前に固定する）:
  - **棚卸しの対象:** Unity の出力 directory（`BuildManifestHash.txt` のある directory）の直下の全 file。`P1`・`P2` は coordinator が返すその directory。
  - **file の id:** content file（`.cf`）は `ContentLayout.json` の `SerializedFiles` の `ID`。`.resS` / `.resource` は、`BinaryArtifacts` の `ArtifactReferences` でそれを参照する content file の `ID` と `Category` の組（同じ組に複数あれば sha256 の多重集合で比べる）。`Library/unity default resources` は内容 hash を持たない仮想の項目で、file ではないので比較にも集合の照合にも入れない。
  - **違った file:** 比べる 2 つの build の両方に同じ id があり、sha256 が違う file。**片方の build にしか無い id は「違った」に数えず、`片方だけ` として別に集計する。**
  - **別の file に入った:** 比べる 2 つの build の両方の `ContentLayout.json` に同じ source asset が載っていて、その source asset が帰属する content file の id の集合が 2 つの build で違うこと。id ではなく source asset を軸にした比較で、「違った file」とは別に、全ての対・全ての種類について数える（id が変わると「違った file」には現れないため）。
  - **不在（fixture の不備）:** `S` の中核の 3 種か shader の source asset が、相手の build の `ContentLayout.json` に載っていないこと。
  - **比べる対:** AC2・AC3・AC5 が名指しした対だけ。
  - **除外する file（事前に定義した 3 つだけ。それ以外は全て比べる）:** (1) root 自身（`RootAssets` の id を `LoadableObjectIds` で引いた `SerializedFile`）。**その file の source asset が生成した root の asset 1 つだけである時に限って除外する。** 他の source asset が同居していれば除外せず、未判定とする。(2) Unity の manifest（`BuildManifestHash.txt` が指す hash 名の `.json`）。(3) `BuildManifestHash.txt`（`ContentLayout.json` に載らない既知の制御 file。中身が `ContentLayout.json` の `BuildManifestHash` と一致することを build ごとに確かめる）。除外する file も size と sha256 は棚卸しと bytes の集計に入れる。
  - **未知の file:** 棚卸しの対象のうち、`ContentLayout.json` の `BinaryArtifacts` に内容 hash が無く、除外する file の (3) でもない file。
  - **種類の分類は source asset で事前に決め、結果を見て変えない:** `SharedTex` は texture、`SharedMat`・`LitPlain` は material、`SharedPrefab` は prefab（この 3 つを**中核の 3 種**と呼ぶ）。source asset に `.shader` / `.shadergraph` を含む file は shader（`SharedShader` も URP の `Lit` も同じ種類。`SharedShader` の結果は別の行にも書く）。`Resources/unity_builtin_extra` は built-in。source asset が `.cs` だけの file は script。`.unity` は Scene。どれでもなければ `その他`。
  - **想定した変更:** AC5 の各 round で、書き換えた source asset に帰属する file が違うこと。これは違った file の集計から外す。
- 判定定義。上から順に当てはめ、最初に当たったものを結論とする。どの結論も、証拠が揃えばスパイクの正常な終了である:
  1. **未判定（結論を出さない）:** 次のどれか。build を開始できない、終了行の無い build がある、build の前後で source が一致しない（AC1）、別 process の source が第 1 process と一致しない（AC2）、`ContentLayout.json` を対応づけられない・読めない欄や未対応の Version がある（AC4）、未知の file がある（AC4）、root 自身の file に root 以外の source asset が同居している（AC4。原因と帰属を記録して Phase A へ戻す）、`BuildManifestHash.txt` と layout が一致しない、`S` に `.resS` が無い（AC1）、中核の 3 種か shader に実際に比べた file が 0 件の対がある（AC3）、`不在（fixture の不備）` がある（AC3）、AC5 の round で想定した変更が起きていない、AC6 の数値が無い、AC7 を満たさない。harness・fixture・環境の問題は B へ戻し、未知の file と読めない欄は Phase A へ戻す。
  2. **NO-GO（前提は不成立）:** 次のどれか。(N1) AC2 の対に違った file か、別の file に入った source asset がある。(N2) AC3 の対で、中核の 3 種に違った file か、別の file に入った source asset がある。(N3) AC5 の round で、想定した変更以外に違った file か、別の file に入った source asset がある。(N4) AC3 の対で、種類が Scene か `その他` の違った file か、別の file に入った source asset がある。program §5.3 に従い、F1〜F3 のままの S2 か縮退かを owner に返す。N1 が別 process の対だけで起きた時は、「再 import と再 build のどちらの差かは分けていない」と結論に付ける。
  3. **GO（前提は成立）:** AC3 の対に、違った file も、別の file に入った source asset も 1 件も無い。
  4. **CONDITIONAL ACCEPT（条件付きで成立）:** AC3 の対の違った file と、別の file に入った source asset が全件、script・built-in・shader のどれかである。意味は「texture・material・prefab の file は hash でまとめられる。ただし列挙した種類は build ごとに別の file になる」。`SharedShader` が違ったかどうかを結論に書く。配布側で足りるかは owner が決める（§2.5 U-3）。
- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO / CONDITIONAL ACCEPT で終了する。NO-GO も証拠が揃えば終了する。最低条件未達（未判定）のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 統制していない比較を結論にしない:** 比べる 2 つの build は、source の bytes と GUID が記録で同一と確かめられ、片方が他方の出力や cache の写しでないことが分かる。
  - 崩しうる経路: (a) Unity の増分 build（`Library` の build cache。同じ出力先・別の出力先のどちらでも前回の成果を再利用しうる）。(b) fixture の再生成で GUID や import の結果が変わる（`.meta` を固定しない生成）。(c) 変更 round の書き換えが `AssetDatabase` に届かない。(d) 製品の adapter の `CleanupOrphans` と一時 root の生成が fixture の folder に触れる（触れないはずで、marker の無い folder は消さない）。
  - 探索した境界: `UnityContentDirectoryAdapter`、Unity の文書の増分 build の記述。未確認: 増分 cache が別の出力先・別の `name` にどう効くか（AC2 で測る）。
- **I-2 帰属を確かめていない差を無害と読まない:** build の間で名前か bytes が違う file は、全件が source asset か種類（root、manifest、script、built-in）に帰属し、record に残る。
  - 崩しうる経路: (a) `ContentLayout.json` と build の対応を取り違える（`Library/BuildHistory` には過去の build が残る。`BuildManifestHash` で対応を取る）。(b) `.resS` / `.resource` が `SerializedFiles` ではなく `BinaryArtifacts` に居て帰属から漏れる。(c) `ContentLayout.json` の形が版で変わる（Version 2 を確認して読む。違えば未判定）。
  - 探索した境界: 手元の `ContentLayout.json`（Version 2）と実際の出力 directory 1 件。`SerializedFiles` は `ID`・`SourceAssets`・`ContentHash` を持ち、`Library/unity default resources` だけ `ContentHash` が無い。`RootAssets` は id の一覧で、`LoadableObjectIds` が id から `SerializedFile` を引く。`BinaryArtifacts` は `ContentHash`・`Category`（手元では `contentfile`・`texture`・`manifest`）・`Size`・`ArtifactReferences` を持ち、manifest の `.json` も載る。出力 directory の file 名は内容 hash + 拡張子で、`BuildManifestHash.txt` だけが layout に載らない。未確認: `texture` 以外の binary の `Category`、fixture の build で同じ形になるか。
- **I-3 製品を変えない（S0-b と同じ）:** §3 の新規 file と本書以外の変更が無く、実験は assets・設定・build の出力を repo に残さない。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) `Assets/OneStarMakerGenerated` の残り。(c) Build Settings、active target、shader の設定（Graphics Settings の variant の設定）を変える。(d) 2 つの process の間に fixture を残す設計。

**成立条件（起きなければならないこと）**

- **E1 build の行列:** §3 の fixture から、§2.2 の全 build が 2 回の実行（別の Unity process）でできる。
- **E2 棚卸し:** 全 build の全 file について、名前、size、sha256、帰属（source asset か種類）が record にある。
- **E3 比較:** 問い 1（同じ source の再 build、別の root 集合）と問い 2（粒度）と変更の波及について、判定定義のどれに当たるかが record から一意に決まる。
- **E4 数値:** 重複の量と、組み合わせで変わる file の量が bytes で出る。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id、head、Unity の版、target、process の別を持ち、build ごとに終了行がある（無い build は未判定）。build は全て loose file（`Uncompressed`、`BuildContentOptions.None`。`CleanBuildCache` を付ける build だけ明記）で、出力先は build ごとに別にする。build の名前は §3 の表による。

- **AC1（E1、E2、I-1）fixture と棚卸し:** fixture は §3 の形である。各 build の**直前と直後**に source の file と `.meta` の sha256 と GUID を採り、両者が一致する（build の間に source が動いていない。一致しなければその build は未判定）。全 build の出力の全 file に名前・size・sha256 がある。build `S` に `.resS` が 1 つ以上ある。**観測（合否にしない）:** fixture の asset の import 後の hash（`AssetDatabase` で取れる範囲）を process ごとに記録する（第 2 process は fixture を作り直すので、差が出た時に再 import と再 build を切り分ける材料にする）。同じ名前の file が全 build を通じて同じ sha256 か、同じ sha256 の file が同じ名前か。破れがあれば record に残し、結論に「名前ではなく sha256 でだけまとめられる」と付ける。以降の比較は全て sha256 で行う。
- **AC2（E3、I-1）同じ source の再 build:** 次の対に違った file が無く、`片方だけ` の id も除外する file だけである（除外する file 以外に `片方だけ` があれば N1 と同じに扱う）。`A` と `A-same`（同じ引数、別の出力先）。`A` と `A-name`（`name` と root の identity だけ別）。`A` と別 process の `A-clean`（`CleanBuildCache`）。`B` と別 process の `B-clean`。製品の `BuildContentCoordinator.Build`（無改変。公開の入口で、既存の `BuildContentDirectoryIntegrationTests` と同じ呼び方）を通した `P1` と `P2`（identity が別。coordinator は出力の root を `artifacts/bs2b` か `TestResults/bs3-fixture` の下に限るので、`P1`・`P2` の `ArtifactsRoot` は `TestResults/bs3-fixture/` の下の専用 directory に固定する）。別 process の build の source の sha256 と GUID が第 1 process と一致する（一致しなければ未判定）。build ごとの所要時間と、`CleanBuildCache` の build が cache を使わなかったことを示す log の行を record に入れる（取れなければ「増分 cache の関与は不明」と結論に付ける）。**観測（合否にしない）:** `P1` と `A` の対。
- **AC3（E3）別の root 集合:** 対は `S`–`A`、`S`–`B`、`S`–`AB`、`A`–`B`、`A`–`AB`、`B`–`AB` の 6 つ。各対で 2 つの軸の比較を行う。(i) 両方にある id の file を全種類について比べ、違った file を種類付きで全件記録する。(ii) 両方の layout に載っている source asset を全種類について比べ、別の file に入った source asset を種類付きで全件記録する（6 対の全て。`A`–`AB` の `SceneA` のような固有物を含む）。`S` を含む 3 対では、`S` の中核の 3 種と shader の source asset が相手の layout に全て載っているかを見て、載っていなければ `不在（fixture の不備）` と記録する。中核の 3 種と shader のそれぞれに、実際に比べた file が `S` を含む各対で 1 件以上ある。`B` には `A` に無い id があり、`A` には `B` に無い id がある（組み合わせが実際に違うことの確認。無ければ未判定）。結論への写像は判定定義による。**観測（合否にしない）:** 上の 6 対は全て第 1 process の build で、増分 cache の上の比較である。別 process の `A-clean`–`B-clean` の対でも同じ 2 つの軸を記録し、`A`–`B` の結果と食い違えば結論に付ける。
- **AC4（E2、E3、I-2）粒度と帰属:** 各 build の**直後**に、その build で `Library/BuildHistory` に増えた項目の `ContentLayout.json` を record の置き場へ写し、その build の record に結びつける（後から `BuildManifestHash` だけで探さない。`A` と `A-same` のように同じ hash の build がありうる）。写した layout の `BuildManifestHash` が、その build の `BuildManifestHash.txt` の中身と一致することを確かめる。build の間に項目が増えていない、または 2 つ以上増えた時は、hash が一致する項目の layout の bytes が全て同じ時だけそれを使い、そうでなければ未判定とする。**棚卸しの対象から除外する file の (3) を除いた集合と、`BinaryArtifacts` の内容 hash の集合が一致する**（file 名から拡張子を除いた部分が内容 hash）。一致しない file は未知の file として名前を record に列挙する。全 content file を source asset の数で分類し、source asset を 2 つ以上持つ file と、違った file・`片方だけ`・別の file に入った source asset・`不在（fixture の不備）`（AC2・AC3・AC5 の全ての対）を全件、種類・source asset の一覧・size 付きで列挙する。除外する file の (1) について、root 自身の file の source asset の一覧を build ごとに記録し、生成した root の asset 以外が同居していれば未判定とする。中核の 3 種の asset が他の source asset と同じ file に入っているかを記録する（入っていても、それだけでは合否にしない。その file が build の間で変わるかは AC3 で読む）。
- **AC5（E3、I-1）変更の波及:** 第 1 process の最後に次を行う。各 round は第 1 process の最初の `A`（`B-tex` は `B`）との対で読み、除外する file は判定の語のとおり。**変更 round (1)〜(3):** 書き換えた source asset に帰属する file が 1 つ以上違い（想定した変更。違わなければその round は未判定）、それ以外に違った file も、別の file に入った source asset も、除外する file 以外の `片方だけ` も無い。(1) `SharedTex` の pixel を変えて `A-tex`・`B-tex` を build する。(2) texture を戻し、`SharedMat` の色を変えて `A-mat` を build する。(3) material を戻し、`SharedPrefab` の component の値を変えて `A-prefab` を build する。**復元 round (4):** prefab を戻して `A-back` を build する。これは上の条件の例外で、最初の `A` との対に違った file も、別の file に入った source asset も、除外する file 以外の `片方だけ` も 0 件である（差が無いことが合格）。
- **AC6（E4）数値:** record に次がある（全て sha256 で数える）。各 build の合計 bytes。`A` と `B` の file の和集合の bytes と、単純な合計との差（hash でまとめた時に減る量）。`AB`（1 build）の bytes との差。違った file の種類ごとの件数と bytes。AC5 の各 round で配り直しになる bytes。`P1`・`P2` を `ContentTransportPublisher.Publish` に通せた時は、transport manifest の `files` の `sha256` だけで同じ和集合が計算できること（通せなければ「製品の manifest での確認は未実施」と書く。合否にしない）。record と結論に「bytes は fixture の比率で、実 project の代表値ではない」と書く。
- **AC7（I-3）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書だけである。`pwsh tools/contract-audit.ps1` が合格する。2 回の実行それぞれの前後で `git status --porcelain` が一致し、Build Settings、active target、Graphics Settings に変化が無い。**git とは別に file system を直接見て**、各 process の終わりに fixture の folder とその `.meta`、`Assets/OneStarMakerGenerated` が存在しないことを record に残す。各 process は開始時に前回の fixture の folder の残り（自分の marker がある物だけ）を消してから始め、消した事実を record に残す。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 問い 3: 同じ file を持つ 2 directory を登録した時のメモリ上の実体の数、`Loadable` の解決先、片方の登録解除の影響 | GO / CONDITIONAL ACCEPT の時だけ要る。推奨は保留中の S2-a を新しい revision で再開し、その AC6（直接参照の共有の実測）に足す（§2.5 U-2）。NO-GO なら不要 |
| 問い 4 の実装: 配布・保存を hash でまとめる transport・cache・索引の形 | S2-b / S4（本スライスは AC6 の数値と §1 の現況までを入力として残す） |
| 別の PC・別の `Library`・別の版・別の target の間の同一性 | CI が publish を始める時の後続（program §8「外部依存の gate」） |
| shader・script・built-in の file を build の間で同じにする方法（variant の集合の固定等） | CONDITIONAL ACCEPT で量が問題になった時に owner が切る後続 |
| 実データでの重複の量 | S2-b の前に owner（読み取りの集計で足りる） |

### 2.4 スライス分割の判断

契機: owner の 4 つの問いのうち、1・2（build だけ）と 3（Play と登録）は独立して失敗しうる未知で、3 の要否は 1・2 の答えで決まる。**分割する。** 本スライスは 1・2 と、そこから計算だけで出る 4 の数値に限る。3 を同じスライスに条件付きの受け入れ条件として入れると、凍結した条件の適用が結果に依存し、判定 C が何を判定するかが head で決まらない。3 は Play の往復と登録の harness が要り、保留中の S2-a の fixture と AC6 が既にその形を持つので、作り直さずそちらへ足すのが安い。

### 2.5 未決事項（owner へ）

**owner の裁定（2026-11-11）:** U-1〜U-3 を全て推奨どおりとする。U-1 は本スライスを問い 1・2 と 4 の数値に限る。U-2 は (a)（S2-a の新しい revision に足す。確定は本スライスの Phase D）。U-3 は閾値を置かず、CONDITIONAL ACCEPT の時は実データの build 履歴の読み取り集計を取ってから owner が決める。

- **U-1 gate の切り方:** 本スライスを問い 1・2（と 4 の数値）に限り、問い 3 を結果の後に決める形でよいか。推奨: よい（§2.4）。
- **U-2 問い 3 の置き場:** GO / CONDITIONAL ACCEPT の時、問い 3 を (a) 保留中の S2-a の新しい revision に足す、(b) 独立の小さなスパイクにする、のどちらにするか。推奨: (a)。本スライスの Phase D で確定すれば足りる。
- **U-3 CONDITIONAL ACCEPT の受け入れ:** 予備の観察では script・built-in・`Lit.shader` が build ごとに変わっていた。この形で終わった時に「配布側で足りる」とするかは owner が決める。AC6 の bytes は fixture の比率なので、決める前に実データの build 履歴の読み取り集計（§2.3 の最後の行）を取るのを推奨する。本スライスは閾値を置かない。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentFileIdentity/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし）に置く。寿命は 1 回の test、依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。予想の合計は約 900 行で、行数の警報は無い。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `FileInventory.cs` | build 1 件の棚卸しの型（file の名前・size・sha256・id・帰属・種類、source の sha256 と GUID）と、棚卸しの対の比較（違った・片方だけ・不在、和集合の bytes）、判定定義への写像。入力は文字列と数値だけで、Unity の型も製品の型も受け取らない | 240 | Unity 非依存の pure。§2 の判定の語と AC1〜AC6 の比較の規則 |
| `FileInventoryTests.cs` | 上の比較と写像だけの EditMode test。合成の棚卸しで §2 の (b)(e)(f)(g) の誤った YES が YES にならないこと、片方だけの file を「違った」に数えないこと、比べた file が 0 件の種類が未判定になること、`SharedShader` だけが別の id に入った時は CONDITIONAL ACCEPT・`SceneA` が `A`–`AB` で別の id に入った時は NO-GO・全て同じ id と hash なら GO になること、root 自身の file に共通物が同居した時に GO / CONDITIONAL ACCEPT にならないことを含む | 220 | `[Explicit]` なし |
| `IdentityFixture.cs` | fixture の asset の生成（`.meta` の GUID を固定し、内容を決定的にする）、所有の marker、変更 round の書き換えと復元、開始時の残りの回収と終了時の削除 | 220 | Unity / `AssetDatabase` の I/O |
| `IdentityBuilds.cs` | build の行列の実行（直接の `BuildPipeline.BuildContentDirectory` と製品の `BuildContentCoordinator`）、build の前後の source の採取、出力の走査と sha256、`ContentLayout.json` の対応づけと帰属、JSONL の record | 300 | `[Explicit]` の EditMode test `FirstProcess` / `SecondProcess`。製品への依存はこの file だけ |

fixture（SampleGame を使わない。D10。一時 folder `Assets/OsmSpikeCfi/` に marker と一緒に作り、各 process の終わりに消す。この folder は git の ignore 対象ではないので、残れば `git status` にも出る）:

- 共通物: `SharedTex`（`.resS` が出る大きさ）、`SharedShader`（variant の無い自作 shader）、`SharedMat`（`SharedShader` と `SharedTex`）、`LitPlain`（URP の `Lit`、追加の keyword なし）、`SharedPrefab`（`SharedMat` と `LitPlain` の Renderer、Runtime の既存の component 1 つ）。
- 固有物: Scene `SceneA`・`SceneB`、`LitEmissive`（URP の `Lit` に emission の keyword、`SharedTex`）、`OnlyB`（`LitEmissive` と、`SharedPrefab` とは別の Runtime の既存の component を持つ prefab）。
- **参照の形（固定。B 適応で変えない）:** Scene は prefab instance を置かず、Scene 内の Renderer が `SharedMat` と `LitPlain` を直接参照する（Scene に置いた prefab instance は Scene の file に焼き込まれ、prefab の file が出ないため）。`SharedPrefab` は asset として、root の `Loadable<Object>` から参照する。
- root は製品の `BuildContentRoot`（無改変）を使い、Scene は `LoadableSceneId`、Object は `Loadable<Object>` で持つ。新しい Runtime の型は足さない。

| build | root の中身 | 備考 |
|---|---|---|
| `S` | `SharedPrefab` | 共通物だけ |
| `A` | `SceneA`、`SharedPrefab` | |
| `A-same` | `A` と同じ | `A` と同じ引数、別の出力先 |
| `A-name` | `A` と同じ | `name` と root の identity だけ別 |
| `B` | `SceneB`、`SharedPrefab`、`OnlyB` | `Lit` の keyword と script の集合が `A` と違う |
| `AB` | `SceneA`、`SceneB`、`SharedPrefab`、`OnlyB` | 1 build にまとめた基準 |
| `P1`・`P2` | `A` と同じ | 製品の `BuildContentCoordinator.Build` を無改変で通す。identity が別 |
| `A-tex`・`B-tex`・`A-mat`・`A-prefab`・`A-back` | `A` / `B` と同じ | AC5 の変更 round |
| `A-clean`・`B-clean` | `A` / `B` と同じ | 別 process、`CleanBuildCache` |

## 4. 実装計画

- 順序: (1) pure の 2 file。(2) fixture。(3) build の行列と record。(4) 実行は Phase C。
- 対象外を維持する方法: 製品 file を変えない（AC7）。record と直接の build の出力は `TestResults/` 下の一時 root（git 管理外、path 予算内）に置き、`P1`・`P2` の出力は `TestResults/bs3-fixture/` の下の専用 directory に置く（AC2）。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る。`BuildContentCoordinator.Build` を fixture の root で無改変のまま呼べない（`P1`・`P2` が作れない。`OneStarMaker.Tests.Editor` は `OneStarMaker.Build.Content` を参照済みで、同 asmdef は `InternalsVisibleTo` を持つ）。`ContentLayout.json` から帰属が取れず、代わりの帰属の方法が要る。§3 の参照の形を変えないと fixture が作れない。
- B 適応の範囲: fixture の asset の細部（keyword の選び方、component の選び方）、record の項目名、build の順序、`SecondProcess` が第 1 process の record を読む方法。

## 5. テストとレビュー計画

- 単体テスト: `FileInventoryTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.FileInventoryTests`。
- 判定必須テスト（GO 候補 head）: 次の 3 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.FileInventoryTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.IdentityBuilds.FirstProcess`。(3) `-Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.IdentityBuilds.SecondProcess`。各 XML の total が 1 以上で failed 0、root result `Passed`、record に §3 の全 build の終了行がある。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC7）、build を行う `[Explicit]` の実験 test は通常の回帰に混ぜない。pure の `FileInventoryTests` は `[Explicit]` を付けないので、Phase D で削除するまで通常の回帰にも入る（Unity も file も触らない）。代替証拠: AC7、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 3 コマンドで閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64 Player、作業ツリーが clean。観測は record と XML。目視は無い。証拠は record・XML・log・各 build の `ContentLayout.json` の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: batch の EditMode test から Content Directory を build する経路は既存の `BuildContentDirectoryIntegrationTests` が成立させている。**未確認:** `[Explicit]` の test を完全名の filter で選べること、build と `Library/BuildHistory` の `ContentLayout.json` を `BuildManifestHash` で対応づけること、`CleanBuildCache` の所要時間。初回確認は Phase C（理由: Unity テストと Build の実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録）。
- 人間の判断が必要な条件: なし（CONDITIONAL ACCEPT の受け入れは Phase D の後に owner。§2.5 U-3）。
- 高コスト証拠: なし（fixture の build は S0-b の実測で 1 回約 21〜25 秒。15 build で 10 分以内の見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。pure の比較の出力（`FileInventory` の写像）は補助で、判定 C は棚卸しの行から独立に確かめる。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: A3 と同じ session（Claude）。
- C' 用に予約した担当: Phase A に関与していないベンダー（`cursor-agent` は AGENTS.md の契約どおり）。

### 5.1 A2 の独立レビューと A3 の採否

入力版は commit `179012dd` の本書（内容 sha256 `d46b344482872aa11591db1a9d921876b3d08234a44d5e0eddc2d238491fa052`）。全て未関与の新規 session で、互いの指摘を見せていない。risk は `normal` なので、A0 だけからの代替構成は行っていない。

| レビュー | 担当 | 入力 | 結果 |
|---|---|---|---|
| アーキテクチャゲート（G） | Codex CLI（read-only） | 入力版と関係 source の写し 5 file | blocker なし、major 5、minor 2 |
| 反例（X） | Claude の新規 subagent（枠固定: §3・§4 は実装済みと仮定） | 入力版 | blocker 4、major 6、minor 4（X-15 は分割を妥当と判断） |
| 目的（P） | Claude の新規 subagent | 目的・利用者の 2 欄の原文だけ（sha256 `b5d9e628ec51fe63f10a0993faf6c3b02f2898cb20cb42b7a5ffa85e92a4cb95`） | 22 項 |

目的・利用者の 2 欄は A3 で変えていないので、目的レビューのやり直しは発生していない。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| G-01 adapter を test から呼べない | 一部 | 可視性は実 repo で成立している（レビューの写しに asmdef と `AssemblyInfo` が無かった）。呼び方が未定だった点は採用し、公開の `BuildContentCoordinator.Build` に固定（AC2、§3、§4） |
| G-02 pure の入力 | 採用 | `FileInventory` の入力を文字列と数値に限定し、製品への依存を `IdentityBuilds` だけにした（§3） |
| G-03 source の採取の時点 | 採用 | build の直前と直後に採る（AC1） |
| G-04 棚卸しと帰属の集合の一致、X-04 読めない欄 | 採用 | AC4。読めない欄は未判定。記載の無い file は当初 NO-GO としたが、S-06 で未判定に改めた |
| G-05 git に出ない残り | 採用 | file system の直接の確認と、開始時の回収（AC7）。fixture の folder は ignore 対象でないことを確かめた（§3） |
| G-06 build を開始できない時 | 採用 | 判定定義（未判定） |
| G-07 判定の重なり | 指摘なし | X-01 で別の穴が出たので判定の語を足した |
| X-01 「違った file」の定義 | 採用 | 判定の語。片方だけの file は別集計 |
| X-02 Scene への prefab の焼き込み | 採用 | §3 の参照の形を固定し、prefab は asset として root から参照。AC3 に `不在`、AC5 に prefab の round。instance の焼き込みは測らないものに明記 |
| X-03 事後の分類と 0 件の合格 | 採用 | 種類を source asset で事前に固定。各対で 1 件以上（AC3） |
| X-05 AC1 と判定の矛盾 | 採用 | 名前と sha256 の対応は観測にし、比較は sha256（AC1） |
| X-06 fixture の比率 | 採用 | 成果の範囲、AC6、U-3 |
| X-07 除外の同定 | 採用 | 判定の語（`RootAssets` と名指しの manifest だけ） |
| X-08 AC5 の文言 | 採用 | AC5 |
| X-09 `P1` と `A` | 採用 | 観測にした（AC2） |
| X-10 cache の証拠 | 一部 | 所要時間と log の行を record に入れる。取れない時は結論に付記する（log の形は未確認なので合否にしない） |
| X-11 和集合の基準 | 採用 | AC6 |
| X-12・X-13・X-14 fixture が踏まない原因 | 採用（範囲の明記） | 成果の範囲。fixture は増やさない |
| X-15 GO の読み | 採用 | 成果の範囲の最初の項 |
| P-01〜P-12 | 既に計画の内容 | P-04 は AC3 の種類ごとの記録、P-08 の「変更 1 件で配り直す量」は AC6 に足した |
| P-13〜P-20、P-22 | 既に計画の内容 | 対象外と §2.3 |
| P-21 生データを履歴に残さない | 既に計画の内容 | §4（evidence bundle に置き git に入れない） |

**外部レビュー（2026-11-11、owner 経由、PR 上。入力は commit `a02f05d6` の未凍結の統合版）と、主担当による判定の再点検。** 外部レビューの 3 件は、coordinator の source、手元の実際の出力 directory、`ContentLayout.json` で事実を確かめてから採用した。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| E-01 `BuildManifestHash.txt` と仮想の built-in 項目で、正常な出力が NO-GO になる | 採用 | 判定に使う語（除外する file の (3)、file の id）、AC4。pointer と layout の一致を別に確かめる |
| E-02 `不在` がどの判定にも入らない | 採用 | `不在` を 2 つに分け、別の file に入った時は NO-GO（N2）、fixture の不備は未判定 |
| E-03 coordinator の出力 root の制限 | 採用 | AC2、§4（`TestResults/bs3-fixture/` の下） |
| E-04 build の数 | 採用 | 15（§5） |
| S-01 AC5 は必ず違った file を生むので、「違った file が無い」の GO に到達できなかった | 採用 | 「想定した変更」を定義して集計から外し、GO / CONDITIONAL ACCEPT は AC3 の対で決める |
| S-02 「列挙に当たらない結果は NO-GO」で、後始末の不備（AC7）や数値の欠落（AC6）まで「前提は不成立」になった | 採用 | 判定定義を順序付きにし、証拠の不備は全て未判定へ |
| S-03 Scene と固有物の file を比べる対が無かった（`A`–`B` は Scene を共有しない） | 採用 | AC3 に `A`–`AB`、`B`–`AB`。Scene・`その他` の違った file は N4 |
| S-04 「variant の無い shader」が違った時、分類を固定したままだと shader だけの差で NO-GO になり、緩めると事後の分類になる | 採用 | shader を 1 つの種類にして CONDITIONAL ACCEPT の側に置き、NO-GO の根拠は中核の 3 種（texture・material・prefab）に限る。`SharedShader` の結果は結論に別に書く。判定の意味の変更なので owner に確認し、2026-11-11 に承認を得た |
| S-05 `.resS` の「同じ id」が未定義 | 採用 | file の id（参照する content file の id と `Category`） |
| S-06 layout に無い file を NO-GO にしていた（前提の反証ではなく説明できない物） | 採用 | 未知の file は未判定で Phase A へ |
| S-07 AC2 の `片方だけ`、別 process の差の読み | 採用 | AC2、N1 の付記 |

**外部レビューの 2 巡目（2026-11-11、owner 経由。入力は上の表までを反映した未 commit の版で、owner が直接渡した）。** 判定は「R-01 を直せば凍結に条件付きで賛成。fixture とスライスは広げない」。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| R-01 id が変わった共通 asset が GO に紛れ込む | 採用 | 「別の file に入った」を source asset を軸にした比較として定義し直し、全ての対・全ての種類で数える。中核の 3 種は N2、Scene・`その他` は N4、shader・script・built-in は CONDITIONAL ACCEPT、GO は 0 件（判定に使う語、判定定義、AC3、AC5）。合成の棚卸しの 3 例を `FileInventoryTests` に足した（§3） |
| R-02 root 自身の file の一括除外 | 採用 | source asset が root 1 つだけの時に限って除外し、同居があれば未判定で Phase A へ（判定に使う語、判定定義、AC4） |
| R-03 `A-back` と AC5 の一律条件 | 採用 | 変更 round と復元 round を書き分けた（AC5） |

**PR 上の bot レビュー（2026-11-11、owner 経由）。** この反映の後、owner が「これ以上細かくしない」として凍結を指示した。

| 指摘 | 分類 | 理由と反映先 |
|---|---|---|
| layout と build の対応づけが同じ hash で曖昧になる | 採用 | build の直後に写して record に結びつける（AC4）。手元の `Library/BuildHistory` の項目名は時刻と hash で、`name` 由来の上書きは起きない |
| AC3 の比較が増分 cache の上だけ | 採用（観測） | `A-clean`–`B-clean` を観測の対に足した（AC3）。build は増やさない |
| §5.1 の G-04 の行が古い | 採用 | 行を直した |
| `FileInventoryTests` が通常の回帰に入る | 採用（文言） | §5 の適用除外の理由 |
| import 後の hash の記録 | 採用（観測） | AC1。合否にしない |
| status 行が長い | 採用 | 凍結版で短くした |

## 6. Phase B 実装結果

§3 の 4 file を `unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentFileIdentity/` に新規で置いた（`.meta` と folder の `.meta` を含む）。製品 code、asmdef、他の file は変えていない。

| file | 行数（予想） | 実際の中身 |
|---|---|---|
| `FileInventory.cs` | 453（240） | `ContentLayout.json` の写しの型、棚卸し（`Read`）、対の比較（`Compare`。違った・想定した変更・`片方だけ`・別の file に入った source asset）、`Absent`、bytes の集計、AC1 の名前と sha256 の観測、判定定義の写像（`Judgement.Judge`） |
| `FileInventoryTests.cs` | 247（220） | 合成の layout で 13 件。§3 に挙げた全ケースと、pointer の不一致・仮想の built-in 項目の扱い |
| `IdentityFixture.cs` | 322（220） | fixture の生成・変更 round・復元・source の採取・後始末 |
| `IdentityBuilds.cs` | 477（300） | `[Explicit]` の `FirstProcess`（13 build）と `SecondProcess`（2 build と判定）、JSONL の record、AC7 の確認 |

行数は予想の約 1.7 倍。増えた理由は、判定の写像を 1 つの関数に閉じず項目ごとに書いたこと（`FileInventory`）、fixture の決定性の処理（下の B 適応 1）、record の行の型（`IdentityBuilds`）。責務の配置と依存の向きは §3 のとおりで、製品への依存は `IdentityBuilds` だけ、`FileInventory` は Unity と製品の型を受け取らない。

**B 適応（§4 の範囲内。判定の語と判定定義は変えていない）:**

1. **fixture の決定性の方法:** Editor で確かめたところ、prefab の object と URP material の副 object（`AssetVersion`）の fileID は作るたびに乱数になる。別 process で source の bytes を一致させる（AC2）ため、各 asset を一度 Unity に作らせ、その text の fileID を class id から決まる固定値に、`.meta` の GUID を file 名から決まる値に置き換えてから最終の path へ書く。同じ class が 1 file に 2 つあれば止める。folder の `.meta` と marker は build の入力でないので source の採取から外した。
2. **component の選び方:** `SharedPrefab` に `AssetReleaseOnDestroy`、`OnlyB` に `VfxOwnerDestroyNotifier`（どちらも Runtime の既存 component）。この 2 つは直列化する値を持たないので、AC5 (3) の「component の値」は `SharedPrefab` の Transform の位置にした。
3. **Scene の作り方:** untitled の Scene と追加の新規 Scene は並べられないので、単独で開いて作り、終われば元の Scene 構成に戻す。開いている Scene が dirty なら始めない。
4. **root:** 直接の build の root は fixture の folder に固定 GUID で作る（`S`・`A`・`A-name`・`B`・`AB`。`A-same`・変更 round・`A-clean` は `A` の root、`B-tex`・`B-clean` は `B` の root）。build の `name` は root の identity と同じ。第 2 process は `A` と `B` の root だけを作り、作った全 file が第 1 process と同じ bytes と GUID であることと、root 以外の file が欠けていないことを確かめる。
5. **`SecondProcess` が第 1 process の record を読む方法:** `TestResults/cfi/first-process.txt` が run id を指し、`TestResults/cfi/<run id>/` の `first-state.json` と `inventories/<build>.json` を読む。最終の判定は第 2 process が全 build の棚卸しから計算し直して `verdict` 行に書く。
6. **test の形:** 既存の `BuildContentDirectoryIntegrationTests` と同じく `[UnityTest]` + `[Timeout]`（第 1 process は 3600 秒）にした。判定の結論は test の合否にせず、harness が record を書けなかった時だけ test が失敗する。
7. **`P1`・`P2` の publish 先:** `TestResults/bs3-fixture/cfi/pub/<run id>/P1`（`P2`）。

**record の置き場:** `TestResults/cfi/<run id>/record.jsonl`、`layouts/<build>/`（`ContentLayout.json`・`BuildLog.jsonl`・`BuildReportSummary.json` の写し）、`out/<build>/`（直接の build の出力）。全て git 管理外。

**確認したこと:** Unity 6000.6.0f1 の Editor でコンパイル成功（新しい file の警告 0、全ての型がロードされた）。`BuildContentOptions.CleanBuildCache` が存在することを Editor で確かめた。`pwsh tools/contract-audit.ps1` は errors 0・warnings 0。確認に使った Editor はこの session で起動し、正常終了させた。

**未実行:** Unity テスト（`FileInventoryTests` を含む）と build は一度も実行していない（Phase C）。§5 の「未知の操作経路の疎通」の 3 点（`[Explicit]` の完全名の filter、`BuildManifestHash` による layout の対応づけ、`CleanBuildCache` の所要時間）は未確認のまま。fixture の生成が別 process で同じ bytes になるかは、判定 C の第 2 process で初めて分かる（ならなければ AC2 で未判定となり B へ戻る）。

**発見 C 1 巡目の修正（§7.1。判定の語・判定定義・§3 の参照の形は変えていない）:**

- **DC-01:** `IdentityBuilds.Measure` が build ごとに、直前の一覧を `sources-before-<build>`、直後の一覧を `sources-after-<build>` の 2 行で record に書く（以前は直前だけを `sources-<build>` に書いていた）。各行は path・sha256・GUID。`build-end` の `SourcesStable` は従来どおり両者の一致で、判定 C は 2 行から独立に確かめられる。
- **DC-03:** root の生成（`BuildContentRoot`・`BuildContentEntry`・`Loadable` の組み立て）を `IdentityFixture.CreateRoot` から `IdentityBuilds.CreateRoot` へ移した。fixture は作り終えた root を `AddRoot`（`ScriptableObject` を受け、他の asset と同じ固定 GUID の処理で置く）で受け取るだけにした。prefab の component は `IdentityFixture` の constructor が `System.Type` で受け（`Component` の派生で、2 つが別の型であることを確かめる）、`IdentityBuilds` が `AssetReleaseOnDestroy` と `VfxOwnerDestroyNotifier` を渡す。`IdentityFixture` から `OneStarMaker.Runtime.*` と `Unity.Loading` の using は無くなり、製品への依存は `IdentityBuilds` だけになった。新しい Runtime の型は足していない。
- 行数は `IdentityBuilds.cs` 507、`IdentityFixture.cs` 315（他の 2 file は変えていない）。
- **確認したこと:** Unity 6000.6.0f1 の Editor でコンパイル成功（`scriptCompilationFailed` が false、4 つの型がロードされ、`IdentityFixture` の constructor が `Type, Type` であることを確認。Editor.log に spike の file の警告・エラーは 0）。`pwsh tools/contract-audit.ps1` は errors 0・warnings 0。確認の Editor はこの session で起動し、`EditorApplication.Exit(0)` で正常終了させ、Editor の process が消えたことをプロセス一覧で確かめた（強制終了はしていない）。Unity CLI 自身の常駐 process（`unity.exe --internal-identity-serve`）は Editor ではないので残している。
- **未実行:** Unity テストと build（Phase C）。

**判定 C 1 回目の差し戻しの修正（§7.3 の EC-01。§4 の B 適応「fixture の決定性」と「keyword の選び方」の範囲。判定の語・判定定義・§3 の参照の形・製品 code・asmdef は変えていない）:**

- **原因（確認した事実）:** Editor で fixture の生成（`IdentityFixture.Create` と `Delete`）を同じ process で 2 回繰り返すと、`LitEmissive.mat` の sha256 が判定 C の第 2 process の値（`02c2…`）と第 1 process の値（`ee62…`）に分かれた。2 つの bytes の差は、URP の `AssetVersion`（class 114）の文書が `Material` の文書の前にあるか後にあるかだけだった。Unity は object の文書を元の乱数の fileID の順に書き、`FixFileIds` は fileID を固定値へ置き換えても文書の順を変えていなかった。`LitPlain.mat` も同じ 2 つの object を持つので、判定 C で一致したのは偶然の範囲と読む（仮説。確率は測っていない）。prefab と Scene は乱数の fileID を持つが、判定 C の 2 process で一致しており、Unity が乱数の順以外で書くと見られる（未確認）。
- **修正 1:** `FixFileIds` の後に、YAML の文書を固定後の fileID の昇順に並べ直す（`SortDocuments`。全ての text の asset に効く。固定後の fileID が重なれば止める）。並べ直しの後の prefab・material・Scene を Editor で読み、component・material の参照・Scene の Renderer の material・`AssetVersion` の副 object が揃い、warning と error の log が 0 件であることを確かめた。
- **修正 2（同じ調査で見つけた §3 との食い違い）:** 保存された `LitEmissive.mat` の `m_ValidKeywords` が空で、§3 の「URP の `Lit` に emission の keyword」になっていなかった（判定 C 1 回目の 2 つの bytes とも同じ）。新しい material の既定の GI flag `EmissiveIsBlack` を見て、URP の material の検証が `_EMISSION` を外すため（keyword が外れたことは確認した事実、外す主体が URP の検証であることは仮説）。`globalIlluminationFlags` を `RealtimeEmissive` にして keyword を残した。保存された file の `m_ValidKeywords` は `_EMISSION`、`m_LightmapFlags` は 1 になった。
- **確かめたこと:** 修正後、同じ Editor の process で fixture の生成を 3 回、Editor を終了して起動し直した別の process で 1 回行い（別 process ではもう 1 回を依頼したが、起動直後の Editor で実行されず結果が無い）、4 回とも全ての source の file と `.meta`（root を除く 18 個）の sha256 が一致した（root は `IdentityBuilds` が build ごとに作るので、この確認には入れていない）。生成の試行のたびに `Assets/OsmSpikeCfi` が消えたことを file system で確かめた。Unity 6000.6.0f1 の Editor でコンパイル成功（`scriptCompilationFailed` が false）。`pwsh tools/contract-audit.ps1` は errors 0・warnings 0。
- **source の bytes が変わったことの影響:** prefab・Scene・`LitPlain.mat`・`LitEmissive.mat` の bytes は判定 C 1 回目と違う（文書の順と keyword）。判定 C 1 回目の record は旧 head のもので流用しない。
- 行数は `IdentityFixture.cs` 335（他の 3 file は変えていない）。
- Editor はこの session で起動し（2 回）、`EditorApplication.Exit(0)` で終了させ、Editor の process が消えたことをプロセス一覧で確かめた。判定 C 1 回目の record（`TestResults/cfi/20261111T152153Z/`）は読んだだけで変えていない。
- **未実行:** Unity テストと build（Phase C）。

**C' 1 巡目の差し戻しの修正（§8.1 の CP-01〜CP-03。判定の語・判定定義・§3 の参照の形・製品 code・asmdef は変えていない）:**

- **違った file の種類を両側の build から決める（`FileInventory.Compare`）:** 両方の build にある id の sha256 が違う時、以前は種類と source asset の一覧を左の build の file だけから取っていた。同じ id でも source asset の一覧は build ごとに違いうるので、右の build でその id に中核の asset や Scene が加わっていても、左が script か shader なら CONDITIONAL ACCEPT 側に読まれえた。`PairItem` に右の build の file の種類 `KindB` を足し、`Kind`（左）と `KindB` の両方を `FindingKinds` に入れる（どちらかが中核なら N2、Scene か `その他` なら N4）。source asset は両側の和集合を記録する。各側の種類は `Read` が凍結した分類（§2 の順）で付けた値をそのまま使い、分類の規則は変えていない。record と判定の理由の文字列では、両側の種類が違えば `script/scene` のように `/` で並べる（`KindLabel`。AC6 の種類ごとの件数もこの表示で集計する）。比べた file の数（`compared`、種類ごとの 1 件以上の確認）は以前どおり左の file の種類で数える。
- **AC7 の `git status` の取得の失敗（`IdentityBuilds.Begin` / `End`）:** 以前は `git` が失敗すると前後が同じ失敗の文字列になり、一致と読まれえた。`TryGit` で取得の成否を返し、record の `process-start` / `process-aftercare` に `GitStatusBeforeRead`・`GitStatusAfterRead` を足した。どちらかが失敗なら `AC7: git status could not be read` で未判定にし、前後の比較はしない。
- **直接 build の失敗の message（`IdentityBuilds.Direct`）:** `BuildReport`（`UnityEngine.Object`）に `?.` を使っていたので、`report == null` の分岐と結果の分岐に分けた（偽 null の契約）。
- 同じ修正で、`IdentityFixture.cs` の code のコメントから本書の指摘の記号への参照を外した（本書は Phase D で消えるので、コメントは理由だけを書く）。
- **足したテスト（`FileInventoryTests`、13 件から 15 件）:** `SceneJoiningTheScriptIdOnTheRightOnly_IsNoGo`（`B` だけで script の id に Scene が加わり bytes が違う時、`A`–`B` と `S`–`B` が N4 になる。以前の左だけの分類では script の差になり、この 2 対は N4 に入らなかった）。`CoreAssetJoiningTheScriptIdOnTheRightOnly_IsDifferedAsCore`（同じ形で prefab が加わった時、違った file の `Kind` が script・`KindB` が prefab・source が和集合で、`A`–`B` が N2 になる）。
- 行数は `FileInventory.cs` 465、`FileInventoryTests.cs` 280、`IdentityBuilds.cs` 525、`IdentityFixture.cs` 335。
- **確認したこと:** `pwsh tools/contract-audit.ps1` は errors 0・warnings 0。Unity 6000.6.0f1 の Editor でコンパイル成功（`scriptCompilationFailed` が false、4 つの型と `PairItem.KindB` がロードされ、`FileInventoryTests` の `[Test]` が 15 件。Editor.log に spike の file の警告・エラーは 0）。Editor はこの session で起動し、`EditorApplication.Exit(0)` で正常終了させ、Editor と asset worker の `Unity.exe` が消えたことをプロセス一覧で確かめた（強制終了はしていない）。終了後に `Assets/OsmSpikeCfi` と `Assets/OneStarMakerGenerated` が無いことを file system で確かめた。
- **未実行:** Unity テスト（足した 2 件を含む）と build（Phase C）。判定 C 2 回目の record は旧 head のもので流用しない（今回の修正は判定の写像と AC7 の記録を変えるので、新しい head で取り直す）。

## 7. Phase C

### 7.1 発見 C 1 巡目（対象 implementation head `df21b817`）

- 種別: 発見。動作確認は §5 のとおり `対象なし`。
- 入力: base `f8ffd023` から head `df21b817` の完全 diff・stat・name-status、head の新規 4 file と呼び出す製品 code の写し、本書（head の版）、`AGENTS.md`、アーキテクチャゲート、`contract-audit`（errors 0・warnings 0）。commit で固定し、Artifact Storage は使っていない（発見 C の入力は commit 固定で足りる）。
- 構造適合: `FileInventory` は Unity と製品の型を受け取らず §3 どおり。行数の約 1.7 倍は判定の写像の明示、fixture の決定性、record の型で説明でき、責務の混在は無い。`#nullable enable`・`record` 不使用・`Task.Delay` / `Thread.Sleep` 不使用・偽 null（`== null`）は満たす。
- 現在の問いを阻害する findings（B へ差し戻し、1 巡目）:
  - **DC-01**（AC1）: build ごとの source の採取は直前と直後の 2 回だが、record に書くのは直前の一覧だけで、直後の sha256 と GUID が残らない（`IdentityBuilds.cs` の `Measure`）。AC1 の「直前と直後に採り、両者が一致する」を record から確かめられない。修正: 直後の一覧も build ごとに record へ書く。
  - **DC-03**（§3 の責務マップ「製品への依存はこの file だけ」= `IdentityBuilds`）: `IdentityFixture` が `BuildContentRoot` の生成（`CreateRoot`）と Runtime の component 型（`AssetReleaseOnDestroy`・`VfxOwnerDestroyNotifier`）を直接参照する。修正: root の生成と製品の型の参照を `IdentityBuilds` へ移し、fixture へは `System.Type` 等で渡す（§3 の参照の形と新しい Runtime 型を足さない条件は変えない）。
- 阻害しない findings:
  - **DC-02**（決定記録の候補）: source に `.shader` と `.unity` が同居する file は、§2 の分類の順で shader になり、違えば CONDITIONAL ACCEPT 側に入る。凍結した分類の文言どおりで違反ではない。AC4 が複数 source の file と違った file を source の一覧付きで列挙するので、判定 C はその一覧で同居の有無を読む。
  - §2 (a): `CleanBuildCache` の build が cache を使わなかったことは log の行の抽出までで、自動の判定は無い。AC2 の文言どおり（取れなければ結論に付記）で、判定 C が record を読んで確かめる。
- 後続スライスへ移送する findings: なし。
- 実行したテスト: なし（Unity は起動していない。静的レビューのみ）。
- 未確認事項: 実際の `ContentLayout.json` の対応づけ、`CleanBuildCache` の log、別 process の fixture の一致、例外時の後始末、AC1〜AC7 の record の充足（全て判定 C の実行で確かめる）。
- 担当: Codex CLI（read-only sandbox、新規 session）。DC-02 の重さは、凍結した分類の文言を示して同じ session に再確認させ、blocker から外した。

### 7.2 発見 C 2 巡目（対象 implementation head `0b72f0ff`）

- 種別: 発見（差し戻し 1 巡目の確認）。入力は `df21b817` から `0b72f0ff` の `unity/` の修正差分、head の spike folder、本書（head の版）。
- 結果: DC-01・DC-03 は解消。修正差分に新しい凍結条件・常時契約の違反は無い。現在の問いを阻害する指摘 0 件で、`0b72f0ff` を GO 候補 head とする。`contract-audit` は errors 0・warnings 0。
- 後続スライスへ移送する findings: なし。
- 履歴の整理: 未公開の commit は実装・発見 C の記録・修正の 3 つで、レビューの単位を超えないので整理しない。
- 担当: Codex CLI（1 巡目と同じ session を再開）。

### 7.3 判定 C 1 回目（対象 implementation head `0b72f0ff`、local tag `evidence/content-file-identity-spike/1`）

- 種別: 判定。実行担当は進行役の session（判定の判断をしない。§5 の凍結済み 3 コマンドだけを順に、別々の Unity process で sandbox 外から実行）。判断担当は Codex CLI（read-only、新規 session）で、bundle を Storage から hash 照合で取得した写しだけを読んだ。
- 実行したコマンドと結果（XML）: (1) `FileInventoryTests` total 13・failed 0・`Passed`。(2) `IdentityBuilds.FirstProcess` total 1・failed 0・`Passed`（約 8 分）。(3) `IdentityBuilds.SecondProcess` total 1・failed 0・`Passed`。3 つとも `step.json` は `passed`・exit 0。各実行の後に Unity の process は残らず、終了時の stall は起きなかった。15 build 全てに `build-end` がある。
- 結論: **未判定**（判定定義の 1 番目、AC2「別 process の source が第 1 process と一致しない」）。
- 現在の問いを阻害する findings:
  - **EC-01**（AC2）: 第 2 process で作り直した `Assets/OsmSpikeCfi/LitEmissive.mat` の sha256 が第 1 process と違う。`.meta` の sha256 と GUID は一致し、import 後の hash も変わった。他の fixture の source は一致。戻し先は B（§4 の fixture の決定性）。原因は未確認。
- AC ごとの読み（EC-01 を除く。head が変わるので次の判定で読み直す）: AC1・AC3〜AC7 に凍結条件の違反は見つからなかった。AC3 の違った file は shader（URP の `Lit`）と script をまとめた file だけ。AC6 の数値と transport manifest の和集合の一致も record にある。
- 未確認事項: `CleanBuildCache` の build の log に cache を使わなかったことを示す行が無く、増分 cache の関与は不明（AC2 の文言どおり結論に付記する事項で、合否ではない）。

### 7.4 発見 C 3 巡目（対象 implementation head `bb4efe77`）

- 種別: 発見（判定 C 1 回目の差し戻しの確認）。入力は `0b72f0ff` から `bb4efe77` の `unity/` の修正差分、head の spike folder、本書（B の記録 commit の版）。
- 結果: 現在の問いを阻害する指摘 0 件。YAML 文書を固定後の fileID の順に並べ直す修正は、乱数の fileID に由来する文書順の差を除き、重複 ID は並べ替えの前に止める。`LitEmissive` の `RealtimeEmissive` は §3 の「emission の keyword」に fixture を合わせる修正。どちらも §4 の B 適応（fixture の決定性、keyword の選び方）の範囲で、§3 の参照の形と判定の語を変えない。`contract-audit` は errors 0・warnings 0。`bb4efe77` を GO 候補 head とする。
- 後続スライスへ移送する findings: なし。
- 担当: Codex CLI（1 巡目と同じ session を再開）。

### 7.5 判定 C 2 回目（対象 implementation head `bb4efe77`、local tag `evidence/content-file-identity-spike/2`）

- 種別: 判定。実行担当は進行役の session（§5 の凍結済み 3 コマンドだけを順に、別々の Unity process で sandbox 外から実行）。判断担当は Codex CLI（read-only、新規 session）で、bundle を Storage から hash 照合で取得した写しだけを読んだ。
- 実行したコマンドと結果（XML）: (1) `FileInventoryTests` total 13・failed 0・`Passed`。(2) `IdentityBuilds.FirstProcess` total 1・failed 0・`Passed`。(3) `IdentityBuilds.SecondProcess` total 1・failed 0・`Passed`。15 build 全てに `build-end` がある。
- **(2) の Unity の終了 stall:** XML の完成後、Unity の process が終わらなかった（thread 1 本で CPU 時間が増え続けた。`tools/Harness/README.md` の既知の native 終了 stall と同じ形）。約 6 分 30 秒待ってから、実行担当がその PID だけを `Win32_Process.Terminate` で止めた。Terminate は終了コードを 0 にするので、(2) の `step.json` の passed・exit 0 は自然終了の証拠ではない。この事実は bundle の `executor-note.md` に置いた。§5 のゲートは XML と record の終了行で、process の自然終了を含まないので、判断担当はゲートの成否を変えないと判断した。停止規則に従い、dump も原因調査もしていない。(1) と (3) は自然に終了した。
- 結論: **CONDITIONAL ACCEPT**。結論に付ける文: 対象はこの PC、Unity 6000.6.0f1、StandaloneWindows64、loose file、fixture に限る。texture・material・prefab の file は hash でまとめられる。build ごとに別の file になったのは shader（URP の `Lit`）と script をまとめた file。`SharedShader` は違っておらず、別の file にも入っていない。**名前ではなく sha256 でだけまとめられる**（`BuildManifestHash.txt` は同じ名前で中身が build ごとに違う）。`CleanBuildCache` の build の log に cache を使わなかったことを示す行が無く、増分 cache の関与は不明。N1 は該当なし。bytes は fixture の比率で、実 project の代表値ではない。
- AC ごとの読み: AC1〜AC7 を全て満たす。AC2 の 5 対に違った file・別の file に入った source asset は無く、2 つの process の source の sha256・GUID と import 後の hash も一致した。AC3 で違った file は `S`–`A` で shader 1 件、`S`–`B`・`S`–`AB`・`A`–`B`・`A`–`AB` で shader と script 各 1 件、`B`–`AB` は 0 件、別の file に入った source asset は 0 件。AC4 の未知の file は 0 件。AC5 の各 round は想定した変更だけで、`A-back` は 0 件。AC7 の後始末と設定の前後一致を満たす。
- AC6 の数値（fixture の比率）: `A` と `B` の合計 2,521,007 bytes、sha256 の和集合 1,574,899 bytes（hash でまとめて減る量 946,108 bytes）。違った file は shader 5 件 1,679,044 bytes、script 4 件 7,984 bytes。`P1`・`P2` の transport manifest の `sha256` で計算した和集合と棚卸しの和集合は同じ（1,171,553 bytes）。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし（問い 3 と U-3 の判断は §2.3・§2.5 のとおり）。
- 未確認事項: 増分 cache の関与（上記）。

### 7.6 発見 C 4 巡目（C' 1 巡目の差し戻しの確認。対象 implementation head `f632707a`）

- 入力は `bb4efe77` から `f632707a` の `unity/` の修正差分、head の spike folder、本書（B の記録 commit の版）。
- 結果: §8.1 の CP-01〜CP-03 は解消。CP-01 の修正は凍結した分類の規則を両側の build の file に当てるだけで、判定の語と判定定義の意味を変えない。種類ごとの比べた file の数は左の種類で数えるが、0 件で未判定にする対（`S`–`A`・`S`–`B`・`S`–`AB`）では `S` が常に左なので、中核の 3 種と shader の件数は正しい。足した 2 つの test は右にだけ Scene が加わると N4、prefab が加わると N2 になることを確かめる。修正差分に新しい違反は無い。`contract-audit` は errors 0・warnings 0。`f632707a` を GO 候補 head とする。
- 担当: Codex CLI（発見 C の session を再開）。

## 8. Phase C'

### 8.1 C' 1 巡目（最終 C'。対象 implementation head `bb4efe77`）

- 担当方式: AI。cursor-agent CLI の `grok-4.7-high`（`--mode=ask`、読み取りだけ）で新規 session を 2 つ。論点を分けた（job 1: code と凍結計画、job 2: 証拠と結論の導き直し）。
- 入力: C' 用の blind bundle（A3 凍結版の本書、Phase B の最初の実装報告、`docs/handoff/` を除いた base..head の diff、head の source、判定 C 2 回目と同じ 3 実行の生の出力と record、coordinator の出力、`contract-audit`）。Storage から hash 照合で取得した写しを workspace にした。発見 C・判定 C の結論と指摘、§6 の差し戻し修正の記述は入れていない。ただし head の code のコメント 1 箇所が過去の差し戻しの記号（EC-01）に触れている（code の一部なので除いていない）。
- 判定（job 2 の導き直し）: 判定定義の順に読むと CONDITIONAL ACCEPT。§5 のゲートは満たし、run 2 の強制終了は §5 の条件（XML と終了行）に入らないのでゲートを変えない。
- 現在の問いを阻害する findings（B へ差し戻し）:
  - **CP-01**（§2 の判定定義 N2・N4、種類の事前の分類）: 両方の build にある id の sha256 が違う時、種類と source の一覧を左の build の file だけから決める（`FileInventory.cs` の `Compare`。`sample = (l ?? r)![0]`）。右の build で同じ id に中核の asset か `.unity` が加わっていても、左が script か shader なら N2 / N4 に入らず CONDITIONAL ACCEPT になりうる。今回の証拠では該当する対の両側が script で、結論は変わらない（job 2 と同じ読み）。修正: 両側の source で種類を決め、どちらかが中核なら N2、Scene か `その他` なら N4 に入れ、source は両側の和集合を記録する。合成の例を `FileInventoryTests` に足す。
  - **CP-02**（AGENTS.md の偽 null の契約）: `IdentityBuilds.cs` の直接 build の失敗の message が、`BuildReport`（`UnityEngine.Object`）に `report?.summary` を使う。修正: `== null` の分岐で message を作る。
  - **CP-03**（AC7 の `git status --porcelain` の前後一致）: `git` が失敗した時は前後が同じ失敗の文字列になり、一致と読まれる。修正: 取得に失敗したら AC7 を未判定にする。
- 阻害しない findings: record の `head` は実行時の HEAD（`c9d65b3a`）で、bundle の implementation head（`bb4efe77`）と違う。`c9d65b3a` は `bb4efe77` に本書だけを足した記録 commit なので、実行した code は implementation head と同じ。
- 後続スライスへ移送する findings: なし。残存リスクとして job 1 が挙げたもの: `LitEmissive` と `OnlyB` は分類で `その他` になり、build の間で変われば N4 になる。fileID の置換が GUID の 16 進の列に数字として一致しうる（今回の layout に参照が壊れた形跡は無い）。出力の直下に directory があると未知の file として未判定になる（今回の出力は平坦）。
- 監査できなかった範囲: command を実行していない（読み取りだけ）。`out/` の全 file の sha256 の再計算と、全 source の行の全文比較はしていない。
- 独立性: Phase A（Claude・Codex）、B（Claude）、C（Codex）のどれにも関与していない系列。発見 C・判定 C の結論を読んでいない。

## 9. Phase D

未着手。
