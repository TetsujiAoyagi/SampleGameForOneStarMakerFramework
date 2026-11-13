# 取得した build への上書きの登録の実証

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-1）
- status: Phase A revision 1 の A2（2026-11-13。probe の置き場所を、test 制約の無い新しい asmdef へ移す。owner が承認。§5.2）。経緯: 判定 C（1 回目）は未判定で停止し（(2) の base の build が Failed）、B が原因を調べた結果、probe を `OneStarMaker.TestSupport` に置く形では probe が Content Directory の build に入らないと分かった（§6 の「判定 C からの差し戻し（1 回目）」）。発見 C は完了済み（阻害する指摘なし）。Phase A の revision 0 は A2 入力版が commit `1cfcad07`、A3 凍結版が commit `6d9908c3`。凍結前に draft PR #182 で外部レビュー 3 巡と owner のレビューを受けた（§5.1）
- branch: `claude/content-overlay-registration-spike`（PR の base は `develop`）
- implementation base commit: `bc3ca436`（`develop` の head、2026-11-13。§1 の現況はこの base）
- implementation head commit: `未固定`
- risk: `high`（program §8 の指定。製品 code は変えないが、結論が S2・S3 の形を決める）
- owner: root（依頼者）
- created: 2026-11-13
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「同じ file を持つ 2 つの directory の登録」の節の「測っていないもの」のうち「中身が違う同じ id の file を両方登録した時」を実測の事実へ置き換える。新しい文書は作らない）、program §5.3「当面の方針」と §8（本スライスの行と S2 の行の改訂）。実験 code は製品に残さず git 履歴に置く（S0-b・S2-0・S2-0b と同じ）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§5.3「当面の方針」、§8 の S2-0・S2-0b の移送）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（S2-0・S2-0b の実測と harness の所在）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書だけ）
- Phase B result snapshot の取得案内・状態: 該当なし（発見 C は implementation head の commit を固定して読む。機械向けの期待値は無い）
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A revision 1 の A2（アーキテクチャゲート 1 件。§5.2）。この票は A2 入力版にあり、A3 で Phase B 宛てに書き直す。
  - 引き渡さないもの: §7 の所見・注意・読み方の確認は C' に渡さない（blind bundle に入れない）。

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
  - (h) 1 回の観測で答える（各 round を、順を変えた 5 つの Unity process で行い、違えば未判定）。
  - (i) 前の Play が読んだ file や MonoScript が process に残り、後の round の参照がそれに解決されて正しく見える（Domain Reload 無効。各 round に、その process で最初に content を読む Play になる回を 1 つ持たせ、答えはその回から採る。単独の基準は最後に行う）。
  - (j) base の handle から取った root が、実は上書きの root である（root を別の asset にし、handle ごとの `BuildIdentity` を照合する）。
  - (k) script をまとめた file が「後から登録した側」ではなく「先に読んだ側」で固定される作りだと、Scene を load する順で壊れる側が変わる（base の Scene と上書きの Scene の両方の probe を見る。両方の順は、どちらも process の最初の Play で測る。AC4 は base の `SceneKeep` を先に load し（`ClassFirst` の最初の Play）、AC5 の (1) は上書きの `SceneEdit` を先に load する（`LifetimeFirst` の最初の Play）。AC5 の (5) の読み直しは寿命の確認で、逆順の根拠にはしない）。
  - (l) 対照の順の答えが主の順と同じであることを、前の round の残りが効いたまま「登録の順は無関係」と読む（対照の答えも、対照が最初の Play になる process から採る。class の対照と版の対照のそれぞれに、最初の Play になる process を持たせる）。
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
  - **Q3:** **収まる**（AC4 の計数の時点と AC5 の (1) の計数の時点の両方で、両方の directory にある `SharedMat`・`SharedTex`・`SharedShader`・`EditedMat`・`EditedTex` の実体がそれぞれ 2 以下で、上書きに無い `SharedPrefab` と `KeepTex` が 1）、**超える**。上書きで編集した物（`EditedTex`）も対象に含める。
  - **Q4:** **無傷**（Q4 の round の全ての確認点で、保持している物が壊れておらず、終わりの解除の後に登録が 0 件）、**壊れた**。確認点の間に Unity が出した Error・Exception は答えに添えて書く（壊れていなければ「無傷（log あり）」）。
- **答えを採る回:** 各 round の答えは、その round が process の最初の Play になった回（§2.2 の round の順）から採る。他の 4 つの process の同じ round の答えは照合に使い、1 つでも違えば未判定とする。Q3 は AC4 と AC5 の (1) の 2 つの時点を使うので、それぞれが最初の Play になった回（`ClassFirst` と `LifetimeFirst`）から採る。
- スパイクの答え（主の順の round だけから決める）:
  - **YES:** Q1 強、Q2 正しい、Q3 収まる、Q4 無傷。
  - **条件付き YES:** Q1 が弱で、他は YES と同じ。条件は「要求する側が、上書きの root の id を base の物より先に引く」。id が違った時にだけ起こる答えで、強の YES をこの条件へ読み替えない。
  - **答えの範囲:** YES も条件付き YES も、登録の順を base → 上書きに固定した時の答えである。
  - **NO:** 上のどちらでもない。どの問がどう外れたかを添える。
- 判定定義。スパイクの合否は「答えが証拠付きで出たか」で決め、答えの向きは合否に使わない（S2-0b と同じ）。
  - **未判定（結論を出さない）:** build を開始できない。P1〜P3 のどれかが成り立たない。第 2 process の入力が第 1 process の同じ実行の出力だと照合できない。Play の round の開始時に fixture の source が AssetDatabase に残っているか、登録中の directory か fixture の名前の物が 1 件以上ある。単独の基準（AC2）で期待と違う。handle ごとの root の `BuildIdentity` が、その directory の build の物と一致しない。record の Enter Play Mode の設定が Domain Reload 無効でないか、target が StandaloneWindows64 でない。round の `Register` の順が record に無いか、その round の定義と違う。終了行の無い round がある。実体の数が整合の規則に合わない。同じ round の答えが process の間で違う。round の後始末が終わらない。harness・fixture・環境の問題は B へ戻し、Unity の挙動が定義した語で読めない時と、process の間で答えが違う時（前の Play の残りが答えを変えるという事実で、それ自体を結論に書く）は Phase A へ戻す。
  - **GO（答えが出た）:** 未判定に当たらず、record から Q1〜Q4 の答えとスパイクの答え（YES / 条件付き YES / NO）が一意に読める。主の順で 2 つ目の登録か、上書きの下での load を Unity が拒否した時は、その log と状態が record にあれば「NO（登録できない / load できない）」という答えであり、未判定ではない。**答えにできるのは、同じ呼び出しが単独の登録では成功し、2 つを登録した時だけ失敗した場合に限る。** 単独の成功の根拠は、その directory だけを登録する round を process の最初の Play として行った回（§5 の条件付きのコマンド `BaseAloneFirst`・`OverlayAloneFirst`）から採る。AC2 は全ての round の後に行うので、前の Play の残りが単独の成功を助けた可能性を除けず、拒否を答えにする根拠には使わない。条件付きのコマンドを実行していない間、拒否は未判定に留める。単独でも失敗する呼び出し、path の誤り、manifest か type tree の欠落、harness の例外による失敗は答えにしない（未判定）。
  - CONDITIONAL ACCEPT と NO-GO は使わない。NO は GO の一形（答えが出た）である。
  - **test の合否と判定は別である。** 第 2 process の各 test が assert するのは harness の成立だけ（全 round の開始行と終了行、後始末、log の購読と `LogAssert` の復元、record を閉じた）。答えの向きは assert しない。XML の failed 0 は「harness が最後まで動いた」ことだけを意味する。判定は判定 C が record の観測の行から読む。`OverlayVerdict` が record に書く答えの行は補助である。
  - 結論は、実測した事実と、仕組みの推測を分けて書く。
- **後続への含意（判定ではない。結論に添えて owner へ渡す。行は排他ではなく、当たった行を全て書く）:**

| 答え・観測 | owner が決めること |
|---|---|
| YES | 「base 1 つ + 上書き」を開発の Play の形として採る候補にできる。program §8 の S2 の行を「目的 build を選ぶ索引 + 上書きの登録契約」へ縮めるか |
| 条件付き YES | 同上。加えて、resolver が上書きの entry を先に引く規則を S3 の契約に入れる |
| NO（Q2 が誤り） | 上書きの形は script の顔ぶれが違うと使えない。縮退の候補: 触った物を含む目的 build を手元で丸ごと再 build する（増分の所要は実測済み）、script の顔ぶれを全 build で揃える、Unity へ問い合わせる |
| NO（Q1 不成立、Q3 超える、Q4 壊れた、登録・load の拒否） | どの問が外れたかに応じて、上書きの形を採れる範囲を狭めるか、縮退するか |
| `SceneKeep` の `EditedTex` が v1 のまま（AC4 の観測） | 共通の asset を編集した時、それを直接参照する Scene も上書きに入れないと、古い見た目と新しい見た目が混ざる。上書きの build に入れる Scene を逆引きする道具を誰が持つか（S2 か S5） |
| 対照の順（class の対照、版の対照のそれぞれが最初の Play の回）で答えが主の順と違う | 製品は登録の順を base → 上書きに固定する |
| 対照の順で答えが主の順と同じ | この fixture の範囲では、登録の順で答えは変わらなかった。製品の順を緩めてよい根拠にはしない（測ったのは 1 つの fixture） |
| YES（id が等しい） | 上書きを登録している間、要求する側は base の版を選べない。それでよいか（開発の Play では上書きを見たいので、既定はよい） |

- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO で終了する。最低条件未達（未判定）のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 危険な条件の無い fixture で答えない:** 答えは、P1〜P3 が登録の前の record で成り立った実行からだけ出す。
  - 崩しうる経路: (a) base と上書きの component の型の集合が結果的に同じになり、script をまとめた file が同じ bytes になる（`BuildContentRoot` 自体の script は両方に入る。probe の 3 型で差を作り、P1 で確かめる）。(b) v2 への書き換えが import されず、上書きに v1 の file が入る（P2）。(c) 共通物が他の asset と同じ file に入り、bytes が変わる（P3）。(d) layout を名前で引いて別の asset の file を見る（固定 GUID と local id）。(e) base と上書きの root を同じ asset の書き換えで作り、root の file まで同じ id で中身違いになって、base の handle が上書きの root を返す（root は別の固定 GUID の asset にし、handle ごとの `BuildIdentity` を照合する）。(f) probe の script が build に入らず、Scene の component が最初から Missing になる（P1 が layout の source で確かめ、AC2 の単独の基準が class を確かめる）。
  - 探索した境界: S2-0・S2-0b の harness と実測。未確認: probe 用の asmdef（§3）の probe が、その file の一覧に入ること（P1 が確かめる。一覧が使う script だけを持つこと、id が build によらず同じことは、build 履歴 50 件で確認した）。layout は番号を持たないので、番号の衝突は前提にせず、Q2 の観測（位置ごとの class）で受ける。Scene の file の帰属は layout の `LoadableSceneIds` の GUID で引ける（build 履歴の読み取りで確認）。
- **I-2 見分けられない観測で答えない:** Q1 は版を区別できる物、Q2 は class の完全名と値、Q3 は整合した実体の数、Q4 は保持したままの参照で答える。期待値は fixture の定義から採る。
  - 崩しうる経路: (a) Editor の AssetDatabase が fixture の source（Scene・prefab・material・texture）を載せ、Content Directory の物でない実体が観測に混ざる（fixture の folder を Play の前に消し、消したことを確かめる。probe の script は probe 用の asmdef に残る。build された component は script を名前で指すので、script の source が残ること自体は class の解決を助けない、という §1 の参照の仕組みに立つ）。(b) Domain Reload 無効で前の Play の物が残る（round ごとに新しい Play、開始時に 0 件）。(c) Missing の component が `GetComponent<T>` では見えない（`GetComponents<Component>()` の `== null` を数える）。(d) 参照を離した後の `== null` を壊れたと読む（Q4 の確認点まで参照を保持する）。(e) `?.`・`is null` で偽 null を見落とす。(f) 単独の基準や前の round が読んだ script の file が process に残り、主の順の参照がそれに解決される（各 round が最初の Play になる process を持つことで防ぐ。開始時の 0 件の検査は MonoScript を見られない）。
  - 探索した境界: S2-0b の数え方（`FindObjectsOfTypeAll` は Content Directory から載った texture を見た）。未確認: source を消した後の Play で、Content Directory から載った probe の component の `GetType()` が probe 用の asmdef の型に解決されること（単独の基準 AC2 が確かめる）。
- **I-3 製品を変えない:** §3 の新規 file と本書・program 以外の変更が無く、実験は assets・設定・build の出力を repo に残さない。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) Build Settings・active target・Enter Play Mode の設定の変更。(c) round が登録を解除せずに終わる。(d) log の購読と `LogAssert.ignoreFailingMessages` が次の test に残る。(e) 残りの回収が、自分の marker の無い folder を消す。
  - 範囲: 守るのは git で追跡する file、fixture の folder、`Assets/OneStarMakerGenerated`、一時 root。`Library` の cache は対象にしない。

**成立条件（起きなければならないこと）**

- **E1 準備:** base と上書きが build でき、P1〜P3 が record にある。
- **E2 基準:** 各 directory を単独で登録した時の版・class・値が record にあり、fixture の定義と一致する。
- **E3 答え:** 主の順で Q1〜Q4 の答えが、対照の順で Q1・Q2 の答えが、5 つの process の全ての record にあり、AC3〜AC6 の各 round（AC6 は 2 つ）に、それが最初の Play になった回がある。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が書く JSONL の record と XML を根拠に判定する。record は実行 id、head、Unity の版、target、Enter Play Mode の設定を持ち、round ごとに開始行と終了行がある。Play の round は全て新しい Play で行い、開始時に登録中の directory が 0 件であることと fixture の名前の物が 0 件であることを記録する。登録は製品の `UnityContentDirectoryBackend` を無改変で 2 つ持って行い、両方を登録してから load を始める。Unity の Error・Exception・Warning の log は test の失敗にせず（`LogAssert.ignoreFailingMessages`）、発生順のまま全件を round と呼び出しに結びつけて record に残す（`LogType.Log` も残す）。async の load・unload・`UnloadUnusedAssets` は完了を待つ。後始末は `finally` で行い、登録が残れば `GetContentDirectories` の handle から直接解除して記録する。**round の順と反復:** 第 2 process の test は、最初に行う round だけが違う 5 つで、それぞれ別の Unity process で 1 回ずつ実行する。round は AC4、AC5、AC3、AC6-class（対照の順で AC4 と同じ観測）、AC6-read（対照の順で AC3 と同じ観測）の 5 つと、最後の AC2（base だけ、上書きだけの順）である。各 process は、名前の round を最初に行い、残りを AC4 → AC5 → AC3 → AC6-class → AC6-read の順に（最初に行った物を飛ばして）続け、AC2 を最後に行う。`ClassFirst` は AC4、`LifetimeFirst` は AC5、`ReadFirst` は AC3、`ControlClassFirst` は AC6-class、`ControlReadFirst` は AC6-read が最初である。5 つとも同じ第 1 process の出力を読み、record は process ごとに分ける。各 round の答えは、その round が最初の Play になった process から採り、他の 4 つは照合に使う（§2 の「答えを採る回」）。各 round で、`Register` を呼んだ順と、両方の handle の root の `BuildIdentity` を記録し、その directory の build の物と照合する。

- **AC1（E1、I-1）準備と前提:** 第 1 process が fixture（§3）を v1 で作って base を build し、source を v2 に変えて上書き（root は `SceneEdit` と `EditedTex` だけ）を build し、両方の出力の全 file の名前・size・sha256 と `ContentLayout.json` の写しを記録し、P1〜P3 の成否を record に書き、fixture の folder を消して終わる（消したことを file system で確かめる）。P1〜P3 のどれかが成り立たなければ未判定。
- **AC2（E2、I-2）単独の基準:** 第 2 process が、第 1 process の同じ実行の出力であることを実行 id・head・棚卸しの digest で照合した後、base だけを登録した round で `SceneKeep`・`SceneEdit`（v1）・`SharedPrefab` の版・class・値と `EditedTex`（v1）の pixel を、上書きだけを登録した round で `SceneEdit`（v2）と `EditedTex`（v2）を記録する。どれも fixture の定義と一致する（一致しなければ未判定）。単独の基準は期待値の出所ではない（期待値は fixture の定義）。fixture と load の経路が単独で成り立つことの確認に使う。Unity の拒否を答えにする時の単独の成功の根拠は、AC2 ではなく §5 の条件付きのコマンドから採る（§2 の GO）。
- **AC3（E3、Q1）読まれる側:** 主の順の round で、base の root の `SceneEdit` の id と上書きの root の `SceneEdit` の id が等しいかを記録し、それぞれから 1 回ずつ load して版を記録して unload する。`EditedTex` を両方の root の `Loadable` から load して両方を保持し、pixel の版、`EntityId`、`LoadableObjectId` が等しいかを記録する（等しい時は、`EditedTex` に「弱」は起こりえず、強か不成立になる）。
  - 合格例（強）: どちらの id からも `Marker_v2`。不合格ではない例（弱）: 上書きの id から `Marker_v2`、base の id から `Marker_v1`。
- **AC4（E3、Q2・Q3）class と重複:** 主の順の round で、`SceneKeep`（base の root の id）、`SceneEdit`（上書きの root の id）の順に additive に load して両方を載せたままにし、base の root から `SharedPrefab` を、上書きの root から `EditedTex` を load して保持した状態で、全 probe の class と値、Missing の数、`SharedMat`・`SharedTex`・`SharedShader`・`EditedMat`・`EditedTex`・`SharedPrefab`・`KeepTex` の実体の数を記録する。あわせて、`SceneKeep` の Renderer が直接参照する `EditedMat` の `mainTexture` の pixel の版を観測として記録する（答えには使わない）。
  - 失敗経路の例: `SceneKeep` か `SharedPrefab` の `ProbeBaseOnly` の位置の component が `== null`（Missing）、または `FullName` が `ProbeOverlayOnly`・`ProbeBoth` になる。どちらも Q2 は誤り。
- **AC5（E3、Q4）寿命:** 主の順の round で、次の列を順に行い、各段の後に、その時点で保持している物（Scene の probe と Renderer の material・texture、`Loadable` の物）が壊れていないかを記録する。(1) **上書きの `SceneEdit` を先に、`SceneKeep` を後に** load し（AC4 と逆の順。§2 の (k)）、base の root から `SharedPrefab`、上書きの root から `EditedTex` を load して保持し、AC4 と同じ 7 つの物の実体の数を記録する。(2) `SceneEdit` を unload し `UnloadUnusedAssets`。(3) `SceneEdit` を load し直す。(4) `SceneKeep` を unload し `UnloadUnusedAssets`。(5) `SceneKeep` を load し直す。(6) `Loadable` を Release し、両 Scene を unload し、上書き → base の順に解除して、登録が 0 件であることを記録する。(6) の間の log は (1)〜(5) と分けて記録する。
  - 境界: 「Play の終わりまで」は (6) の解除の完了までを指す。Play を抜けた後の Editor の状態は AC8 が見る。
- **AC6（E3、対照）対照の順:** 上書き → base の順で、AC4 と同じ観測（AC6-class）と、AC3 と同じ観測（AC6-read）の 2 つの round を行う。答えは対照として記録し、スパイクの答えには使わない。AC6-class の答えは `ControlClassFirst` の最初の Play から、AC6-read の答えは `ControlReadFirst` の最初の Play から採る。主の順との差も一致も、この 2 つの回の答えでだけ読む。
- **AC7（I-2）数え方の成立:** AC3〜AC6 の各 round（AC5 は (1) の計数の時点）で、load の結果の `EntityId` の集合が列挙の集合と一致する。一致しない round は未判定。
  - 補足（AC4・AC5 に共通）: `SceneKeep` と `SharedPrefab` は上書きに無い。上書きを登録した下でこれらを要求して load できない時は、同じ呼び出しが `BaseAloneFirst`（base だけを登録した、process の最初の Play）で成功していれば「NO（load できない）」という答えである（Unity が、後から登録した directory に無い物を先に登録した directory へ探しに行かない、という事実になる）。`BaseAloneFirst` を実行していない間は未判定とし、AC2 の成功は根拠にしない（§2 の GO）。
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

新規 file は `unity/Assets/OneStarMaker/Tests/Editor/Spikes/OverlayRegistration/`（既存の `OneStarMaker.Tests.Editor` asmdef。参照は足さない）と、probe の 3 file と、その probe だけを持つ新しい asmdef 1 つである（revision 1。§5.2）。製品の file は変えない。全て Phase D で削除する。S2-0b の harness（commit `444651ed`）から、fixture の固定 GUID・marker、棚卸し、record、Play の round の枠、log の捕捉、実体の数え方を写して縮める。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---:|---|
| `OverlayVerdict.cs` | round の観測（版、class の名前と値、Missing の数、実体の数、壊れた項目、前提 P）を受け取り、Q1〜Q4 の答え、スパイクの答え、未判定の理由を返す。入力は文字列・数値・真偽だけ | 200 | Unity なしで単体テストできる pure な写像 |
| `OverlayVerdictTests.cs` | 上の写像だけの EditMode test。§2 の (a)〜(e)・(h)・(j) の誤りが YES にならないこと、型の集合は同じで位置だけ入れ替わった入力が正しいにならないこと、P1 を Scene の中身の差や sha256 の差だけで満たさないこと、答えを採る回と process の間の不一致、Unity の拒否を答えにする条件（単独の基準との組）、強・弱・不成立、Missing と別の class、P の不成立、反復の不一致 | 250 | 通常の EditMode 回帰に入る |
| `OverlayFixture.cs` | fixture の生成（固定 GUID、v1 と v2）、所有の marker、残りの回収と削除 | 300 | Unity / `AssetDatabase` の I/O |
| `OverlayBuilds.cs` | `[Explicit]` の `FirstProcess`。base と上書きの build、棚卸しと sha256、`ContentLayout.json` による帰属と P1〜P3、record、fixture の削除 | 350 | Unity の build の I/O |
| `OverlayRounds.cs` | `[Explicit]` の `[UnityTest]` 7 つ。round の並びだけが違う 5 つ（`ClassFirst`・`LifetimeFirst`・`ReadFirst`・`ControlClassFirst`・`ControlReadFirst`）と、単独の登録の round だけを行う条件付きの 2 つ（`BaseAloneFirst`・`OverlayAloneFirst`）。照合、AC2〜AC6 の round、観測と log の record、後始末 | 540 | Play を伴う orchestration。判断は `OverlayVerdict` に置く |
| `Tests/Spikes/OverlayProbes/ProbeBaseOnly.cs`・`ProbeOverlayOnly.cs`・`ProbeBoth.cs` | `_tag`（string）と `_value`（int）だけを持つ空の `MonoBehaviour`（namespace は `OneStarMaker.Tests.Spikes.OverlayProbes`、先頭に `#nullable enable`。期待する `FullName` はこの namespace と class 名）。base の Scene と `SharedPrefab` は `ProbeBaseOnly` と `ProbeBoth`、上書きの `SceneEdit` は `ProbeOverlayOnly` と `ProbeBoth` を持つ | 各 15 | Content Directory の component が指す script は、player の script の compile に入る assembly に要る。test 制約（`UNITY_INCLUDE_TESTS`）を持つ assembly は、build に define を渡しても compile に入らない（§6 の差し戻しの事実）ので、下の probe 用の asmdef に置く。test 側は型を参照せず名前と `.meta` の GUID で照合する |
| `Tests/Spikes/OverlayProbes/OneStarMaker.Spikes.OverlayProbes.asmdef` | probe の 3 型だけを持つ assembly。`references` は空、`defineConstraints` は空、`includePlatforms` は空（全 platform。Editor の Play と StandaloneWindows64 の build の両方に要る）、`autoReferenced` は false。どの assembly もこれを参照しない | — | test 制約を持たないので player の compile に入る。どこからも参照されないので依存の向き（Game → Framework）と循環に辺を足さない。Phase D まで、SampleGame の player にも 3 型が入る（空の `MonoBehaviour` で、どの Scene も使わない）。一時の物なので `unity/Assets/README.md` の依存図には載せない（`OneStarMaker.TestSupport` も載っていない） |

合計は約 1,700 行。500 行に届く `OverlayRounds.cs` は非分割とする（1 つの Play の往復の列で、変更理由は round の定義だけ。判断は pure 側に出してある）。S2-0b より小さくする手段: 解除の case の表（U1〜U7）と R0・R1・T0〜T3 の時点を持たない、pixel の所在の判定を持たない（`EditedTex` と `SharedTex` は読める小さな texture で、load 後の pixel の sha256 だけを見る）、正の対照は S2-0b の結果（列挙は同名の別の texture を 2 つと数えた）を前提にして再実施しない。反復は同じ round を process の中で繰り返さず、最初の round だけが違う 5 つの test を 1 回ずつ起動する（違いは round の並びの定数だけ）。

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
| probe の 3 script と probe 用の asmdef | 本スライスの branch | Phase D まで | harvest の commit で削除 |

## 4. 実装計画

- 順序: (1) pure の 2 file。(2) probe、fixture、build。(3) Play の round と record。(4) 実行は Phase C。
- Phase B から Phase A へ差し戻す条件: 製品 code か asmdef の変更が要る。製品の wrapper を 2 つ持って登録できず `ContentLoadManager` を直接呼ぶ必要がある。P1〜P3 を満たす fixture が §3 の形で作れない。probe を probe 用の asmdef（§3）に置く形で Content Directory の build か load が成り立たない。§2 の語で観測できない。
- B 適応の範囲: `BuildPipeline.BuildContentDirectory` の引数（harness が直接呼ぶ build の引数で、製品の build の経路は変えない。revision 1 では `extraScriptingDefines` は製品と同じ空を既定とする）、fixture の asset の細部（大きさ、色、GameObject の構成）、record の項目名、round の順序、component の付け方（`MonoScript.GetClass` と `AddComponent`、または YAML の書き換え）、S2-0b の harness から写す範囲、layout から MonoScript の番号が読める時の追加の記録。
- 対象外を維持する方法: 製品 file を変えない（AC8）。build の出力と record は `TestResults/` 下の一時 root に置く。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。

## 5. テストとレビュー計画

- 単体テスト: `OverlayVerdictTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdictTests`。
- 判定必須テスト（GO 候補 head）: 次の 7 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdictTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayBuilds.FirstProcess`。(3)〜(7) `-Filter OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayRounds.ClassFirst`、同 `.LifetimeFirst`、同 `.ReadFirst`、同 `.ControlClassFirst`、同 `.ControlReadFirst`（どれも (2) の同じ出力を読む）。**条件付き:** (3)〜(7) のどれかで、主の順の 2 つ目の登録か load を Unity が拒否した時だけ、同 `.BaseAloneFirst` と同 `.OverlayAloneFirst` をそれぞれ別の process で実行し、拒否された呼び出しが単独の登録の最初の Play で成功するかを記録する（§2 の GO）。拒否が無ければ実行しない。各 XML の total が 1 以上で failed 0、root result `Passed`、record に全 round の終了行がある。ここまでは harness が最後まで動いたことの確認で、その後に判定 C が §2 の定義を record に当てて答えを読む。**process の自然終了をゲートにしない**（XML の完成後に Unity が終わらない既知の終了 stall は、`tools/Harness/README.md` の規則に従い、XML と record の終了行で判定する）。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC8）、build と Play を行う `[Explicit]` の実験 test は通常の回帰に混ぜない。足すのは、どこからも参照されない asmdef 1 つと、その中の空の `MonoBehaviour` 3 つだけである。代替証拠: AC8、Unity のコンパイル確認、`contract-audit`。
- 操作・実行時・目視条件の検証経路: 全て上の 7 コマンド（と条件付きの 2 つ）で閉じる（Unity バッチで、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、Enter Play Mode の Domain Reload が無効、作業ツリーが clean。観測は record・XML・log。目視は無い。証拠は record・XML・log・各 build の `ContentLayout.json` の写しを evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路の疎通: EditMode の `[UnityTest]` から Play に入り、製品の wrapper を 2 つ持って 2 つの directory を登録する経路は S2-0b が成立させた。**未確認:** probe 用の asmdef の `MonoBehaviour` が、StandaloneWindows64 向けの Content Directory の build に script として入ること（test 制約の無い asmdef は player の compile に入る見込みで、B は Editor の compile 確認に加えて、build ではない player の script の compile でその dll が出ることを確かめてよい。build に入ったかは P1 の `SourceAssets` が確かめる）。その Scene を、source を消した Editor の Play で load して `GetType()` が読めること。source を v2 に変えた後の 2 回目の build が v2 を取り込むこと。初回確認は Phase C（理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: (2) の P1・P2 と、(3) の AC4・AC2。不成立時: 基盤不備なら B、fixture の形で作れないなら Phase A）。
- 人間の判断が必要な条件: なし（結論を受けた形の採否は Phase D で owner）。
- 高コスト証拠: なし（build 2 回、Play は 1 process あたり 7 回（AC4、AC5、AC3、AC6 の 2 回、AC2 の 2 回）で、5 process の 35 回の見込み。Unity の起動は 7 回。拒否が出た時だけ 2 回増える）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。`OverlayVerdict` の出力は補助である。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude、Anthropic）。A0 は program §5.3「当面の方針」と §8 の S2-1 の行・S2-0・S2-0b の移送を入力とした。
- A2 独立レビュー（実施 2026-11-13。互いの指摘を見せていない。全て Anthropic の系列で、別ベンダーは C' 用に残した）:
  - (1) 目的レビュー: 本 session が起動した新しい subagent（Claude Opus、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `4b072c253db3e532693773377896e0ac790bb8d00da101ba0b65c88545a8bfe7`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。
  - (2) A0 だけからの代替構成: (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は program §5.3・§8、事実の文書、製品の native 層の source、前例の harness の file 一覧。本書は読んでいない。
  - (3) アーキテクチャゲート兼、各不変条件を破る順序と §2 の誤答の一覧の外の探索: 本 session が起動した別の新しい subagent（Claude Fable、Anthropic）。入力は A2 入力版の本書（内容 sha256 `561cc79f03995623d6562928a338eda957abc6454fa38ed3a3d0456650e72767`、commit `1cfcad07`）と、アーキテクチャゲート、事実の文書、program、製品の `Runtime/BuildContent` の source、asmdef、前例の harness、AGENTS.md。枠は「計画した実験 code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。
  - (4) 凍結前の外部レビュー（draft PR #182、2026-11-13）: cursor の background agent（Grok、xAI。A2 とは別ベンダー）が 3 巡。対象は 1 巡目が commit `28ea0846`、2 巡目が `e1c3e234` の本書。観点は PR 本文の 4 つ（誤って YES を出す道、判定の形、範囲、規模）。3 巡目の対象は `713e283a`。同じ PR に owner のレビュー（対象 `e1c3e234`）が 1 件。
- A3 統合担当・採否: 本 session（§5.1）。2026-11-13 に owner が凍結を指示し、revision 0 として凍結した。目的と利用者の欄は A1 の後に変えていない（目的レビューのやり直しは無い）。
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
| A-2 | 別の process で 1 回再現する | 採用 | 同じ process の中の反復を、別々の process に替えた（その後、見直し S-1 と N-2 で、順を変えた 5 つの process にした）。§2.2 の前文、§5 |
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
| G-06 | should-fix | Unity の拒否（答え）と、type tree の欠落などの失敗（未判定）が分けられない | 採用 | 単独の登録で成功し 2 つの登録でだけ失敗した時に限って答えにする（単独の成功の根拠は、その後 N-3 で条件付きのコマンドへ移した）。§2 の GO、AC2 |
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
| S-1 | 前の Play の残りから守られていたのは Q2 の round（process の最初）だけで、Q1・Q4・対照の答えは守られていなかった。同じ並びの process を 2 回繰り返しても、残りが効くなら 2 回とも同じ誤りになる | 最初の round だけが違う process に替えた（この時点で 4 つ。N-2 で 5 つ）。各 round の答えは、それが最初の Play になった回から採り、他の回と違えば未判定。残りが答えを変えるかどうか自体も、この照合で分かる。§2 の (h)(i)(l)、§2.2 の前文、§5 |
| S-2 | Scene の id は path から作られ、load は handle を取らない。base の root の id と上書きの root の id は同じ要求になる見込みで、初稿の「弱」と「条件付き YES」は、id が違った時にしか起こらない | 現況に事実を足し、Q1 の語に「id が等しい時は強か不成立」を書いた。含意の表に「上書きを登録している間は base の版を選べない」を足した |
| S-3 | script をまとめた file が「先に読んだ側」で固定される作りだと、Scene を読む順で壊れる側が変わる。初稿はその順を意識していなかった | 両方の Scene の probe を見ることと、AC4 と AC5 で両方の順が入ることを (k) に書いた |
| S-4 | 上書きに無い物（`SceneKeep`・`SharedPrefab`）が、上書きを登録した下で load できない場合の読み方が無かった | AC7 の補足（単独の登録で成功していれば NO という答え。根拠は、その後 N-3・R3-1 で `BaseAloneFirst` へ移した） |
| S-5 | 「未確認」のうち 3 つは、Unity を起動せずに build 履歴から確かめられた（script の file の id が build によらず同じ、一覧は使う script だけ、Scene の帰属は GUID で引ける） | §1 の現況に事実として書き、I-1 の未確認から外した |
| S-6 | `EditedMat` が両方で同じ file であることを前提にしていなかった（`SceneKeep` の `EditedTex` の観測を読む時に要る） | P3 に足した |

**(6) 外部レビュー 2 巡目と owner のレビュー（PR #182、対象 `e1c3e234`。blocker なし）**

| ID | 重さ | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|---|
| R2-1 | should-fix | (k) の「両方の順」の片方を AC5 の (5) の読み直しに頼っているが、AC5 の (1) の load の順が書かれておらず、base を先にすると逆順の冷たい回が無い | 採用 | AC5 の (1) を上書きの `SceneEdit` 先に固定し、(k) を「AC4（base 先）と AC5 の (1)（上書き先）が、それぞれ最初の Play」に書き直した。(5) は逆順の根拠から外した |
| R2 の文言 | — | 残りの round の「決めた順」を書く | 採用 | §2.2 の前文に列を書いた |
| N-1 | should-fix | Q3 の計数に、両方の directory にある `EditedTex`・`EditedMat` が入っておらず、3 実体になっても「収まる」になる | 採用 | Q3 の対象に足し、AC4 と AC5 の (1) の両方の時点で数える。AC4 でも `EditedTex` の `Loadable` を保持する。§2 の Q3、AC4、AC5、AC7 |
| N-2 | should-fix | 対照の版の観測（AC6 の AC3 相当）は必ず対照の class の round の後で、差が出ても登録の順だけの効果と言えない | 採用（process を足す側） | `ControlReadFirst` を足し、対照の 2 つの round のそれぞれに最初の Play になる回を持たせた。参考観測へ下げる案より、owner が測ると決めた対照（U-2）を使える形にする方を採った。Unity の起動が 1 回増える |
| N-3 | note | AC2 は全ての round の後なので、「単独では成功する」の根拠として弱い。拒否を NO にする根拠が切り分けられない | 採用 | 拒否が出た時だけ、単独の登録を最初の Play で行う条件付きのコマンドを実行し、その結果でだけ拒否を答えにする。実行しない間は未判定。§2 の GO、AC2、§5 |

**(7) 外部レビュー 3 巡目（PR #182、対象 `713e283a`。blocker なし）**

| ID | 重さ | 指摘（要約） | 採否 | 理由と反映先 |
|---|---|---|---|---|
| R3-1 | should-fix | AC7 の補足だけが「単独の基準で load できていれば NO」のままで、全ての round の後の AC2 の成功で拒否を NO にできる | 採用 | N-3 の反映の取り残し。補足の根拠を `BaseAloneFirst` に替え、実行していない間は未判定と書いた。AC7 の補足 |

**A1 の自己点検（A2 の前）:** 目的の 4 項目と Q1〜Q4、AC3〜AC5 が 1 対 1 に対応すること、利用者の入力が無いこと、結果に依存する文書（事実の文書と program の改訂）を受け入れ条件に入れず harvest に置いたことを確かめた。

### 5.2 revision 1（2026-11-13）

- 契機: 判定 C（1 回目）の後の B の調べ（§6 の「判定 C からの差し戻し（1 回目）」）。probe を置いた `OneStarMaker.TestSupport` は `UNITY_INCLUDE_TESTS` の制約を持ち、harness が build に `UNITY_INCLUDE_TESTS` を渡しても、player の script の compile の対象に入らなかった（判定 C の build の compile の graph で観測）。§4 の「Phase A へ差し戻す条件」に当たる。
- 検討した置き場所: (A) test 制約の無い新しい asmdef。(B) asmdef の無い場所（`Assembly-CSharp`）。依存の契約の外に script を置くので取らない。(C) fixture が一時に asmdef と probe を作り、第 1 process の最後に消す。第 2 process の Play で class を解決する assembly が Editor に無くなり、残せば I-3 に反するので成り立たない。(D) 既存の製品の assembly。I-3 に反する。
- **owner の決定（2026-11-13、root）:** (A) を承認。AGENTS.md の「asmdef 参照の追加は設計判断」と、§3 の「新しい asmdef を足さない」を、この決定で置き換える。
- 変えた所: §3 の前文・probe の行・asmdef の行・資源の表、§4 の差し戻す条件と B 適応の範囲、§5 の適用除外の理由と未確認、I-1 の未確認、I-2 (a) と未確認の語（「test 用 assembly」→「probe 用の asmdef」）。
- 変えない所: 目的と利用者の欄、§2 の問い・語・判定定義、§2.1 の不変条件と成立条件の主張、§2.2 の受け入れ条件、§5 の判定必須テスト。probe の namespace（`OneStarMaker.Tests.Spikes.OverlayProbes`）と class 名と `.meta` の GUID は変えないので、期待する `FullName` と fixture の `ProbeGuids` は変わらない。
- Phase B への影響（実装の指示ではなく、変わった計画から出ること）: probe の 3 file を `.meta` ごと新しい folder へ移す、asmdef を足す、`OverlayBuilds` の `ScriptingDefines` を空にする（`UNITY_INCLUDE_TESTS` も、差し戻しで足した `DEVELOPMENT_BUILD`・`DEBUG` も要らなくなる）。差し戻しで入れた第 1 process の log の捕捉（`FirstProcessLog`）は置き場所と独立なので残す。
- A2: アーキテクチャゲート 1 件（asmdef の追加なので高リスクの観点。対象は本 revision の差分の部分。owner がこの規模を承認）。目的と利用者の欄を変えていないので、目的レビューはやり直さない。A0 からの代替構成の担当は、置き場所の候補の検討が上の 4 つに尽きているので立てない。

## 6. Phase B 実装結果

実施 2026-11-13。§3 の file を §4 の順で作った。製品の file と asmdef は変えていない。

| file | 行数（計画） | 中身 |
|---|---:|---|
| `Tests/TestSupport/ProbeBaseOnly.cs`・`ProbeOverlayOnly.cs`・`ProbeBoth.cs` | 各 19（15） | `_tag`・`_value` と読み取り用の property だけの `MonoBehaviour`。property は未使用の field の警告を避けるため |
| `OverlayVerdict.cs` | 511（200） | fixture の定義（`OverlayExpected`。期待値の出所）、観測の型（JSON に書ける形）、P1〜P3、位置ごとの class の照合、round の並び（`Plan`）と登録の順、Q1〜Q4・スパイクの答え・未判定の理由 |
| `OverlayVerdictTests.cs` | 403（250） | 全てが成り立つ入力を作り、§2 の (a)〜(e)・(h)〜(l) と §3 の列挙を 1 つずつ入れる 30 件 |
| `OverlayFixture.cs` | 285（300） | v1 の作成、v2 への書き換え（`SceneEdit` と `EditedTex` だけ、同じ GUID）、root、marker と残りの回収 |
| `OverlayBuilds.cs` | 430（350） | `FirstProcess`、build、棚卸し、layout による帰属と P の材料、record（`SpikeRun`）、process の前後の比較（`Aftercare`） |
| `OverlayRounds.cs` | 746（540） | 7 つの test、round（AC2〜AC6）、観測、保持した物の確認点（`Held`）、log の捕捉、後始末 |

**行数の超過（計 約 2,430 行、計画 約 1,700 行）の構造の説明:** 分割の判断は変えていない。超えた主な所は 3 つ。(1) `OverlayVerdict.cs` に、fixture の定義と観測の型を置いた。fixture・round・判定の 3 者が同じ定義と照合するので、定義を 1 か所に置き、Unity の型を持たない pure 側に置いた。(2) 判定は 5 つの process の観測を合わせて行う（答えを採る回と照合、単独の最初の Play の根拠）。各 process は自分の観測を JSON に書き、補助の判定はその実行の全ての観測を読む。そのための型と読み書きが増えた。(3) `OverlayRounds.cs` の `Held`（AC5 の確認点ごとの「壊れた」の検査、約 90 行）。round の列は計画どおり 1 つの Play の往復の列で、判断は pure 側にある。

**B 適応（§4 の範囲。Phase A は再開していない）:**

- build の引数: `extraScriptingDefines` に `UNITY_INCLUDE_TESTS` を渡す（probe は `OneStarMaker.TestSupport` にあり、その asmdef の制約が `UNITY_INCLUDE_TESTS`）。この define を制約に持つ非 Editor の asmdef は `OneStarMaker.TestSupport` だけであることを確かめた（他の test の asmdef は Editor 専用）。
- component の付け方: probe の `.meta` の GUID から `MonoScript` を引き、`GetClass()` と `AddComponent`、値は `SerializedObject` で書く。test 側は probe の型を参照しない。
- S2-0b の fixture から、固定 GUID・marker・残りの回収は写し、固定 fileID への書き換えは写さなかった。共通物は v1 で 1 回だけ作って両方の build が同じ source を使い、帰属は GUID で引くので、fileID を揃える必要が無い。
- record は process ごとに `TestResults/ovl/<実行 id>/record-<process>-<時刻>.jsonl` へ分け、同じ process を再び実行しても前の record に追記しない。各 process の観測は同じ場所の `observations-<process>-<時刻>.json` に書く。各 record の先頭に成果の範囲の行（`scope`）を書く。
- AC3 では、`SceneEdit` を 2 回 load して unload した後に `UnloadUnusedAssets` を待ってから `EditedTex` を load する（AC7 の数え方を `EditedTex` で見る時に、Scene から載った物を混ぜないため）。
- AC2 の round と、条件付きの `BaseAloneFirst`・`OverlayAloneFirst` の round は同じ実装である（違いは process の最初の Play かどうかだけ）。
- 呼び出しの名前は `base:register`・`overlay:SceneEdit` の形（directory と物）。Unity の拒否を答えにする条件（§2 の GO）は、この呼び出しの単位で、同じ呼び出しが単独の登録の最初の Play で成功したかを見る。

**判定の写像で、語の読み方を決めた所（発見 C と判定 C が確かめる。補助の判定だけの話で、判定 C は record から読む）:**

- AC4 の `SceneEdit` が上書き版（`Marker_v2`）でない時は、Q2 を未判定にした。Q2 の「上書き版の `SceneEdit`」が観測できず、定義した語で読めないため（§2 の未判定の「Phase A へ戻す」側）。
- Q1 で、両方の root の id が等しいのに「弱」が観測された時は未判定にした（§2 の Q1 の語では起こらない）。上書きが v2 で base の版が v1 でも v2 でもない時も未判定。
- AC5 の Q4 に添える log は Error と Exception だけを数え、(6) と後始末の間の物は (1)〜(5) と分けて数える。log 自体は種類を問わず全件を record に書く。
- AC7 の整合は、AC4・AC6-class・AC5 の (1) では 7 つの物、AC3・AC6-read では両方の root から保持した `EditedTex` で見る。

**確認:**

- Unity Editor（6000.6.0f1）のコンパイル: 成功。この worktree の project を開き、`EditorUtility.scriptCompilationFailed` が偽、Editor.log に `error CS` が 0 件、新しい file の警告が 0 件だった。3 つの test の型は `OneStarMaker.Tests.Editor`、probe の 3 型は `OneStarMaker.TestSupport` に読み込まれ、probe の `.meta` の GUID から引いた `MonoScript.GetClass()` が期待する完全名を返した。Editor は確認後に正常終了させた。
- Phase C への注意: この worktree の project を初めて開いた時、Editor が本スライスと関係の無い tracked file（`Assets/MobileDependencyResolver` の `.pdb.meta` 5 つの削除と、`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の `m_RuntimeSettings` の空化）を書き換えた。commit には含めず、Editor の終了後に HEAD の内容へ戻した。判定 C の process がこれを再び起こすと、AC8 の前後の `git status` の比較に出うる（record の `process-start` と `process-aftercare` の `gitStatus` で区別できる）。
- `pwsh tools/contract-audit.ps1`: 合格（errors 0、warnings 0、検査 1〜9）。
- Unity のテストと build は実行していない（SKILL の Phase B）。`OverlayVerdictTests` も未実行。§5 の未確認（probe が build に入るか、source を消した Play で class が読めるか、2 回目の build が v2 を取り込むか）は Phase C の初回確認に残る。
- 未確認の前提: Unity が `ContentLoadManager.GetContentDirectories()` の `BuildName` に build の名前（`ovl-base`・`ovl-overlay`）を返すこと（後始末の回収が使う。S2-0b と同じ前提）。

### 判定 C からの差し戻し（1 回目、2026-11-13）

判定 C（1 回目）の (2) `FirstProcess` の base の build の失敗を調べた。Unity のテストと build は実行していない（SKILL の Phase B）。

**分かった事実（観測）:**

- build の失敗の直接の原因: Input System の `InputSystemTestHooks.cs`（`#if UNITY_EDITOR || UNITY_INCLUDE_TESTS` の中）が使う `InputSystem.remoteConnection` は `#if DEBUG` の中にしか無い（package の source で確認）。`UNITY_INCLUDE_TESTS` だけを渡した非 development の compile では未定義になり、`error CS0117` になる。`BuildContentOptions` には development の option が無い（Unity の API 文書の列挙は `None`・`CleanBuildCache`・`DisableWriteTypeTree`・`FailBuildWhenErrorsLogged`・`SerializeUnityVersion`・`SkipExportToOutputPath`・`UseArchive`）。
- fixture が残った原因: build の compile の Error を Unity Test Framework が未処理の log として test の失敗にし、`FirstProcess` の coroutine を打ち切ったので、`finally`（fixture の削除と `process-aftercare`）が走らなかった。record が 3 行で止まったことと符合する。
- **probe が build に入らない:** 判定 C の build が作った player の script の compile の graph（`unity/Library/Bee/1900b0aP.dag.json` と `artifacts/1900b0aP.dag/` の `.rsp`）では、`Unity.InputSystem.rsp` の define に `UNITY_INCLUDE_TESTS` があるのに、`OneStarMaker.TestSupport` の compile の項目が無い（同じ `UNITY_INCLUDE_TESTS` の制約を持つ `UnityEngine.TestRunner` も無い）。下の compile 確認の graph（`19c0000P`）でも同じだった。`extraScriptingDefines` は script の中の `#if` には効くが、asmdef の `defineConstraints` で除かれる assembly を compile に入れない、と読める（Unity の内部の採否の規則は確かめていない）。この compile は build の失敗の前に graph が作られた物で、失敗が無くても probe の 3 型は player の assembly に無い見込みである。その時、Scene と `SharedPrefab` の probe は最初から Missing になり、P1 の `SourceAssets` にも probe が入らない（I-1 (f) で止まる）。

**分類:** §4 の「Phase A へ差し戻す条件」の「probe を `OneStarMaker.TestSupport` に置く形で Content Directory の build か load が成り立たない」に当たる（G-01 の採否で「置き場を変える必要が出たら Phase A へ戻す」と決めた所）。B 適応の範囲（§4 の build の引数）には、test assembly を compile に入れる手段が無い。`ProjectSettings` の scripting define を process の間だけ変える手は、AC8（設定を前後で変えない）と I-3 (b) に反し、domain reload も起こすので取らない。置き場所の候補（新しい非 test の asmdef、fixture が一時に作る asmdef と probe、等）は asmdef と §3 の責務マップに関わるので、B では選ばない。

**入れた B 適応（`OverlayBuilds.cs` だけ。置き場所の決定とは独立に要る修正）:**

- `ScriptingDefines` に `DEVELOPMENT_BUILD` と `DEBUG` を足した（package の test 用 code は development player で compile される前提なので、同じ 2 つを渡す）。Phase A が probe を `UNITY_INCLUDE_TESTS` に頼らない置き場所へ移すなら、`UNITY_INCLUDE_TESTS` ごと外せる。
- 第 1 process の log の捕捉（`FirstProcessLog`）。process の間 `LogAssert.ignoreFailingMessages` を立てて log を test の失敗にせず、種類を問わず `unity-log` の行として record に書き、`finally` で購読と `LogAssert` を戻す。build の失敗は record の行（未判定の材料）にする、という `FirstProcess` の作りに実装を合わせた（§2.2 の log の扱い、I-3 (d)）。

**確認:**

- Unity Editor（6000.6.0f1）のコンパイル: 成功。この worktree の project を開き、`EditorUtility.scriptCompilationFailed` が偽、`unity/Logs/Editor.log` に `error CS` が 0 件、`OverlayRegistration` の警告が 0 件、`FirstProcessLog` の型が `OneStarMaker.Tests.Editor` に読み込まれた。
- player の script の compile（build ではない。`PlayerBuildInterface.CompilePlayerScripts`、StandaloneWindows64、define は `UNITY_INCLUDE_TESTS`・`DEVELOPMENT_BUILD`・`DEBUG`、出力は一時 folder で確認後に削除）: `error CS` 0 件。出力の `Unity.InputSystem.dll` に `InputSystemTestHooks` と `remoteConnection` の名前が入っていた（define が code に効き、CS0117 が消えた）。`OneStarMaker.TestSupport.dll` は出力に無かった（上の事実）。この compile が `BuildContentDirectory` の compile と同じ規則かは確かめていない（graph の作りは同じに見える）。
- 判定 C が残した fixture の folder（`unity/Assets/OsmSpikeOvl/` と `.meta`、untracked）は、marker の文字列が `OverlayFixture` の定義と一致することを確かめて消した（次の `FirstProcess` の開始時の回収と同じ扱い）。
- Editor は確認後に正常終了させた。Editor が §6 の「Phase C への注意」と同じ `.pdb.meta` 5 つを消したので、HEAD の内容へ戻した。
- `pwsh tools/contract-audit.ps1`: 合格（errors 0、warnings 0、検査 1〜9）。
- Unity のテストと build、`OverlayVerdictTests` は実行していない。

### 動作確認

`対象なし`（§5）。

## 7. Phase C

### 発見 C（1 回目、2026-11-13）

- 種別: 発見。GO 判定はしていない。C' は起動していない。
- 対象: implementation base `bc3ca436` から head `4191ed9d663b42adaa0ac56e080e06666c862a2c` の差分（commit 固定。stage snapshot は使っていない）。開始時の照合（渡された SHA と HEAD、clean、ブランチ、`git diff --stat HEAD^ HEAD` に `docs/handoff/` 以外の file、PR #182 の状態ブロック）は全て一致した。
- 使用した evidence bundle の取得案内・状態: 未生成（発見 C は commit を直接読んだ。判定 C が作る）。
- 構造適合: 適合。
  - 変更は §3 の新規 file と `.meta`、本書、program の 1 行だけ（`git diff --name-status bc3ca436 HEAD`）。製品の file と asmdef は変わっていない。probe は `OneStarMaker.TestSupport`、他は `OneStarMaker.Tests.Editor` にあり、Runtime 側に `UnityEditor` 依存は無い。test 側は probe の型を参照していない。
  - 責務は §3 の表どおり。判断は Unity の型を持たない `OverlayVerdict` にあり、単体テスト 30 件が付く。`OverlayRounds.cs`（746 行）と `OverlayVerdict.cs`（511 行）の超過は §6 の説明（fixture の定義と観測の型を pure 側に 1 か所、5 process を合わせる判定、`Held`）で読め、変更理由の混在は無い。Phase D で消す実験 code なので分割は求めない。
  - 資源の所有と寿命は §3 の表どおり（fixture は第 1 process の `finally`、登録・token・Scene は round の `finally`、log の購読と `LogAssert` は round ごとと `Finish`）。
  - 偽 null: `UnityEngine.Object` は全て `== null` / `!= null` で判定している。`?.`・`ReferenceEquals` は C# の class（layout の行、token）にだけ使っている。
  - 製品の `UnityContentDirectoryBackend` は無改変で 2 つ持ち、両方を登録してから load している。
- 凍結済みの条件との照合（静的に読んだ範囲）:
  - §2 の (a)〜(l): P1 は script の file の layout の `SourceAssets` を probe の `.meta` の GUID（実 file と一致を確認）から引いた path で照合し、sha256 の差だけでは通らない。P2・P3 は固定 GUID で帰属を引く。class は位置ごとの完全名と値、Missing は `GetComponents<Component>()` の `== null`。期待値は `OverlayExpected` だけから採る。root は別の固定 GUID の 2 asset で、handle ごとの `BuildIdentity` を照合する。答えは `PlayIndex == 0` の回から採り、他の回と違えば未判定。拒否は、同じ呼び出しが `BaseAloneFirst`・`OverlayAloneFirst` の最初の Play で成功した時だけ答えになる。
  - AC1〜AC8 の手順（round の並び、AC4 と AC5 (1) の load の順、AC5 の (1)〜(6)、対照の順、後始末と直接回収、前後の `git status` と設定の比較）は凍結した文言どおり。
  - §6 の「語の読み方を決めた所」の 4 つと B 適応は、凍結済みの条件と矛盾しない（F-2・F-6 に補足）。`UNITY_INCLUDE_TESTS` を制約に持つ非 Editor の asmdef が `OneStarMaker.TestSupport` だけであることは `Assets` 下で確かめた。
- 現在の問いを阻害する findings: なし。
- 阻害しない findings（どれも凍結済みの条件・常時契約への違反ではない。修正は求めない。category は全て semantic、unique、担当は本欄の末尾）:

| ID | 重さ | 指摘 | 根拠 | bucket と扱い |
|---|---|---|---|---|
| F-1 | note | script をまとめた file を最初に読むのは、どの round でも Scene の load ではなく `RegisterAll` の root の読み取り（`BuildContentRoot` の MonoScript はその file にある）で、順は登録の順である。AC4 と AC5 (1) の Scene の順の違いは「先に読んだ側」を変えていない見込み。YES が出た時は base だけの script と上書きだけの script の両方が解決されたことになるので、(k) の誤答の道は開かない。NO が出た時に「壊れる側が Scene の順で変わる・変わらない」と読む根拠にはできない | `OverlayRounds.cs` の `RegisterAll`（root の照合）と `Entry`、§2 の (k) | 文言の隙間。判定 C と Phase D は、結論で Scene の順の効果を述べる時にこの事実（root は登録の順に先に読まれた）を添える。root を読む順を変えて測るかは owner が決める後続（S3 の入力） |
| F-2 | note | AC4 の `SceneEdit` が上書き版でない時に Q2 を未判定にする読み方は、凍結した Q2 の語（上書き版の `SceneEdit`）に沿う。ただし Scene で Q1 が不成立（上書きの id から v1）なら AC4 でも v1 になる見込みで、その時の補助の判定は NO ではなく UNDECIDED になる。凍結文はこの場合の Q2 の読み方を書いていない | `OverlayVerdict.cs` の `Q2`、§2 の Q2 と GO | 文言の隙間。起きた時は、判定 C が record の事実を示して owner に返す（どの条件の合否も変えない読み方なら決定記録、変えるなら Phase A）。補助の判定の UNDECIDED をそのまま結論にしない |
| F-3 | note | Q4 に添える log の数（`ErrorLogs`）は、Play に入る間と登録の間の Error も (1)〜(5) 側に数える。Play を抜ける間の Error は `round-end` の行を書いた後に `ErrorLogsInRelease` に足される（観測の JSON と `round-end` の数がずれうる）。答え（無傷 / 壊れた）には影響しない | `OverlayRounds.cs` の `Flush`・`BeforePlay`・`AfterPlay` | 判定 C は「確認点の間の log」を、数ではなく `unity-log` の行の `phase` で読む |
| F-4 | note | AC2 の `EditedTex` は版（pixel の sha256 が定義と一致したか）だけを record に書き、sha256 の実値は書かない（AC3 は書く） | `OverlayRounds.cs` の `EditAsync` と `alone` の行 | 版は定義の sha256 との完全一致でしか付かないので、AC2 の照合はできる。追加は求めない |
| F-5 | note | 第 1・第 2 process の後始末の比較（`git status`、設定、fixture の残り）は `problems` として record と補助の判定に入るが、test の失敗にはならない | `Aftercare.End`、`Session.Finish` | XML の Passed は AC8 を意味しない。判定 C は各 process の `process-aftercare` の行で AC8 を読む |
| F-6 | note | 対照の順の round で登録か load が拒否された時、補助の判定は単独の最初の Play の成功を求めて UNDECIDED になるが、§5 の条件付きのコマンドは主の順の拒否の時だけ実行する | `OverlayVerdict.cs` の `RoundAnswer`、§5 | 文言の隙間。起きた時は判定 C が owner に返す |

- 判定 C への注意（運用。条件の追加ではない）:
  - 第 2 process は record の head を第 1 process と照合する（`InputMatched`）。(2) から (7)（条件付きを含む）の間に commit を切ると未判定になるので、記録 commit は全ての実行の後に切る。
  - 補助の判定は、同じ実行 id の `observations-*.json` を全て読む。ある process をやり直すと、前の回の観測（途中で止まった回を含む）も読まれ、UNDECIDED の理由に残る。答えは record の行から読む。
  - §6 の「Phase C への注意」のとおり、Editor がこの worktree で関係の無い tracked file を書き換えることがある。各 process の前に `git status --porcelain` が空であることを確かめ、書き換えが出たら HEAD の内容へ戻してから次を起動する（§5 の初期状態）。process の中で起きた分は `process-aftercare` に出る。
- 後続スライスへ移送する findings: F-1 の後半（root を読む順を変えた時の挙動。S3）。
- 実行したテストコマンドと `-Filter`: なし（阻害する指摘が無く、起点の `OverlayVerdictTests` は判定必須の (1) と同じなので、判定 C でまとめて実行する）。
- 機械検査: `pwsh tools/contract-audit.ps1` 合格（errors 0、warnings 0、検査 1〜9）。本記録を書いた後の `pwsh tools/docs-audit.ps1` は errors 0、warnings 2（本書と program の §7 が埋まっているという harvest の促しで、違反ではない）。
- 判定必須のうち未実行: 7 つ全て（と条件付きの 2 つ）。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: Unity 上の挙動は何も確かめていない。§5 の未確認（probe が build に入るか、source を消した Play で class が読めるか、2 回目の build が v2 を取り込むか）、§6 の未確認の前提（`BuildName`）、Play の間に前の round の物が残らないこと（開始時の 0 件）は、判定 C の実行で初めて分かる。
- B との往復: 0 巡。
- 担当・モデル: 発見 C は新しい session（Claude Fable 5.2、Anthropic）。B の実装 commit の記録は Claude Opus 5.5 で、モデルは異なり、ベンダーは同じ。

### 判定 C（1 回目、2026-11-13）

- 種別: 判定。**未判定で停止**（§2 の「build を開始できない」に当たる）。GO 判定も答えも出していない。C' は起動していない。実装（`unity/` の file）は変えていない。
- 対象: implementation head `4191ed9d663b42adaa0ac56e080e06666c862a2c`。開始時の照合（渡された SHA と HEAD、clean、ブランチ、implementation head が祖先で差分が本書だけ、PR #182 の状態ブロック）は全て一致した。
- 実行（sandbox 外の承認済み経路、各 process の前に `git status --porcelain` が空、別の Unity process で 1 つずつ）:

| # | `-Filter`（`OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.` 以降） | 結果 | 生ログの所在（`TestResults/`、git 管理外） |
|---|---|---|---|
| 1 | `OverlayVerdictTests` | exit 0。XML は total 30、passed 30、failed 0、root `Passed` | `556aa355-dcf4-42f7-a46d-72f210233814/` |
| 2 | `OverlayBuilds.FirstProcess` | exit 1。XML は total 1、passed 0、failed 1、root `Failed(Child)`。step.json の status は failed、Unity の exit code は 2 | `b2694148-4420-4b41-8544-3172502a8a54/`（results.xml、unity.log、step.json）、record は `ovl/20261113T040007Z/record-first-20261113T040007Z.jsonl` |
| 2'（owner の指示で同じ head のまま 1 回だけ再実行） | `OverlayBuilds.FirstProcess` | **同じ失敗が再現**。exit 1。XML は total 1、passed 0、failed 1。step.json の status は failed、Unity の exit code は 2。`unity.log` の `error CS0117` は 12 件。base の build が Failed の点も同じ | `7b973236-5cb2-4987-a1df-33d3f2bd8ef7/`、record は `ovl/20261113T050846Z/`。再実行の前に `unity/Assets/OsmSpikeOvl/`（marker 付き）が残っていたが、その状態で process は始まった（fixture を消す行の有無は確かめていない）。再実行の後も untracked で残り、`.pdb.meta` 5 つの削除は HEAD へ戻した |
| 3〜7 | `OverlayRounds.ClassFirst` ほか 5 つ | **未実行**（(2) の出力が入力で、(2) が成立しなかったため。条件付きの 2 つも対象外） | — |

- 再実行（2'）の扱い: owner の「もう一回だけ通ればそれを採用」の指示で、同じ head のまま (2) だけを 1 回再実行した。**通らなかったので採用していない**。同じ失敗が再現したので、偶発ではなく決定的な問題として B へ返す結論は変わらない。(2) と (2') の 2 回とも判定の根拠にはせず、B の修正後に 7 つをやり直す。
- (2) の観測（事実のみ。原因調査はしていない）:
  - record の行は `scope`、`process-start`、`build-end` の 3 行だけ。`build-end` は `directory: base`、`result: Failed`、`elapsedMs: 51099`、`extraScriptingDefines: ["UNITY_INCLUDE_TESTS"]`、root は `Assets/OsmSpikeOvl/Root_base.asset`。P1〜P3 の行も上書きの build の行も無く、`process-aftercare` の行も無い。
  - `unity.log` に `Library\PackageCache\com.unity.inputsystem@7a4e1a2a8194\InputSystem\Runtime\InputSystemTestHooks.cs(38,29)` ほか 3 か所の `error CS0117: 'InputSystem' does not contain a definition for 'remoteConnection'` と、`Script Compilation Error for: Csc ... Unity.InputSystem.dll` がある。test の失敗メッセージは同じ Error の `Unhandled log message`。
  - 読み: build の引数 `UNITY_INCLUDE_TESTS`（§6 の B 適応）を渡した script compile で、Unity の入力パッケージの test 用 code が通らなかったことと符合する。これは log の読みで、因果は確かめていない（確かめるのは B）。
  - (2) の後、`unity/Assets/OsmSpikeOvl/` と `.meta`（fixture の folder。record に fixture を消した行が無い）が untracked で残っている。marker は `TestResults/ovl/20261113T040007Z/ovl-run.marker`。何も消していない。
- 各 process の前後の `git status --porcelain`: 起動前は (1)(2) とも空。(1)(2) のどちらの後も、§6 の「Phase C への注意」と同じ tracked file（`Assets/MobileDependencyResolver` の `.pdb.meta` 5 つの削除）が出たので、HEAD の内容へ戻した。(2) の後に残るのは上記の untracked fixture だけ。
- §2 の定義への当てはめ: build を開始できない（base の build が Failed）。Q1〜Q4 とスパイクの答えは読めない。AC1・AC8 の process の前後の比較は record に無く、判定できない。
- 返す先: **Phase B**（harness・fixture・環境の問題。製品 code・asmdef の変更も、凍結した語で読めない挙動も、process の間の不一致も出ていない）。B が B 適応か Phase A の再開かを §4・「新しい事実の分類」で決める。実装 head が変われば、この判定 C の実行は無効で、新しい GO 候補 head で 7 つをやり直す（判定後の head 変更）。残った fixture の folder は、marker を確かめてから扱う。
- 発見 C の所見（F-1〜F-6）への影響: なし（今回の事実は F-1〜F-6 のどれにも当たらない）。
- evidence bundle: 未生成（判定が成立しなかったので作らない。生ログは上の所在に保持）。
- 実行しなかったこと: `contract-audit`（実装は変えていない。発見 C の結果が有効）、Artifact Storage への stage。
- 担当・モデル: 判定 C は新しい session（Claude Sonnet 5.5、Anthropic）。B（Claude Opus 5.5）とモデルが異なり、ベンダーは同じ。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
