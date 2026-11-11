# 同じ file を持つ 2 つの Content Directory の登録の実証

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-0b。S2 `CONTENT_PACKAGE_DISCOVERY` の再開条件）
- status: `A`（A1 初稿。未 commit）
- branch: `claude/content-shared-file-registration-spike`（PR の base は `develop`）
- implementation base commit: `aa6b9488`（`develop` の head、2026-11-12。§1 の現況はこの base）
- implementation head commit: 未固定
- risk: `normal`（製品 code を変えない。Editor の Play の中で、製品の native 層の wrapper を 2 つ使うだけ）
- owner: root（依頼者）
- created: 2026-11-12
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「確かめていないこと」の該当行を実測の事実へ置き換える。または登録の事実の節を足す）、program §2.4「Unity の複数 Content Directory」・§5.3・§8（本スライスの行と S2 の再開条件）。実験 code は製品に残さず git 履歴に置く（S0-b・S2-0 と同じ）。program の本文は Phase D まで変えない
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§2.4、§5.3、§8 の S2-0 の移送、§9 の G11・G24）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（S2-0 の実測と測り直しの harness の所在）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2。A2 入力版を commit し、A2 の独立レビュー 2 件（§5）を起動する。
  - 次の作業: A2 入力版を切り、目的レビューとアーキテクチャゲートを互いの指摘を見せずに依頼する。
  - 引き継ぐ commit: A2 入力版の commit（tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: §2 の判定定義と §4 の差し戻し条件に従う。
  - 引き渡さないもの: 目的レビューには §1 の「目的」と「利用者」の欄の原文だけを渡す（他の欄・初稿・UC の表を渡さない）。

## 1. 目的と対象外

- 目的: 中身が同じ content file を持つ 2 つの Content Directory を同じ Play の中で両方登録した時、Unity がその asset をメモリに何個載せるか、片方の登録解除がもう片方から載せた物を壊すかを、Unity 6000.6 の Editor の Play の実測だけで確かめる。その結果から、S2 が共有依存の置き場（共通の asset を共有 package に分けるか、各 package が複製を持ったまま登録に任せるか）をどう設計するかを、owner が S2 の中で決められるようにする。
- 利用者: OSMFramework の開発者（S2 の配布単位と共有依存を設計する owner と、後続で package の登録と解放を書く人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続）:
  - **共有依存の置き場の設計そのもの:** S2。本スライスは事実を出すだけで、どの結果でも S2 を止めない（§2 の「S2 への含意」）。
  - **2 つの directory のうちどちらの file を読むか**（片方の directory の file が disk から消えた時、もう片方から載せた物が読めるか）: S4（cache の掃除と install の失敗の設計で確かめる）。中身が同じ file は読んだ結果から区別できず、確かめるには片方の file を使えなくする操作が要る。S2 の判断に要る依存は、本スライスの登録解除（AC4 の U3・U4）でも表に出る。
  - 中身が違う同じ id の file（同じ asset を名乗るが bytes が違う複製）の扱い。S2-0 の実測では URP の `Lit` の shader と script をまとめた file が build の組み合わせでこうなる。本スライスの fixture でも現れうるので、現れたら観測として記録するが、問いの答えにも判定にも使わない（program §9 の G24。S2）。
  - Player での挙動: 後続（S2 か S3 の A1 で、Editor の結論を Player で確かめ直すかを決める）。
  - 製品の `ContentDirectorySession` を 2 つ持つこと、`InstallContentDirectory`・`ContentRevisionGate` の 2 件目の拒否の変更: S2 / S3（S0-b の移送のとおり、製品は 2 件目を拒否したまま）。
  - archive（`UseArchive`、圧縮）、別の PC・`Library`・Unity の版・target: S2-0 と同じく見ない。
  - 実データ（SampleGame）: 見ない。
- 現況（base `aa6b9488`）:
  - **S2-0 の実測（[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)）:** 別々に build した directory の間で、共通の texture・material・asset として参照される prefab の content file は同じ id・同じ名前・同じ bytes になる。`Lit` の shader と script をまとめた file は同じ id のまま bytes が変わる。名前（Unity の内容 hash）と sha256 は fixture の全 build で一対一だった。
  - **Unity の公開文書（6000.6、2026-11-12 に確認）が言うこと:** 登録中の directory は登録の逆順で探索される（後から登録した物が先、stack のように）。`GetRootAssets<T>()` は全 directory から、handle を渡すと 1 つの directory から root を返す。登録解除は、その directory から載せた各 asset に `Loadable.Release` を呼び、各 Scene を unload した後に行う。file が開いている間に登録解除すると Unity は error を log に出す。Editor の例は Play と Player の両方で登録している。各 build は自己完結し、同じ asset を参照する 2 build はそれぞれ複製を持つ。出力 file は内容の hash で名付けられ、複数の build に現れる同じ file 名は build をまたいで重複した内容を示す。
  - **公開文書が言わないこと（本スライスが測る）:** directory A の root に serialize された `Loadable` が A の中だけで解決されるか、全 directory の逆順の探索で解決されるか。同じ file 名が 2 つの登録中の directory にある時の扱い（警告の有無を含む）。同じ asset を 2 つの directory から載せた時にメモリ上で 1 つか 2 つか（2 つになると書くのは AssetBundle の節だけ）。登録解除が既に載った物をどうするか。program §2.4 の「直接参照の依存は元の directory に閉じ、同じ asset を別 directory に入れると重複ロードされうる」は、公開文書の文としては確認できなかった（仮説として扱う）。
  - **API（`UnityEngine.ContentLoadModule` 6000.6 の XML doc）:** `ContentLoadManager.RegisterContentDirectory(path)` は handle を返し、`UnregisterContentDirectory(handle)`、`GetContentDirectories()`（登録順の handle）、`GetRootAssets<T>(handle)` がある。`Loadable<T>` は `LoadableObjectId`・`LoadAsync`・`Release`・`Status`・`Target` を持つ。どの directory から解決したかを返す API は無い。
  - **製品の native 層:** `UnityContentDirectoryBackend`（`Runtime/BuildContent/UnityContentDirectoryBackend.cs`、133 行、internal）は 1 つの handle を包み、`Register`・`GetRoots`・`Unregister`・`LoadObjectAsync`（root の `BuildContentEntry` の `Loadable<Object>` を `LoadAsync`）・`LoadSceneAsync`（`LoadableSceneId`）・`Release` を持つ。2 件目の拒否は、この層ではなく `AssetManagement.InstallContentDirectory` と `ContentRevisionGate` にある。
  - **前例:** S0-b（PR #172、commit `94b0c4a6` に実験 code）は、EditMode の `[UnityTest]` から Play に入り、native 層で 2 つの directory を直接登録し、同時 load・unload と再 load・取消の回収が成立した。2 つの directory は別の内容で、同じ file を共有していない。Editor での登録には type tree が要る（type tree の無い directory は例外で拒否された）。S2-0（PR #177、commit `f632707a` に harness）の fixture は、`.meta` の GUID を名前から固定し、別の process で作り直しても同じ bytes になる。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力: `なし`（開発時の宣言も実行時の操作も無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 中身が同じ content file を持つ 2 つの Content Directory を両方登録した時、(Q1) Unity はその asset をメモリに何個載せるか、(Q3) 片方の登録解除は、もう片方から載せて保持している物を壊すか。問いの番号は program §8 の 3 つの問いに合わせ、Q2（どちらの file を読むか）は §1 の対象外として S4 へ送った。
- 仮説（owner の予想。判定には使わない）: Unity は同じ file 名の重複を警告し、メモリに 2 つ載せる。本スライスは登録と load の間の Warning を全件記録して、この予想を確かめる。
- 成果の範囲: 結論は「この PC、Unity 6000.6.0f1、StandaloneWindows64、Editor の Play、loose file、fixture」に限る。record の先頭にそれを書き、Player・別の版・実データへ広げない。
  - 対象は中身が同じ file（両 directory で名前と sha256 が同じ file）だけ。中身が違う同じ id の file は観測だけ（§1 対象外）。
  - **測らないもの（結果を変えうる。record と harvest にこの一覧を付ける）:** Player、archive、3 つ以上の directory、Scene に置いた prefab instance、sprite atlas、`.resource`（audio・video）、同じ asset の bytes が違う複製、片方の directory の file が disk から消えた時、Domain Reload 有効の Play（本 project の既定は無効。S0-b は両方を測った）。
- どうすればこの問いに誤って答えるか（C と C' はこれを探す）:
  - (a) 2 つの「directory からの load」が実は同じ directory の root から来ている（AC2 の handle ごとの root 取得）。
  - (b) 数え方が重複を見えない（名前で数えるが、2 つ目が別名になる、または Editor の AssetDatabase の物が混ざる）。AC1 の正の対照（同じ名前で GUID の違う 2 つの texture が 2 つと数えられる）と、fixture の source を Play の前に消すこと。
  - (c) 前の round の物がメモリに残り、2 回目の load がそれを返す（round ごとの新しい Play と、round の開始時に fixture の名前の物が 0 件であること）。
  - (d) 「同じ file」のつもりが、両 directory で名前か sha256 が違う（AC1 の前提の確認）。
  - (e) Q3 で、偽 null を見落とす（`?.` や `is null` で判定する）か、object は残っているが中身（texture の pixel、material の texture 参照）が失われたのを見ない（AC4 の中身の確認）。
  - (f) Unity の Error・Warning の log をテストの失敗として握りつぶすか、捕捉せずに record に残さない（全 AC の log の捕捉）。
  - (g) 登録の順で答えが変わるのに、1 つの順でしか測らない（AC2・AC3・AC4 の両方の順）。
- 判定に使う語（実測の前に固定する）:
  - **共通物:** `SharedTex`（texture）、`SharedMat`（material、自作 shader と `SharedTex`）、`SharedPrefab`（prefab、`SharedMat` の Renderer）。**中核の 3 種**と呼ぶ。
  - **同じ file:** 両 directory の出力の直下にあり、名前と sha256 が同じ file。中核の 3 種のそれぞれについて、それが帰属する file が両 directory で同じ file であることを AC1 で確かめる。
  - **実体の数:** 1 つの Play の中で、ある共通物の名前と型を持つ `UnityEngine.Object` のうち、`Resources.FindObjectsOfTypeAll` で見える物の数。加えて、2 つの load の結果の `EntityId.ToULong(GetEntityId())` が等しいか（`GetInstanceID` は使わない）。
  - **壊れた:** 生き残る側から載せて保持している物について、次のどれか。`== null` になる。texture の `width` が 0 か pixel の sha256 が load 直後と違う（読める texture にしておく）。material の `mainTexture` が `== null` か別の `EntityId` になる。prefab の Renderer の `sharedMaterial` が `== null` になる。生き残る側の handle から同じ asset を Release して再 load できない。Scene の Renderer の material と texture が上と同じ条件を満たさない。
  - **拒否された:** 登録解除の呼び出しの間に Unity が Error か Exception を log に出したが、生き残る側の物は壊れていない。
  - **無傷:** 壊れた物も拒否も無い。
- 判定定義。スパイクの合否は「問いに証拠付きの答えが出たか」だけで決め、答えの向き（実体が 1 つか 2 つか、壊れるか壊れないか）は合否に使わない。
  1. **未判定（結論を出さない）:** 次のどれか。build を開始できない、終了行の無い round がある、中核の 3 種のどれかが両 directory で同じ file でない（AC1）、Play の round の開始時に fixture の source が AssetDatabase に残っている、round の開始時に共通物の名前の物が 1 件以上ある、正の対照が 2 つと数えられない（AC1）、AC2〜AC4 のどれかの round が無い、実体の数と `EntityId` の比較が食い違う。harness・fixture・環境の問題は B へ戻し、Unity の挙動が定義した語で読めない時は Phase A へ戻す。
  2. **GO（答えが出た）:** 未判定に当たらず、record から次が一意に読める。Q1: 中核の 3 種のそれぞれについて、Loadable からの load（AC2）と直接参照からの load（AC3）の、登録の順ごとの実体の数。Q3: AC4 の各 case が、壊れた・拒否された・無傷のどれか。2 つの directory の同時登録を Unity が拒否した時も、拒否と log が record にあれば GO とし、Q1・Q3 の答えを「同時登録できない」とする。
  - CONDITIONAL ACCEPT と NO-GO は使わない。
- **S2 への含意（判定ではない。結論に添えて owner へ渡す。S2 の設計判断は S2 の A1 で行う）:**

| Q1 / Q3 の答え | S2 が決めること |
|---|---|
| 実体が 1 つ、全 case が無傷 | 各 package が複製を持ったまま登録に任せる形を候補にできる（中身が同じ file に限る） |
| 実体が 2 つ（どの load の形・登録の順かを添える） | 共有 package に分けるか、複製のメモリを受け入れるか |
| どれかの case が拒否された | 登録の寿命を directory の間で結びつけるか、共有 package に分けるか |
| どれかの case が壊れた | 同上。加えて、壊れた case を製品の登録解除の順序で防げるか |
| 同時登録できない | 共有 package に分けるか、1 つの合成単位の build に限るか |

- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO で終了する。最低条件未達（未判定）のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 数え違いを答えにしない:** 実体の数は、同じ Play の中で、fixture の source も前の round の物も無い状態から数えた値で、正の対照で数え方が重複を見分けられると確かめた値に限る。
  - 崩しうる経路: (a) Editor の AssetDatabase が fixture の source asset を載せ、同じ名前の物が増える（source を Play の前に消す）。(b) Domain Reload 無効で、前の Play の static と native の物が残る（round ごとに新しい Play、開始時に 0 件を確かめる）。(c) `Resources.FindObjectsOfTypeAll` が、Unity が内部で作った同じ名前の物（例: material の instance）を拾う（数える型を固定し、`EntityId` の比較と突き合わせる）。(d) 2 つの root の取得が同じ directory を指す（handle ごとの `GetRootAssets`。§1 の製品 wrapper は handle ごと）。
  - 探索した境界: `UnityContentDirectoryBackend` と S0-b の native 層の使い方、6000.6 の XML doc。未確認: `FindObjectsOfTypeAll` が Content Directory から載った物を全て見るか（AC1 の正の対照で測る）。
- **I-2 中身が同じでない file で答えを出さない:** 問いの答えは、両 directory で名前と sha256 が同じ file に帰属する共通物だけから出す。
  - 崩しうる経路: (a) 共通物が他の asset と同じ file に入り、その file が build の組み合わせで変わる（S2-0 の AC4 と同じ確認を AC1 で行う）。(b) shader の file が違い、material の file だけ同じ（material の答えに shader の差が混ざりうる。観測として記録する）。(c) fixture の再生成で GUID が変わる（S2-0 の固定 GUID の生成を使う）。
  - 探索した境界: S2-0 の実測と harness。未確認: 中身の違う同じ id の file（script をまとめた file 等）が両 directory にある時、それが共通物の load に影響するか（観測。§1 対象外）。
- **I-3 製品を変えない（S0-b・S2-0 と同じ）:** §3 の新規 file と本書以外の変更が無く、実験は assets・設定・build の出力を repo に残さない。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) Build Settings・active target・Enter Play Mode の設定の変更。(c) Play の round が登録を解除せずに終わり、次の round か次の test に残る。

**成立条件（起きなければならないこと）**

- **E1 準備:** 共通物を含む 2 つの directory が build でき、中核の 3 種が両方で同じ file に帰属する。
- **E2 Q1:** 中核の 3 種のそれぞれについて、Loadable からの load と、Scene の直接参照からの load の両方で、実体の数が record にある。
- **E3 Q3:** 登録解除の各 case で、生き残る側の物の状態と Unity の log が record にある。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id、head、Unity の版、target、Enter Play Mode の設定、process の別を持ち、round ごとに開始行と終了行がある（終了行の無い round は未判定）。Play の round は全て新しい Play で行い、round の開始時に登録中の directory が 0 件であること（`GetContentDirectories`）と、共通物と正の対照の名前の物が 0 件であることを記録する。登録は製品の `UnityContentDirectoryBackend` を無改変で 2 つ持って行う（`ContentDirectorySession` と `AssetManagement` は使わない。§1）。Unity の Error・Exception・Warning の log は test の失敗にせず（`LogAssert.ignoreFailingMessages`）、全件を round と呼び出し（登録、load、登録解除）に結びつけて record に残す（§2 の仮説の確認を兼ねる）。

- **AC1（E1、I-1、I-2）準備と数え方:** 第 1 process が fixture（§3）を作り、directory `A`（`SceneA`、`SharedPrefab`、`TwinA`）と `B`（`SceneB`、`SharedPrefab`、`TwinB`）を build し、出力の全 file の名前・size・sha256 と、`ContentLayout.json` による中核の 3 種の帰属を記録し、fixture の folder を消して終わる（消したことを file system で確かめて記録する）。中核の 3 種のそれぞれが両 directory で同じ file に帰属する（そうでなければ未判定）。両方にある同じ id で sha256 が違う file を全件、種類付きで記録する（観測）。第 2 process の開始時に fixture の folder が無い。**正の対照:** `TwinA` と `TwinB` は名前と pixel が同じで GUID が違う texture で、それぞれ自分の directory の root から load すると、実体の数が 2、`EntityId` が別である（そうでなければ数え方が使えないので未判定）。**基準:** `A` だけを登録して `SharedPrefab` を load した時の中核の 3 種の実体の数が各 1 である。
- **AC2（E2、Q1）Loadable からの load:** 登録の順 `A`→`B` と `B`→`A` の各 round で、各 handle の root から `SharedPrefab` を load し、両方を保持したまま、中核の 3 種の実体の数と、2 つの load の結果の prefab・その `sharedMaterial`・その `mainTexture` の `EntityId` が等しいかを記録する。両 root の `SharedPrefab` の `LoadableObjectId` が等しいかを記録する（観測）。**観測（合否にしない）:** load の前後の `Profiler.GetTotalAllocatedMemoryLong` と、`SharedTex` の `Profiler.GetRuntimeMemorySizeLong`（`SharedTex` は差が見える大きさにする）。
- **AC3（E2、Q1）直接参照からの load:** 登録の順 `A`→`B` と `B`→`A` の各 round で、`SceneA`（`A` の `LoadableSceneId`）と `SceneB`（`B`）を additive に load し、両 Scene の Renderer の `sharedMaterial` とその `mainTexture` の `EntityId` が等しいか、中核のうち material と texture の実体の数を記録する。同じ round で続けて `A` の root から `SharedPrefab` を load し、Scene の material と `EntityId` が等しいかを記録する（Loadable と直接参照の混在）。
- **AC4（E3、Q3）登録解除:** 次の各 case を、指定した登録の順の新しい Play で行う。各 case で、登録解除の直前と直後に、生き残る側の物を「壊れた」の語の全項目で確かめ、登録解除の呼び出しの間の log を記録し、case を壊れた・拒否された・無傷のどれかに分類する。最後に生き残る側を Release して再 load し、全て Release・unload して生き残る側も登録解除し、`Resources.UnloadUnusedAssets` の後に共通物の実体の数が 0 であることを記録する（0 でなければ観測として残す）。
  - (U1) 順 `A`→`B`。`A` と `B` の root から `SharedPrefab` を load。`A` の物を Release して `A` を登録解除。生き残る側は `B`。
  - (U2) 順 `A`→`B`。`B` の root から `SharedPrefab` を load。`A` からは何も load せずに `A` を登録解除。生き残る側は `B`。
  - (U3) 順 `A`→`B`。`A` の root から `SharedPrefab` を load。`B` からは何も load せずに `B`（後から登録した側）を登録解除。生き残る側は `A`。
  - (U4) U3 を順 `B`→`A` で行う（`A` が後から登録した側になる。`B` を登録解除）。
  - (U5) 順 `A`→`B`。`SceneA` を load。`B` を登録解除。生き残る側は `SceneA`（Renderer の material と texture）。
  - 失敗経路の検証例: U3 は、後から登録した側の file が先に探索される（§1 の公開文書）ので、`A` の load が `B` の file を読んでいれば、`B` の登録解除で `A` の物が壊れるか拒否される経路である。U3 と U4 の分類が違えば、登録の順に依存した結果として結論に書く。
- **AC5（I-3）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書だけである。`pwsh tools/contract-audit.ps1` が合格する。2 つの process それぞれの前後で `git status --porcelain` が一致し、Build Settings、active target、Enter Play Mode の設定に変化が無い。各 process の終わりに、fixture の folder とその `.meta`、`Assets/OneStarMakerGenerated` が無いことを file system で確かめて record に残す。各 round の終わりに登録中の directory が 0 件であることを記録する。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 共有依存の置き場の設計（§2 の「S2 への含意」の各行の選択） | S2 の A1 |
| どちらの directory の file を読むか（片方の file が disk から消えた時に、もう片方から載せた物が読めるか） | S4（cache の掃除と install の失敗）。S2 が「片方の directory の file だけを install する形」を採る時は S2 の A1 で前倒しする |
| 中身が違う同じ id の file（`Lit` の shader、script をまとめた file）を両方登録した時の扱い | S2（program §9 の G24）。本スライスは観測だけを移送する |
| Player での挙動 | S2 か S3 の A1 で、Editor の結論を Player で確かめ直すかを決める |
| 製品の session を複数持つ形（`InstallContentDirectory`・`ContentRevisionGate`・revision lease・close の順序） | S2 / S3（S0-b の移送） |
| 3 つ以上の directory、登録解除と load の競合、取消 | S2 |
| 配布と保存を hash でまとめる実装 | S2 か S4 の A1（S2-0 の移送） |

### 2.4 スライス分割の判断

契機: Q1 と Q3 は独立して失敗しうる未知である。**分割しない。** 2 つとも同じ fixture、同じ 2 directory、同じ Play の往復で測れ、分けると build と harness を 2 回作る。owner は 2026-11-12 に program §8 の問いを 1 つの小スパイクにすると裁定している。A1 の検討で、program §8 の 3 つ目の問い（`Loadable` がどちらの複製に解決されるか）は、中身が同じ file では読んだ結果から区別できず、片方の file を使えなくする操作が要ること、S2 の判断に要る依存は Q3 の U3・U4 で表に出ることから、owner の了承（2026-11-12）を得て S4 へ送った（§2.3）。

### 2.5 未決事項（owner へ）

**owner の裁定（2026-11-12）:** U-2 を推奨どおりとする（Editor の Play だけで測り、Player は §2.3 の後続へ）。U-1（実体が 2 つ載る時の判定の向き）と U-3（Q2 の観測の方法）は、判定を答えの有無だけで決める形に改め、Q2 を S4 へ送ったことで不要になった（同日、owner の指摘による）。

- **U-2 Player:** Editor の Play だけで測る。Player の検証は数分の build と別の実行経路が要る。推奨: Editor だけ（§2.3 で後続へ）。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/SharedFileRegistration/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし。`OneStarMaker.Build.Content` と Runtime の internal に friend access で届くことは S0-b と S2-0 で確かめ済み）に置く。寿命は 1 回の test、依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。S2-0 の `IdentityFixture`・`FileInventory` の一部は commit `f632707a` から写して縮めてよい（B 適応。写した元を §6 に書く）。予想の合計は約 950 行で、500 行を超える file は無い。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `RegistrationVerdict.cs` | round の観測（実体の数、`EntityId` の一致、壊れた項目、log）を受け取り、AC ごとの合否、Q1・Q3 の答え、判定定義と「S2 への含意」の行への写像を返す。入力は文字列と数値だけで、Unity の型も製品の型も受け取らない | 180 | Unity 非依存の pure。§2 の語と判定定義 |
| `RegistrationVerdictTests.cs` | 上の写像だけの EditMode test。§2 の (b)(c)(e)(f)(g) の誤りが答えにならないこと（正の対照が 1 の時に未判定、round の開始時に残りがある時に未判定、`== null` でない物の中身が失われた時に「壊れた」、Error の log と無傷の物の組が「拒否された」、片方の順だけの時に未判定、実体の数と `EntityId` の食い違いで未判定）、答えの向きによらず GO になること（実体 1・2、無傷・拒否・壊れた、同時登録の拒否の各例）、各例が「S2 への含意」の正しい行に写ること | 200 | `[Explicit]` なし |
| `RegistrationFixture.cs` | fixture の asset の生成（固定 GUID、`SharedTex` は読める texture で差が見える大きさ）、所有の marker、開始時の残りの回収と終了時の削除 | 200 | Unity / `AssetDatabase` の I/O |
| `RegistrationBuilds.cs` | `[Explicit]` の `FirstProcess`。`A`・`B` の build、出力の棚卸しと sha256、`ContentLayout.json` による中核の 3 種の帰属、JSONL の record、fixture の削除 | 180 | build と file system |
| `RegistrationRounds.cs` | `[Explicit]` の `[UnityTest]` `SecondProcess`。AC1 の正の対照と基準、AC2〜AC4 の round を新しい Play ごとに行う。`UnityContentDirectoryBackend` を 2 つ持ち、観測と log を record に書く。Play の待ちは `EnterPlayMode` / `ExitPlayMode` の yield と load の await | 220 | 製品への依存はこの file と `RegistrationBuilds.cs` だけ |

fixture（SampleGame を使わない。一時 folder `Assets/OsmSpikeSfr/` に marker と一緒に作り、第 1 process の終わりに消す）:

- 共通物: `SharedTex`（読める texture。`Profiler` で差が見える大きさ）、`SharedShader`（variant の無い自作 shader。URP の `Lit` を使わず、shader の file が build の組み合わせで変わらないようにする）、`SharedMat`（`SharedShader` と `SharedTex`）、`SharedPrefab`（`SharedMat` の Renderer。Runtime の既存の component 以外を持たない）。
- 固有物: Scene `SceneA`・`SceneB`（prefab instance を置かず、Scene 内の Renderer が `SharedMat` を直接参照する）、`TwinA`・`TwinB`（正の対照。同じ名前・同じ pixel・別の GUID の texture）。
- root は製品の `BuildContentRoot`（無改変）。Scene は `LoadableSceneId`、Object は `Loadable<Object>`。新しい Runtime の型は足さない。
- script をまとめた file は両 directory で中身が違いうる（S2-0）。共通物に自作の script を持たせないことで、中核の 3 種の答えから切り離す。

## 4. 実装計画

- 順序: (1) pure の 2 file。(2) fixture と build。(3) Play の round と record。(4) 実行は Phase C。
- 対象外を維持する方法: 製品 file を変えない（AC5）。build の出力と record は `TestResults/` 下の一時 root（git 管理外、path 予算内）に置く。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る。`UnityContentDirectoryBackend` を 2 つ持って同時に登録できない（製品の wrapper では足りず、`ContentLoadManager` を直接呼ぶ必要がある時も、根拠を書いて Phase A へ返す。Unity 自身が同時登録を拒否した時は差し戻しではなく GO の「同時登録できない」）。中核の 3 種を両 directory で同じ file にする fixture が §3 の形で作れない。§2 の語で数えられない（`FindObjectsOfTypeAll` が Content Directory の物を見ない等）。
- B 適応の範囲: fixture の asset の細部（大きさ、component の選び方）、record の項目名、round の順序、`SecondProcess` が第 1 process の record と出力を読む方法、S2-0 の harness から写す範囲。

## 5. テストとレビュー計画

- 単体テスト: `RegistrationVerdictTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationVerdictTests`。
- 判定必須テスト（GO 候補 head）: 次の 3 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationVerdictTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationBuilds.FirstProcess`。(3) `-Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationRounds.SecondProcess`。各 XML の total が 1 以上で failed 0、root result `Passed`、record に全 round の終了行がある。**process の自然終了をゲートにしない**（S2-0 の移送。XML の完成後に Unity が終わらない既知の終了 stall は、`tools/Harness/README.md` の規則に従い、XML と record の終了行で判定する）。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC5）、build と Play を行う `[Explicit]` の実験 test は通常の回帰に混ぜない。pure の `RegistrationVerdictTests` は Phase D で削除するまで通常の回帰にも入る。代替証拠: AC5、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 3 コマンドで閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean。観測は record・XML・log。目視は無い。証拠は record・XML・log・各 build の `ContentLayout.json` の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: EditMode の `[UnityTest]` から Play に入り、native 層で 2 つの directory を登録する経路は S0-b が成立させた。fixture の build と棚卸しは S2-0 が成立させた。**未確認:** 同じ file を持つ 2 つの directory の同時登録が受け付けられること（拒否されれば GO の「同時登録できない」）、`LogAssert.ignoreFailingMessages` の下で Unity の Error と Warning を record に結びつけられること。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録し、判定定義に従う）。
- 人間の判断が必要な条件: なし（結論を受けた S2 の設計判断は S2 の A1 で owner）。
- 高コスト証拠: なし（build 2 回で 1 分以内、Play の round は約 11 回の見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。pure の写像（`RegistrationVerdict`）の出力は補助で、判定 C は観測の行から独立に確かめる。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude）。A0 は program §2.4・§5.3・§8 を入力とした。
- A2 独立レビュー（予定）: (1) 目的レビュー（§1 の目的と利用者の原文だけを渡す。初稿を読んでいない担当）。(2) アーキテクチャゲート兼、各不変条件を破る順序と §2 の (a)〜(g) の外の誤答の探索。入力版は A2 入力版の commit の本書（内容 sha256 を正、commit hash を補助）。
- A3 統合担当・採否: 未実施。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。
- 独立性の強化条件: risk `normal` のため必須ではない。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
