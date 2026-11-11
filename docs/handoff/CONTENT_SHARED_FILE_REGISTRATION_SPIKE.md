# 同じ file を持つ 2 つの Content Directory の登録の実証

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-0b。S2 `CONTENT_PACKAGE_DISCOVERY` の再開条件）
- status: 発見 C の差し戻し F-1 を B で修正した（2026-11-12。§6.1。B と発見 C の往復の 1 巡目）。Phase A は revision 0（A3 凍結版は commit `68b4fe3b`、A2 入力版は commit `1c96bd8c`。採否の経緯は §5.1）。次は発見 C の再レビュー（進行役が起こす）
- branch: `claude/content-shared-file-registration-spike`（PR の base は `develop`）
- implementation base commit: `aa6b9488`（`develop` の head、2026-11-12。§1 の現況はこの base）
- implementation head commit: `0b2b6625`
- risk: `normal`（製品 code を変えない。Editor の Play の中で、製品の native 層の wrapper を 2 つ使うだけ）
- owner: root（依頼者）
- created: 2026-11-12
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「確かめていないこと」の該当行を実測の事実へ置き換え、登録の事実の節を足す。文書の冒頭の範囲は build の出力だけを言うので、登録と load の事実を含む範囲へ一文を改める。新しい文書は作らない。自己点検 S-5）、program §2.4「Unity の複数 Content Directory」・§5.3・§8（本スライスの行と S2 の再開条件）。実験 code は製品に残さず git 履歴に置く（S0-b・S2-0 と同じ）。program の本文は Phase D まで変えない
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§2.4、§5.3、§8 の S2-0 の移送、§9 の G11・G24）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（S2-0 の実測と測り直しの harness の所在）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。C / C' が読む凍結の入力は、この票を含む commit の本書だけ）
- Phase B result snapshot の取得案内・状態: 該当なし（同じ PC の発見 C へ commit の SHA で引き継ぐ。Phase B は Unity のテストも build も実行していないので、渡す実行結果は無い）
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: 発見 C の再レビュー（B と発見 C の往復の 1 巡目の後半。F-1 の修正は §6.1）。Phase C から C' までの進行役が新しい session で起こし、起動指示で引き継ぐ commit の SHA を渡す。担当は会話ではなく本書を入力にする。明確な差し戻しが無くなれば判定 C、C' と進める。進行役自身はレビュー・判定・監査をしない（動作確認は §5 のとおり `対象なし`）。
  - 開始時の照合: 起動指示で渡された SHA と自分の HEAD が一致し、作業ツリーが clean で、ブランチが `claude/content-shared-file-registration-spike` であること。implementation head 欄は SHA なので、それが HEAD の祖先で（`git merge-base --is-ancestor`）、implementation head から HEAD までの差分が本書だけであること。PR #179 の状態ブロックの `引き継ぐ commit` が渡された SHA と HEAD に一致し、implementation base / head が本欄と一致すること。食い違えば引き継がずに止まる（`.agents/skills/osm-workflow/references/phases-and-handoff.md` の「引継ぎ票」）。
  - 次の作業（B）: 完了（§6.1。F-1 の修正は commit `0b2b6625`）。
  - 次の作業（発見 C の再レビュー）: B の修正の差分（`a1c211d7..0b2b6625`）と F-1 の解消を中心に見る。初回の観点は次のとおり。implementation base `aa6b9488` から implementation head までの差分を、§2.1 の不変条件・§2.2 の受け入れ条件・§2 の誤答の経路 (a)〜(g)・§3 の責務マップに照らしてレビューする（構造を先に）。§6 の「HANDOFF との差」9 件が §4 の B 適応の範囲に収まるかと、`RegistrationRounds.cs` の行数（§6）の説明も確かめる。差し戻し中のテストは §5 の起点 filter を使う。明確な差し戻しが無くなれば、GO 候補 head を固定して判定 C（§5 の 3 コマンド）へ進める。
  - 必要な権限: 差し戻し中の限定のテストと判定 C の Unity バッチテスト・Content Directory の build は、sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1` を実行する（SKILL の Phase C）。Unity Editor は閉じてある。`unity test` / `unity run` は使わない。push は SKILL の公開点の規則に従う。C' の担当の手段: 2026-11-12 時点で Codex CLI は使えず、`cursor-agent` は使える（AGENTS.md の契約どおり Grok 系だけ）。§5 の「Phase A・B・C に関与していないベンダー」に当たるかは C' を起こす時に確かめ（Phase A の凍結前の外部レビュー（cursor の background agent、§5.1）が使ったモデルを含む）、当たらなければ独立性の制約として §8 に記録する（risk `normal` のため強化条件は必須ではない。§5）。
  - 停止規則: 指摘は「凍結済み条件または常時契約への違反により現在の問いを阻害する欠陥」と「後続スライスの入力」に分ける。B 適応で足りる欠陥は B へ戻す。B と発見 C の往復は 4 巡まで。
  - 引き渡さないもの: なし（C' への隔離は Phase C で扱う）。

## 1. 目的と対象外

- 目的: 中身が同じ content file を持つ 2 つの Content Directory を同じ Play の中で両方登録した時、Unity がその asset をメモリに何個載せるか、片方の登録解除がもう片方から載せた物を壊すかを、Unity 6000.6 の Editor の Play の実測だけで確かめる。その結果から、S2 が共有依存の置き場（共通の asset を共有 package に分けるか、各 package が複製を持ったまま登録に任せるか）をどう設計するかを、owner が S2 の中で決められるようにする。
- 利用者: OSMFramework の開発者（S2 の配布単位と共有依存を設計する owner と、後続で package の登録と解放を書く人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続）:
  - **共有依存の置き場の設計そのもの:** S2。本スライスは事実を出すだけで、どの結果でも S2 を止めない（§2 の「S2 への含意」）。
  - **2 つの directory のうちどちらの file を読むか**（片方の directory の file が disk から消えた時、もう片方から載せた物が読めるか）: S4（cache の掃除と install の失敗の設計で確かめる）。中身が同じ file は読んだ結果から区別できず、確かめるには片方の file を使えなくする操作が要る。本スライスの登録解除（AC4 の U3・U4）で表に出るのは、依存が壊れた・拒否されたとして現れた時だけで、U3・U4 が無傷ならどちらの file を読んだかは未知のまま残り、分かるのは解除が生き残る側を壊さなかったことだけである（外部レビュー Q2 の指摘）。
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
  - **「メモリに何個」の意味:** Q1 が答えるのは、load の結果として区別できる `UnityEngine.Object` の実体の数（相異なる `EntityId` の個数）である。texture の GPU 側の資源や stream の bytes がメモリ上で共有されるか、どれだけ節約されるかは証明しない（`Profiler` の値は観測で、合否にも答えにも使わない）。結論・含意の表・harvest は、実体の数と物理メモリの量を混同しない形で書く（PR の独立レビュー）。目的の文言（「メモリに何個載せるか」）は変えず、この限定で読む。
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
  - **同じ file:** 両 directory の出力の直下にあり、名前と sha256 が同じ file。中核の 3 種のそれぞれについて、それが帰属する file が両 directory で同じ file であることを AC1 で確かめる。texture は serialized object の `.cf` に加えて、pixel を持つ `.resS` も対象とする（`.resS` は layout の `SerializedFiles` ではなく `BinaryArtifacts` にある。参照の向きは、`Category` が `contentfile` の項目の `ArtifactReferences` が、`Category` が `texture` の項目の `Index` を指す形で、texture の項目から `.cf` を指すのではない。引き方は AC1 の手順に固定する。S2-0 の harness の `FileInventory` と同じ向き。外部レビュー E-13）。`SharedTex` の `.cf` と `.resS` の両方が同じ file である時だけ、texture は「同じ file」である（PR の独立レビュー）。
  - **実体の数:** 1 つの Play の中で、ある共通物の名前と型を持つ `UnityEngine.Object` のうち、`Resources.FindObjectsOfTypeAll` で見える物の数と、その `EntityId.ToULong(GetEntityId())` の集合（`GetInstanceID` は使わない）。加えて、load の結果（Loadable の戻り値、Scene の Renderer の参照）の `EntityId` を記録し、次の整合の規則で読む。**答えに使う数は、load の結果の相異なる `EntityId` の個数である**（load の回数ではない）。その集合が列挙の集合と一致する時だけ、その大きさを答えにする。例: load 2 回で相異なる `EntityId` が 1 つ、列挙もその 1 件なら実体 1。load 2 回で相異なる `EntityId` が 2 つ、列挙もその 2 件なら実体 2。load の結果が `== null`、集合が一致しない、大きさが 0 か 3 以上、のどれかなら未判定（A2 G-05、外部レビュー E-3）。
  - **正の対照の範囲:** 正の対照（AC1 の `TwinA`・`TwinB`。`Object.name` だけが同じで pixel は違う texture）が示すのは、texture について、同じ名前の別の実体を列挙が 2 つと数えられることだけである。material・prefab の列挙の網羅性は、load の結果の `EntityId` が列挙に含まれることでだけ確かめ、列挙に出ない実体が無いとは主張しない（結論にこの限界を書く）。
  - **観測の時点（解除の前の Release と unload）:** case が登録解除の前に、解除する側の Loadable を Release するか Scene を unload する時は、R0（その直前）と R1（その完了の直後）にも生き残る側の物を確かめる。R0・R1 で見るのは、その時点の状態の項目だけである（`== null`、texture の `width` と pixel の sha256、material の `mainTexture` と `shader`、prefab の Renderer の `sharedMaterial`、Scene の Renderer の material と texture）。「壊れた」の項目のうち、T3 の後の Release と再 load は R0・R1 では行わず、T3 の後に 1 回だけ行う（外部レビュー E-11）。R0 で既にこれらの項目に当たっていれば、解除の前の操作の結果にせず未判定とする。R1 でどれかに当たれば、その case は「解除の前の Release か unload で壊れた」とし、登録解除による分類を付けない。R1 でどれにも当たらなければ、登録解除の分類（壊れた・拒否された・無傷）を付ける（外部レビュー E-2・E-8）。Release と unload の間の log は、登録解除の呼び出しの log と分けて記録する。
  - **観測の時点（登録解除）:** T0 は登録解除の呼び出しの直前、T1 は呼び出しの直後（同じ frame）、T2 は 1 frame の後、T3 は `Resources.UnloadUnusedAssets` の完了の後（A2 G-04、目的レビュー）。各時点で、登録中の directory の一覧（`GetContentDirectories`。対象の directory が残っているか）と、生き残る側の物の状態を記録する。log は、呼び出しの間（同期）と、T1 から T3 の間に出た物を分けて記録する。
  - **壊れた:** T1〜T3 のどれかで、生き残る側から載せて保持している物について、次のどれか。`== null` になる。texture の `width` が 0 か pixel の sha256 が load 直後と違う（読める texture にしておく）。material の `mainTexture` が `== null` か別の `EntityId` になる。material の `shader` が `== null` か別の `EntityId` になる（外部レビュー E-2）。prefab の Renderer の `sharedMaterial` が `== null` になる。テストは生き残る側の物の `UnityEngine.Object` 参照を T3 の `UnloadUnusedAssets` の完了まで保持する（参照を離した後に `== null` になった観測は、壊れた証拠にせず未判定とする）。T3 の後に生き残る側の handle から同じ asset を Release して再 load できない。Scene の Renderer の material と texture が上と同じ条件を満たさない。
  - **拒否された:** 壊れていない、かつ、登録解除の呼び出しの間に Unity が Error か Exception を log に出したか、T3 で対象の directory が登録の一覧に残っている。log と一覧のどちらに当たったかを分けて書く。
  - **無傷:** 壊れていない、拒否されていない、T3 で対象の directory が登録の一覧に無い。
  - **優先:** 1 つの case が壊れたと拒否されたの両方に当たる時は「壊れた」とする（A2 G-04）。
- 判定定義。スパイクの合否は「問いに証拠付きの答えが出たか」だけで決め、答えの向き（実体が 1 つか 2 つか、壊れるか壊れないか）は合否に使わない。
  - **同時登録の拒否の条件:** ある登録の順で、1 つ目の登録が成功し、2 つ目の `RegisterContentDirectory` が無効な handle を返すか例外を投げ、その時点の登録の一覧に 2 つ目が無く、Unity の log か例外の型が record にあり、1 つ目の登録解除と後始末が終わった記録がある。ただし、2 つ目の directory の path の誤り（存在しない、出力の root でない）、manifest か type tree の欠落・不正（AC1 の build の設定と棚卸しで確かめる）、harness の例外による失敗は、この条件を満たさない（未判定）。この条件は順ごとに判定し、下の (r1) と判定の手順 1 が参照する（外部レビュー E-10・E-12）。
  - **round の答え（終端ではない）。** 次の 2 つは、スパイク全体の判定ではなく、その round の答えである。その round は終了行にこの答えを書き、下の手順 2 の「round が無い」「終了行が無い」「load の結果が `== null` なので未判定」の対象から除く。E2・E3 は、この 2 つの round については、実体の数か生き残る側の状態の代わりにこの答えが record にあれば満たす（外部レビュー E-7）。
    - **(r1) 片方の順だけの同時登録の拒否:** AC1 の「同時登録の確認」で、同時登録の拒否の条件が片方の順だけで成り立ち、もう片方の順では 2 つ目の登録が成功した時。拒否された順の AC2〜AC4 の round は実行せず、終了行に「同時登録の拒否により適用除外」と書き、その順の Q1・Q3 の答えを「その順では同時登録できない」とする。
    - **(r2) 両方の登録の下での load の失敗:** 両方を登録した round で、片方の handle の root からの `SharedPrefab` の load、または片方の directory の Scene の load が、null を返すか例外を投げるか Scene が loaded にならず、同じ directory を単独で登録した基準（AC1）では同じ load が成功している時。その round の Q1（または Q3）は数えず、「両方の登録の下ではその directory から load できない」を答えとする。基準でも失敗する時は harness か fixture の問題として未判定（外部レビュー E-6・E-7）。
  - **判定の手順。** 上から順に当てはめ、最初に当たったものを結論とする。
  1. **両方の順で同時登録の拒否（GO の一形）:** 同時登録の拒否の条件が両方の順で成り立った時だけ、ここで終える。AC2〜AC4 と、両方の登録を要する正の対照の load は実行せず（record に適用除外と書く）、Q1・Q3 の答えを「同時登録できない」とする。正の対照の file 名と sha256 が違うことの確認（第 1 process）と、単独登録の基準は残す。path・manifest・type tree・harness の失敗は、拒否の条件の側で除いてある（A2 G-01、外部レビュー E-6・E-7・E-12）。
  2. **未判定（結論を出さない）:** (r1)・(r2) の round を除いて、次のどれか。build を開始できない、終了行の無い round がある、第 2 process の入力が第 1 process の同じ実行の出力だと照合できない（AC1）、中核の 3 種か `SharedShader` のどれかが両 directory で同じ file でない（AC1。外部レビュー E-4）、`SharedTex` の `.resS` を特定できないか両 directory で同じ file でない（AC1。PR の独立レビュー）、正の対照の 2 つの texture が同じ file 名になった（AC1。外部レビュー E-1）、Play の round の開始時に fixture の source が AssetDatabase に残っている、round の開始時に共通物の名前の物が 1 件以上ある、正の対照が 2 つと数えられない（AC1）、実体の数が整合の規則に合わない、AC2〜AC4 のどれかの round が無い（適用除外と書かれていない）、同じ round の 2 回の反復で Q1 の数か Q3 の分類か (r1)・(r2) の答えが違う、R0 で生き残る側が既に「壊れた」の項目に当たっている、round の後始末が終わらない（登録が残る、Scene が unload されない）。harness・fixture・環境の問題は B へ戻し、Unity の挙動が定義した語で読めない時と反復で結果が違う時は Phase A へ戻す。
  3. **GO（答えが出た）:** 未判定に当たらず、record から次が一意に読める。Q1: Loadable からの load（AC2）で中核の 3 種それぞれの、直接参照からの load（AC3）で material と texture の、登録の順ごとの実体の数（prefab は Scene に置かない fixture なので直接参照では数えない。A2 G-02）。Q3: AC4 の各 case が、解除の前の Release か unload で壊れた・壊れた・拒否された・無傷のどれか。(r1)・(r2) に当たった順と round は、その答えで置き換える。登録できた順の round が整合の規則に合わなければ、(r1) があっても手順 2 の未判定である。
  - CONDITIONAL ACCEPT と NO-GO は使わない。
  - 結論は、実測した事実（record の値）と、仕組みの推測（例: 逆順の探索で後から登録した側の file を読んだらしい）を分けて書く（目的レビュー）。
  - **test の合否と判定は別である。** `SecondProcess` は、答えの向きも未判定かどうかも assert しない。assert するのは harness の成立だけである（全 round の開始行と終了行を書いた、後始末が終わった、log の購読と `LogAssert` を戻した、record を閉じた）。判定は判定 C が record から上の手順で読む。したがって XML の failed 0 は「harness が最後まで動いた」ことだけを意味し、GO の証拠ではない。`RegistrationVerdict` が record に書く判定の行は補助で、判定 C はそれを観測の行から独立に確かめる（Phase C で XML と判定を取り違えないため。自己点検 S-1）。
  - **反復の限界:** 反復の 2 回は同じ Unity process（Domain Reload 無効）の中で行うので、process をまたいだ再現ではない。process 単位の cache が 2 回に共通して効く可能性は除けない。結論にこの限界を書く（自己点検 S-2）。
- **S2 への含意（判定ではない。結論に添えて owner へ渡す。S2 の設計判断は S2 の A1 で行う）:** 行は排他ではない。当たった行を全て書き、候補を 1 つに絞らない。load の形・登録の順・種類（texture / material / prefab）で答えが違う時は、どの条件でどの行に当たったかを添える（外部レビュー E-5）。「全 case」は AC4 の表の U1〜U7 の 7 case（各 case は表に書いた登録の順だけで行う。表に無い逆順の case を足す意味ではない）と、AC2・AC3 の両方の順の round を指す。それらのうち (r1) か (r2) で答えに置き換えた round が 1 つでもあれば、1 行目（実体が 1 つ、全 case が無傷）の前提は満たさない（登録できた順だけの成功で 1 行目に当てない。外部レビュー E-9）。

| Q1 / Q3 の答え | S2 が決めること |
|---|---|
| 実体が 1 つ、全 case が無傷 | 各 package が複製を持ったまま登録に任せる形を候補にできる（中身が同じ file に限る）。**どちらの directory の file を読むかは未知のまま**なので、片方の package の file を消す・入れ替える形はこの行だけでは安全と言えない（S4 で確かめる。外部レビュー Q2 の指摘） |
| 実体が 2 つ | 共有 package に分けるか、複製のメモリを受け入れるか（受け入れる量は本スライスでは測っていない。実体の数は物理メモリの量ではない） |
| どれかの case が拒否された | 登録の寿命を directory の間で結びつけるか、共有 package に分けるか |
| どれかの case が壊れた（解除の前の Release か unload で壊れたを含む） | 同上。加えて、壊れた case を製品の Release と登録解除の順序で防げるか |
| 同時登録できない（どちらかの順で） | 共有 package に分けるか、1 つの合成単位の build に限るか |
| 両方の登録の下で片方から load できない | 同上 |

- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO で終了する。最低条件未達（未判定）のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 数え違いを答えにしない:** 実体の数は、同じ Play の中で、fixture の source も前の round の物も無い状態から数えた値で、正の対照で数え方が重複を見分けられると確かめた値に限る。
  - 崩しうる経路: (a) Editor の AssetDatabase が fixture の source asset を載せ、同じ名前の物が増える（source を Play の前に消す）。(b) Domain Reload 無効で、前の Play の static と native の物が残る（round ごとに新しい Play、開始時に 0 件を確かめる）。(c) `Resources.FindObjectsOfTypeAll` が、Unity が内部で作った同じ名前の物（例: material の instance）を拾う（数える型を固定し、`EntityId` の比較と突き合わせる）。(d) 2 つの root の取得が同じ directory を指す（handle ごとの `GetRootAssets`。§1 の製品 wrapper は handle ごと）。
  - 探索した境界: `UnityContentDirectoryBackend` と S0-b の native 層の使い方、6000.6 の XML doc。未確認: `FindObjectsOfTypeAll` が Content Directory から載った物を全て見るか（AC1 の正の対照で測る）。
- **I-2 中身が同じでない file で答えを出さない:** 問いの答えは、両 directory で名前と sha256 が同じ file に帰属する共通物だけから出す。
  - 崩しうる経路: (a) 共通物が他の asset と同じ file に入り、その file が build の組み合わせで変わる（S2-0 の AC4 と同じ確認を AC1 で行う）。(b) shader の file が違い、material の file だけ同じ（material と prefab の load と解除は shader の file に依存する。`SharedShader` も両 directory で同じ file であることを AC1 の前提に含め、違えば未判定とする。外部レビュー E-4）。(c) fixture の再生成で GUID が変わる（S2-0 の固定 GUID の生成を使う）。(d) layout に同じ名前の別の asset があり、名前で対応づけると、load する asset とは別の file を「同じ file」と記録する（AC1 で固定 GUID と local id で対応づける。A2 G-06）。
  - 探索した境界: S2-0 の実測と harness。未確認: 中身の違う同じ id の file（script をまとめた file 等）が両 directory にある時、それが共通物の load に影響するか（観測。§1 対象外）。
- **I-3 製品を変えない（S0-b・S2-0 と同じ）:** §3 の新規 file と本書以外の変更が無く、実験は assets・設定・build の出力を repo に残さない。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) Build Settings・active target・Enter Play Mode の設定の変更。(c) Play の round が登録を解除せずに終わり、次の round か次の test に残る（Unity が登録解除を拒否した時、製品 wrapper は handle を捨てるので、wrapper からは再解除できない）。(d) Domain Reload 無効で、log の購読と `LogAssert.ignoreFailingMessages` が次の round か次の test に残る。(e) 開始時の残りの回収が、自分の marker の無い既存の folder を消す（A2 G-07）。
  - 範囲: 守るのは git で追跡する file、§3 の fixture の folder、`Assets/OneStarMakerGenerated`、§4 の一時 root である。Unity が通常の import と build で更新する `Library` の cache は対象にしない（A2 G-07 の一部不採用。§5.1）。

**成立条件（起きなければならないこと）**

- **E1 準備:** 共通物を含む 2 つの directory が build でき、中核の 3 種が両方で同じ file に帰属する。
- **E2 Q1:** 中核の 3 種のそれぞれについて Loadable からの load で、material と texture について Scene の直接参照からの load で、登録の順ごとの実体の数が record にある（(r1)・(r2) の round はその答えで代える。§2）。
- **E3 Q3:** 登録解除の各 case で、生き残る側の物の状態と Unity の log が record にある（(r1)・(r2) の round はその答えで代える。§2）。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id、head、Unity の版、target、Enter Play Mode の設定、process の別を持ち、round ごとに開始行と終了行がある（終了行の無い round は未判定）。Play の round は全て新しい Play で行い、round の開始時に登録中の directory が 0 件であること（`GetContentDirectories`）と、共通物と正の対照の名前の物が 0 件であることを記録する。登録は製品の `UnityContentDirectoryBackend` を無改変で 2 つ持って行う（`ContentDirectorySession` と `AssetManagement` は使わない。§1）。Unity の Error・Exception・Warning の log は test の失敗にせず（`LogAssert.ignoreFailingMessages`）、全件を round と呼び出し（登録、load、解除の前の Release と unload、登録解除）に結びつけて record に残す（§2 の仮説の確認を兼ねる）。log の購読の開始と解除、`LogAssert.ignoreFailingMessages` の元の値への復元を round ごとに記録する。各 async の load・unload・`UnloadUnusedAssets` は完了を待ってから次へ進み、完了を record に書く（開始しただけで観測しない）。round の後始末は例外の時も `finally` で行い、登録が残れば `GetContentDirectories` の handle から `ContentLoadManager.UnregisterContentDirectory` を直接呼んで回収し、回収したことを記録する（後始末の未完了は観測値と分けて記録し、未判定とする。A2 G-08）。**反復:** AC2〜AC4 の round の集合を、同じ process の中で 2 回続けて行う。2 回で Q1 の数か Q3 の分類が違えば未判定とする（目的レビュー）。

- **AC1（E1、I-1、I-2）準備と数え方:** 第 1 process が fixture（§3）を作り、directory `A`（`SceneA`、`SharedPrefab`、`TwinA`）と `B`（`SceneB`、`SharedPrefab`、`TwinB`）を build し、出力の全 file の名前・size・sha256 と、`ContentLayout.json` による中核の 3 種の帰属を記録し、fixture の folder を消して終わる（消したことを file system で確かめて記録する）。帰属は、source の固定 GUID（と local id）から layout の `SerializedFiles` の source asset と file の id を引いて決め、名前では対応づけない。候補が無い・複数ある・file が出力に実在しない時は未判定（A2 G-06）。中核の 3 種のそれぞれが両 directory で同じ file に帰属する（そうでなければ未判定）。**`SharedTex` の `.resS`:** 各 directory の layout で、次の順に引く（外部レビュー E-13。S2-0 の harness の `FileInventory` が読んだ向き）。(1) 上の GUID と local id から、`SharedTex` の `SerializedFiles` の項目と、その `.cf` の `ContentHash` を引く。(2) `BinaryArtifacts` のうち `Category` が `contentfile` で、`ContentHash` がその `.cf` と一致する項目を引く。(3) その項目の `ArtifactReferences`（`BinaryArtifacts` の `Index` の一覧）が指す項目のうち、`Category` が `texture` の物を `.resS` とする。(4) その `ContentHash` を名前に持つ file が出力に実在し、両 directory で名前と sha256 が一致することを record に書く。(2) か (3) が 0 件か複数、(4) の file が無いか名前か sha256 が違う時は未判定（PR の独立レビュー）。正の対照の `TwinA`・`TwinB` も同じ順で `.cf` と `.resS` を特定し、互いの両方の名前と sha256 が違うことを確かめる。**process の間の照合（A2 G-09）:** 第 1 process は実行 id、head、両 directory の棚卸しの digest（file の名前と sha256 の一覧の sha256）、build の完了行を record に書く。第 2 process は開始時に、同じ実行 id の第 1 process の record を読み、head が一致し、両 directory の棚卸しを取り直して digest が一致することを記録する。一致しなければ未判定。**同時登録の確認:** 第 2 process の最初の Play で、登録の順 `A`→`B` と `B`→`A` のそれぞれで 2 つ目の登録が有効な handle を返すかを記録する（片方の順の拒否なら §2 の (r1)、両方の順なら判定の手順 1）。両方にある同じ id で sha256 が違う file を全件、種類付きで記録する（観測）。第 2 process の開始時に fixture の folder が無い。**正の対照:** `TwinA` と `TwinB` は `Object.name` だけが同じで、pixel と GUID が違う texture である（pixel を同じにすると内容 hash の file 名が一致しうり、2 にならなかった時に、数え方の欠陥か Unity が同じ内容を 1 つに載せたのかが分かれない。外部レビュー E-1）。両 directory の対照の texture の file 名と sha256 が違うことを第 1 process で確かめ（一致すれば対照不成立で未判定）、それぞれ自分の directory の root から load すると、実体の数が 2、`EntityId` が別である（そうでなければ数え方が使えないので未判定）。両方の順で同時登録の拒否の条件が成り立った時（判定の手順 1）は、正の対照の load を行わず、record に適用除外と書き、未判定にしない。片方の順だけ拒否された時（(r1)）は、登録できた順で正の対照を load する（外部レビュー E-10）。**基準:** `A` だけを登録した Play と `B` だけを登録した Play のそれぞれで `SharedPrefab` を load し、中核の 3 種の実体の数が各 1 で、load が成功する。同じく `SceneA`・`SceneB` もそれぞれ単独の登録で load できることを記録する（§2 の (r2) の比較に使う）。**`SharedShader` の帰属:** 中核の 3 種と同じく、両 directory で同じ file に帰属することを確かめる（外部レビュー E-4）。
- **AC2（E2、Q1）Loadable からの load:** 登録の順 `A`→`B` と `B`→`A` の各 round で、各 handle の root から `SharedPrefab` を load し、両方を保持したまま、中核の 3 種の実体の数と、2 つの load の結果の prefab・その `sharedMaterial`・その `mainTexture` の `EntityId` が等しいかを記録する。両 root の `SharedPrefab` の `LoadableObjectId` が等しいかを記録する（観測）。**観測（合否にしない）:** load の前後の `Profiler.GetTotalAllocatedMemoryLong` と、`SharedTex` の `Profiler.GetRuntimeMemorySizeLong`（`SharedTex` は差が見える大きさにする）。
- **AC3（E2、Q1）直接参照からの load:** 登録の順 `A`→`B` と `B`→`A` の各 round で、`SceneA`（`A` の `LoadableSceneId`）と `SceneB`（`B`）を additive に load し、両 Scene の Renderer の `sharedMaterial` とその `mainTexture` の `EntityId` が等しいか、中核のうち material と texture の実体の数を記録する。同じ round で続けて `A` の root から `SharedPrefab` を load し、Scene の material と `EntityId` が等しいかを記録する（Loadable と直接参照の混在）。
- **AC4（E3、Q3）登録解除:** 次の各 case を、指定した登録の順の新しい Play で行う。各 case で、R0・R1 がある case は R0・R1 に §2 が列挙したその時点の状態の項目だけを、T0〜T3 には「壊れた」の語の全項目を確かめ（T3 の後の Release と再 load は T3 の後に 1 回だけで、R0・R1 では行わない。外部レビュー E-11 の AC4 側）、登録の一覧と log を記録し、case を「解除の前の Release か unload で壊れた」・壊れた・拒否された・無傷のどれかに分類する（外部レビュー E-8）。最後に生き残る側を Release して再 load し、全て Release・unload して生き残る側も登録解除し、`Resources.UnloadUnusedAssets` の完了の後に共通物の実体の数が 0 であることを記録する（0 でなければ観測として残す）。
  - 各 case は、登録の順・load 元・登録解除の対象・生き残る側を次のとおり固定する（A2 G-03）。

| case | 登録の順 | load 元 | 登録解除の対象 | 生き残る側 | 意図 |
|---|---|---|---|---|---|
| U1 | `A`→`B` | `A` と `B` の root の `SharedPrefab`（`A` の物は Release 済み） | `A`（先に登録） | `B` | 文書どおりの手順 |
| U2 | `A`→`B` | `B` の root の `SharedPrefab` | `A`（先に登録。何も load していない） | `B` | load していない側の解除 |
| U3 | `A`→`B` | `A` の root の `SharedPrefab` | `B`（後から登録。何も load していない） | `A` | 先に登録した側から load し、後から登録した側を解除 |
| U4 | `B`→`A` | `B` の root の `SharedPrefab` | `A`（後から登録。何も load していない） | `B` | U3 を `A`・`B` を入れ替えて行う |
| U5 | `A`→`B` | `SceneA`（`A` の `LoadableSceneId`） | `B`（後から登録） | `SceneA` の Renderer の material と texture | 直接参照で U3 と同じ形 |
| U6 | `A`→`B` | `A` と `B` の root の `SharedPrefab`（`B` の物を Release。R0・R1 を観測） | `B`（後から登録） | `A` | U1 の鏡。後から登録した側を Release して解除し、先に登録した側の Loadable は持ったまま（外部レビュー E-2） |
| U7 | `A`→`B` | `SceneA` と `SceneB`（`SceneB` を unload。R0・R1 を観測） | `B`（後から登録） | `SceneA` の Renderer の material と texture | 直接参照で両方の Scene を載せた後、文書の手順（unload の後に解除）で片方だけ解除する（外部レビュー E-2） |

  - U1・U6・U7 は、解除の前の Release か unload を §2 の R0・R1 で観測する。U1 の Release は `A`（先に登録した側）、U6 は `B`（後から登録した側）である。

  - 失敗経路の検証例: U3・U4・U5・U6・U7 は、後から登録した側の file が先に探索される（§1 の公開文書）ので、先に登録した側からの load が後から登録した側の file を読んでいれば、その登録解除で生き残る側の物が壊れるか拒否される経路である。U3 と U4 の分類が違えば、`A`・`B` の違い（fixture の差）に依存した結果として結論に書く。
- **AC5（I-3）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書だけである。`pwsh tools/contract-audit.ps1` が合格する。2 つの process それぞれの前後で `git status --porcelain` が一致し、Build Settings、active target、Enter Play Mode の設定に変化が無い。各 process の終わりに、fixture の folder とその `.meta`、`Assets/OneStarMakerGenerated` が無いことを file system で確かめて record に残す。各 round の終わりに登録中の directory が 0 件であることを記録する。各 process の開始時に fixture の folder か一時 root の残りがあれば、自分の marker がある物だけを消して記録し、marker の無い物があれば何も消さずに停止する（未判定。A2 G-07）。build の出力と record は実行 id ごとの一時 root に置き、他の実行の出力を上書きしない。

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

契機: Q1 と Q3 は独立して失敗しうる未知である。**分割しない。** 2 つとも同じ fixture、同じ 2 directory、同じ Play の往復で測れ、分けると build と harness を 2 回作る。owner は 2026-11-12 に program §8 の問いを 1 つの小スパイクにすると裁定している。A1 の検討で、program §8 の 3 つ目の問い（`Loadable` がどちらの複製に解決されるか）は、中身が同じ file では読んだ結果から区別できず、片方の file を使えなくする操作が要ること、依存が壊れた・拒否されたとして現れる場合は Q3 の U3・U4 で表に出ることから、owner の了承（2026-11-12）を得て S4 へ送った（§2.3）。U3・U4 が無傷の時は、どちらの file を読んだかは未知のまま残る（「S2 への含意」の 1 行目の限定）。

### 2.5 未決事項（owner へ）

**owner の裁定（2026-11-12）:** U-2 を推奨どおりとする（Editor の Play だけで測り、Player は §2.3 の後続へ）。U-1（実体が 2 つ載る時の判定の向き）と U-3（Q2 の観測の方法）は、判定を答えの有無だけで決める形に改め、Q2 を S4 へ送ったことで不要になった（同日、owner の指摘による）。

- **U-2 Player:** Editor の Play だけで測る。Player の検証は数分の build と別の実行経路が要る。推奨: Editor だけ（§2.3 で後続へ）。

## 3. 責務マップ

全て新規で、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/SharedFileRegistration/`（asmdef は既存の `OneStarMaker.Tests.Editor`。参照の追加なし。`OneStarMaker.Build.Content` と Runtime の internal に friend access で届くことは S0-b と S2-0 で確かめ済み）に置く。寿命は 1 回の test、依存は Tests → 製品の一方向で、製品からは参照されない。Phase D で git 履歴へ退避して削除する。S2-0 の `IdentityFixture`・`FileInventory` の一部は commit `f632707a` から写して縮めてよい（B 適応。写した元を §6 に書く）。予想の合計は約 950 行で、500 行を超える file は無い。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---|---|
| `RegistrationVerdict.cs` | round の観測（実体の数、`EntityId` の一致、壊れた項目、log）を受け取り、AC ごとの合否、Q1・Q3 の答え、判定定義と「S2 への含意」の行への写像を返す。入力は文字列と数値だけで、Unity の型も製品の型も受け取らない | 180 | Unity 非依存の pure。§2 の語と判定定義 |
| `RegistrationVerdictTests.cs` | 上の写像だけの EditMode test。§2 の (b)(c)(e)(f)(g) の誤りが答えにならないこと（正の対照が 1 の時に未判定、round の開始時に残りがある時に未判定、`== null` でない物の中身が失われた時に「壊れた」、Error の log と無傷の物の組が「拒否された」、片方の順を適用除外と書かずに実行しなかった時に未判定、実体の数と `EntityId` の食い違いで未判定）、答えの向きによらず GO になること（実体 1・2、無傷・拒否・壊れた、同時登録の拒否の各例）、各例が「S2 への含意」の正しい行に写ること。A3 で足した規則: 整合の規則（load の結果が列挙に無い・数 0・3 以上で未判定）、壊れたと拒否の両方で「壊れた」、T2・T3 でだけ壊れた例、T3 で登録が残る例が「拒否された」、反復の 2 回が違えば未判定、同時登録の拒否と harness の例外の区別、process 間の digest の不一致で未判定。外部レビューで足した規則: load 2 回・相異なる `EntityId` 1 つ・列挙 1 件が実体 1 の GO、正の対照の file 名の一致で未判定、R1 で壊れた case が「解除の前の Release か unload で壊れた」になり解除の分類を付けない（R1 で項目に当たらなければ解除の分類が付く）、(r1) の片方の順の拒否が終端にならず、登録できた順の整合の欠けで未判定、両方の順の拒否だけが手順 1 で GO、(r2) の Loadable と Scene の load の失敗（基準で成功）が round の答えで、基準でも失敗なら未判定、`SharedShader` の file の不一致で未判定、含意の表に複数行が当たる例、(r1)・(r2) がある時に 1 行目が当たらない例。3 巡目で足した規則: 両方の順で拒否の条件が成り立つと手順 1 の GO（正の対照の未実行で未判定にならない）、R0 で既に壊れていれば未判定、R1 の判定に再 load の項目を使わない。4 巡目で足した規則: 片方の順だけの path・manifest・type tree・harness の失敗が (r1) にならず未判定、`SharedTex` の `.resS` が無い・複数・違う時に未判定 | 320 | `[Explicit]` なし |
| `RegistrationFixture.cs` | fixture の asset の生成（固定 GUID、`SharedTex` は読める texture で差が見える大きさ）、所有の marker、開始時の残りの回収と終了時の削除 | 200 | Unity / `AssetDatabase` の I/O |
| `RegistrationBuilds.cs` | `[Explicit]` の `FirstProcess`。`A`・`B` の build、出力の棚卸しと sha256、`ContentLayout.json` による中核の 3 種の帰属、JSONL の record、fixture の削除 | 180 | build と file system |
| `RegistrationRounds.cs` | `[Explicit]` の `[UnityTest]` `SecondProcess`。AC1 の正の対照と基準、AC2〜AC4 の round を新しい Play ごとに行う。`UnityContentDirectoryBackend` を 2 つ持ち、観測と log を record に書く。Play の待ちは `EnterPlayMode` / `ExitPlayMode` の yield と load の await | 220 | 製品への依存はこの file と `RegistrationBuilds.cs` だけ |

資源の所有と寿命（A2 G-10）:

| 資源 | 作る・解放する所有者 | 寿命 | 失敗時 |
|---|---|---|---|
| fixture の folder と marker | `RegistrationFixture`（第 1 process） | 第 1 process の中 | `finally` で削除。次の process の開始時に marker のある残りだけ回収 |
| build の出力と record | `RegistrationBuilds`（作る）、`RegistrationRounds`（読む・追記） | 実行 id ごとの一時 root。Phase C の evidence に写すまで残す | 他の実行の root に触れない |
| 2 つの `UnityContentDirectoryBackend` と登録 | `RegistrationRounds` の round | 1 つの Play | `finally` で wrapper の `Unregister`、残れば `GetContentDirectories` から直接回収 |
| load の token と Scene | `RegistrationRounds` の round | 登録より短い | 登録解除の前に Release・unload（case が意図して残す物を除く） |
| log の購読と `LogAssert.ignoreFailingMessages` | `RegistrationRounds` の round | 1 つの round | `finally` で購読を解除し、元の値へ戻す |
| Play の状態（Enter Play Mode の設定） | 変えない | — | AC5 で前後一致を確かめる |

fixture（SampleGame を使わない。一時 folder `Assets/OsmSpikeSfr/` に marker と一緒に作り、第 1 process の終わりに消す）:

- 共通物: `SharedTex`（読める texture。`Profiler` で差が見える大きさ）、`SharedShader`（variant の無い自作 shader。URP の `Lit` を使わず、shader の file が build の組み合わせで変わらないようにする）、`SharedMat`（`SharedShader` と `SharedTex`）、`SharedPrefab`（`SharedMat` の Renderer。Runtime の既存の component 以外を持たない）。
- 固有物: Scene `SceneA`・`SceneB`（prefab instance を置かず、Scene 内の Renderer が `SharedMat` を直接参照する）、`TwinA`・`TwinB`（正の対照。`Object.name` だけが同じで、pixel と GUID が違う texture。同じ asset 名にするため別の subfolder に置く。自己点検 S-4）。
- root は製品の `BuildContentRoot`（無改変）。Scene は `LoadableSceneId`、Object は `Loadable<Object>`。新しい Runtime の型は足さない。
- script をまとめた file は両 directory で中身が違いうる（S2-0。違ったのは build 内の component の集合が違う組み合わせだった）。root の `BuildContentRoot` 自体がその file に依存するので、`A` と `B` の script の集合を揃える（両 directory の component の型の集合を同じにし、共通物にも固有物にも自作の script を持たせない）。それでも両方にある同じ id で sha256 が違う file が出れば、AC1 の観測として種類付きで記録し、結論の限界に書く（G24 の条件が入ったことになるため。自己点検 S-3）。

## 4. 実装計画

- 順序: (1) pure の 2 file。(2) fixture と build。(3) Play の round と record。(4) 実行は Phase C。
- 対象外を維持する方法: 製品 file を変えない（AC5）。build の出力と record は `TestResults/` 下の一時 root（git 管理外、path 予算内）に置く。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る。`UnityContentDirectoryBackend` を 2 つ持って同時に登録できない（製品の wrapper では足りず、`ContentLoadManager` を直接呼ぶ必要がある時も、根拠を書いて Phase A へ返す。Unity 自身が同時登録を拒否した時は差し戻しではなく GO の「同時登録できない」）。中核の 3 種を両 directory で同じ file にする fixture が §3 の形で作れない。§2 の語で数えられない（`FindObjectsOfTypeAll` が Content Directory の物を見ない等）。
- B 適応の範囲: fixture の asset の細部（大きさ、component の選び方）、record の項目名、round の順序、`SecondProcess` が第 1 process の record と出力を読む方法、S2-0 の harness から写す範囲。

## 5. テストとレビュー計画

- 単体テスト: `RegistrationVerdictTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationVerdictTests`。
- 判定必須テスト（GO 候補 head）: 次の 3 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationVerdictTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationBuilds.FirstProcess`。(3) `-Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationRounds.SecondProcess`。各 XML の total が 1 以上で failed 0、root result `Passed`、record に全 round の終了行がある。**ここまでは harness が最後まで動いたことの確認で、GO の証拠ではない。** その後に判定 C が §2 の判定の手順を record に当てて、GO か未判定かを決める（§2 の「test の合否と判定は別である」。外部レビュー 3 巡目）。**process の自然終了をゲートにしない**（S2-0 の移送。XML の完成後に Unity が終わらない既知の終了 stall は、`tools/Harness/README.md` の規則に従い、XML と record の終了行で判定する）。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC5）、build と Play を行う `[Explicit]` の実験 test は通常の回帰に混ぜない。pure の `RegistrationVerdictTests` は Phase D で削除するまで通常の回帰にも入る。代替証拠: AC5、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 3 コマンドで閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean。観測は record・XML・log。目視は無い。証拠は record・XML・log・各 build の `ContentLayout.json` の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: EditMode の `[UnityTest]` から Play に入り、native 層で 2 つの directory を登録する経路は S0-b が成立させた。fixture の build と棚卸しは S2-0 が成立させた。**未確認:** 同じ file を持つ 2 つの directory の同時登録が受け付けられること（拒否されれば GO の「同時登録できない」）、`LogAssert.ignoreFailingMessages` の下で Unity の Error と Warning を record に結びつけられること。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 最初の実行。不成立時: 基盤不備なら B、Unity の挙動なら観測として記録し、判定定義に従う）。
- 人間の判断が必要な条件: なし（結論を受けた S2 の設計判断は S2 の A1 で owner）。
- 高コスト証拠: なし（build 2 回で 1 分以内、Play の round は同時登録の確認と AC1 で 4 回、AC2〜AC4 の 11 回を 2 回反復して約 26 回の見込み。超えたら記録する）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。pure の写像（`RegistrationVerdict`）の出力は補助で、判定 C は観測の行から独立に確かめる。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude）。A0 は program §2.4・§5.3・§8 を入力とした。
- A2 独立レビュー（実施 2026-11-12。互いの指摘を見せていない）:
  - (1) 目的レビュー: 本 session が起動した新しい subagent（Claude、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `af641270fdfba8bd68c28f78945d3bc2eb7d33f2154a5aaa7497b6192f603f09`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。
  - (2) アーキテクチャゲート兼、各不変条件を破る順序と §2 の誤答の一覧の外の探索: Codex CLI（OpenAI）。入力は A2 入力版の本書（内容 sha256 `7f4b60e44e45b9c6788440b43c5cfbfd64c65e79f82921f14d5e4a7ec8d9645b`、commit `1c96bd8c`）と、program・S2-0 の事実・AGENTS.md・アーキテクチャゲート・製品の `Runtime/BuildContent` の source・Unity 6000.6 の `ContentLoadModule` の XML doc・S2-0 と S0-b の harness の写し。枠は「計画した実験 code は実装済みと仮定」「判定を答えの有無で決めること、Q2 を S4 へ送ったことは変えない」に固定した。
- A3 統合担当・採否: 本 session（§5.1）。owner の指示で、凍結の前に draft PR #179 で外部レビューを受けた。2026-11-12 に owner が凍結を指示し、revision 0 として凍結した。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。
- 独立性の強化条件: risk `normal` のため必須ではない。

### 5.1 A2 の独立レビューと A3 の採否

A1 の段階で owner が下した裁定（判定を答えの有無だけで決める、Q2 を S4 へ送る、U-2）は §2.4・§2.5 にある。A2 の指摘の採否は次のとおり。目的・利用者の欄は変えていないので、目的レビューはやり直さない。

**目的レビュー（P）。** 回答を初稿と突き合わせ、初稿に無い保証と、初稿が作る要らないものを拾った。

| ID | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| P-1 | 登録解除の直後だけでなく、GC や Unload が効いた後にも生き残る側を見る | 採用 | 遅れて壊れる経路を「無傷」と誤読しうる。§2 の観測の時点 T0〜T3（G-04 と統合） |
| P-2 | 1 回の観測で結論を出さず、Play を開き直して再現する | 採用 | round は新しい Play で行っていたが各 1 回だった。§2.2 の反復（2 回、違えば未判定）。費用は Play 約 9 回の追加 |
| P-3 | 実測した事実と仕組みの推測を分けて書く | 採用 | §2 判定定義の末尾 |
| P-4 | 「同じ」と判定される条件（名前・path が違う場合など）の範囲を宣言する | 既に充足 | 名前は内容 hash で、名前と sha256 は一対一（S2-0）。対象は「同じ file」の語で固定済み。directory の path は 2 つで必ず違う |
| P-5 | 作らないもの: 本番 API、重複の検出・統合の仕組み、benchmark、恒久の計測基盤、本番の Scene 等の変更 | 既に充足 | §1 対象外と §3（実験 code は Phase D で消す。`Profiler` の値は観測で合否にしない） |
| その他 | 個数と同一性を分けて観測、順序の入れ替え、hash による同一の証明、基準、Editor の版の記録、S2 の 2 案への対応付け、再現手順と測っていない範囲 | 既に充足 | §2 の語、AC1〜AC4、「S2 への含意」、成果の範囲 |

**アーキテクチャゲート（G）。**

| ID | 重さ | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|---|
| G-01 | blocker | 同時登録の拒否を GO とする特例と、round が無ければ未判定の規則が衝突する | 採用 | 判定定義に優先順位を付け、拒否の成立条件（2 つ目の handle、一覧、log、後始末、両方の順）と、harness の失敗との区別を書いた。§2 判定定義の 1、AC1 の同時登録の確認 |
| G-02 | blocker | 直接参照の AC3 は prefab を数えないのに、GO は中核の 3 種を要求する | 採用 | 直接参照で数えるのは material と texture に限ると明記（Scene に prefab instance を置くと Scene の file に焼き込まれ、prefab の file の問いにならない）。§2 判定定義の GO、E2 |
| G-03 | blocker | U4 の「U3 を逆順で」と括弧の指定が両立しない | 採用 | 初稿の誤り。case ごとに登録の順・load 元・解除の対象・生き残る側を表で固定した。U4 は `B`→`A`、`B` から load、後から登録した `A` を解除。§2.2 AC4 |
| G-04 | blocker | 登録解除の成否（登録が実際に残るか）と、観測の終わりの時点が無い | 採用 | 観測の時点 T0〜T3 と、各時点の登録の一覧を足し、「拒否された」を log と一覧の 2 つで定義し、壊れたを優先した。wrapper が handle を捨てるので、後始末は一覧から直接回収する。§2 の語、§2.2 の前文、I-3 (c) |
| G-05 | should-fix | 正の対照は texture だけで、列挙の網羅性を示さない | 採用 | load の結果が列挙の集合に含まれる時だけ数を使う整合の規則と、正の対照の範囲（texture だけ）を語に書き、結論に限界を書く。material・prefab の正の対照は足さない（同じ名前で別の GUID の物を作るだけで、列挙に出ない実体の不在は示せないため） |
| G-06 | should-fix | 名前で帰属を対応づけると別の file を「同じ file」と記録しうる | 採用 | 固定 GUID と local id で対応づけ、曖昧なら未判定。I-2 (d)、AC1 |
| G-07 | should-fix | git status の一致だけでは ignored file の上書きを見ない。marker の無い folder を消しうる | 一部採用 | marker の無い残りがあれば何も消さずに停止、実行 id ごとの一時 root、守る範囲の明記を採用（I-3 (e)、AC5）。ignored file 全体と `Library` の比較は不採用（Unity が通常の import と build で更新し、差を製品の変化と区別できない。実験が書く先は一時 root と fixture の folder に限っており、そこは AC5 で見る） |
| G-08 | should-fix | 非同期の完了、後始末、log の購読と `LogAssert` の復元が曖昧 | 採用 | §2.2 の前文（完了を待つ、`finally`、購読と復元の記録、同期と後続 frame の log の区別、後始末の未完了は未判定） |
| G-09 | should-fix | 第 2 process が古い build を読んでも気づけない | 採用 | 実行 id・head・棚卸しの digest の照合。AC1 |
| G-10 | note | 資源ごとの所有者と寿命が file ごとに書かれていない | 採用 | §3 の資源の所有と寿命の表 |

**凍結前の外部レビュー（E）。** draft PR #179 で、A3 統合案（commit `ea519183`）に対して受けた（cursor の background agent。Phase A の凍結前レビューなので、§5 の C' 用に予約した未関与の担当には数えない）。

| ID | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| E-1 | 正の対照の `TwinA`・`TwinB` が pixel まで同じだと、内容 hash の file 名が一致しうり、問いと同じ条件になる | 採用 | 初稿の誤り。対照は `Object.name` だけを揃え pixel を変え、file 名の一致で対照不成立（未判定）。§2 の語、AC1、§3 |
| E-2 | 登録解除の前の Release が観測の窓の外。U1 の鏡と、両方の Scene を載せた case が無い。T3 まで参照を保持する規則と、`material.shader` が無い | 採用 | R0・R1 の観測と「Release で壊れた」の分類、U6（後から登録した側を Release して解除）と U7（両方の Scene を載せ、片方を unload して解除）、参照の保持、`shader` を「壊れた」に追加。U7 は「両方の Scene を載せたまま解除」を、文書の手順（unload の後に解除）に沿う形で解した（載せたまま解除するのは文書の手順外で、U3〜U5 の「load していない側の解除」と別の問いになるため） |
| E-3 | 「数」を load の回数と読むと、実体 1 つが未判定に落ちる | 採用 | 初稿の誤り。答えの数を load の結果の相異なる `EntityId` の個数と定義し、例を付けた。§2 の語 |
| E-4 | shader の file が違う時に material の答えを残している | 採用 | `SharedShader` も同じ file であることを AC1 の前提にし、違えば未判定。I-2 (b)、AC1、判定定義 |
| E-5 | 「S2 への含意」に複数行が当たる場合が閉じていない | 採用 | 行は排他でないこと、当たった行を全て書くこと、条件を添えることを表の前に書いた |
| E-6 | 片方の順だけ登録できない場合と、片方からの load の失敗が未判定に落ちる | 採用（GO 側に置く） | どちらも Unity の挙動として読める答えなので、判定定義の 1（順ごとの拒否）と 1b（基準で成功し、両登録で失敗する load）に置いた。基準でも失敗するなら harness の問題で未判定 |
| Q2 | 「依存は U3・U4 で表に出る」は、壊れた・拒否された時だけ | 採用 | §1 対象外、§2.4、含意の表の 1 行目に「どちらの file を読むかは未知のまま」の限定を足した |
| 維持 | 判定を答えの有無で決めること、正の対照を texture に限ること、Q2 を S4 へ送ること、同時登録の拒否を GO の一形にしたこと、GUID と local id による帰属、digest の照合 | 維持 | レビューも維持を可とした |

E-1〜E-6 の反映の後、commit `00fdee92` に対して 2 巡目の外部レビューを受けた（同じ担当）。

| ID | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| E-7 | 片方の順の拒否と load の失敗（旧 1b）が、終端の判定（GO）と未判定の条件（round が無い、load が null）の両方に当たる。Scene の load の失敗が入っていない | 採用 | E-6 の反映で生じた衝突。片方の順の拒否と load の失敗を「round の答え」(r1)・(r2) に分け、終端にしないこと、未判定の条件から除くこと、E2・E3 を答えで代えること、両方の順の拒否だけを手順 1 で終えることを固定した。Scene の load の失敗も (r2) に入れ、単独登録の基準に Scene の load を足した |
| E-8 | R1 の「無傷」が、T3 の登録の一覧を含む無傷の定義と衝突し、解除の分類が付かない。分類の名前が Loadable の Release と Scene の unload を区別しない | 採用 | R1 の条件を「壊れたの項目に当たらない」にし、分類の名前を「解除の前の Release か unload で壊れた」にして、AC4 の前文の分類を 4 つにした。Release と unload の log を登録解除の log と分けた |
| E-9 | (r1) の適用除外を「全 case」から外すと、片方の順の成功だけで含意の表の 1 行目が当たる | 採用 | 表の前に「全 case は両方の順を含めて数え、(r1)・(r2) が 1 つでもあれば 1 行目の前提を満たさない」と書いた |

commit `788e5da9` に対して 3 巡目の外部レビューを受けた（同じ担当。新しい case は不要とされた）。

| ID | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| E-10 | 手順 1 が「両方の順で (r1)」を要求するが、(r1) は「もう片方は成功」を含むので両立せず、手順 1 の GO に届かない。正の対照の未実行が未判定に落ちる | 採用 | E-7 の反映で生じた食い違い。「同時登録の拒否の条件」を語として (r1) から分け、(r1) と手順 1 がそれを参照する形にした。AC1 の正の対照に、手順 1 の時は適用除外とし未判定にしないことを足した |
| E-11 | R1 への読み替えで、T3 の後の Release と再 load が解除の前に来る | 採用 | R0・R1 で見る項目をその時点の状態に限り、再 load は T3 の後に 1 回とした。R0 で既に壊れていれば未判定（判定の手順 2 にも足した） |
| 文言 | 「全 case」を表に無い逆順の case を足す意味に読まない。§5 の判定必須に、XML の後に §2 の手順を当てると書く | 採用 | 含意の表の前文と §5 の判定必須テスト |

commit `974cc4f7` に対して、4 巡目の外部レビュー（同じ担当）と、owner が PR に付けた独立レビュー（review 本文と inline 3 件）を受けた。どちらも新しい case は不要とした。

| ID | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| E-12 | path・harness・type tree の失敗を拒否と区別する文が手順 1 にだけあり、片方の順だけの失敗が (r1) の「その順では同時登録できない」になる | 採用 | E-10 の反映で残った食い違い。区別を共有の「同時登録の拒否の条件」に移し、manifest の欠落・不正も足した。手順 1 からは参照だけにした |
| E-11 の AC4 側 | AC4 の前文が R0・R1 でも「壊れた」の全項目を見ると書き、§2 の「再 load は T3 の後だけ」と食い違う | 採用 | AC4 の前文で R0・R1 を §2 の状態の項目に限った（外部レビューと独立レビューの両方の指摘） |
| PR-1 | 「同じ file」の確認が `.cf` だけで、texture の pixel を持つ `.resS` の一致を確かめていない（I-2 の誤答経路） | 採用 | 新しい指摘で妥当。`BinaryArtifacts` の `ArtifactReferences` から `SharedTex` の `.resS` を特定し、両 directory で名前と sha256 の一致を AC1 の前提と record に足した。特定できない・違う時は未判定。正の対照も同じ引き方で違うことを確かめる。build と case の追加は無い |
| E-13 | PR-1 の反映で書いた `.resS` の引き方の参照の向きが、S2-0 が読んだ layout と逆（texture の項目が `.cf` を指すのではなく、`contentfile` の項目の `ArtifactReferences` が texture の項目の `Index` を指す）。このままだと 0 件で未判定になる | 採用 | 主担当の誤り。S2-0 の harness（commit `f632707a` の `FileInventory`）で向きを確かめ、§2 の「同じ file」と AC1 の引き方を 4 段の手順に固定した。PR の独立レビューも追跡のコメントで同じ訂正を認めた（5 巡目の外部レビュー） |
| PR-2 | `EntityId` が答えるのは区別できる Object の実体の数で、GPU・stream の物理メモリの共有量は証明していない | 採用 | §2 の成果の範囲に「メモリに何個」の意味を限定し、含意の表の「実体が 2 つ」に量を測っていないことを書いた。目的の文言は変えない（変えると目的レビューのやり直しが要り、この限定で読めば目的と食い違わないため） |

commit `ccf043fb` に対して、6 巡目の外部レビュー（同じ担当）と PR の独立レビューの再レビューを受けた。どちらも凍結の前に直す指摘は無く、E-13 の 4 段の引き方が S2-0 の向きと一致すること、E-11・E-12・PR-2 の修正が残っていることを確かめた。独立レビューは、実験計画のレビューであり Unity 上の build・Play の成立と実 fixture での `BinaryArtifacts` の一意な対応は未検証、想定外の layout は計画どおり未判定として B か Phase A へ返し、C で record と照合すること、と添えた（§5 の「未知の操作経路の疎通」の扱いのとおり）。owner が凍結を指示した。

**自己点検（S）。** 2 巡目の反映と同時に、Phase C と C' に問われそうな所を主担当が見直した。

| ID | 問題 | 反映先 |
|---|---|---|
| S-1 | test の合否と判定の関係が書かれていない。`SecondProcess` が未判定で assert して失敗すると、判定 C が harness の失敗と未判定を XML で取り違えうる | §2 判定定義の末尾。test は harness の成立だけを assert し、判定は record から読む。XML の failed 0 は GO の証拠ではない |
| S-2 | 反復は同じ process の中なので、process をまたいだ再現ではない | §2 判定定義の末尾に限界として書いた（process を分けた反復は、費用に対して S2 の判断を変えないので足さない） |
| S-3 | root の `BuildContentRoot` 自体が script をまとめた file に依存し、`A`・`B` の component の集合が違うとその file の bytes が違いうる（S2-0）。G24 の条件が fixture に入る | §3 の fixture で script の集合を揃え、それでも違う file が出たら観測と結論の限界に書く |
| S-4 | `TwinA`・`TwinB` を同じ `Object.name` にするには同じ asset 名が要り、同じ folder には置けない | §3 の fixture（別の subfolder） |
| S-5 | harvest 先の文書の範囲は build の出力だけを言う | §0 の harvest to（範囲の一文を改め、新しい文書は作らない） |

## 6. Phase B 実装結果

§3 の 5 file を `unity/Assets/OneStarMaker/Tests/Editor/Spikes/SharedFileRegistration/` に新規で置いた（2026-11-12）。製品 code・asmdef・既存の test は変えていない。製品への依存は `RegistrationBuilds.cs`（`BuildContentRoot`）と `RegistrationRounds.cs`（`UnityContentDirectoryBackend`、`AbstractApplicationInitializer` の発見）だけである。

| file | 行数（予想） | 内容 |
|---|---|---|
| `RegistrationVerdict.cs` | 292（180） | §2 の語（整合の規則、同時登録の拒否の条件、AC4 の分類）と判定の手順 1〜3、含意の表の行への写像。入力は文字列・数値・真偽だけ |
| `RegistrationVerdictTests.cs` | 395（320） | §3 に列挙した規則の全例（GO の各向き、(r1)・(r2)、拒否から除く 4 原因、R0・R1、反復、整合の規則、含意の複数行） |
| `RegistrationFixture.cs` | 256（200） | fixture の生成（固定 GUID・固定 fileID・文書の並べ替え）、marker、残りの回収と削除 |
| `RegistrationBuilds.cs` | 410（180） | `FirstProcess`。build、棚卸し、帰属と `.resS` の 4 段の引き方、digest。共通の record（`SpikeRun`）、process の前後の比較（`Aftercare`） |
| `RegistrationRounds.cs` | 658（220）。§6.1 の修正の後は 669 | `SecondProcess`。照合、Play の round（同時登録の確認、基準 A・B、正の対照、AC2〜AC4 の 11 round × 2 反復）、R0・R1・T0〜T3 の観測、log の捕捉、後始末、補助の判定の行 |

**行数の差（予想 950、実際 2011）。** `RegistrationRounds.cs` が 500 行を超えた。増えたのは、§2.2 の前文が要求する手順（round の開始時の 0 件の確認、R0・R1・T0〜T3 の各時点の記録、log の phase への結びつけ、`finally` の後始末と一覧からの直接回収、T3 の後の再 load）を case ごとに分けずに 1 つの表（`CaseTable`）と共通の部品で書いた結果で、責務は §3 のとおり 1 つ（Play の round と record）である。file を分けると §3 の責務マップに無い file が増えるので分けなかった。`RegistrationBuilds.cs` の増分は、§3 では置き場所を決めていなかった record・process の前後の比較・git の読み取りを、両 process が共有するのでここに置いたためである。

**写した元（§3・§4 の B 適応）。**

- S2-0（commit `f632707a`）の `IdentityFixture`: 固定 GUID（名前の MD5）、fileID の固定と文書の並べ替え、marker の所有、自作 shader の source。URP の material と component を除いて縮めた。
- S2-0 の `IdentityBuilds`: 実行 id の root、`BuildHistory` の増分から `BuildManifestHash` で layout を対応づける方法、process の前後の git status と設定の比較、git の読み取り。record の writer は、JsonUtility の payload の型を並べる代わりに key と値の組を書く形にした。
- S2-0 の `FileInventory`: `ContentLayout.json` の欄の写し（帰属に使う欄だけ）と、`contentfile` の `ArtifactReferences` が `texture` の `Index` を指す向き。
- S0-b（commit `94b0c4a6`）の `PlayEnvironment`: Game の bootstrap に process の環境変数で addressables mode を与える方法（addressables mode では bootstrap は Content Directory を登録せず、最初の Scene も読まないことを `AbstractApplicationInitializer` で確かめた）と、Play の初期 Scene の退避と復元。

**HANDOFF との差（B 適応。契約・責務配置は変えていない）。**

1. **帰属の引き方:** layout の `SourceAssets` は GUID ではなく asset の path なので、固定 GUID を `AssetDatabase.GUIDToAssetPath` で path に戻し、それが固定の path と一致することを確かめてから `SourceAssets` と照合する（名前（`Object.name`）では対応づけない）。local id は `LoadableObjectIds` に載る物（root の entry の `SharedPrefab` と `Twin`）にだけあるので、それらは GUID と LFID が指す `SerializedFile` が同じ file であることも確かめる。
2. **fixture の細部:** `SharedTex` は 1024×1024、圧縮なし、mipmap なし、読める設定。正の対照は `A/Twin.png` と `B/Twin.png`（`Object.name` は 2 つとも `Twin`、256×256、pixel は variant で変える）。component は `MeshFilter` と `MeshRenderer` だけで、自作の script は持たせない。root の logical key は両 directory で同じ `Scene`・`SharedPrefab`・`Twin` にし、Play 中は handle ごとの root からこの key で引く。
3. **round の順序:** 同時登録の確認 → 基準 A → 基準 B → 正の対照（登録できた順の 1 つ目）→ 反復 1・2 のそれぞれで AC2（2 順）・AC3（2 順）・AC4（U1〜U7）。拒否された順の round は Play に入らず、開始行と終了行（`exclusion: r1`）だけを書く。両方の順で拒否されたら正の対照と AC2〜AC4 を適用除外と書いて終える。
4. **拒否から除く原因の判定:** `path` は 2 つ目の directory の出力に `BuildManifestHash.txt` が無いことで判定する。manifest と type tree は、第 1 process の build の設定（`BuildContentOptions.None`、type tree を書く）と build の成功の記録で確かめる。harness の例外は round の中止（`round-abort`）にし、test を失敗させる（拒否の観測にしない）。
5. **T3 の後の再 load:** prefab は生き残る側の token を Release して同じ handle の root から load し直す。Scene は生き残る側の Scene を unload して同じ handle から load し直す。どちらも、結果の Renderer・material・texture・shader が `== null` でないことを見る。
6. **「壊れた」の項目:** §2 の定義の項目だけを数える。Scene の Renderer の `sharedMaterial` が別の `EntityId` に変わったことは定義に無いので数えず、各時点の行（`survivor` の欄）に記録する。生き残る側の参照は probe が T3 まで保持するので、`ReferencesHeldToT3` は構成上つねに真である。
7. **log:** `Application.logMessageReceivedThreaded` で `Log` 以外（Warning・Error・Assert・Exception）を、その時点の phase（`<round>/<呼び出し>`）と main thread かどうかと一緒に記録する。round ごとに Play に入る前に購読と `LogAssert.ignoreFailingMessages = true` を始め、Play を出た後に元の値へ戻して記録する（Test Framework の `LogScope` は log の時点の値で失敗の log を数えることを確かめた）。
8. **一時 root:** `TestResults/sfr/<実行 id>/`（marker 付き）と `TestResults/sfr/first-process.txt`（最後の第 1 process の実行 id）。同じ実行 id の root があれば上書きせずに止め、他の実行の root には触れない。開始時の残りの回収の対象は fixture の folder だけである（実行 id は毎回新しいので、自分の実行の一時 root の残りは生じない）。
9. **前提の確認:** 第 2 process は Enter Play Mode の Domain Reload が無効でなければ開始時に止める（有効だと test の enumerator の位置と局所変数が Play の出入りで失われる）。

**確認したこと。** Unity Editor（6000.6.0f1、この session で起動して正常終了させた）でコンパイルが成功し（`scriptCompilationFailed=False`、Editor.log に `error CS` なし）、5 つの型が `OneStarMaker.Tests.Editor` に読み込まれた。`pwsh tools/contract-audit.ps1` は errors 0、warnings 0。

**未実行（Phase C の責任）。** `RegistrationVerdictTests`、`FirstProcess`、`SecondProcess` は一度も実行していない（§5 の 3 コマンドを実行していない）。したがって、fixture の build、`.resS` の一意な対応、同時登録が受け付けられるか、`LogAssert.ignoreFailingMessages` の下で log を record に結びつけられるか、Game の bootstrap の非同期の初期化が round の log や Scene に混ざらないかは未確認である（§5 の「未知の操作経路の疎通」）。

### 6.1 発見 C の差し戻しの修正（1 巡目、2026-11-12）

- 開始時の照合: 渡された SHA（`e4e92ec4`）と HEAD が一致、作業ツリーは clean、ブランチ一致。`a1c211d7` は HEAD の祖先で、`a1c211d7` から HEAD までの差分は本書だけ。PR #179 の状態ブロックの `引き継ぐ commit` と implementation base / head は §0 と一致。
- **F-1 の修正（commit `0b2b6625`、`RegistrationRounds.cs` だけ）:** `ErrorIn` を、log の種類の集合を引数に取る `LoggedIn` に置き換え、用途ごとに集合を分けた。
  - 同時登録の確認（`CheckAsync` の `FailureRecorded`）: `RegistrationFailureLogTypes`（Warning・Error・Assert・Exception。`OnLog` が捕捉する全ての種類）。§2「同時登録の拒否の条件」が log の種類を限らないため。
  - 登録解除の呼び出し（`UnregisterAsync` の `CallLoggedError`）: `UnregisterRefusalLogTypes`（Error・Exception）。§2「拒否された」の定義のとおり。
  - 集合の定義の上に、用途の間で集合を共有しない理由をコメントで残した。record の項目名（`failureRecorded`・`callLoggedError`）は変えていない。`RegistrationVerdict` と pure テストは変えていない（真偽を受け取るだけなので、§7.1 の「直す向き」のとおり）。
- N-1〜N-3 は判定 C への注意で、修正していない。
- 確認したこと: Unity Editor（6000.6.0f1、この session で起動し、未保存の変更が無いことを確かめて正常終了させた）でコンパイルが成功し（`scriptCompilationFailed=False`）、新しい集合のフィールドが `RegistrationRounds+Session` に読み込まれた。`pwsh tools/contract-audit.ps1` は errors 0、warnings 0。
- 未実行: Unity テストは実行していない（SKILL の Phase B）。修正の効果は、判定 C の record の `order-check` 行の `failureRecorded` と `case` 行の `callLoggedError` を、同じ phase の `unity-log` 行の `logType` と突き合わせて確かめる。

## 7. Phase C

### 7.1 発見 C（1 回目、2026-11-12）

- 種別: 発見（GO 判定はしない。C' は起動しない）。
- 対象: implementation base `aa6b9488`、implementation head `a1c211d7` の完全 diff（§3 の 5 file、2 つの folder の `.meta`、本書）。同じ PC の commit の SHA で受け取り、固定した diff を直接読んだ。
- 使用した evidence bundle の取得案内・状態: 未生成（発見 C は commit で固定した diff と機械検査だけで行った。限定テストの XML と log は git 管理外の `TestResults` にローカルで残し、判定の入力にしない）。
- 開始時の照合: 渡された SHA（`a097c69c`）と HEAD が一致、作業ツリーは clean、ブランチ一致。`a1c211d7` は HEAD の祖先で、`a1c211d7` から HEAD までの差分は本書だけ。PR #179 の状態ブロックの `引き継ぐ commit` と implementation base / head は §0 と一致。

**構造適合（構造を先に見た）。**

- 配置・namespace・asmdef: §3 のとおり。5 file は全て新規で `Tests/Editor/Spikes/SharedFileRegistration/` にあり、asmdef は既存の `OneStarMaker.Tests.Editor`、参照の追加は無い。製品 file・既存 test の変更は無い（AC5 の前半）。
- 依存: `RegistrationVerdict.cs` は `System` だけを使い、Unity の型も製品の型も受け取らない。`RegistrationVerdictTests.cs` は NUnit だけ。`RegistrationFixture.cs` は製品の型を使わない。製品への依存は `RegistrationBuilds.cs`（`BuildContentRoot`・`BuildContentEntry`）と `RegistrationRounds.cs`（`UnityContentDirectoryBackend` と、その token の型、`AbstractApplicationInitializer` の発見）だけで、§6 の記述と一致する。依存は Tests → 製品の一方向。
- 行数の警報: `RegistrationRounds.cs` 658 行（予想 220、500 行超）は **非分割妥当**。責務は Play の round と観測の record の 1 つで、case は `CaseTable` の表と共通の部品で書かれている。case や観測時点の変更は表と `UnregisterAsync` に閉じる。`Probe` と Game の bootstrap の抑止は round の中でだけ使い、寿命と依存が round と同じである。`RegistrationBuilds.cs` 410 行（予想 180）も **非分割妥当**。増分の `SpikeRun`（record）・`Aftercare`（process の前後の比較）・`Git` は両 process が共有する。§3 は record をこの file に置いており、残りの置き場所は決めていなかった。寿命は 1 回の test で、テスト境界も同じ（Unity と file system の I/O）。Phase D で削除する実験 code なので、分ける利益が無い。
- §6 の「HANDOFF との差」9 件: 全て §4 の B 適応の範囲に収まる。範囲は fixture の細部、record の項目、round の順序、第 1 process の出力を読む方法、S2-0 から写す範囲である。残りは、凍結した定義をそのまま実装したことの説明である。1 は GUID を path に戻して照合するもので、名前では対応づけない（I-2 (d) を保つ）。6 は §2 の「壊れた」の項目の列挙どおりである。9 は §2 の「測らないもの」（Domain Reload 有効）と整合する。
- 偽 null: 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定している（`Probe`、`Id`、`Chain`、列挙）。`?.` / `??` は `Loadable`・`OutputFile`・reflection・`int?` にだけ使っている。
- 機械検査: `pwsh tools/contract-audit.ps1` は errors 0、warnings 0。5 file とも先頭が `#nullable enable` で、`record`・`Task.Delay`・`Thread.Sleep`・`GetInstanceID` は無い。

**現在の問いを阻害する findings（B へ差し戻す。1 巡目）。**

- **F-1 log の種類の扱いが §2 の 2 つの語と合わない（B 適応）。** `RegistrationRounds.ErrorIn`（`RegistrationRounds.cs` の 533 行）は Error・Exception・Assert だけを数え、意味の違う 2 か所で同じ集合を使っている。
  - (a) 同時登録の確認（`CheckAsync` の `FailureRecorded`）: §2「同時登録の拒否の条件」は「Unity の log か例外の型が record にある」とし、log の種類を限らない（§2.2 は Warning も捕捉して record に残す）。違反に至る順序: ある順の 2 つ目の `Register` が Warning だけを出して無効な handle を返す → `FailureRecorded` が偽 → `Refused` が偽 → `Plan` がその順の AC2〜AC4 の round を実行する → `RegisterBoth` が例外を投げる → round が中止され、終了行が無く未判定になる。定義では、この場合は (r1) か判定の手順 1 の GO である。
  - (b) 登録解除の呼び出し（`UnregisterAsync` の `CallLoggedError`）: §2「拒否された」は「Error か Exception」とする。Assert を数えるので、Assert だけを出した無傷の case を「拒否された」に分類しうる。
  - 違反する凍結済み条件: §2 の「同時登録の拒否の条件」（AC1）と「拒否された」（AC4）。
  - 直す向き: 用途ごとに集合を分ける。登録の確認は、捕捉した全ての種類（Warning・Error・Assert・Exception）を数える。登録解除は Error と Exception だけを数える。`RegistrationVerdict` は真偽を受け取るだけなので変えなくてよい。変えるのは Unity I/O 側なので pure テストは足さず、判定 C の record の `order-check` 行と `case` 行の値で確かめる。

**阻害しない所見（判定 C が record を読む時の注意。blocker の基準では文言の隙間に当たる）。**

- N-1 T0 で既に壊れている case: `RegistrationVerdict.Classify` は T0 の状態を使わない。解除の前に Release も unload もしない case（U2〜U5）で、T0 で既に「壊れた」の項目に当たっていると、補助の判定はその case を「壊れた」と書く。§2 の「壊れた」は「になる」と書き、既に当たっていた時の未判定の規則は R0 にしか無い。record の `t0` 行に項目が書かれるので、判定 C は U2〜U5 の `t0` 行を見る。空ならこの所見は影響しない。空でない case があれば、補助の判定を使わず、owner の決定記録で読み方を確定してから判定する。
- N-2 判定の手順 1 と AC1 の前提: `Judge` は §2 の手順の順序どおりに、両方の順の拒否（手順 1）を AC1 の前提（digest の照合、同じ file）より先に評価する。凍結した順序のとおりで違反ではない。手順 1 に当たった場合は、判定 C は `second-start` 行の `digestMatched` と `same-file` 行も読み、拒否が「同じ file を持つ 2 つの directory」についての観測であることを確かめてから答えに使う。
- N-3 後始末の Scene の確認: `CleanupAsync` は、残った Scene を名前（`SceneA`・`SceneB`）で探す。Content Directory から読んだ Scene の名前が違うと、wrapper が追跡していない Scene の残りを見落とす。判定 C は `round-cleanup` 行の `fixtureScenesLoaded` を、Scene の load の行と突き合わせる。

**後続スライスへ移送する findings:** なし。

- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration.RegistrationVerdictTests`（§5 の起点 filter。pure な写像のテストは Phase B で一度も実行されていなかった。sandbox 外の承認済み経路で、Unity Editor が閉じていることを確かめてから実行した）。
- テスト結果: XML の root result `Passed`、total 45、passed 45、failed 0（`RegistrationVerdictTests` の 35 メソッド、TestCase を含めて 45 件）。exit 0、Unity は正常に終了した。
- 判定必須のうち未実行: `FirstProcess` と `SecondProcess`（§5 の (2)・(3)）。差し戻しが決まったので実行しない。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし（build と Play は実行していない）。
- 未確認事項: §6 の「未実行」の各項目（fixture の build、`.resS` の一意な対応、同時登録が受け付けられるか、log を record に結びつけられるか、Game の bootstrap の混入）は、判定 C の最初の実行で確かめる（§5 の「未知の操作経路の疎通」）。加えて、round の後始末の `UnloadUnusedAssets` の後に、Unity が `SharedShader` の物を残すかも未確認である。残すと、次の round の開始時の 0 件の確認で、以降の round が全て未判定になりうる。これは Unity の挙動であり、判定 C の record で確かめる。
- 担当・モデル: 発見 C は Claude（Anthropic）の新しい session で行った。モデルは Phase B と同じで、「Phase C は Phase B と異なるモデルで行う」の条件を満たさない。owner が「独立性制約あり」として受け入れた（2026-11-12、進行役を通じた指示）。残存リスク: B と同じモデルの見落としの傾向を発見 C が共有しうる。C' は Phase A・B・C に関与していない担当で行う（§0）。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
