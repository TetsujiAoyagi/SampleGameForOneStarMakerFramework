# build をまたぐ content file の同一性の実証

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §5.3 の Stage II の前提の確認。§8 の S2-a より前に置く）
- status: `A`（A2 入力版、revision 0）
- branch: `claude/content-file-identity-spike`（PR の base は `develop`）
- implementation base commit: `f8ffd023`（`develop` の head、2026-11-11。§1 の実測はこの base）
- implementation head commit: `未固定`
- risk: `normal`（製品 code を変えない。build だけで、Play も登録もしない）
- owner: root（依頼者）
- created: 2026-11-11
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: program §2.4「Unity の複数 Content Directory」と §5.3（build をまたぐ file の同一性、組み合わせで変わる file の種類、重複の数値、Q3 の要否）、§8（本スライスの行と S2-a の再開条件）。実験 code は製品に残さず git 履歴に置く（S0-b と同じ）。program の本文は Phase A では変えない（保留中の S2 のブランチが §8 を編集済みで、同じ行の衝突を避ける）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§2.4、§5.3、D10）。保留中の S2 計画 `CONTENT_PACKAGE_DISCOVERY.md` はブランチ `claude/content-package-discovery` にあり、本ブランチには無い（本スライスの結論が再開条件）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー）と A3（統合）。凍結は owner の確認の後。
  - 次の作業: A2 の指摘を §5.1 に分類し、§2.5 の裁定を owner から受けて A3 凍結版を commit する。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結前に Phase B を始めない。目的・利用者の 2 欄を変えたら目的レビューを 1 回やり直す。
  - 引き渡さないもの: なし。

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
- 成果の範囲: 結論は「この PC、Unity 6000.6.0f1、StandaloneWindows64、loose file、fixture」に限る。record の先頭にそれを書き、別の PC・版・target・実データへ広げない。build を作り直す必要があるか（依存先が変われば、それを含む directory は build し直さないと新しい複製を持たない）と、作り直した結果のうち配り直す file がどれか、は別の問いで、本スライスが測るのは後者である。
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
- 判定定義（列挙に当たらない結果は NO-GO。環境の不調、harness の不備、record の欠落で AC の観測が無い時は判定せず未判定として B へ戻す）。3 つとも証拠が揃えばスパイクの正常な終了である:
  - **GO（前提は成立）:** §2.1 と §2.2 を全て満たし、build の間で違った file が root 自身と manifest だけである。
  - **CONDITIONAL ACCEPT（条件付きで成立）:** AC1・AC2・AC5・AC6・AC7 を満たし、AC3 が texture・material・prefab・variant の無い shader について成立し、build の間で違った file が全件、root 自身、manifest、script をまとめた file、built-in の file、variant を持つ shader のどれかに帰属する（AC4）。意味は「共通の asset の file は hash でまとめられる。ただし列挙した種類は build ごとに別の file になる」。その種類ごとの bytes（AC6）を見て、配布側で足りるかを owner が決める（§2.5 U-3）。
  - **NO-GO（前提は不成立）:** 同じ source の再 build で file が変わる（AC2）、texture・material・prefab・variant の無い shader の file が root 集合で変わる（AC3）、依存先の変更で参照元の file が変わる（AC5）、帰属できない差がある（AC4）、のいずれか。program §5.3 に従い、F1〜F3 のままの S2 か縮退かを owner に返す。
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
  - 探索した境界: 手元の `ContentLayout.json` の上位の key（`SerializedFiles`、`RootAssets`、`LoadableObjectIds`、`LoadableSceneIds`、`BinaryArtifacts`）。未確認: `BinaryArtifacts` の欄の形。
- **I-3 製品を変えない（S0-b と同じ）:** §3 の新規 file と本書以外の変更が無く、実験は assets・設定・build の出力を repo に残さない。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) `Assets/OneStarMakerGenerated` の残り。(c) Build Settings、active target、shader の設定（Graphics Settings の variant の設定）を変える。(d) 2 つの process の間に fixture を残す設計。

**成立条件（起きなければならないこと）**

- **E1 build の行列:** §3 の fixture から、§2.2 の全 build が 2 回の実行（別の Unity process）でできる。
- **E2 棚卸し:** 全 build の全 file について、名前、size、sha256、帰属（source asset か種類）が record にある。
- **E3 比較:** 問い 1（同じ source の再 build、別の root 集合）と問い 2（粒度）と変更の波及について、判定定義のどれに当たるかが record から一意に決まる。
- **E4 数値:** 重複の量と、組み合わせで変わる file の量が bytes で出る。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id、head、Unity の版、target、process の別を持ち、build ごとに終了行がある（無い build は未判定）。build は全て loose file（`Uncompressed`、`BuildContentOptions.None`。`CleanBuildCache` を付ける build だけ明記）で、出力先は build ごとに別にする。build の名前は §3 の表による。

- **AC1（E1、E2、I-1）fixture と棚卸し:** fixture は §3 の条件（`.resS` が出る大きさの texture、variant の無い自作 shader、URP の `Lit` を keyword の違う 2 material で使う、script の集合が build で違う）を満たし、各 build の record に source の file と `.meta` の sha256、GUID がある。全 build の全 file に名前・size・sha256 がある。**同じ名前の file は全 build を通じて同じ sha256 で、同じ sha256 の file は同じ名前である**（名前が内容の hash であることの確認。破れは NO-GO ではなく、名前でなく sha256 で以降の比較を行い、破れを record に残す）。build `S` に `.resS` が 1 つ以上ある。
- **AC2（E3、I-1）同じ source の再 build:** root 自身の file と manifest を除く全 file が、名前と sha256 で次の組の間で一致する。`A` と `A-same`（同じ引数、別の出力先）。`A` と `A-name`（`name` と root の identity だけ別）。`A` と別 process の `A-clean`（`CleanBuildCache`）。`B` と別 process の `B-clean`。製品の adapter を無改変で通した `P1` と `P2`（identity が別）、および `P1` と `A`（root の entry が同じ集合の時）。別 process の build の source の sha256 と GUID が第 1 process と一致する（一致しなければ未判定）。
- **AC3（E3）別の root 集合:** `S`（共通物だけ）の file のうち、root 自身・manifest・AC4 で帰属が script / built-in / variant を持つ shader となった物を除く全 file が、`A`・`B`・`AB` に同じ名前・同じ sha256 で存在する。`B` には `A` に無い file があり、`A` には `B` に無い file がある（組み合わせが実際に違うことの確認）。共通物 4 種（texture、material、prefab、variant の無い shader）それぞれについて、帰属する file の一覧と一致・不一致が record にある。
- **AC4（E2、E3、I-2）粒度と帰属:** 各 build の `ContentLayout.json` を `BuildManifestHash` で対応づけ、全 file を source asset の数で分類する。source asset を 2 つ以上持つ file と、同じ id で build の間に内容 hash が違う file を全件、source asset の一覧と size 付きで列挙する。共通物 4 種の asset が、他の source asset と同じ file に入っていない（入っていれば、その file が build の間で変わるかを AC3 で読む）。build の間で違った file は全件が帰属を持つ。帰属できない file が 1 件でもあれば未判定ではなく NO-GO とする（harness が `ContentLayout.json` を読めない時は未判定）。
- **AC5（E3、I-1）変更の波及:** 第 1 process の最後に、(1) 共通の texture の pixel を変えて `A` と `B` を build し直す。texture に帰属する file の名前が元と変わり（変わらなければ round は無効で未判定）、それを参照する material・prefab・Scene に帰属する file は元と同じ名前・同じ sha256 である。(2) texture を戻し、共通の material の色を変えて `A` を build し直す。material の file だけが変わり、prefab・Scene・texture・shader の file は変わらない。(3) material を戻して `A` を build し直すと、全 file が最初の `A` と一致する。
- **AC6（E4）数値:** record に次がある。各 build の合計 bytes。`A` と `B` の file を名前で和集合にした bytes と、単純な合計との差（hash でまとめた時に減る量）。`AB`（1 build）の bytes との差。build の間で違った file の種類ごとの件数と bytes。`ContentTransportManifest.files` の `sha256` で同じ計算ができること（製品の manifest の欄で足りるかの確認。`P1`・`P2` の transport manifest があれば実値で、無ければ file の sha256 からの計算でよい）。
- **AC7（I-3）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書だけである。`pwsh tools/contract-audit.ps1` が合格する。2 回の実行それぞれの前後で `git status --porcelain` が一致し、fixture の folder、`Assets/OneStarMakerGenerated`、Build Settings、active target、Graphics Settings に変化が無い。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 問い 3: 同じ file を持つ 2 directory を登録した時のメモリ上の実体の数、`Loadable` の解決先、片方の登録解除の影響 | GO / CONDITIONAL ACCEPT の時だけ要る。推奨は保留中の S2-a を新しい revision で再開し、その AC6（直接参照の共有の実測）に足す（§2.5 U-2）。NO-GO なら不要 |
| 問い 4 の実装: 配布・保存を hash でまとめる transport・cache・索引の形 | S2-b / S4（本スライスは AC6 の数値と §1 の現況までを入力として残す） |
| 別の PC・別の `Library`・別の版・別の target の間の同一性 | CI が publish を始める時の後続（program §8「外部依存の gate」） |
| variant を持つ shader を build の間で同じにする方法（variant の集合の固定等） | CONDITIONAL ACCEPT で量が問題になった時に owner が切る後続 |
| 実データでの重複の量 | S2-b の前に owner（読み取りの集計で足りる） |

### 2.4 スライス分割の判断

契機: owner の 4 つの問いのうち、1・2（build だけ）と 3（Play と登録）は独立して失敗しうる未知で、3 の要否は 1・2 の答えで決まる。**分割する。** 本スライスは 1・2 と、そこから計算だけで出る 4 の数値に限る。3 を同じスライスに条件付きの受け入れ条件として入れると、凍結した条件の適用が結果に依存し、判定 C が何を判定するかが head で決まらない。3 は Play の往復と登録の harness が要り、保留中の S2-a の fixture と AC6 が既にその形を持つので、作り直さずそちらへ足すのが安い。

### 2.5 未決事項（owner へ）

- **U-1 gate の切り方:** 本スライスを問い 1・2（と 4 の数値）に限り、問い 3 を結果の後に決める形でよいか。推奨: よい（§2.4）。
- **U-2 問い 3 の置き場:** GO / CONDITIONAL ACCEPT の時、問い 3 を (a) 保留中の S2-a の新しい revision に足す、(b) 独立の小さなスパイクにする、のどちらにするか。推奨: (a)。本スライスの Phase D で確定すれば足りる。
- **U-3 CONDITIONAL ACCEPT の受け入れ:** 予備の観察では script・built-in・`Lit.shader` が build ごとに変わっていた。この形で終わった時に「配布側で足りる」とするかは、AC6 の bytes を見て owner が決める。本スライスは閾値を置かない。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentFileIdentity/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし）に置く。寿命は 1 回の test、依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。予想の合計は約 900 行で、行数の警報は無い。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `FileInventory.cs` | build 1 件の棚卸しの型（file の名前・size・sha256・帰属、source の sha256 と GUID）と、棚卸し同士の比較（一致・不一致・片方だけ、名前と sha256 の対応、和集合の bytes）、判定定義への写像 | 220 | Unity 非依存の pure。AC1〜AC6 の比較の規則 |
| `FileInventoryTests.cs` | 上の比較と写像の EditMode test。合成の棚卸しで §2 の (b)(e)(f)(g) の誤った YES が YES にならないことを含む | 200 | `[Explicit]` なし |
| `IdentityFixture.cs` | fixture の asset の生成（`.meta` の GUID を固定し、内容を決定的にする）、変更 round の書き換えと復元、削除 | 200 | Unity / `AssetDatabase` の I/O |
| `IdentityBuilds.cs` | build の行列の実行（直接の `BuildPipeline.BuildContentDirectory` と製品の adapter）、出力の走査と sha256、`ContentLayout.json` の対応づけと帰属、JSONL の record | 280 | `[Explicit]` の EditMode test `FirstProcess` / `SecondProcess` |

fixture（SampleGame を使わない。D10。一時 folder `Assets/OsmSpikeCfi/` に作り、各 process の終わりに消す）:

- 共通物: `SharedTex`（`.resS` が出る大きさ）、`SharedShader`（variant の無い自作 shader）、`SharedMat`（`SharedShader` と `SharedTex`）、`LitPlain`（URP の `Lit`、追加の keyword なし）、`SharedPrefab`（`SharedMat` と `LitPlain` の Renderer、Runtime の既存の component 1 つ）。
- 固有物: Scene `SceneA`・`SceneB`（どちらも `SharedPrefab` を直接参照）、`LitEmissive`（URP の `Lit` に emission の keyword、`SharedTex`）、`OnlyB`（`LitEmissive` と、`SharedPrefab` とは別の Runtime の既存の component を持つ prefab）。
- root は製品の `BuildContentRoot`（無改変）を使い、Scene は `LoadableSceneId`、Object は `Loadable<Object>` で持つ。新しい Runtime の型は足さない。

| build | root の中身 | 備考 |
|---|---|---|
| `S` | `SharedPrefab` | 共通物だけ |
| `A` | `SceneA` | |
| `A-same` | `SceneA` | `A` と同じ引数、別の出力先 |
| `A-name` | `SceneA` | `name` と root の identity だけ別 |
| `B` | `SceneB`、`OnlyB` | `Lit` の keyword と script の集合が `A` と違う |
| `AB` | `SceneA`、`SceneB`、`OnlyB` | 1 build にまとめた基準 |
| `P1`・`P2` | `SceneA` | 製品の `UnityContentDirectoryAdapter` を無改変で通す。identity が別 |
| `A-tex`・`B-tex`・`A-mat`・`A-back` | `A` / `B` と同じ | AC5 の変更 round |
| `A-clean`・`B-clean` | `A` / `B` と同じ | 別 process、`CleanBuildCache` |

## 4. 実装計画

- 順序: (1) pure の 2 file。(2) fixture。(3) build の行列と record。(4) 実行は Phase C。
- 対象外を維持する方法: 製品 file を変えない（AC7）。record と build の出力は `TestResults/` 下の一時 root（git 管理外、path 予算内）に置く。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る。製品の adapter を test から無改変で呼べない（`P1`・`P2` が作れない）。`ContentLayout.json` から帰属が取れず、代わりの帰属の方法が要る。
- B 適応の範囲: fixture の asset の細部（keyword の選び方、component の選び方）、record の項目名、build の順序、`SecondProcess` が第 1 process の record を読む方法。

## 5. テストとレビュー計画

- 単体テスト: `FileInventoryTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.FileInventoryTests`。
- 判定必須テスト（GO 候補 head）: 次の 3 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.FileInventoryTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.IdentityBuilds.FirstProcess`。(3) `-Filter OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity.IdentityBuilds.SecondProcess`。各 XML の total が 1 以上で failed 0、root result `Passed`、record に §3 の全 build の終了行がある。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC7）、実験 test は通常の回帰に混ぜない。代替証拠: AC7、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 3 コマンドで閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64 Player、作業ツリーが clean。観測は record と XML。目視は無い。証拠は record・XML・log・各 build の `ContentLayout.json` の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: batch の EditMode test から Content Directory を build する経路は既存の `BuildContentDirectoryIntegrationTests` が成立させている。**未確認:** `[Explicit]` の test を完全名の filter で選べること、build と `Library/BuildHistory` の `ContentLayout.json` を `BuildManifestHash` で対応づけること、`CleanBuildCache` の所要時間。初回確認は Phase C（理由: Unity テストと Build の実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録）。
- 人間の判断が必要な条件: なし（CONDITIONAL ACCEPT の受け入れは Phase D の後に owner。§2.5 U-3）。
- 高コスト証拠: なし（fixture の build は S0-b の実測で 1 回約 21〜25 秒。16 build で 10 分前後の見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。pure の比較の出力（`FileInventory` の写像）は補助で、判定 C は棚卸しの行から独立に確かめる。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: Claude。
- C' 用に予約した担当: Phase A に関与していないベンダー（`cursor-agent` は AGENTS.md の契約どおり）。

### 5.1 A2 の独立レビューと A3 の採否

未実施。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
