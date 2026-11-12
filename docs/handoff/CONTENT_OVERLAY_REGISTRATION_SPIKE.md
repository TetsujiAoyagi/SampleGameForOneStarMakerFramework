# 取得した build への上書きの登録の実証

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-1）
- status: `A`（revision 0 の A3 の統合版。**未凍結。** A2 の 3 件と、draft PR #182 の外部レビュー 1 巡目、それを受けた主担当の見直しを反映した（§5.1）。A2 入力版は commit `1cfcad07`）
- branch: `claude/content-overlay-registration-spike`（PR の base は `develop`）
- implementation base commit: `bc3ca436`（`develop` の head、2026-11-13。§1 の現況はこの base）
- implementation head commit: 未固定
- risk: `high`（program §8 の指定。製品 code は変えないが、結論が S2・S3 の形を決める）
- owner: root（依頼者）
- created: 2026-11-13
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「同じ file を持つ 2 つの directory の登録」の節の「測っていないもの」のうち「中身が違う同じ id の file を両方登録した時」を実測の事実へ置き換える。新しい文書は作らない）、program §5.3「当面の方針」と §8（本スライスの行と S2 の行の改訂）。実験 code は製品に残さず git 履歴に置く（S0-b・S2-0・S2-0b と同じ）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§5.3「当面の方針」、§8 の S2-0・S2-0b の移送）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（S2-0・S2-0b の実測と harness の所在）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（凍結）。**凍結していない。** 外部レビューの指摘を反映し、owner の凍結の指示で A3 凍結版を切る（§2.5 の U-1・U-2 は裁定済み）。
  - 次の作業: 外部レビューの指摘を §5.1 に採否付きで記録する。目的・利用者の欄を変えたら目的レビューをやり直す。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結の指示があるまで Phase B を始めない。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 取得した 1 つの目的 build に、開発者が手元で build した上書き（自分が触った Scene と asset だけの Content Directory）を後から登録して Editor で Play した時、上書きに入れた物は上書き側が読まれるか、上書きに入れていない物も含めて component に正しい class が付くか、同じ asset の実体の重複が上書きの分に収まるか、読んだ物が Play の終わりまで壊れないかを、Unity 6000.6 の Editor の Play の実測だけで確かめる。その結果から、owner が「取得した build 1 つ + 手元の上書き」を開発の Play の形として採るか、別の形へ縮退するかを決められるようにする。
- 利用者: OSMFramework の開発者（配布単位、上書きの登録、Play の入口を後続で設計する owner と、それを実装する人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続は §2.3）:
  - 上書きを製品に入れること（2 件目の登録を拒否する `InstallContentDirectory`・`ContentRevisionGate` の変更、resolver の候補、Play の入口）。本スライスは製品 code を変えない。
  - 上書きの build に何を入れるかを決める道具（触った asset から、入れるべき Scene を逆引きする等）。
  - 目的 build 同士を同時に登録する形（program §5.3 で主にしないと決めた）、3 つ以上の directory。
  - Player。上書きは開発の Editor の Play だけで使う形なので、Player で確かめ直さない。
  - 別の PC・別の `Library` で build した base（base は取得物で、上書きは手元の build という実際の形）。本スライスは 1 台の PC・1 つの `Library` で両方を build する。
  - URP の `Lit` の shader の variant の解決（§2.5 の U-1）。
  - 後から登録した directory を Play の途中で解除すること。先に登録した側から `Loadable` で載せた物を黙って壊すと S2-0b が実測済みで、本スライスは「上書きは Play の終わりまで解除しない」を前提に置く。
- 現況（base `bc3ca436`）:
  - **方針（program §5.3、2026-11-12、owner）:** 開発では重複を許し、リリースでは重複させない。開発者は取得した 1 つの目的 build に、自分が触った物だけ手元で build して上書きとして後から登録する。
  - **実測済みの事実（[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)）:** 別々に build した directory の間で、共通の texture・material・prefab・variant の無い自作 shader の content file は同じ id・同じ bytes になる。build 内の全 MonoScript をまとめた file と URP の `Lit` の shader は、同じ id のまま、build の組み合わせで bytes が変わる。中身が同じ file を持つ 2 つの directory を両方登録すると、`Loadable` からの load では実体は 1 つ、Scene の直接参照からの load では directory の数だけできる。後から登録した側を解除すると、先に登録した側から `Loadable` で載せた物が Error なしに `== null` になる。
  - **測っていないこと（本スライスが測る）:** 同じ id で中身が違う file を両方登録した時の解決。上書きの形では必ず起きる。(a) 編集した Scene と asset は、同じ id で中身の違う file が base と上書きの両方にある。(b) script をまとめた file は、base と上書きで使う script の顔ぶれが違えば、同じ id で中身が違う。
  - **Unity の公開文書（6000.6、2026-11-12 に確認）が言うこと:** 登録中の directory は登録の逆順に探索される。`GetRootAssets<T>()` は handle を渡すと 1 つの directory の root を返す。各 build は自己完結し、同じ asset を参照する 2 build はそれぞれ複製を持つ。**言わないこと:** 同じ id で中身の違う file が 2 つの登録中の directory にある時、どちらを読むか。Scene の直接参照と、component から MonoScript への参照が、Scene のある directory の中で解決されるか、逆順の探索で解決されるか。
  - **script の参照の仕組み（build 履歴の `ContentLayout.json` の読み取りと公開の形式文書から。bytes の中は見ていない）:** build された component は GUID を持たず、「MonoScript の object」（assembly 名・namespace・class 名）を file の id とその中の番号で指す。Content Directory はその build の MonoScript を 1 つの file にまとめ、その file の id は build によらず同じになる。したがって、後から登録した directory の一覧だけが読まれるなら、base の Scene の component は、上書きの一覧に無い script で Missing になるか、番号が被れば別の class が付く。
  - **build 履歴の読み取り（2026-11-13、この PC の `Library/BuildHistory` の `ContentLayout.json` 50 件。Unity を起動せずに読んだ。fixture と SampleGame の build が混ざる）:** MonoScript をまとめた file の id は 50 件とも同じだった。その file の `SourceAssets` は、その build が使う script の path の一覧で、assembly の全 script ではない（最小は `BuildContentRoot.cs` の 1 件）。一覧が同じ build は `ContentHash` も同じで（一覧 4 通りに hash 4 通り）、一覧が違えば hash も違った。Scene は `LoadableSceneIds` の項目が GUID と `SerializedFile` の番号を持つので、Scene の file の帰属は GUID で引ける。layout は file の中の object の番号を持たない。
  - **Scene の id の作り方:** 製品の build は `LoadableSceneIdEditorUtility.CreateLoadableSceneId(path)` で Scene の id を作る（`UnityContentDirectoryAdapter`）。load は handle を取らない `SceneManager.LoadSceneAsync(sceneId)` である。したがって、同じ path の Scene を持つ base と上書きでは、どちらの root から取った id も同じ要求になる見込みで、どちらの directory の Scene が出るかは Unity が決める。`Loadable` も、S2-0b の実測では両方の root の `LoadableObjectId` が等しかった。
  - **製品の native 層:** `UnityContentDirectoryBackend`（`Scripts/Runtime/BuildContent/`、133 行、internal）は 1 つの handle を包み、`Register`・`GetRoots`・`LoadObjectAsync`・`LoadSceneAsync`・`Release`・`UnloadSceneAsync`・`Unregister` を持つ。2 件目の拒否はこの層ではなく `AssetManagement.InstallContentDirectory` と `ContentRevisionGate` にある。root は `BuildContentRoot`（Scene は `LoadableSceneId`、Object は `Loadable<Object>`）。
  - **前例:** S2-0b の harness（commit `444651ed` の `Tests/Editor/Spikes/SharedFileRegistration/`、約 2,400 行）は、EditMode の `[UnityTest]` から Play に入り、製品の wrapper を無改変で 2 つ持って 2 つの directory を登録し、実体の数と壊れ方を JSONL の record に書いた。fixture は固定 GUID で、自作の script を意図的に持たせていない。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力: `なし`（開発時の宣言も実行時の操作も無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 取得した 1 つの build（以下 base）に、手元で build した上書きを後から登録した Editor の Play で、次の 4 つが成り立つか。
  - **Q1 読まれる側:** base と上書きの両方にある Scene と asset を要求した時、上書き側の中身が読まれるか。
  - **Q2 class:** base にだけある Scene と、上書きの Scene の component に、正しい class と値が付くか。
  - **Q3 重複:** 同じ asset の実体の数が、base の 1 つと上書きの 1 つに収まるか。
  - **Q4 寿命:** 上書きを解除しない限り、Scene の出入りと `Loadable` の保持・解放を繰り返しても、読んだ物が Play の終わりまで壊れないか。
- 成果の範囲: 結論は「この PC、Unity 6000.6.0f1、StandaloneWindows64、Editor の Play（Domain Reload 無効）、loose file、合成 fixture、base と上書きを同じ `Library` で build」に限る。record の先頭に書き、Player・別の PC・実データへ広げない。言えるのは「測った範囲でこう振る舞った」までで、「設計上そうなる」は言わない。
- どうすればこの問いに誤って YES を出せるか（C と C' はこれを探す）:
  - (a) 危険な条件が fixture に入っていない。script をまとめた file が base と上書きでたまたま同じ中身になる、編集したはずの Scene・asset の file が同じ bytes になる（P の前提の確認。満たさなければ未判定）。
  - (b) 「null でない」だけを見て、別の class が付いたのを見逃す。型の集合だけを比べて、同じ GameObject の 2 つの probe の型が入れ替わったのを見逃す（`GetComponents` の並びの位置ごとに、class の完全名と、component ごとに違う値を照合する）。
  - (c) Missing の component を数えない（`GetComponents<Component>()` の中の `== null` を数える）。
  - (d) 上書き版か base 版かを区別できない物で Q1 を答える（版ごとに違う名前の GameObject と、版ごとに違う pixel を持たせる）。
  - (e) 期待値を、測る対象と同じ 2 directory 登録の Play から採る（期待値は fixture の定義から採る。各 directory を単独で登録した基準は、その経路が成り立つことの確認に使う）。
  - (f) Editor の AssetDatabase の物や前の round の物が混ざる（fixture の source を Play の前に消す、round ごとに新しい Play、開始時に 0 件を確かめる）。
  - (g) Unity の Error・Warning を握りつぶす（全件を round と呼び出しに結びつけて record に残す）。
  - (h) 1 回の観測で答える（各 round を、順を変えた 4 つの Unity process で行い、違えば未判定）。
  - (i) 前の Play が読んだ file や MonoScript が process に残り、後の round の参照がそれに解決されて正しく見える（Domain Reload 無効。各 round に、その process で最初に content を読む Play になる回を 1 つ持たせ、答えはその回から採る。単独の基準は最後に行う）。
  - (j) base の handle から取った root が、実は上書きの root である（root を別の asset にし、handle ごとの `BuildIdentity` を照合する）。
  - (k) script をまとめた file が「後から登録した側」ではなく「先に読んだ側」で固定される作りだと、Scene を load する順で壊れる側が変わる（base の Scene と上書きの Scene の両方の probe を見る。AC4 は base の Scene を先に、AC5 の (4)・(5) は上書きの Scene を載せたまま base の Scene を読み直すので、両方の順が入る）。
  - (l) 対照の順の答えが主の順と同じであることを、前の round の残りが効いたまま「登録の順は無関係」と読む（対照の答えも、対照が最初の Play になる process から採る）。
- 判定に使う語（実測の前に固定する）:
  - **base / 上書き:** fixture の source を v1 の状態で build した directory が base、source を v2 に変えた後に一部の root だけを build した directory が上書き。**主の順** は base → 上書きの登録、**対照の順** は上書き → base。
  - **危険な条件の前提（P）:** 両 directory の出力の棚卸し（名前・size・sha256）と `ContentLayout.json` から、登録の前に次を確かめる。P1: MonoScript をまとめた file が両方にあり、id が同じで sha256 が違い、**その file の layout の `SourceAssets` の一覧**について、base の一覧に `ProbeBaseOnly` と `ProbeBoth` があって `ProbeOverlayOnly` が無く、上書きの一覧に `ProbeOverlayOnly` と `ProbeBoth` があって `ProbeBaseOnly` が無い。見るのはこの file の一覧だけで、Scene や prefab の中身に型の差があることは P1 の根拠にしない（sha256 の差だけでも根拠にしない）。一覧の path は、その script の `.meta` の GUID と結びつけて照合し、file 名だけでは引かない。P2: `SceneEdit` と `EditedTex` の file が両方にあり、id が同じで sha256 が違う。P3: `SharedTex`・`SharedMat`・`SharedShader`・`EditedMat` の file が両方にあり、名前と sha256 が同じである。帰属は固定 GUID（Scene は layout の `LoadableSceneIds` の GUID、その他は GUID と local id）で引き、名前で対応づけない。
  - **版:** `SceneEdit` は root の GameObject の名前（v1 は `Marker_v1`、v2 は `Marker_v2`）、`EditedTex` は pixel の sha256（v1 と v2 で違う単色）で区別する。
  - **正しい class:** 決めた GameObject の `GetComponents<Component>()` に `== null` が無く、その並びから probe でない物（`Transform`、Renderer 等）を除いた列が、fixture が付けた順の期待の列と**位置ごとに**一致する。一致とは、`GetType().FullName` が期待の名前と同じで、serialized の値（`_tag`、`_value`）が、fixture がその component に書いた値と同じであること。値は全ての probe の component で互いに違える（同じ GameObject の 2 つも違う値）。型の集合が同じでも、位置が入れ替わっていれば正しくない。値は `JsonUtility.ToJson` で読む（test の asmdef から probe の型を参照しない）。
  - **実体の数:** S2-0b と同じ。load の結果の相異なる `EntityId`（`EntityId.ToULong(GetEntityId())`）の個数で、`Resources.FindObjectsOfTypeAll` の同名・同型の列挙と集合が一致する時だけ答えに使う。物理メモリの量ではない。
  - **壊れた:** 保持している物について、`== null` になる、texture の pixel の sha256 が load 直後と違う、material の `mainTexture`・`shader` が `== null` か別の `EntityId` になる、Renderer の `sharedMaterial` が `== null` になる、component が正しい class でなくなる、のどれか。偽 null は `== null` で判定する。`EntityId` の比較は「その物を最後に load した時点から、今の確認点まで」で行う。Scene を load し直した後の Scene の物は新しい load として扱い、前の load の `EntityId` と比べない。段をまたいで同じ `EntityId` を追うのは、`Loadable` で保持し続けている物だけである。
- 各問の答え（round の観測から機械的に決める）:
  - **Q1:** 物ごとに、**強**（base の root の id から要求しても、上書きの root の id から要求しても v2）、**弱**（上書きの root の id からは v2、base の root の id からは v1）、**不成立**（上書きの root の id から要求して v1 が出るか、load できない）。`SceneEdit` と `EditedTex` の両方に付け、弱い方を Q1 の答えとする。両方の root の id が等しい時は、2 つの要求は同じ 1 つの要求なので、弱は起こらず、強か不成立になる（現況の「Scene の id の作り方」から、等しい見込み）。id が等しいかどうかを record に残す。
  - **Q2:** **正しい**（`SceneKeep`、上書き版の `SceneEdit`、base の root から load した `SharedPrefab` の全 probe が正しい class）、**誤り**（どれかが Missing・別の class・値の不一致）。
  - **Q3:** **収まる**（`SharedMat`・`SharedTex`・`SharedShader` の実体がそれぞれ 2 以下で、上書きに無い `SharedPrefab` と `KeepTex` が 1）、**超える**。
  - **Q4:** **無傷**（Q4 の round の全ての確認点で、保持している物が壊れておらず、終わりの解除の後に登録が 0 件）、**壊れた**。確認点の間に Unity が出した Error・Exception は答えに添えて書く（壊れていなければ「無傷（log あり）」）。
- **答えを採る回:** 各 round の答えは、その round が process の最初の Play になった回（§2.2 の round の順）から採る。他の 3 つの process の同じ round の答えは照合に使い、1 つでも違えば未判定とする。
- スパイクの答え（主の順の round だけから決める）:
  - **YES:** Q1 強、Q2 正しい、Q3 収まる、Q4 無傷。
  - **条件付き YES:** Q1 が弱で、他は YES と同じ。条件は「要求する側が、上書きの root の id を base の物より先に引く」。id が違った時にだけ起こる答えで、強の YES をこの条件へ読み替えない。
  - **答えの範囲:** YES も条件付き YES も、登録の順を base → 上書きに固定した時の答えである。
  - **NO:** 上のどちらでもない。どの問がどう外れたかを添える。
- 判定定義。スパイクの合否は「答えが証拠付きで出たか」で決め、答えの向きは合否に使わない（S2-0b と同じ）。
  - **未判定（結論を出さない）:** build を開始できない。P1〜P3 のどれかが成り立たない。第 2 process の入力が第 1 process の同じ実行の出力だと照合できない。Play の round の開始時に fixture の source が AssetDatabase に残っているか、登録中の directory か fixture の名前の物が 1 件以上ある。単独の基準（AC2）で期待と違う。handle ごとの root の `BuildIdentity` が、その directory の build の物と一致しない。record の Enter Play Mode の設定が Domain Reload 無効でないか、target が StandaloneWindows64 でない。round の `Register` の順が record に無いか、その round の定義と違う。終了行の無い round がある。実体の数が整合の規則に合わない。同じ round の答えが process の間で違う。round の後始末が終わらない。harness・fixture・環境の問題は B へ戻し、Unity の挙動が定義した語で読めない時と、process の間で答えが違う時（前の Play の残りが答えを変えるという事実で、それ自体を結論に書く）は Phase A へ戻す。
  - **GO（答えが出た）:** 未判定に当たらず、record から Q1〜Q4 の答えとスパイクの答え（YES / 条件付き YES / NO）が一意に読める。主の順で 2 つ目の登録か、上書きの下での load を Unity が拒否した時は、その log と状態が record にあれば「NO（登録できない / load できない）」という答えであり、未判定ではない。**答えにできるのは、同じ呼び出しが単独の基準（AC2）では成功し、2 つを登録した時だけ失敗した場合に限る。** 単独の基準でも失敗する呼び出し、path の誤り、manifest か type tree の欠落、harness の例外による失敗は答えにしない（未判定）。
  - CONDITIONAL ACCEPT と NO-GO は使わない。NO は GO の一形（答えが出た）である。
  - **test の合否と判定は別である。** 第 2 process の 4 つの test が assert するのは harness の成立だけ（全 round の開始行と終了行、後始末、log の購読と `LogAssert` の復元、record を閉じた）。答えの向きは assert しない。XML の failed 0 は「harness が最後まで動いた」ことだけを意味する。判定は判定 C が record の観測の行から読む。`OverlayVerdict` が record に書く答えの行は補助である。
  - 結論は、実測した事実と、仕組みの推測を分けて書く。
- **後続への含意（判定ではない。結論に添えて owner へ渡す。行は排他ではなく、当たった行を全て書く）:**

| 答え・観測 | owner が決めること |
|---|---|
| YES | 「base 1 つ + 上書き」を開発の Play の形として採る候補にできる。program §8 の S2 の行を「目的 build を選ぶ索引 + 上書きの登録契約」へ縮めるか |
| 条件付き YES | 同上。加えて、resolver が上書きの entry を先に引く規則を S3 の契約に入れる |
| NO（Q2 が誤り） | 上書きの形は script の顔ぶれが違うと使えない。縮退の候補: 触った物を含む目的 build を手元で丸ごと再 build する（増分の所要は実測済み）、script の顔ぶれを全 build で揃える、Unity へ問い合わせる |
| NO（Q1 不成立、Q3 超える、Q4 壊れた、登録・load の拒否） | どの問が外れたかに応じて、上書きの形を採れる範囲を狭めるか、縮退するか |
| `SceneKeep` の `EditedTex` が v1 のまま（AC4 の観測） | 共通の asset を編集した時、それを直接参照する Scene も上書きに入れないと、古い見た目と新しい見た目が混ざる。上書きの build に入れる Scene を逆引きする道具を誰が持つか（S2 か S5） |
| 対照の順（対照が最初の Play の回）で答えが主の順と違う | 製品は登録の順を base → 上書きに固定する |
| 対照の順で答えが主の順と同じ | この fixture の範囲では、登録の順で答えは変わらなかった。製品の順を緩めてよい根拠にはしない（測ったのは 1 つの fixture） |
| YES（id が等しい） | 上書きを登録している間、要求する側は base の版を選べない。それでよいか（開発の Play では上書きを見たいので、既定はよい） |

- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO で終了する。最低条件未達（未判定）のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 危険な条件の無い fixture で答えない:** 答えは、P1〜P3 が登録の前の record で成り立った実行からだけ出す。
  - 崩しうる経路: (a) base と上書きの component の型の集合が結果的に同じになり、script をまとめた file が同じ bytes になる（`BuildContentRoot` 自体の script は両方に入る。probe の 3 型で差を作り、P1 で確かめる）。(b) v2 への書き換えが import されず、上書きに v1 の file が入る（P2）。(c) 共通物が他の asset と同じ file に入り、bytes が変わる（P3）。(d) layout を名前で引いて別の asset の file を見る（固定 GUID と local id）。(e) base と上書きの root を同じ asset の書き換えで作り、root の file まで同じ id で中身違いになって、base の handle が上書きの root を返す（root は別の固定 GUID の asset にし、handle ごとの `BuildIdentity` を照合する）。(f) probe の script が build に入らず、Scene の component が最初から Missing になる（P1 が layout の source で確かめ、AC2 の単独の基準が class を確かめる）。
  - 探索した境界: S2-0・S2-0b の harness と実測。未確認: test 用 assembly の probe が、その file の一覧に入ること（P1 が確かめる。一覧が使う script だけを持つこと、id が build によらず同じことは、build 履歴 50 件で確認した）。layout は番号を持たないので、番号の衝突は前提にせず、Q2 の観測（位置ごとの class）で受ける。Scene の file の帰属は layout の `LoadableSceneIds` の GUID で引ける（build 履歴の読み取りで確認）。
- **I-2 見分けられない観測で答えない:** Q1 は版を区別できる物、Q2 は class の完全名と値、Q3 は整合した実体の数、Q4 は保持したままの参照で答える。期待値は fixture の定義から採る。
  - 崩しうる経路: (a) Editor の AssetDatabase が fixture の source（Scene・prefab・material・texture）を載せ、Content Directory の物でない実体が観測に混ざる（fixture の folder を Play の前に消し、消したことを確かめる。probe の script は test 用 assembly に残る。build された component は script を名前で指すので、script の source が残ること自体は class の解決を助けない、という §1 の参照の仕組みに立つ）。(b) Domain Reload 無効で前の Play の物が残る（round ごとに新しい Play、開始時に 0 件）。(c) Missing の component が `GetComponent<T>` では見えない（`GetComponents<Component>()` の `== null` を数える）。(d) 参照を離した後の `== null` を壊れたと読む（Q4 の確認点まで参照を保持する）。(e) `?.`・`is null` で偽 null を見落とす。(f) 単独の基準や前の round が読んだ script の file が process に残り、主の順の参照がそれに解決される（各 round が最初の Play になる process を持つことで防ぐ。開始時の 0 件の検査は MonoScript を見られない）。
  - 探索した境界: S2-0b の数え方（`FindObjectsOfTypeAll` は Content Directory から載った texture を見た）。未確認: source を消した後の Play で、Content Directory から載った probe の component の `GetType()` が test 用 assembly の型に解決されること（単独の基準 AC2 が確かめる）。
- **I-3 製品を変えない:** §3 の新規 file と本書・program 以外の変更が無く、実験は assets・設定・build の出力を repo に残さない。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) Build Settings・active target・Enter Play Mode の設定の変更。(c) round が登録を解除せずに終わる。(d) log の購読と `LogAssert.ignoreFailingMessages` が次の test に残る。(e) 残りの回収が、自分の marker の無い folder を消す。
  - 範囲: 守るのは git で追跡する file、fixture の folder、`Assets/OneStarMakerGenerated`、一時 root。`Library` の cache は対象にしない。

**成立条件（起きなければならないこと）**

- **E1 準備:** base と上書きが build でき、P1〜P3 が record にある。
- **E2 基準:** 各 directory を単独で登録した時の版・class・値が record にあり、fixture の定義と一致する。
- **E3 答え:** 主の順で Q1〜Q4 の答えが、対照の順で Q1・Q2 の答えが、4 つの process の全ての record にあり、各 round に、それが最初の Play になった回がある。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id、head、Unity の版、target、Enter Play Mode の設定を持ち、round ごとに開始行と終了行がある。Play の round は全て新しい Play で行い、開始時に登録中の directory が 0 件であることと fixture の名前の物が 0 件であることを記録する。登録は製品の `UnityContentDirectoryBackend` を無改変で 2 つ持って行い、両方を登録してから load を始める。Unity の Error・Exception・Warning の log は test の失敗にせず（`LogAssert.ignoreFailingMessages`）、発生順のまま全件を round と呼び出しに結びつけて record に残す（`LogType.Log` も残す）。async の load・unload・`UnloadUnusedAssets` は完了を待つ。後始末は `finally` で行い、登録が残れば `GetContentDirectories` の handle から直接解除して記録する。**round の順と反復:** 第 2 process の test は、最初に行う round だけが違う 4 つ（`ClassFirst`・`LifetimeFirst`・`ReadFirst`・`ControlFirst`）で、それぞれ別の Unity process で 1 回ずつ実行する。どの process も AC3〜AC6 の全 round を行い、最初の round だけを順に AC4・AC5・AC3・AC6 とし、残りは決めた順で続け、単独の基準（AC2）を最後に行う。4 つとも同じ第 1 process の出力を読み、record は process ごとに分ける。各 round の答えは、その round が最初の Play になった process から採り、他の 3 つは照合に使う（§2 の「答えを採る回」）。各 round で、`Register` を呼んだ順と、両方の handle の root の `BuildIdentity` を記録し、その directory の build の物と照合する。

- **AC1（E1、I-1）準備と前提:** 第 1 process が fixture（§3）を v1 で作って base を build し、source を v2 に変えて上書き（root は `SceneEdit` と `EditedTex` だけ）を build し、両方の出力の全 file の名前・size・sha256 と `ContentLayout.json` の写しを記録し、P1〜P3 の成否を record に書き、fixture の folder を消して終わる（消したことを file system で確かめる）。P1〜P3 のどれかが成り立たなければ未判定。
- **AC2（E2、I-2）単独の基準:** 第 2 process が、第 1 process の同じ実行の出力であることを実行 id・head・棚卸しの digest で照合した後、base だけを登録した round で `SceneKeep`・`SceneEdit`（v1）・`SharedPrefab` の版・class・値と `EditedTex`（v1）の pixel を、上書きだけを登録した round で `SceneEdit`（v2）と `EditedTex`（v2）を記録する。どれも fixture の定義と一致する（一致しなければ未判定）。単独の基準は期待値の出所ではない（期待値は fixture の定義）。fixture と load の経路が単独で成り立つことの確認と、Unity の拒否を答えにできるかの切り分け（§2 の GO）に使う。
- **AC3（E3、Q1）読まれる側:** 主の順の round で、base の root の `SceneEdit` の id と上書きの root の `SceneEdit` の id が等しいかを記録し、それぞれから 1 回ずつ load して版を記録して unload する。`EditedTex` を両方の root の `Loadable` から load して両方を保持し、pixel の版、`EntityId`、`LoadableObjectId` が等しいかを記録する（等しい時は、`EditedTex` に「弱」は起こりえず、強か不成立になる）。
  - 合格例（強）: どちらの id からも `Marker_v2`。不合格ではない例（弱）: 上書きの id から `Marker_v2`、base の id から `Marker_v1`。
- **AC4（E3、Q2・Q3）class と重複:** 主の順の round で、`SceneKeep`（base の root の id）、`SceneEdit`（上書きの root の id）の順に additive に load して両方を載せたままにし、base の root から `SharedPrefab` を load して保持した状態で、全 probe の class と値、Missing の数、`SharedMat`・`SharedTex`・`SharedShader`・`SharedPrefab`・`KeepTex` の実体の数を記録する。あわせて、`SceneKeep` の Renderer が直接参照する `EditedMat` の `mainTexture` の pixel の版を観測として記録する（答えには使わない）。
  - 失敗経路の例: `SceneKeep` か `SharedPrefab` の `ProbeBaseOnly` の位置の component が `== null`（Missing）、または `FullName` が `ProbeOverlayOnly`・`ProbeBoth` になる。どちらも Q2 は誤り。
- **AC5（E3、Q4）寿命:** 主の順の round で、次の列を順に行い、各段の後に、その時点で保持している物（Scene の probe と Renderer の material・texture、`Loadable` の物）が壊れていないかを記録する。(1) `SceneKeep` と `SceneEdit`（上書きの id）を load、base の root から `SharedPrefab`、上書きの root から `EditedTex` を load して保持。(2) `SceneEdit` を unload し `UnloadUnusedAssets`。(3) `SceneEdit` を load し直す。(4) `SceneKeep` を unload し `UnloadUnusedAssets`。(5) `SceneKeep` を load し直す。(6) `Loadable` を Release し、両 Scene を unload し、上書き → base の順に解除して、登録が 0 件であることを記録する。(6) の間の log は (1)〜(5) と分けて記録する。
  - 境界: 「Play の終わりまで」は (6) の解除の完了までを指す。Play を抜けた後の Editor の状態は AC8 が見る。
- **AC6（E3、対照）対照の順:** 上書き → base の順で、AC4 と同じ観測、AC3 と同じ観測の順に 2 つの round を行う。答えは対照として記録し、スパイクの答えには使わない。対照の答えは `ControlFirst` の process の最初の round（AC4 と同じ観測）から採る。AC3 と同じ観測の方は最初の Play になる回を持たないので、主の順と違った時にだけ含意に使い、同じだった時は根拠にしない。
- **AC7（I-2）数え方の成立:** AC3〜AC6 の各 round で、load の結果の `EntityId` の集合が列挙の集合と一致する。一致しない round は未判定。
  - 補足（AC4・AC5 に共通）: `SceneKeep` と `SharedPrefab` は上書きに無い。上書きを登録した下でこれらを要求して load できない時は、単独の基準で load できていれば「NO（load できない）」という答えである（Unity が、後から登録した directory に無い物を先に登録した directory へ探しに行かない、という事実になる）。
- **AC8（I-3）製品の無変更と後始末:** base から head までの変更が §3 の新規 file と `.meta`、本書、program だけである。`pwsh tools/contract-audit.ps1` が合格する。全ての process それぞれの前後で `git status --porcelain` が一致し、fixture の folder が無く、Build Settings・active target・Enter Play Mode の設定が前後で同じである。marker の無い残りがあれば何も消さずに止まる。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 上書きの製品化（2 件目の登録、上書きの寿命、resolver の候補、Play の入口） | S3（program §8）。入口は S5 |
| 上書きの build に入れる Scene の逆引きと、上書きを作る操作 | S2 の改訂後の行か S5（本スライスの Phase D で owner が決める） |
| 目的 build を選ぶ索引（program §8 の S2 の行の改訂） | 本スライスの Phase D で owner が決める |
| 別の PC・別の `Library` で build した base と手元の上書きの組み合わせ | S4 |
| URP の `Lit` の shader の variant が上書きの下で正しく解決されるか | §2.5 の U-1 で owner が決める。既定は S2 の A1 |
| Scene を load した後に上書きを登録する順序、Play の途中での上書きの作り直し | S3 |
| Scene に置いた prefab instance・nested prefab・sprite atlas・audio | S2 の A1 |
| Domain Reload 有効の Play | S3 |
| 上書きで script の code や field を変えた時、上書きが 2 つ以上ある時 | S2 の改訂後の行 |
| GPU 側の資源の重複と、実データでの重複の量 | S2（実データの集計の後） |
| base の file が disk から欠けた時にどちらの file を読むか | S4（S2-0b の移送） |

### 2.4 スライス分割の判断

契機: Q1〜Q4 は独立して失敗しうる未知である。**分割しない。** 4 つとも同じ 2 つの directory と同じ Play の往復で測れ、分けると build と harness を作り直す。Q2 が NO なら Q3・Q4 の価値は下がるが、同じ実行で一緒に出るので先に Q2 だけを測る利得は小さい。`Lit` の variant は観測に描画と pixel の判定が要り、失敗の理由も別（graphics の有無）なので、本スライスに入れず §2.5 の U-1 に分けた。

### 2.5 未決事項（owner へ）

**owner の裁定（2026-11-13）:** U-1・U-2 とも推奨どおりとする。`Lit` の variant は本スライスで測らず（§2.3 の後続へ）、対照の順（AC6）は測る。

- **U-1 `Lit` の variant:** URP の `Lit` の shader も、script をまとめた file と同じく、同じ id のまま build の組み合わせで中身が変わる。上書きの下で base の material の variant が欠けるかは、本スライスでは測らない案を推奨する。理由: 欠けたかどうかは描画した pixel でしか分からず、graphics ありの実行と色の判定が要る。script の結果（Q2）で、同じ id の file を Unity が directory ごとに解決するかどうかの見込みが先に立つ。測るなら、Q2 が正しいと出た後に小さな後続として切る。
- **U-2 対照の順:** 上書き → base の順（AC6）は、製品が登録の順を固定すべきかを知るための対照で、process ごとに round が 2 つ増える。推奨: 測る。

## 3. 責務マップ

新規 file は `unity/Assets/OneStarMaker/Tests/Editor/Spikes/OverlayRegistration/`（既存の `OneStarMaker.Tests.Editor` asmdef。新しい asmdef も参照も足さない）と、probe の 3 file だけである。製品の file は変えない。全て Phase D で削除する。S2-0b の harness（commit `444651ed`）から、fixture の固定 GUID・marker、棚卸し、record、Play の round の枠、log の捕捉、実体の数え方を写して縮める。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---:|---|
| `OverlayVerdict.cs` | round の観測（版、class の名前と値、Missing の数、実体の数、壊れた項目、前提 P）を受け取り、Q1〜Q4 の答え、スパイクの答え、未判定の理由を返す。入力は文字列・数値・真偽だけ | 200 | Unity なしで単体テストできる pure な写像 |
| `OverlayVerdictTests.cs` | 上の写像だけの EditMode test。§2 の (a)〜(e)・(h)・(j) の誤りが YES にならないこと、型の集合は同じで位置だけ入れ替わった入力が正しいにならないこと、P1 を Scene の中身の差や sha256 の差だけで満たさないこと、答えを採る回と process の間の不一致、Unity の拒否を答えにする条件（単独の基準との組）、強・弱・不成立、Missing と別の class、P の不成立、反復の不一致 | 250 | 通常の EditMode 回帰に入る |
| `OverlayFixture.cs` | fixture の生成（固定 GUID、v1 と v2）、所有の marker、残りの回収と削除 | 300 | Unity / `AssetDatabase` の I/O |
| `OverlayBuilds.cs` | `[Explicit]` の `FirstProcess`。base と上書きの build、棚卸しと sha256、`ContentLayout.json` による帰属と P1〜P3、record、fixture の削除 | 350 | Unity の build の I/O |
| `OverlayRounds.cs` | `[Explicit]` の `[UnityTest]` 4 つ（`ClassFirst`・`LifetimeFirst`・`ReadFirst`・`ControlFirst`。round の並びだけが違う）。照合、AC2〜AC6 の round、観測と log の record、後始末 | 520 | Play を伴う orchestration。判断は `OverlayVerdict` に置く |
| `Tests/TestSupport/ProbeBaseOnly.cs`・`ProbeOverlayOnly.cs`・`ProbeBoth.cs` | `_tag`（string）と `_value`（int）だけを持つ空の `MonoBehaviour`（namespace は `OneStarMaker.Tests.Spikes.OverlayProbes`、先頭に `#nullable enable`。期待する `FullName` はこの namespace と class 名）。base の Scene と `SharedPrefab` は `ProbeBaseOnly` と `ProbeBoth`、上書きの `SceneEdit` は `ProbeOverlayOnly` と `ProbeBoth` を持つ | 各 15 | Content Directory の component が指す script は Editor 専用でない assembly に要る。既存の `OneStarMaker.TestSupport`（`UNITY_INCLUDE_TESTS`、全 platform）に置き、asmdef を変えない。test 側は型を参照せず名前で照合する |

合計は約 1,700 行。500 行に届く `OverlayRounds.cs` は非分割とする（1 つの Play の往復の列で、変更理由は round の定義だけ。判断は pure 側に出してある）。S2-0b より小さくする手段: 解除の case の表（U1〜U7）と R0・R1・T0〜T3 の時点を持たない、pixel の所在の判定を持たない（`EditedTex` と `SharedTex` は読める小さな texture で、load 後の pixel の sha256 だけを見る）、正の対照は S2-0b の結果（列挙は同名の別の texture を 2 つと数えた）を前提にして再実施しない。反復は同じ round を process の中で繰り返さず、最初の round だけが違う 4 つの test を 1 回ずつ起動する（違いは round の並びの定数だけ）。

**fixture（`Assets/OsmSpikeOvl`、SampleGame を使わない）:**

- 共通物: `SharedShader`（variant の無い自作 shader。`Lit` を使わない）、`SharedTex`、`SharedMat`（`SharedShader` と `SharedTex`）、`SharedPrefab`（`SharedMat` の Renderer と、`ProbeBaseOnly`・`ProbeBoth`。base の root だけが持つ。`Loadable` 経由で載る component の class を見るため）。
- 編集する物: `EditedTex`（読める小さな texture。v1 と v2 で違う単色。GUID は同じ）、`EditedMat`（`SharedShader` と `EditedTex`）。
- base だけの物: `KeepTex`、`KeepMat`、Scene `SceneKeep`（Renderer が `SharedMat`・`EditedMat`・`KeepMat` を直接参照し、GameObject が `ProbeBaseOnly` と `ProbeBoth` を持つ）。
- 両方にある Scene: `SceneEdit`。v1 は `Marker_v1` に `ProbeBaseOnly` と `ProbeBoth`、v2 は `Marker_v2` に `ProbeOverlayOnly` と `ProbeBoth`。どちらも Renderer が `SharedMat` と `EditedMat` を直接参照する。probe の `_tag`・`_value` は、fixture の全ての probe の component で互いに違う値にする（同じ GameObject の 2 つも違う）。各 GameObject は `ProbeBaseOnly`（または `ProbeOverlayOnly`）、`ProbeBoth` の順に付ける。
- root は製品の `BuildContentRoot`（無改変）。base の root は `SceneKeep`・`SceneEdit`・`SharedPrefab`・`EditedTex`、上書きの root は `SceneEdit`・`EditedTex`。**base の root と上書きの root は、別の固定 GUID を持つ別の asset にする**（同じ asset を書き換えて使わない）。Scene に prefab instance を置かない。

**資源の所有と寿命:**

| 資源 | 作る・解放する所有者 | 寿命 | 失敗時 |
|---|---|---|---|
| fixture の folder と marker | `OverlayFixture`（第 1 process） | 第 1 process の中 | `finally` で削除。次の開始時に marker のある残りだけ回収 |
| build の出力と record | `OverlayBuilds`（作る）、`OverlayRounds`（読む・追記） | 実行 id ごとの一時 root（`TestResults/ovl/<run id>/`、git 管理外） | 他の実行の root に触れない |
| 2 つの `UnityContentDirectoryBackend` と登録 | `OverlayRounds` の round | 1 つの Play | `finally` で解除、残れば直接回収 |
| load の token と Scene | `OverlayRounds` の round | 登録より短い | 登録解除の前に Release・unload |
| log の購読と `LogAssert.ignoreFailingMessages` | `OverlayRounds` の round | 1 つの round | `finally` で戻す |
| probe の 3 script | 本スライスの branch | Phase D まで | harvest の commit で削除 |

## 4. 実装計画

- 順序: (1) pure の 2 file。(2) probe、fixture、build。(3) Play の round と record。(4) 実行は Phase C。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る。製品の wrapper を 2 つ持って登録できず `ContentLoadManager` を直接呼ぶ必要がある。P1〜P3 を満たす fixture が §3 の形で作れない。probe を `OneStarMaker.TestSupport` に置く形で Content Directory の build か load が成り立たない。§2 の語で観測できない。
- B 適応の範囲: probe を build に入れるための `BuildPipeline.BuildContentDirectory` の引数（`extraScriptingDefines` に `UNITY_INCLUDE_TESTS` を渡す等。harness が直接呼ぶ build の引数で、製品の build の経路は変えない）、fixture の asset の細部（大きさ、色、GameObject の構成）、record の項目名、round の順序、component の付け方（`MonoScript.GetClass` と `AddComponent`、または YAML の書き換え）、S2-0b の harness から写す範囲、layout から MonoScript の番号が読める時の追加の記録。
- 対象外を維持する方法: 製品 file を変えない（AC8）。build の出力と record は `TestResults/` 下の一時 root に置く。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。

## 5. テストとレビュー計画

- 単体テスト: `OverlayVerdictTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdictTests`。
- 判定必須テスト（GO 候補 head）: 次の 6 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdictTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayBuilds.FirstProcess`。(3)〜(6) `-Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayRounds.ClassFirst`、同 `.LifetimeFirst`、同 `.ReadFirst`、同 `.ControlFirst`（どれも (2) の同じ出力を読む）。各 XML の total が 1 以上で failed 0、root result `Passed`、record に全 round の終了行がある。ここまでは harness が最後まで動いたことの確認で、その後に判定 C が §2 の定義を record に当てて答えを読む。**process の自然終了をゲートにしない**（XML の完成後に Unity が終わらない既知の終了 stall は、`tools/Harness/README.md` の規則に従い、XML と record の終了行で判定する）。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC8）、build と Play を行う `[Explicit]` の実験 test は通常の回帰に混ぜない。足すのは test 用 assembly の空の `MonoBehaviour` 3 つだけである。代替証拠: AC8、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 6 コマンドで閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、Enter Play Mode の Domain Reload が無効、作業ツリーが clean。観測は record・XML・log。目視は無い。証拠は record・XML・log・各 build の `ContentLayout.json` の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: EditMode の `[UnityTest]` から Play に入り、製品の wrapper を 2 つ持って 2 つの directory を登録する経路は S2-0b が成立させた。**未確認:** `UNITY_INCLUDE_TESTS` の制約を持つ `OneStarMaker.TestSupport` の `MonoBehaviour` が、StandaloneWindows64 向けの Content Directory の build に script として入ること（前例の build も製品の build も `extraScriptingDefines` が空で、この assembly に `MonoBehaviour` を置いた前例は無い）。その Scene を、source を消した Editor の Play で load して `GetType()` が読めること。source を v2 に変えた後の 2 回目の build が v2 を取り込むこと。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: (2) の P1・P2 と、(3) の AC4・AC2。不成立時: 基盤不備なら B、fixture の形で作れないなら Phase A）。
- 人間の判断が必要な条件: なし（結論を受けた形の採否は Phase D で owner）。
- 高コスト証拠: なし（build 2 回、Play は 1 process あたり 7 回（AC4、AC5、AC3、AC6 の 2 回、AC2 の 2 回）で、4 process の 28 回の見込み。Unity の起動は 6 回）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。`OverlayVerdict` の出力は補助である。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude、Anthropic）。A0 は program §5.3「当面の方針」と §8 の S2-1 の行・S2-0・S2-0b の移送を入力とした。
- A2 独立レビュー（実施 2026-11-13。互いの指摘を見せていない。全て Anthropic の系列で、別ベンダーは C' 用に残した）:
  - (1) 目的レビュー: 本 session が起動した新しい subagent（Claude Opus、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `4b072c253db3e532693773377896e0ac790bb8d00da101ba0b65c88545a8bfe7`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。
  - (2) A0 だけからの代替構成: (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は program §5.3・§8、事実の文書、製品の native 層の source、前例の harness の file 一覧。本書は読んでいない。
  - (3) アーキテクチャゲート兼、各不変条件を破る順序と §2 の誤答の一覧の外の探索: 本 session が起動した別の新しい subagent（Claude Fable、Anthropic）。入力は A2 入力版の本書（内容 sha256 `561cc79f03995623d6562928a338eda957abc6454fa38ed3a3d0456650e72767`、commit `1cfcad07`）と、アーキテクチャゲート、事実の文書、program、製品の `Runtime/BuildContent` の source、asmdef、前例の harness、AGENTS.md。枠は「計画した実験 code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。
  - (4) 凍結前の外部レビュー（draft PR #182、1 巡目、2026-11-13）: cursor の background agent（Grok、xAI。A2 とは別ベンダー）。対象は commit `28ea0846` の本書。観点は PR 本文の 4 つ（誤って YES を出す道、判定の形、範囲、規模）。
- A3 統合担当・採否: 本 session（§5.1）。未凍結。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。A2 では Grok 系を使わない。
- 独立性の強化条件を満たせない場合の理由: A2 は 1 つのベンダーの 2 つのモデルで行った（A1 の主担当は (3) と同じモデルの別 session）。別ベンダーの目は、凍結前の外部レビューと C' で入れる。凍結前の外部レビューが Grok 系を使ったので、C' に Grok 系を使う時は、Phase A に関与した系列として独立性の制約を §8 に記録する。C' の充足は起こす時に確かめて記録する。

### 5.1 A2 の独立レビューと A3 の採否

**(1) 目的レビュー**

| ID | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| P-1 | 4 項目を別々に記録し、失敗した時はどの項目がどう崩れたかを残す | 既に充足 | §2 の各問の答えと、NO に添える内訳 |
| P-2 | 上書きに入れていない物も検査に含める | 既に充足 | `SceneKeep` と `SharedPrefab`（AC4） |
| P-3 | 根拠は Editor の Play の実測だけにし、版・build の条件・手順を記録する | 既に充足 | §2 の成果の範囲、record の先頭 |
| P-4 | 「取得した build 1 つ + 上書き」の形を守り、複数の build や全体の再 build に置き換えない | 既に充足 | §3 の fixture。対照の順（AC6）は答えに使わない |
| P-5 | 採否の決定は owner に残す | 既に充足 | 判定は答えの有無だけ。含意の表は判定ではない |
| P-6 | 作らないもの: 製品の配布単位・登録の API・Play の入口、利用者の入力と UI、Player と他の版の検証、縮退先の実装と比較、重複の削減と自動回復、上書きを自動で build する仕組み | 既に充足 | §1 の対象外、§2.3 |

**(2) A0 だけからの代替構成**

| ID | 提案（要約） | 採否 | 理由と反映先 |
|---|---|---|---|
| A-1 | Play 1 回にまとめ、harness を数百行にする | 不採用 | Q4 の出入りの列と Q1 の 2 つの経路を 1 つの Play に入れると、前の load が後の観測に効く（§2 の (i)）。round は分けたまま、S2-0b から削る物を §3 に書いた |
| A-2 | 別の process で 1 回再現する | 採用 | 同じ process の中の反復を、別々の process に替えた（その後、見直し S-1 で、順を変えた 4 つの process にした）。§2.2 の前文、§5 |
| A-3 | prefab の component も class の検査に含める | 採用 | `Loadable` 経由の解決は逆順の探索に乗る側なので、Scene だけでは足りない。`SharedPrefab` に probe を持たせた。§3、AC4 |
| A-4 | 登録の前の前提: 共通物の file が同じ、script の file が別、Scene の identity が同じで file が別、上書きが共通物を直接参照、Domain Reload 無効 | 既に充足 | P1〜P3 と §5 の初期状態 |
| A-5 | 同じ identity の Scene を両方から load できない時は、prefab で測る代替へ移る | 不採用 | Unity が拒否したこと自体が Q1 の答えになる（§2 の GO）。代替の fixture へ移るのは別の問い |
| A-6 | texture を小さくする | 採用 | 読める小さな texture にした（版の区別に pixel を読むので、読める設定は残す）。§3 |
| A-7 | 答えられない事: 上書きで script の code が変わる時、上書きが複数ある時、GPU 側の重複 | 採用 | §2.3 に足した |

**(3) アーキテクチャゲートと反例（blocker なし）**

| ID | 重さ | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|---|
| G-01 | should-fix | `UNITY_INCLUDE_TESTS` の制約を持つ `OneStarMaker.TestSupport` の probe が、StandaloneWindows64 の build に入らない恐れ（推測） | 採用 | §5 の未確認に明記し、build の引数を B 適応に入れた。入らなければ P1 と AC2 で止まる（I-1 (f)）。置き場を変える必要が出たら Phase A へ戻す（§4） |
| G-02 | note | probe の namespace と期待する `FullName`、`#nullable enable` を固定する | 採用 | §3 |
| G-03 | should-fix | 基準と本番が同じ process で、前の Play が読んだ script の file が残ると Q2 が正しく見える | 採用 | round の順（Q2 を process の最初に、基準を最後に）と、別々の process（その後、見直し S-1 で全 round に広げた）。§2 の (i)、I-2 (f)、§2.2 の前文 |
| G-04 | should-fix | base と上書きの root が同じ asset の書き換えだと、base の handle が上書きの root を返しうる | 採用 | root を別の固定 GUID の asset にし、handle ごとの `BuildIdentity` を照合する。§2 の (j)、I-1 (e)、§3 |
| G-05 | should-fix | 「別の `EntityId`」の基準の時点が無く、load し直した Scene の物が合格にも不合格にも読める | 採用 | §2 の「壊れた」に比較の区間を書いた |
| G-06 | should-fix | Unity の拒否（答え）と、type tree の欠落などの失敗（未判定）が分けられない | 採用 | 単独の基準で成功し 2 つの登録でだけ失敗した時に限って答えにする。§2 の GO、AC2 |
| G-07 | note | Scene の file の帰属を layout から引いた前例が無い。AC4 の「同時に」の順が無い。`LoadableObjectId` が等しければ `EditedTex` に弱は無い | 採用 | I-1 の未確認、§5 の未確認、AC3、AC4 |

**(4) 凍結前の外部レビュー（PR #182、1 巡目。blocker なし）**

| ID | 重さ | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|---|
| F-1 | should-fix | P1 の「source」が Scene の中身とも読め、sha256 の差だけで前提を通過しうる。両方の file に 3 型とも入った build で Q2 が正しく見える | 採用 | P1 を、その file の layout の `SourceAssets` の一覧に限り、path を `.meta` の GUID と結びつけた。§2 の P1、`OverlayVerdictTests`。なお「script の顔ぶれが同じ build 同士でも bytes が変わる」は、build 履歴 50 件では起きていなかった（一覧が同じなら hash も同じ。§1 の現況）。どちらでも、P1 の根拠は一覧に置く |
| F-2 | should-fix | 「正しい class」を集合で比べると、同じ GameObject の 2 つの probe の入れ替わりを見逃す | 採用 | 並びの位置ごとの照合にし、値を全 component で違えた。§2 の「正しい class」と (b)、§3 の fixture、`OverlayVerdictTests` |
| F-3 | should-fix | 対照は同じ process の後の Play なので、主の順との一致は証拠にならない。強の YES を「上書きの id を先に引く」へ訳さない。`Register` の順を record に残す。Domain Reload 無効と target を満たさなければ未判定 | 採用（一部は提案より進めた） | 一致を証拠にしないだけでなく、対照が最初の Play になる process を足して、対照の答えもそこから採る（下の S-1）。強の読み替えの禁止、`Register` の順、設定の未判定は §2 に入れた |
| 文言 | — | I-2 (a) の「source を消せば probe の class が source から解決されない」は、probe が test 用 assembly に残るので広すぎる | 採用 | I-2 (a) を、消すのは fixture の asset で、script は残ると書き直した |

**(5) 外部レビューを受けた主担当の見直し（2026-11-13）**

owner の指示で、指摘の 3 件を直すだけでなく、検証の組み立てを見直した。

| ID | 見つけた問題 | 直した所 |
|---|---|---|
| S-1 | 前の Play の残りから守られていたのは Q2 の round（process の最初）だけで、Q1・Q4・対照の答えは守られていなかった。同じ並びの process を 2 回繰り返しても、残りが効くなら 2 回とも同じ誤りになる | 最初の round だけが違う 4 つの process に替えた。各 round の答えは、それが最初の Play になった回から採り、他の回と違えば未判定。残りが答えを変えるかどうか自体も、この照合で分かる。§2 の (h)(i)(l)、§2.2 の前文、§5 |
| S-2 | Scene の id は path から作られ、load は handle を取らない。base の root の id と上書きの root の id は同じ要求になる見込みで、初稿の「弱」と「条件付き YES」は、id が違った時にしか起こらない | 現況に事実を足し、Q1 の語に「id が等しい時は強か不成立」を書いた。含意の表に「上書きを登録している間は base の版を選べない」を足した |
| S-3 | script をまとめた file が「先に読んだ側」で固定される作りだと、Scene を読む順で壊れる側が変わる。初稿はその順を意識していなかった | 両方の Scene の probe を見ることと、AC4 と AC5 で両方の順が入ることを (k) に書いた |
| S-4 | 上書きに無い物（`SceneKeep`・`SharedPrefab`）が、上書きを登録した下で load できない場合の読み方が無かった | AC7 の補足（単独の基準で成功していれば NO という答え） |
| S-5 | 「未確認」のうち 3 つは、Unity を起動せずに build 履歴から確かめられた（script の file の id が build によらず同じ、一覧は使う script だけ、Scene の帰属は GUID で引ける） | §1 の現況に事実として書き、I-1 の未確認から外した |
| S-6 | `EditedMat` が両方で同じ file であることを前提にしていなかった（`SceneKeep` の `EditedTex` の観測を読む時に要る） | P3 に足した |

**A1 の自己点検（A2 の前）:** 目的の 4 項目と Q1〜Q4、AC3〜AC5 が 1 対 1 に対応すること、利用者の入力が無いこと、結果に依存する文書（事実の文書と program の改訂）を受け入れ条件に入れず harvest に置いたことを確かめた。

## 6. Phase B 実装結果

未着手。

### 動作確認

`対象なし`（§5）。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
