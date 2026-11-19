# Editor snapshot を要求時解決の候補にする（S3-3）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S3-3）
- status: `A`（A3 の統合案。A2 の 4 件と担当の採否を §5.1 に記録した。外部計画レビューと owner の採否・凍結の前。未凍結）
- branch: `claude/editor-snapshot-candidate`（PR の base は `develop`）
- implementation base commit: `a629f9c1`（`origin/develop` の head、2026-11-19。§1 の現況と §3 の行数はこの base）
- implementation head commit: `未固定`
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: `未凍結`
- risk: `high`（program の行の値。Scene の所有者と寿命（Scene の台帳に 3 つ目の種別を足す）、起動の段の順序、`SceneDirector` の load の分岐、Editor の public の引数を変える）
- owner: root（依頼者）
- created: 2026-11-19
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「要求時の Content 解決」（snapshot の供給元、既ロードの経路、record、「まだ保証しないこと」）と「Scene 解放の役割分担」、[04-app-startup.md](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.6（取り込みの段、起動時の明示の要求、key と mode の組）、[18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」（経路 Built + open scenes の行）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.2（連携部分が使う `AssetManagement` の internal の口の棚卸しに、預かる口と預け元の port を足す）、program §8・§10
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§3、§4.1、§4.4、§5.2、§6、§8 の S3-3 と S0-b・S0-d・S1・S3-1 からの移送、§9 の G12・G21・G23）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A3（外部計画レビュー、owner の採否、凍結）。凍結の後に Phase B。
  - 次の作業: Draft PR で外部計画レビューを行い、指摘は 1 巡につき 1 commit で反映する。owner が §2.8 の U2〜U11 と §5.1 の採否を決め、凍結判断を上の凍結記録に書いて凍結版を commit する。目的・利用者の 2 欄を変えた時は、凍結の前に目的レビューをやり直す。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結の前に Phase B を始めない。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: Editor で Scene を開いて編集し、検証済みの Content Directory で Play する開発者に、次を保証する: 開いて編集した Scene は、その Play の間ずっと自分の版（Play に入る直前に入口が作る複製。未保存の変更を含む）で動き、開いていない Scene は build 済みの版で動き、途中で黙って入れ替わらない。そのための手段は 3 つである。(1) 複製を要求時解決の供給元の 1 つにし、Scene 名が複製と同じ Scene の要求は複製を最優先で採る。同じ Scene が 2 つ load されることは無く、複製を採れない時は build 済みの版で続けずに失敗する。起動の後に unload して再び要求した時も同じである。(2) 呼び手は複製と directory のどちらから来たかを知らずに済む。得た Scene の所有と解放は directory から読んだ Scene と同じ契約で、record（型は変えない）は複製から読んだことを言う。複製は digest で検証した物ではなく、採る理由は利用者が開いている Scene であることである。(3) Scene が物理的に load されていることと、その Scene を OSM の lifecycle に入れることを分ける。lifecycle に入るのは明示的に要求された Scene だけで、起動時の要求は Play の入口が宣言し、その後の要求は Runtime の code が出す。Framework は「開いていれば要求する」という既定を持たない。
- 利用者: (a) 編集する開発者。担当の Scene を Editor で開いて編集し、Play で確認する。新しく選ぶ・入れるものは無い。(b) Editor の Play の入口を実装する者（SampleGame の入口と、他の host の Editor code）。開いている Scene のうち、起動時に lifecycle に入れる Scene を決める規則を入口の code に書く。規則を渡さない入口は、複製を載せた Play を始められない。(c) loader か SceneDirector で Scene を読む host の Runtime の code。新しい入力は無く、複製から読んだ時は record がそれを言う。(d) 後続（上書きの入口、Local / LAN の供給元、観測の UI）で供給元や表示を足す実装者。
- 目的・利用者の文言の経緯: 2 欄は program §8 の S3-3 の行、§4.1「物理的な Scene ロードと OSM lifecycle は別契約」、S0-b・S0-d・S3-1 から移送した入力から、本書で初めて書いた。A2 の目的レビューは初稿の文言（commit `2ca643f3` の本書 §1）で行い、文言への指摘が 8 件出た（§5.1）。A3 で、結果（開発者に何が保証されるか）を先に書き、手段を後に並べる形に改めた。変更の理由は、初稿が機構（供給元・record・lifecycle）を主語にしていて、指摘の大半がそこから出ていたことである。保証の中身と利用者の入力は変えていない（Scene の同定、「1 つだけ」の意味、宣言と要求の関係、Play 前の時点、複製が検証の対象でないこと、既定が無いこと、record の型を変えないことを書き足した）。owner（root）が 2026-11-19 に、結果を先に書く形への改稿を承認した。改めた 2 欄で目的レビューをやり直した（§5.1。やり直しは 1 回まで）。
- 対象外:
  - 上書きを Play に載せる入口と、上書きを重ねた時の SceneDirector の経路（S3-4）。
  - Object の snapshot（開いている Scene が参照する asset・prefab の手元の版を届けること。program I11・G22。S3-4 の最初の作業が測る）。snapshot の供給元は Scene の要求にだけ答える。
  - 複製の機構（S0-d の (i)）、置き場、回収、Play の入口の判定表の行（経路 Built + open scenes の拒否の条件）を変えること。
  - bootstrap の読み込み（UICommon の Scene、SceneResourceMap）を resolver に通すこと。UICommon を開いたままの Play は今までどおり入口が拒む（S5）。
  - 経路 Source（Addressables）の Play と、snapshot を宣言しない directory の Play の既ロード登録（§2.4「適用する Play」、§2.8 U6）。
  - Player（複製も snapshot の供給元も Editor の build だけにある）。
  - `SceneDirector` の取消を Unity の Scene の load に届かせること（§2.3）。
  - 未要求の複製を、要求されるまで load しない最適化（§2.4「未要求の複製の物理的な load」）。
  - 複製の中身の再検証（Play の間に複製の file が差し替わらないことの証明）。record の到達度は Traceable まで（program §3）。
  - 要求の時点の install（S4）、観測の UI（S5）。
- 現況（base `a629f9c1`。code の読みと `grep` で確かめた。この節と §3 の path は `unity/Assets/OneStarMaker/Scripts/` からの相対で、SampleGame と test だけ `unity/Assets/` からの相対）:
  - **複製は起動時に全て load され、名前で再利用される。** `AbstractApplicationInitializer`（1197 行）の `AfterSceneLoad` は、directory mode で `editor:playSnapshotExpected` が真の時、stage `intake-editor-snapshots`（422〜430 行、`IntakeEditorSnapshotsAsync` は 495〜546 行）で宣言の複製を並び順に全て additive で load して照合する。次の stage `register-loaded-scenes`（`RegisterAlreadyLoadedScenes`、774〜792 行）が、`SceneManager` に load 済みで SceneResourceMap にある全ての Scene に `SceneDirector.AddScene` を呼ぶ。**物理的に load 済みであることが、そのまま lifecycle への要求になる。**
  - **`SceneDirector` は複製を loader に尋ねずに採る。** `PerformUnitySceneLoad`（`Runtime/SceneSystem/SceneDirector.Loading.cs` 629〜664 行）は最初に `SceneManager.GetSceneByName(identity)` を見て、load 済みなら由来を確かめずに `(BackendSceneLoaded: false, roots)` を返す。loader（`LoadThroughContentLoaderAsync`、670〜694 行）に進むのは同名が load 済みでない時だけで、複製の Scene に record は無い（`GetContentLoadRecord` は null）。unload は `PerformUnitySceneUnload`（`SceneDirector.Unloading.cs` 452〜476 行）が `SceneManager.UnloadSceneAsync(名前)` で行う。
  - **起動の後の再要求は directory から読む。** unload の後は同名が load 済みでないので、loader が登録した directory の供給元の候補を採る。Editor の `PlaySnapshotSession.OnSceneLoaded`（`Editor/PlaySnapshot/PlaySnapshotSession.cs` 213〜221 行）が「宣言の identity と同名の Scene が複製以外の path で load された」ことを `SourceChanged` で知らせ、SampleGame の `PlayEntryRecord.MarkSourceChanged`（`SampleGame/DependOnAll/Editor/Build/PlayEntryRecord.cs` 183〜195 行）が由来を `snapshot->directory` と記録して Warning を 1 回出す。
  - **core は snapshot の候補を想定済みだが、供給元と load の経路が無い。** `ContentSourceKind.EditorSnapshot`、`ContentAcceptanceBasis.OpenSceneSnapshot`、`ContentLoadPath.AlreadyLoadedScene` は定義済みで、`ContentResolver.RequiredBasis`（`Runtime/ContentResolution/Core/ContentResolver.cs` 220〜234 行）と `ContentCompatibilityRule`（119〜140 行。snapshot は identity・engine・schema・host の述語を見ずに採る。expected identity だけは由来に関係なく先に比べる）に組がある。製品の code でこの 3 値を使う所は無く（`grep`。使うのは core の表と test だけ）、`ContentRequestLoader`（135 行）は経路が `ContentDirectory` でない候補を `LoadPathUnsupported` で拒む（98〜99 行）。
  - **Scene の台帳は複製を知らない。** `AssetManagement` の Scene の台帳（`AssetRegistry` の identity → `IBackendScene`）に入るのは、Addressables の Scene と directory の token の Scene だけである。`UnloadSceneAsync`（`Runtime/AssetManagement/AssetManagement.cs` 210〜226 行）は token なら directory、それ以外は Addressables の backend に渡す。`LoadContentSceneAsync`（`AssetManagement.ContentDirectory.cs` 112〜142 行）は、台帳に同じ identity の token でない Scene があれば `EntryAmbiguous` で拒むが、台帳に無い複製は見えないので、複製が load 済みの identity を directory から読める（今は `SceneDirector` の名前の再利用だけが 2 つ目を防いでいる）。
  - **失敗と取消の後始末は名前で unload する。** 組の `BackendSceneLoaded` は、`PerformUnitySceneLoad` が戻るまで既定の false である（`SceneDirector.Loading.cs` 524〜526 行）。それより前に後始末が走る経路（`AddScene` の取消の窓、PreLoad の失敗・取消、`PerformUnitySceneLoad` の例外）では、`CleanupCanceledScene`（`SceneDirector.Unloading.cs` 393 行）と `SceneDirector.FailedLoadCleanup.cs` が `SceneManager` の同名の Scene を unload する。その後 `ReleaseScene` を無条件に呼ぶ（397 行）。
  - **snapshot の Play で走査が拾うのは複製だけである。** 取り込みの後に load 済みなのは、空の開始 Scene、bootstrap が directory から読んだ `UIScene`（SceneResourceMap に無い）、複製である。走査が `AddScene` するのは map にある複製だけになる。
  - **取り込みの段は directory mode の時だけ走る。** key が真でも directory mode でなければ段を飛ばす（入口は directory の値と key を同じ ledger で置くので、今の入口からは到達しない）。検証済みでない directory（loader を配線しない）でも段は走る。
  - **入口の宣言。** 経路 Built + open scenes は、`PlayEntryTransaction`（127 行）が `PlaySnapshotSession.Prepare()` を呼び、開いている全ての load 済みの Scene を複製する。1 回の Play の環境から `content:firstScene` を外し、`editor:playSnapshotExpected=true` を置く（`PurposeLaunchSettings.SnapshotPlay`）。新規未保存・UICommon・同名 2 つの Scene は入口が拒む（`PlayEntryDecision.OpenSceneProblems`）。SceneResourceMap の Scene 名は `VerifyPlayLauncher.FindReusableLoadedScenes` が asset から読んでいる。宣言の 1 件（Runtime の `EditorPlaySnapshotScene`）は identity（= Scene 名 = 複製の file 名）・複製の path・active の印だけを持つ。Editor 側の `PlaySnapshotScene` は元の path・GUID・Dirty・SHA-256 も持つ。
  - **測定済み（Phase A の疎通、2026-11-20）:** 同じ Play の中で、unload した複製の path を `EditorSceneManager.LoadSceneAsyncInPlayMode` でもう一度読める（lifecycle の外に居る空の Scene の複製で 2 回。§5 の未知の操作経路 (i) の記録。S0-d の比較表の「同じ複製の file から読み直せる」には実測の記録が無かった）。
- UC ごとの利用者の入力:

  **UC-1 開いている Scene を編集して Play で確認する**（利用者 (a)）: 新しい入力なし。選ぶのは今までどおり RunPurpose と経路（`EditorUserSettings` に残る）だけである。どの Scene が複製か、起動時にどれを要求するか、供給元の順、再要求の読み先、record は機械が決める。

  **UC-2 入口が snapshot を宣言する**（利用者 (b)）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | 複製ごとの「起動時に要求する」の印 | host の入口の code が開発時に規則を書き、Play のたびに機械がその規則を当てる（SampleGame の規則は「SceneResourceMap にある Scene は全て要求する」。§2.8 U3） | 目的の「lifecycle に入るのは明示的に要求された Scene だけ」。何をいつ要求するかは host が決める（program §4.4）。Framework が「load 済みなら要求」と決めると、物理的な load と lifecycle が分かれない |

  **UC-3 loader / SceneDirector で読む**（利用者 (c)）: 新しい入力なし。

  利用者 (d) は開発者としての操作を持たない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **検証済みの directory に snapshot を載せた Editor の Play で、複製を要求時解決の供給元にして既ロードの複製を loader の経路で採った時、(1) 同じ identity の Scene を 1 つだけ実行し、(2) 開いているが要求していない Scene を lifecycle に入れず、(3) 得た Scene を directory の Scene と同じ契約で所有・解放でき、(4) 起動の後の再要求を複製から読み直せて、record がどちらから読んだかを指す、と言えるか。**
- 誤って YES を出す道（各 AC はこれを塞ぐ形にする）:
  - (a) fake の供給元と fake の Scene だけで確かめ、Unity が同じ Play の中で同じ複製を読み直せることと、同名の Scene が 2 つにならないことを見ない。→ 実機（AC5・AC6）。
  - (b) 「未要求」を map に無い Scene で代用する（map に無い Scene は今も登録されない）。→ map にあり、印の無い Scene で見る（AC5）。
  - (c) record が `EditorSnapshot` と言うことだけを見て、実際に動いている Scene の path が複製かを見ない。→ path と、編集の印（directory の版には無い GameObject）を見る（AC5・AC6）。
  - (d) 再要求が成功したことだけを見る（directory の版が載っても `AddScene` は成功する）。→ 再要求の後の path・個数・record を見る（AC5・AC6）。
  - (e) `SceneDirector` の経路だけを見て、loader の直接の経路と既存の解放の API を見ない。→ AC5。
  - (f) SampleGame の入口は map の Scene の全てに印を付けるので、実 Editor の Play だけでは未要求の場合が現れない。→ 合成 fixture（AC5）と、選択の純粋な関数（AC4）。
  - (g) 複製が読めない時に directory の版で続き、Play が成功して見える。→ 失敗経路（AC2・AC5・AC6）。
  - (h) lifecycle に入っていないことを `SceneBase` の有無だけで見て、Scene の台帳を見ない。→ AC5 は台帳も見る。

### 2.1 進める最低条件

以下、「snapshot の Play」は、検証済みの directory を登録し、`editor:playSnapshotExpected` が真で、宣言（bridge）がある Editor の Play を指す。「宣言にある identity」は、その Play の宣言の Scene の identity を指す。

**不変条件（起きてはいけないこと）**

- **I-1 宣言にある identity の Scene は、複製 1 つだけが動く。directory の版に替わらない。** snapshot の Play の間、宣言にある identity の Scene の要求（`SceneDirector` と loader）が成功した時、その identity で load 済みの Scene は複製の path の 1 つだけである。複製を採れない時（file が無い、load が失敗した、同名の別の path の Scene が load 済み、台帳に別の由来の同じ identity がある）は要求が失敗し、directory の版へ進まない。
  - 主張を崩しうる経路: (1) 供給元が `Unavailable` か `NotFound` を返し、resolver が次の供給元（directory）へ進む。(2) 複製の候補の表現が要求と違い、`RepresentationMismatch` で次へ進む。(3) expected identity を載せた要求で、identity の空な複製が pin と合わず、pin に合う directory が採られる（13 章の既知）。(4) `PerformUnitySceneLoad` の名前の再利用と loader の両方が同じ名前を扱う。(5) unload の後の再要求（今は directory から読む）。(6) resolver を通さない `IAssetManagement.LoadContentSceneAsync` の直接の呼び出し。(7) loader の失敗の後に、SceneDirector が content 入口を直接呼ぶ（今は呼ばない）。(8) 取り込みより前に bootstrap が directory から読んだ同名の Scene（UICommon）。(9) `content:firstScene` が宣言と同名。(10) 同じ identity の並行の 2 つの要求。(11) 複製の file が Play の間に消える。
  - 塞ぎ方: snapshot の供給元（`EditorSnapshotProvider`）は policy の先頭に置き、宣言にある identity の Scene の要求には常に `Available` を返す（`Unavailable` も `NotFound` も返さない。宣言は 1 回の Play の間変わらない）。候補の表現は要求の表現をそのまま申告し、表現の指定が無い（空の）要求には `Full` を申告する（directory の供給元と同じ値。配線が constructor で渡す。§2.4「複製の候補の表現」、§2.8 U5）。expected identity を載せた要求には、identity の無い `Rejected` を返して探索を止める（resolver の既存の規則。理由に「この Play はこの Scene を snapshot から読む」と書く。§2.8 U9）。`SceneDirector` は、**宣言にある identity に限り**、名前の再利用より先に loader に尋ねる（loader が internal の問い合わせで「既ロードの口を持つ供給元がこの identity を宣言しているか」を答える。分岐は payload の無い論理 node の判定（`SceneDirector.Loading.cs` 645〜648 行）の後、`GetSceneByName` の再利用より前に置く。宣言に無い identity の順は今のまま）。loader は候補の経路が `AlreadyLoadedScene` の時、供給元の内部の口に load を任せ、供給元は Scene の台帳の gate（`LoadContentSceneAsync` と同じ `_contentSceneLoadGate`）の中で「load 済みの複製を採る、無ければ複製の path を読み直す」を行い、load を終えた時点の Scene を照合する（I-4）。採れなければ例外で、loader も SceneDirector も別の経路で読み直さない。(6) は、配線が宣言の identity を `AssetManagement` に予約し（預け元を 1 つ据える internal の口）、予約した identity の `LoadContentSceneAsync` を、複製が台帳にあるかどうかに関係なく `EntryAmbiguous` で拒む（§2.4「直接の content 入口」、§2.8 U10）。予約の時点で台帳に同じ identity の Scene があれば起動の失敗にする。(8) は入口の拒否と取り込みの照合の失敗（既存）のまま。(9) は入口が `content:firstScene` を外し、JSON の層にあれば拒む（既存）。
  - 探索した境界: `ContentResolver`（探索を止める 5 つの条件、`CheckRequestInvariants`、`CheckAcceptanceBasis`）、`ContentAvailability` の constructor（identity の無い `Rejected` を作れる。63〜68 行）、`ContentCompatibilityRule` の snapshot の分岐、`ContentRequestLoader`、`SceneDirector.Loading.cs` の `PerformUnitySceneLoad` と `LoadThroughContentLoaderAsync`、`AssetManagement.LoadContentSceneAsync` / `LoadSceneAsync`（封じた後は台帳より前で拒む。snapshot の Play は常に封じる）、`AbstractApplicationInitializer` の `AfterSceneLoad` の全ての stage、`PlayEntryDecision` の経路 Built + open scenes の行。A2 の反証は、resolver の経路（`RepresentationMismatch`・規則の不採用・fault・取消）で directory へ進む道を見つけなかった。**既知の制約（塞がない）:** game の code が `SceneManager` を直接呼んで載せた同名の Scene は扱わない。同じ identity を `SceneDirector` と loader の直接の呼び手が同時に持つ使い方は、directory の Scene と同じく保証しない（台帳は identity ごとに 1 件で、参照を数えない。片方の unload がもう片方の Scene を離す）。未確認: SampleGame の Scene の `Awake` が `SceneManager` を直接呼ぶ箇所の有無（`grep` していない）。
- **I-2 要求していない Scene は lifecycle に入らない。** snapshot の Play で、宣言にあって起動時の要求の印が無い Scene は、host の code が後から明示的に要求するまで、`SceneDirector` の組（`SceneBase`）を持たず、Scene の台帳に現れず、Framework はその Scene の `AssetOwner.Scene` の所有を作らない。物理的に load されていてよく、Unity の `Awake` / `Start` は止めない。**host の規約（Framework は強制しない）:** host の code は、Scene が明示的に要求されて OSM の lifecycle に入る前に、その Scene の `AssetOwner.Scene` で asset を取ってはならない。規約を守る host では、未要求の Scene が所有する asset は無い。要求した Scene の親や子として SceneResourceMap が指す Scene は、明示の要求の帰結であって未要求ではない。
  - 経路: (1) `RegisterAlreadyLoadedScenes` の走査が、load 済みの map の Scene を全て要求する。(2) 取り込みの段が複製を台帳に入れる。(3) 入口が全ての Scene に印を付ける（SampleGame の規則。host の選択であって Framework の既定ではない）。(4) Domain Reload の後の bridge の置き直しで印が落ちる、または全て真になる。(5) 印のある Scene が SceneResourceMap に無い。(6) 印が 0 件の宣言。(7) 未要求の複製の `Awake` / `Start` が、自分の `AssetOwner.Scene` で asset を取る（`AssetRegistry.Acquire` / `TrackOwner` は、owner の Scene に `SceneDirector` の組や台帳の entry があるかを検査しない。取った asset は、後のその Scene の要求の取消・失敗の後始末が呼ぶ `ReleaseScene` で、Scene が load 済みのまま解放されうる）。
  - 塞ぎ方: 宣言の 1 件に「起動時に要求する」の印を持たせ、Editor の session が `SessionState` に保存して置き直す。snapshot の Play では stage `register-loaded-scenes` を走らせず、代わりの stage `request-snapshot-scenes` が、印のある Scene だけを宣言の並び順に `SceneDirector.AddScene` で要求する。取り込みの段は今までどおり複製を物理的に load するだけで、台帳に触らない。印のある Scene が SceneResourceMap に無ければ起動の失敗にする（明示の要求を黙って飛ばさない）。この検査（起動の計画）は取り込みより前に行い、複製を load する前に失敗させる。印が 0 件なら何も要求せずに起動を続ける。選択は純粋な関数にする。(7) は上の host の規約で扱い、Framework は検査も禁止の強制も足さない（物理的な load と lifecycle を分けたことの帰結として、13 章に規約を書く）。`request-snapshot-scenes` の中の `AddScene` が失敗した時は、load 済みの複製を、取り込みの段の失敗と同じ規則（`SelectUnloadTargets`）で全て unload してから例外を出す（起動の失敗の後始末は Unity の Scene を unload しないので、段のほかに回収する者がいない）。
  - 探索した境界: `AfterSceneLoad` の stage の列、`RegisterAlreadyLoadedScenes`、`LoadEditorFirstSceneAsync`、`SceneDirector.AddScene` の親の解決（`SceneDirector.Loading.cs` 227〜230 行）、`PlaySnapshotSession` の `Document` と `InstallBridge`。未確認: SampleGame の Scene で、子を自分の `Awake` から `AddScene` する物の一覧（明示の要求なので I-2 の対象外だが、実 Editor の観測で数が変わりうる）。
- **I-3 複製から読んだ Scene の所有と解放は、directory の Scene と同じ契約である。** loader が複製から読んだ Scene は Scene の台帳に 1 件で入り、`UnloadSceneAsync` → `ReleaseScene`、`ReleaseAll`、load の失敗・取消の後始末で、directory の Scene と同じ API で離せる。離した後、台帳・`SceneManager`・`SceneDirector` の組にその Scene は残らない。SceneDirector が持っていない Scene（未要求の複製、他の identity の Scene）を、後始末が名前で unload しない。Play の停止の後、置き場と `playModeStartScene` は今までどおり戻る。
  - 経路: (1) `UnloadSceneAsync` が複製の Scene を Addressables の backend に渡す。(2) `ReleaseAll`（Play 停止）が native の unload を呼ぶ。(3) `SceneDirector` の `BackendSceneLoaded: false` の分岐（名前の unload）が複製に残る。(4) `PerformUnitySceneLoad` に入る前（`AddScene` の取消の窓、PreLoad の失敗・取消、要求した Scene の子として自動で load される未要求の複製の PreLoad）や loader の失敗で後始末が走り、組の `BackendSceneLoaded` が既定の false なので、同名の未要求の複製を名前で unload する（A2 の反証の blocker。loader に尋ねる直前に印を立てても、それより前の後始末には効かない）。(5) 読み直しの途中で取消され、遅れて完了した Scene が台帳に入らずに残る。(6) `SceneManager.UnloadSceneAsync` が null を返す、または Scene が既に無効になっている。(7) 預かる前から load 済みだった複製（未要求だった物）を、要求 → unload した時の扱い。(8) 預かる口と directory の load が別の gate で台帳を見て、`AssetRegistry.AddScene` の上書きで片方が台帳から漏れる。(9) directory の close の最中・後に預かる。(10) `SceneDirector` の unload の Phase 2 と Phase 3 の間（`ExecuteAfterUnLoad` の `await`）に同じ identity を再び預かり、Phase 3 の `ReleaseScene` が新しい entry を消す。(11) 起動の stage の失敗の後に、複製と預かった Scene が load 済みのまま残る。
  - 塞ぎ方: `AssetManagement` に、外で読んだ Scene を台帳に預かる internal の口（Editor の build だけ。§2.4「所有の置き場」）を足し、`UnloadSceneAsync` は預かった Scene を、預けた側の unload（`SceneManager.UnloadSceneAsync(scene)`）に渡す（今の else の分岐、つまり封じた Addressables の backend に落ちる前に置く）。`ReleaseAll` は今までどおり台帳の印だけを倒す（Unity が解体する）。(4): `SceneDirector` は、宣言にある identity の組を、作る時点（`LoadSceneBaseCore`）で「台帳経由で unload する」と印す。後始末は `UnloadSceneAsync(identity)` だけを呼び、台帳に無ければ何もしないので、未要求の複製は load 済みのまま残る。予約（I-1）により、宣言にある identity の台帳の entry は預かった Scene だけなので、この印が他の由来の Scene を unload することは無い。(5): 読み直しは取消できない（Unity の load は止まらない）ので、完了を待ってから unload して取消を返す。(6): 預け元の unload は、既に無効または load 済みでない Scene を unload 済みとして扱う。load 済みの Scene に対して null か失敗なら例外にし、台帳は未 unload のまま残す（再試行できる）。(7) は読み直した複製と同じ扱い（unload する。次の要求は読み直す）。(8): 預かる口は `LoadContentSceneAsync` と同じ `_contentSceneLoadGate` の中で、台帳の検査・取得・登録を行う。(9): 預かる口は directory が受付中の時だけ預かる。close は token の Scene だけを unload し、預かった Scene は close の後も残る（directory の物ではない。離すのは `UnloadSceneAsync` か `ReleaseAll`）。(10) は directory の Scene と同じ窓で、本書は変えない（同じ契約として 13 章に書く）。(11) は I-2 の塞ぎ方（計画の検査を取り込みの前に、stage の失敗で複製を unload）。
  - 探索した境界: `AssetManagement.UnloadSceneAsync` / `ReleaseScene` / `ReleaseAll` / `CloseContentDirectoryAsync`（`AssetManagement.ContentDirectory.cs` 43〜51 行）、`AssetRegistry` の Scene の member、`SceneDirector.Loading.cs` の `AddScene` の取消の窓（264〜286 行）と `LoadSceneBaseCore`（418〜431 行）、`SceneDirector.Unloading.cs` の `PerformUnitySceneUnload` の呼び出しと Phase 2 / Phase 3（268〜291 行）、`SceneDirector.FailedLoadCleanup.cs`、`AbstractApplicationInitializer.ReleaseAll` と起動の失敗の後始末、`PlaySnapshotSession.Release`。未確認: Play の停止で Unity が複製の Scene を解体する順と `ReleaseAll` の前後（今も同じ順で、S0-d の実 Editor の Play で Error は出ていない）。SampleGame の Scene の `Awake` / `Start` が、lifecycle に入る前に `AssetOwner.Scene` で asset を取る箇所の有無（I-2 の host の規約に反する使い方。`grep` していない）。
- **I-4 record は、実際に動く Scene の由来を言う。** loader が成功として返す record が `EditorSnapshot` を言う時、その handle の Scene は、名前が identity と等しく、path が宣言の複製の path で、同名の load 済みの Scene は他に無い。この照合は、候補を採った時点ではなく、load（または採用）を終えた時点の Scene で行い、`SourceBindingVerified` はその時だけ true にする。directory を言う record は今までどおりである。
  - 経路: (1) 名前だけで照合する。(2) 起動の取り込みの時点の照合を、後の要求に使い回す。(3) 供給元の候補と、実際に load する口が別の宣言を見る。(4) Editor の `SourceChanged` が、複製以外の path を一律に directory と呼ぶ（S0-d からの移送）。
  - 塞ぎ方: 1 Scene の照合を純粋な関数にし、load を終えた直後に、台帳へ入れる前に呼ぶ。起動の取り込みの照合（`EditorPlaySnapshotIntake.Verify`）も同じ関数を使い、規則を 1 か所に置く。供給元と load の口は同じ宣言の instance から作る。`SourceChanged` は「複製以外の path で load された」とだけ言い、SampleGame の表示は由来を `snapshot->other` と記録する（どの供給元かは Runtime の record が言う）。
  - 探索した境界: `EditorPlaySnapshotIntake.Verify`、`IContentSourceBinding`、`ContentLoadRecord`、`PlaySnapshotSession.OnSceneLoaded`、`PlayEntryRecord`。既知の制約: 複製の file の中身は Play の間に再検証しない（record は複製の path と、Play の前の SHA-256・Dirty を事実として持つ）。
- **I-5 snapshot を宣言しない Play と、既存の契約を変えない。** Player、経路 Source（Addressables）、snapshot の無い directory の Play（経路 Built、検証 Play、Delivery、`directoryPath` の明示）の動きと log は変わらない。`IAssetManagement` の member、`Runtime/ContentResolution/Core/` の型・member・値、`AssetManagement` の public の面、封じ、protected config の照合、元の Scene の file と Dirty（program I11）は変わらない。`Runtime/AssetManagement/` は `ContentResolution` を参照しない。
  - 経路: (1) `SceneDirector` の分岐の順を変えると、snapshot の無い配線済みの Play で、Unity が載せた同名の Scene を再利用しなくなる。(2) `UnloadSceneAsync` の分岐の追加で Addressables の Scene の unload が変わる。(3) route の引数の追加で既存の呼び手が変わる。(4) stage の差し替えが snapshot の無い Play に及ぶ。(5) Player の build に Editor の code が入る。
  - 塞ぎ方: 新しい動きは「loader の policy に既ロードの口を持つ供給元があり、その供給元が宣言している identity」だけに限り、その供給元を作るのは snapshot の Play の配線だけにする。新しい型と分岐は file ごと、または行ごとに `#if UNITY_EDITOR` に置く。既存の test は変更なしで通る（AC7）。

**成立条件（起きなければならないこと）**

- **S-1** 実 Editor の Play（SampleGame、経路 Built + open scenes）で、開いている map の Scene が複製から起動し、その record が `EditorSnapshot` を言う。Domain Reload の無効と有効の両方で成り立つ。
- **S-2** 起動の後に unload して再要求した Scene が、複製の path から 1 つだけ載り、record が `EditorSnapshot` を言い、Editor の record に由来の変化が出ない。
- **S-3** 同じ Play で、開いていない Scene（親や後続の Scene）は directory から載り、record が directory を言う（混成）。
- **S-4** loader の直接の経路（`SceneDirector` を通さない）でも、複製の Scene を読んで record を得て、`IAssetManagement` の既存の API で離せる。

### 2.2 受け入れ条件

AC1〜AC5 の fixture は SampleGame を使わない合成（program D10）。AC6 だけ SampleGame の実 Editor の Play。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | I-1・I-4 | 単体（供給元と、実物の `ContentResolver`・`ContentCompatibilityRule`、fake の directory の供給元）。snapshot の供給元は、宣言にある identity の Scene の要求に `Available`（由来 `EditorSnapshot`、根拠 `OpenSceneSnapshot`、経路 `AlreadyLoadedScene`、表現は要求の表現、identity は空、事実に元の path・複製の path・Dirty・SHA-256）を返し、policy の先頭に置くと resolver がそれを採って directory の供給元を probe しない。表現の指定が無い要求には表現 `Full` の候補を返し、採られる（例外も fault も出ない）。宣言に無い identity と、kind が Object の要求には `NotFound` を返し、directory の候補が採られる。**失敗経路:** expected identity を載せた、宣言にある identity の要求は、identity の無い `Rejected` で探索が止まり、pin に合う directory の候補があっても採られない。宣言にある identity には、どの状態でも `Unavailable` と `NotFound` を返さない |
| AC2 | I-1・I-3・I-4 | 単体（loader、実物の `AssetManagement`、Scene の実体を差し替えられる fake の預け元）。loader は経路 `AlreadyLoadedScene` の候補を、供給元の内部の口で読み、record（`SourceBindingVerified` true）と handle を返す。load 済みの複製は読み直さずに採り（預け元の load は 0 回）、load 済みでなければ複製の path を 1 回読む。1 Scene の照合の純粋な関数は、名前の違い、path の違い、同名が 2 つ、同名が別の path で load 済み、を不一致と答え、起動の取り込みの照合（`EditorPlaySnapshotIntake.Verify`）の既存の test が同じ関数を通って変更なしで通る。**失敗経路:** 照合の不一致、load が null、load しても Scene が無効、のそれぞれで `ContentResolutionException` になり、例外の文が Scene の identity と採れない理由（file が無い、同名が別の path で load 済み、など）を言い、預け元が読んだ Scene は unload され、台帳に入らず、directory の content 入口は呼ばれない。内部の口を持たない供給元の `AlreadyLoadedScene` の候補は今までどおり `LoadPathUnsupported` |
| AC3 | I-3・I-1 | 単体（実物の `AssetManagement`（directory を install して封じた状態）、fake の預け元、fake の directory の port）。預かった Scene は `UnloadSceneAsync` で預け元の unload が 1 回呼ばれ、`ReleaseScene` の後に台帳に無い。`ReleaseAll` は預け元の unload を呼ばずに台帳を空にする。同じ identity の並行の 2 つの預かりは、預け元の取得 1 回で同じ Scene を返す。`CloseContentDirectoryAsync` は預かった Scene を unload せず、close の後も台帳に残り、`UnloadSceneAsync` で離せる。**失敗経路:** 予約した identity への `LoadContentSceneAsync` は、複製が台帳にある時も無い時も `EntryAmbiguous`。予約していない identity と、directory の token の Scene が台帳にある identity は預からずに拒む。進行中の directory の load（fake の port で止める）と並行した別の identity の預かりは gate で直列になり、どちらの entry も台帳に残る。close の最中と後は預からない。取消済みの token では預け元を呼ばない。取得の途中の取消は、完了を待って unload してから取消を返し、台帳に入らない。既に無効な Scene の unload は unload 済みとして台帳の印が倒れる。load 済みの Scene の unload の失敗は例外になり、台帳は未 unload のままで、もう一度 `UnloadSceneAsync` を呼べる。予約の時点で台帳に同じ identity があれば予約が失敗する |
| AC4 | I-2・I-5 | 単体（純粋な関数）。起動の計画は、(a) key が無いか偽 → 今までの既ロード登録、(b) key が真・directory mode・loader を配線済み・宣言あり → 取り込みの後、印のある Scene だけを宣言の並び順に要求（印が 0 件なら要求 0 件）、を返す。**失敗経路:** key が真で、directory mode でない、loader を配線していない（検証済みでない directory）、宣言が無い、のそれぞれは起動の失敗（理由を言う）。印のある Scene が SceneResourceMap に無ければ起動の失敗で、どちらの失敗も取り込み（複製の load）より前に決まる。Editor の session は印を `SessionState` に保存し、置き直した bridge の印が保存した値と等しい（Editor の単体）。`SceneDirector` の単体（`SceneDirectorContentLoaderTests` に追記）: 宣言にある identity の組は、PreLoad の中の取消と PreLoad の失敗の後始末で `UnloadSceneAsync(identity)` だけが呼ばれ（台帳に無ければ何も起きない）、宣言に無い identity と payload の無い論理 node の分岐は今と同じ |
| AC5 | I-1〜I-4、S-2〜S-4 | 実機（Content Directory の build を伴う EditMode の `[UnityTest]`。Play mode に入って読む。`RegisteredDirectoryRouteIntegrationTests` と同じ手順で base を build・publish・install・検証する）。合成の map の Scene `SnapA`・`SnapB`・`SnapD`・`DirC` を directory に入れた後、`SnapA`・`SnapB`・`SnapD` の source に編集の印（directory の版に無い GameObject）を足して additive に開き、public の `PlaySnapshotSession.Prepare`（印の規則は `SnapA` だけ真）で複製と宣言を作って Play に入る。Play の中で、宣言の複製を取り込みと同じ口（bridge の delegate）で全て load し、供給元を先頭に足した route と loader を渡した `SceneDirector`（fixture の map と factory）で次を見る。(1) `AddScene(SnapA)`: `SnapA` は 1 つ、path は複製、編集の印がある、record は `EditorSnapshot`。(2) この時点の `SnapB` と `SnapD`: 物理的に load 済みで、`SceneDirector` の組が無く、Scene の台帳に無く、`AssetOwner.Scene` の所有する asset が無い（fixture の Scene は I-2 の host の規約どおり、`Awake` / `Start` で `AssetOwner.Scene` の asset を取らない。見ているのは「Framework が所有を作らない」ことで、規約に反する host の副作用が無いことではない）。(3) `AddScene(DirC)`: record は directory（identity は install した manifest の値）。(4) `SnapA` を unload: `SceneManager` に `SnapA` が無い。もう一度 `AddScene(SnapA)`: 1 つ、path は複製、編集の印がある、record は `EditorSnapshot`。(5) `AddScene(SnapB)`: 読み直さずに採られ（root の GameObject の instance が (2) と同じ）、record は `EditorSnapshot`。(6) loader の直接の経路: `SnapB` を unload した後、loader の `LoadSceneAsync` で読み、`UnloadSceneAsync` → `ReleaseScene` で離す。**失敗経路:** (7) `AddScene(SnapD, ct)` を PreLoad の中（fixture の `SceneBase` が待つ）で取り消す → `SnapD` は load 済みのまま（root の instance が (2) と同じ）で、組も台帳の entry も無い。(8) `SnapA` を unload し、複製の file を消してから `AddScene(SnapA)` → 失敗し、`SnapA` という名前の Scene は load 済みに無く、未要求の `SnapD` は load 済みのまま。(9) 未要求の `SnapD` に対する `LoadContentSceneAsync("SnapD", …)` は `EntryAmbiguous` で、`SnapD` は 1 つのまま。最後に全てを離して `CloseContentDirectoryAsync` が完了し、Play を出た後に置き場が無く `playModeStartScene` が null。観測窓（Play に入ってから Edit Mode に戻るまで）の Error / Exception は期待した失敗の分だけ。同じ fixture と同じ Play で、下の観測 O-1 の 1 行を出す（合否に使わない） |
| AC6 | S-1〜S-3、I-1・I-2・I-5 | 実 Editor の Play（SampleGame、経路 Built + open scenes。手順は §5 の検証経路）。Play A（Domain Reload 無効、`Title` を開く）: 起動の log に配線・snapshot の供給元・取り込み・起動時の要求の行が出て、`Title` は 1 つで path が置き場の下、record は `EditorSnapshot`。`Title` を unload して再要求すると、1 つ・path は置き場の下・record は `EditorSnapshot` で、入口の record の由来は `snapshot` のまま。開いていない map の Scene を要求すると record は directory。停止の後、置き場が無く、`playModeStartScene` は null、元の `Title` の SHA-256・Dirty・`git status --short` が Play の前と同じ。Play B（`Title` 以外の、map にあって payload と親を持つ Scene を 1 つだけ開く）: その Scene の record は `EditorSnapshot`、親の record は directory で、どちらも 1 つ。Play C（Domain Reload 有効）: Play A の起動と再要求。**失敗経路:** Play D: Play の中で `Title` を unload し、複製の file を消してから再要求 → 失敗し、`Title` という名前の Scene は load 済みに無い。Play A〜C のそれぞれの観測窓（Play の menu の実行から、停止の後に `isPlaying` と `isPlayingOrWillChangePlaymode` が偽になり置き場が消えるまで）の Error / Exception は 0 件 |
| AC7 | I-5 | 既存の test と検査。`IAssetManagement.cs`、`IContentSourceBinding.cs`、`RegisteredDirectoryProvider.cs`、`Internal/AssetRegistry.cs`、`BuildContent/` の全て、`PlayerContentConfiguration`、`PlayEntryDecision.cs`、`PlayEntryTransaction.cs` は変わらない（diff）。`Runtime/ContentResolution/Core/` の変更は §3 の 3 file の doc comment だけで、型・member・値・code の行は変わらない。`AssetManagement` の public の member は増減しない。次の参照が無い（`grep` と C の構造レビュー）: `Runtime/AssetManagement/` → `ContentResolution`、`Runtime/ContentResolution/Core/` と `ContentRequestLoader.cs` → `BuildContent`、`Runtime/ContentResolution/EditorSnapshot/` → `Bootstrap`・`BuildContent`。新しい file と分岐は `#if UNITY_EDITOR` の中にある。既存の `ContentRequestLoaderTests`・`SceneDirectorContentLoaderTests`・`SceneVariantForwardingTests`・`RegisteredDirectoryProviderTests`・`RegisteredDirectoryRouteIntegrationTests`・`OverlayRegistrationIntegrationTests`・`EditorPlaySnapshotIntakeTests`・`SceneSnapshotWriterTests`・`SnapshotRootTests`・`PlayEntryDecisionTests`・`PlayEntryTransactionTests`・offline の test は変更なしで通る。例外は `PlayEntryRecordTests` の由来の label（`snapshot->directory` → `snapshot->other`）だけ |
| AC8 | 範囲 | Phase B の commit が変える file は、§3 の file とその `.meta`、§3 の test の folder の file、`docs/handoff/` の文書だけで、asmdef の `references`、`AssemblyInfo.cs` の friend、`SceneState`、`tools/` に変更が無い |

**観測（合否に使わない。S3-4 への入力）**

- **O-1 複製が参照する asset の版。** AC5 の fixture で、`SnapA`（複製から載る）と `DirC`（directory から載る）の両方が直接参照する material を 1 つ置き、base の build の後に、Scene の編集と一緒にその material の色を手元で変える。Play の中で、`SnapA` の renderer の `sharedMaterial` の色（手元の値か、build の時の値か）、`DirC` の同じ参照の色、2 つの `sharedMaterial` が同じ instance かを読み、assert せずに test の log に 1 行（`[S3-3 O-1] snap=<色> directory=<色> sameInstance=<true|false>`）で出す。判定 C がその行を evidence に収録し、Phase D で program §8 の S3-4 の行へ渡す。値がどちらでも本書の合否は変わらない。観測が取れない時（参照が解決しない等）は、理由を §7 に書いて S3-4 へ送る。目的は、snapshot が Scene の file の編集だけに効くのか、参照先の編集にも効くのかを、S3-4 の A1 より前に知ることである（program §5.3「編集の種類ごとの扱い」の F の前提。S0-b から未測定）。追加の実装・修正は行わない。
- **O-2 実 Editor の Play での同じ観測。** B の動作確認と判定 C の AC6 の観測の中で、SampleGame の `Title` について同じことを 1 回見る（手順は §5 の検証経路の「観測 O-2」）。O-1 は合成 fixture で「保存した編集」を、O-2 は実 Editor の実データで「保存していない in-memory の編集」を見る（working tree を変えないため）。合否に使わず、値と、観測できなかった時の理由を §6・§7 に書いて S3-4 へ渡す。

### 2.3 ここでは答えない問いと所有する後続

program と先行スライスが「S3-3」へ送った入力の引き取りと送り先を、ここに全て書く。

| 問い（出所） | 所有 |
|---|---|
| 開いているが要求していない Scene を lifecycle に入れないこと、要求時の `AddScene` と `RegisterAlreadyLoadedScenes` の責務の分離（S0-b、program §8 の gate） | **本書**（I-2、AC4・AC5）。分離の形: snapshot の Play では走査を走らせず、要求は宣言の印から出す。既ロードの複製は供給元の候補として loader が採る |
| 起動の後に再要求した Scene を snapshot から読み直すこと（S0-d） | **本書**（I-1、AC5・AC6） |
| 取り込みの段の key と mode の組（S0-d の T-C1） | **本書**（AC4。key が真で directory mode でない・配線なしは起動の失敗） |
| map にある Scene を開いた混成の Play（S0-d の T-C2） | bootstrap が取り込みより前に directory から読む Scene（UICommon）は、今までどおり入口の拒否と取り込みの照合の失敗のまま（bootstrap を resolver に通すかは S5 の A1）。それ以外の map の Scene は **本書**（AC5・AC6 の Play B） |
| 由来の label の一般化（S0-d の独立レビュー） | **本書**（I-4。Editor の event は「複製以外」とだけ言い、由来の正本は Runtime の record） |
| `Title` 以外の Scene での実証（S0-d） | **本書**（AC6 の Play B の 1 Scene。全ての Scene が封じの下で動くことは主張しない） |
| `SceneDirector` の取消が Unity の Scene の load に届かないこと（S0-b、S3-1 の U8） | 変えない。遷移の commit の後は取り消さない設計で、複製の読み直しも同じ（`CancellationToken.None`）。loader の直接の経路の取消だけ I-3 が扱う。届かせる要否は SceneSystem の側の判断で、本 program の後続に送らない（13 章「まだ保証しないこと」に残す） |
| S3 の gate「Player の非退行」の実機の要否（S3-1） | 不要。本書の新しい型と分岐は `#if UNITY_EDITOR` の中だけで、Player の compile の結果は変わらない（AC7）。Player の実機は S4 が持つ（program §8） |
| snapshot を優先する policy は要求に expected identity を載せない（S1） | **本書**（I-1 の (3)。`SceneDirector` は載せない。載せた要求は探索を止める） |
| host の述語を snapshot にも当てること（S1） | 要る host が現れた時の後続（program の割り当てのまま） |
| Object の snapshot、開いている Scene が参照する asset の版（program I11・G22、§5.3 の F） | S3-4。本書は合成 fixture と実 Editor の Play で観測を取って入力にするだけで（§2.2 の O-1・O-2）、扱いは決めない |
| 未要求の複製を要求まで load しないこと | S5 の A1 が要否を決める（Play の開始の所要の観測と一緒に） |
| snapshot の無い配線済みの directory の Play で、Unity が載せた同名の Scene を Runtime が拒むこと（S0-d の A-2） | §2.8 U6。推奨は本書で変えず、S5（入口の統合。G18） |
| 同じ project の Editor process 2 つの衝突、他の Scene の `Awake` が封じの下で呼ぶ物の網羅（S0-d） | S5 |
| 同じ identity を `SceneDirector` と loader の直接の呼び手が同時に持つこと、`SceneDirector` の unload の Phase 2 と Phase 3 の間の再要求（A2 の反証） | directory の Scene と同じ既存の制約で、本書は変えない（台帳は参照を数えない）。13 章「まだ保証しないこと」に書く。直すなら SceneSystem と AssetManagement の側の別の作業 |
| `key` が真で配線しない Play（検証済みでない directory）で今の動き（名前の再利用）を残すこと（A2 の代替構成の案） | 採らない（AC4。起動の失敗にする）。経路 Built + open scenes は検証済みの候補を必須にするので、今の入口からの退行にならない |

### 2.4 代替案の比較

**起動時の要求の出所**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| R1 | 今のまま。load 済みの map の Scene を走査して全て要求する | 変更なし。物理的な load と要求が分かれない（I-2 を満たせない） | 採らない |
| R2 | 宣言の 1 件に「起動時に要求する」の印を持たせ、Runtime は印のある Scene だけを要求する。印の規則は host の入口が決める | 宣言と Editor の session に欄が 1 つ増える。`PlaySnapshotSession.Prepare` に引数が増える | **採る** |
| R3 | Framework が「active の Scene だけ」と決める | host の入力は増えない。何を要求するかという host の policy が Framework に入り、S0-d で確かめた「開いている map の Scene は全て動く」が変わる | 採らない |

**未要求の複製の物理的な load**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| E1 | 今のまま、取り込みの段が宣言の全ての複製を起動時に load して照合する。未要求の複製は load 済みのまま lifecycle の外に居て、要求された時に採る | 取り込みと照合（S0-d で確かめた起動時の fail-closed）を変えない。「開いている物が Play に載る」が map に無い Scene も含めて今までどおり。採る・読み直すの 2 つの入り方がある。未要求の複製の `Awake` は走る（program G23 が許す） | **採る** |
| E2 | 要求された複製だけを、要求の時に load する | 入り方が 1 つになり、未要求の物は存在しない。map に無い Scene（要求できない）を載せるかを別に決める必要があり、複製の不備が起動時ではなく要求の時に分かる。S0-d の挙動が変わる | 採らない（要否は S5） |

**所有の置き場**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| O1 | `AssetManagement` の Scene の台帳が、複製から読んだ Scene を 3 つ目の種別として預かる（internal の口、Editor の build だけ）。load と unload の実体は預け元（snapshot の側）が持ち、台帳は identity・同時 load の gate・解放の入口を持つ | `AssetManagement` に internal の口と、`UnloadSceneAsync` の分岐 1 つが増える。`IAssetManagement` は変えない。解放が directory の Scene と同じ API になり、同じ identity の directory の load を台帳の既存の検査が拒む。S0-d の「snapshot は `IAssetManagement` を通らない別の口から読む」は保つ（読むのは預け元の delegate で、Addressables の取得経路も public の入口も通らない）が、台帳が複製を知る点は S0-d より踏み込む（§2.8 U4） | **採る** |
| O2 | 台帳の外に snapshot の Scene の所有者を置き、`SceneDirector` は今までどおり `SceneManager` で unload する | `AssetManagement` を変えない。loader の直接の経路の呼び手は、複製の Scene を別の API で離す必要があり（供給元を知らずに済む、という目的に反する）、`LoadContentSceneAsync` の直接の呼び出しを止める物が無い | 採らない |
| O3 | 今のまま名前で再利用し、record だけを後から付ける | 最小。既ロードの Scene は loader の経路にならず、再要求は directory から読む | 採らない |

**直接の content 入口**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| D1 | 配線が宣言の identity を `AssetManagement` に予約し、予約した identity の `LoadContentSceneAsync` を Play の間ずっと `EntryAmbiguous` で拒む（A2 の代替構成の案） | `AssetManagement.ContentDirectory.cs` に `#if UNITY_EDITOR` の検査が 1 つ増える。resolver を通さない呼び手も、宣言にある identity の directory の版を読めなくなる（I-1 が直接の経路でも成り立つ）。宣言にある identity の台帳の entry が預かった Scene だけになり、後始末の印（I-3 の (4)）が他の由来の Scene に届かない | **採る**（§2.8 U10） |
| D2 | 予約しない。台帳に入った複製だけを既存の `EntryAmbiguous` が守り、未要求の複製が load 済みの identity は直接の入口から directory の版を読める（初稿） | `AssetManagement.ContentDirectory.cs` を変えない。同じ identity の Scene が 2 つ並ぶ道が残り、その後の要求と後始末が他の所有者の Scene を unload しうる（A2 のゲートの M2） | 採らない |

**loader と SceneDirector の接続**

| 案 | 内容 | 推奨 |
|---|---|---|
| L1 | loader が internal の問い合わせで「既ロードの口（internal の interface）を持つ供給元が、この identity を宣言しているか」を答え、真の identity に限り `SceneDirector` は名前の再利用より先に loader に尋ね、組を作る時点で台帳経由の unload と印す | **採る**。snapshot の無い Play と、宣言に無い identity の分岐の順は変わらない |
| L2 | loader を配線した Play（または snapshot の Play）では、全ての identity で loader に先に尋ねる | 採らない。宣言に無い同名の Scene が load 済みだと directory の版が 2 つ目として載る（A2 のゲートの minor 2。§2.8 U6 の広い案と一緒でなければ成り立たない） |
| L3 | 組に「何も取得していない」の第 3 の状態を足し、後始末がその印で unload を飛ばす（A2 のゲートと代替構成の案） | 採らない。`ScenePair` の状態と後始末の分岐が増える。D1 の予約があれば、既存の bool を作る時点で立てるだけで同じ結果になる |

**複製の候補の表現**

| 案 | 内容 | 推奨 |
|---|---|---|
| P1 | 候補の表現は要求の表現をそのまま申告する（指定が無い要求には `Full`）。複製の受け入れ理由は「利用者が開いている Scene であること」で、表現を検証しない。record の事実に元の path を残す | **採る**。S0-d の挙動（開いている Scene が、その identity のどの表現の要求にも応える）と同じ。snapshot の record の「表現」は要求の写しで、中身の表現の保証ではない、と 13 章に書く |
| P2 | 入口が複製ごとに表現を宣言し、違う表現の要求には応えない | 採らない。host の入口の入力が増え、違う時に directory の版へ進むと、同じ identity の複製（load 済み）と directory の版が並ぶ |

**適用する Play**

| 案 | 内容 | 推奨 |
|---|---|---|
| S1 | 新しい動き（走査をやめる、loader に先に尋ねる）は snapshot の Play だけ | **採る**（I-5） |
| S2 | loader を配線した全ての directory の Play で、由来の無い既ロードの Scene の再利用をやめて拒む | 採らない（§2.8 U6）。SampleGame の入口は既にこの組を拒む（G18）ので利用者の体験は変わらないが、入口を持たない host と既存の test の挙動が変わる |

### 2.5 スライス分割の判断

- 契機: 独立して失敗しうる未知が 2 つある。(1) 複製を loader の経路で採り、台帳に預け、同じ Play の中で読み直すこと（Unity が同じ複製の path を読み直せることは、Phase A の疎通で空の Scene について確かめた。§5）。(2) 起動時の要求を宣言の印から出すこと（stage の差し替えと Editor の session）。
- 判断: **分けない。** (2) だけでは、複製は今までどおり名前で再利用され、record も再要求の読み直しも無いので、問いの (1)(3)(4) に答えない。(1) だけでは、走査が全ての複製を要求し続け、未要求の Scene の扱い（問いの (2)）を確かめる場面が作れない。2 つは同じ宣言と同じ stage の列を触る。単体で閉じる部分（AC1〜AC4）、実機（AC5）、実 Editor（AC6）は別々に判定する。(1) が実装の後の AC5・AC6 で不成立の時は §2.6 の NO-GO で owner に返す。
- owner の確認: 未了（§2.8 U2）。

### 2.6 判定定義と停止規則

- 判定定義:
  - **GO:** AC1〜AC8 が全て成り立ち、判定必須テスト（§5）が通る。
  - **NO-GO（owner へ返す）:** (i) 同じ Play の中で、unload した複製の path を Unity が読み直せない、または読み直した Scene の名前か path が宣言と違う。何がどの段で違ったかを記録して返す（複製を unload せずに無効化して持つ、要求のたびに複製を作り直す、などの代案は Phase A の新しい revision で決める）。(ii) I-1〜I-5 を、`IAssetManagement` の member の追加か `Runtime/ContentResolution/Core/` の型・member・値の変更なしには満たせない。
  - それ以外の不成立は通常の差し戻しで B へ戻す。
  - スパイクではないので CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の `references` と `InternalsVisibleTo` を足さない（足す必要が出たら Phase A へ戻す）。
- アセットは `IAssetManagement` 経由で、`AssetOwner` で寿命を宣言する。`IAssetManagement` に member を足さない。
- `SceneState` の 14 値を変えない。状態の変更は `SceneLifecycleManager` が所有する。
- 公開 API のログは `ILogger<T>`。`ZLogger*` 型を公開面へ出さない。
- 新規・編集する Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。Editor の code（`UnityEditor` を参照する code）を Runtime の assembly に置かない。Runtime の assembly に置く Editor の build だけの code は `#if UNITY_EDITOR` で囲み、`UnityEditor` の API は Editor の assembly から delegate で受ける（今の bridge と同じ形）。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。`Scene` は struct なので `IsValid()` と `isLoaded` で見る。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない（`RegisterAlreadyLoadedScenes`、`PlaySnapshotSession` の public の member を消さない）。
- repo 相対 path は `tools/path-budget.json` の予算（150 字）に収める。
- `Runtime/ContentResolution/Core/` は Unity と `BuildContent` を参照しない（`tools/ContentResolutionOfflineTests` が source を直接 compile する）。本書が `Core/` で変えるのは doc comment だけで、型・member・値・code の行を変えない。
- loader は AssetManagement の public だけを使い、同じ assembly の internal を呼ばない。internal を呼ぶのは連携部分（供給元）だけである。directory の経路の「照合と content 入口の呼び出しの間に `await` を置かない」を変えない。
- 封じ（`SealAddressables`）、protected config の照合、起動の fail-closed を変えない。複製を Addressables と `IAssetManagement` の public の入口で読まない。
- `SceneDirector` の 6 引数・7 引数の constructor の形を変えない（VFX の Play の test が引数の数で reflection する）。
- 元の Scene の file・Dirty・path を変えない（program I11）。複製を置き場の外に作らない。
- 機械向けの記録を git に入れない。

### 2.8 owner の判断を求める点（未決事項）

担当の推奨を付ける。U1 と U12 は決定済み。他は A3 の凍結の時に owner が確定する。

- **owner の意向（2026-11-19、会話）:** U2〜U11 は概ね推奨どおり。項目ごとの確定は凍結の時に記録する。
- **U1 目的と利用者の文言（§1）。** 決定: 結果を先に書く形に改める（owner、2026-11-19。§1 の経緯）。改めた文言で目的レビューをやり直した（§5.1）。最終の文言の確認は凍結の時。
- **U12 複製が参照する asset の版の観測（§2.2 の O-1・O-2）。** 決定: AC5 の fixture（O-1）と、動作確認・AC6 の実 Editor の Play（O-2）で観測を取り、合否に使わず S3-4 への入力にする（owner、2026-11-19）。
- **U2 分割しない（§2.5）。** 推奨: 分けない。
- **U3 起動時に要求する Scene の規則（§2.4 R2）。** 推奨: Framework は印のある Scene だけを要求し、SampleGame の入口は「開いている Scene のうち SceneResourceMap にある物は全て要求する」（今の動きと同じで、利用者の新しい入力が無い）。代案: SampleGame は active の Scene だけを要求する（additive に開いた他の map の Scene は、load 済みのまま lifecycle の外に居る）。
- **U4 複製の Scene を Scene の台帳に預ける（§2.4 O1）。** 推奨: O1。S0-d の A3 の「snapshot は `IAssetManagement` を通らない別の口から読む」は、読む口については保つ。台帳が複製の Scene を預かる点は S0-d の比較表（「Scene は `SceneDirector` が `SceneManager` で unload」）から変わる。
- **U5 複製の候補の表現（§2.4 P1）。** 推奨: P1（要求の表現をそのまま申告し、13 章に限界を書く）。
- **U6 snapshot の無い配線済みの Play の既ロードの Scene（§2.4 S1 / S2）。** 推奨: S1（本書では変えない）。Runtime が拒む形（S2）は S5 の入口の統合で決める。
- **U7 `PlaySnapshotSession.Prepare` の引数（§3）。** 推奨: 印の規則（Scene 名 → 真偽）を必須の引数にし、引数なしの形を残さない（残すと「全て要求」か「何も要求しない」を黙って選ぶ既定が Framework に生まれる）。Editor の assembly の public の形が変わる（呼び手は SampleGame の入口だけ。入口は `Action` の port を閉包で包むので `PlayEntryTransaction` は変わらない）。代案（A2 の代替構成）: 引数なしの形を残し、「active の Scene だけを要求する」と定める（目的レビューも、宣言が無い時の既定が書かれていないと指摘した）。
- **U8 Player の build と実クリックを判定必須にしない。** 推奨: しない。Player の compile の結果は変わらず（AC7）、Play ボタンの実クリックの経路は S0-d の AC15 で確かめた物を変えない（Play は menu の実行で始める。S3-1 と同じ）。
- **U9 `Rejected` の使い方（I-1 の (3)）。** 推奨: 宣言にある identity を pin した要求を、snapshot の供給元の identity の無い `Rejected` で止める。core は変えないが、`Rejected` の今の説明（供給元が申告と bytes の食い違いを検出した）より広い使い方になるので、13 章に書く。代案: 止めずに directory へ進ませ、13 章の既知の制約のままにする（U10 の予約を採るなら、directory の content 入口が `EntryAmbiguous` で拒むので、どちらでも directory の版は載らない。違いは失敗の理由の出方だけになる）。
- **U10 宣言の identity を `AssetManagement` に予約する（§2.4 D1）。** 推奨: D1。resolver を通さない `LoadContentSceneAsync` の直接の呼び出しも、snapshot の Play の間は宣言にある identity を directory から読めなくなる（利用者 (c) のうち、直接の入口を使う code に見える変化）。`AssetManagement.ContentDirectory.cs` に Editor の build だけの検査が入る。
- **U11 `Core/` の doc comment を直す（§3）。** 推奨: 直す。`ContentLoadPath.AlreadyLoadedScene`（「Play 前に既にロードされている Scene をそのまま使う」）と `ContentAcceptanceBasis.OpenSceneSnapshot`（「意味の詳細は後続の作業が定める」）の説明が本書で古くなる。program が「次に Core を触るスライスで直す」とした `IContentCompatibilityRule` の XML doc の古い 1 文（S1 の U-S1-5）も一緒に直す。型・member・値は変えない。

## 3. 責務マップ

行数は base `a629f9c1` の `wc -l`。増分は見込み。型と member の名前は仮で、B が決めて §6 に記録する（責務・置き場・file は変えない）。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/ContentResolution/EditorSnapshot/EditorSnapshotProvider.cs`（新規、file 全体が `UNITY_EDITOR`） | snapshot の供給元（policy）: 宣言の identity に候補を返す。ある identity を宣言しているかを答える。loader の既ロードの口として、台帳の預かる口へ渡す | 新しい責務 | 配線が作り、loader と同じ寿命（1 回の Play）。Scene も token も持たない。宣言（値に写した物）、表現の指定が無い時に申告する値、預け元を constructor で受け、static の bridge を読まない | `Core/`、下の預け元、`AssetManagement` の internal の預かる口 | internal | AC1・AC2 | 0 → 140 |
| `Runtime/ContentResolution/EditorSnapshot/EditorSnapshotSceneSource.cs`（新規、同） | 預け元（Unity I/O）: load 済みの複製を採る・複製の path を delegate で読み直す・unload する（既に無効な Scene は unload 済みと扱う）。1 Scene の照合の純粋な関数（取り込みの照合も使う） | 新しい責務（policy と I/O を分ける） | 供給元と同じ寿命。状態は宣言（不変）だけ | `UnityEngine.SceneManagement`、`AssetManagement/Internal` の預け元の port と `IBackendScene` | internal | AC2（fake と差し替え）・AC5 | 0 → 150 |
| `Runtime/ContentResolution/IContentSceneLoadBinding.cs`（新規） | 供給元が「この identity を宣言しているか」と「自分の候補の Scene を load して handle を返す」を答える内部の口 | loader が AssetManagement の internal を呼ばずに、既ロードの経路を持つため | interface | `Core/`、`IAssetManagement`、`ISceneHandle` | internal | AC2 | 0 → 40 |
| `Runtime/ContentResolution/ContentRequestLoader.cs` | resolver の決定を load へつなぐ | 経路 `AlreadyLoadedScene` の候補を、供給元の内部の口へ渡す分岐。policy に、ある identity を宣言する内部の口の供給元があるかを答える internal の問い合わせ | 既存 | 上の interface | public の member は増減しない | AC2・AC7 | 135 → 180 |
| `Runtime/AssetManagement/Internal/IAdoptedSceneSource.cs`（新規、`UNITY_EDITOR`） | 預け元の port: 予約する identity の集合、取得（`IBackendScene` を返す。Unity の `Scene` を返さないので fake を作れる）、unload | 台帳が預け元の中身を知らずに済むため | interface | `Internal/IAssetBackend.cs` の `IBackendScene` | internal | AC3 | 0 → 35 |
| `Runtime/AssetManagement/AssetManagement.AdoptedScene.cs`（新規、partial、`UNITY_EDITOR`） | 預け元を 1 つ据える口（宣言の identity の予約。据える時に台帳に同じ identity があれば失敗）と、外で読んだ Scene を台帳に預かる口（`_contentSceneLoadGate` の共有、directory が受付中であること、同じ identity の検査、取消の後始末）。預け元は `ReleaseAll` で外す | 新しい所有の種別 | `AssetManagement` と同じ | `AssetRegistry`（変えない）、上の port | internal | AC3 | 0 → 130 |
| `Runtime/AssetManagement/AssetManagement.cs` | 台帳と解放 | `UnloadSceneAsync` に預かった Scene の分岐（今の else より前。`#if UNITY_EDITOR` の数行）。`ReleaseAll` で預け元を外す | 既存 | 既存 | 変えない | AC3・AC7 | 494 → 502 |
| `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` | directory の content 入口 | `LoadContentSceneAsync` が、予約した identity を gate の中で `EntryAmbiguous` で拒む（`#if UNITY_EDITOR` の数行） | 既存 | 既存 | 変えない | AC3・AC5 | 175 → 181 |
| `Runtime/ContentResolution/Directory/RegisteredDirectoryRoute.cs` | 供給元・基準・policy・loader の組み立て | policy の先頭に置く供給元を任意の引数で受ける | 既存 | `Core/` の `IContentProvider`（`EditorSnapshot/` の型は知らない） | internal | AC5・AC7 | 121 → 135 |
| `Runtime/SceneSystem/SceneDirector.Loading.cs`、`SceneDirector.cs` | Scene の load | loader が宣言していると答えた identity に限り、組を作る時点で台帳経由の unload と印し、論理 node の判定の後・名前の再利用の前に loader に尋ねる。`SceneDirector.cs` は `ScenePair.BackendSceneLoaded` の doc comment（「Addressables 経由」）だけ直す | 既存 | 既存 | 変えない | AC4・AC5・AC7（`SceneDirectorContentLoaderTests` に追記） | 721 → 745、336 → 338 |
| `Runtime/Bootstrap/EditorPlaySnapshotBridge.cs` | 宣言の受け渡し | 1 件に「起動時に要求する」の印と、record の事実（元の path・Dirty・SHA-256） | 既存 | なし | internal | AC4 | 70 → 90 |
| `Runtime/Bootstrap/EditorPlaySnapshotIntake.cs` | 取り込みの純粋な判定 | 起動の計画（key・mode・配線・宣言・map の組）と、要求する Scene の選択。照合は `EditorSnapshot/` の 1 Scene の関数を使う。log に `requested=` | 既存 | `ContentResolution/EditorSnapshot/`（`Bootstrap` → `ContentResolution` は既存の向き） | internal | AC2・AC4 | 81 → 135 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 起動の順序 | 配線で預け元と snapshot の供給元を作り、預け元を `AssetManagement` に据え、供給元を route に渡して 1 行 log を出す。snapshot の Play では、計画の検査を取り込みの前に行い、`register-loaded-scenes` の代わりに `request-snapshot-scenes`（要求ごとに 1 行 log。失敗したら複製を unload）。計画が失敗なら起動の失敗 | 既存 | `EditorSnapshot/`（`#if UNITY_EDITOR`） | protected / public は変えない | AC4（純粋な部分）・AC6 | 1197 → 1245 |
| `Runtime/ContentResolution/Core/ContentLoadPath.cs`、`ContentAcceptanceBasis.cs`、`IContentCompatibilityRule.cs` | core の値の説明 | doc comment だけ（§2.8 U11）。型・member・値・code の行は変えない | — | — | 変えない | offline の test が変更なしで通る | 23 → 24、35 → 36、19 → 19 |
| `Editor/PlaySnapshot/PlaySnapshotSession.cs`、`SceneSnapshotWriter.cs` | 1 回の Play の準備と解除、複製の書き出し | `Prepare` が印の規則を受け、印を `SessionState` と bridge に運ぶ。bridge に元の path・Dirty・SHA-256 も渡す。`PlaySnapshotScene` に印。`SourceChanged` の説明を「複製以外の path」に改める | 既存 | 既存 | public: `Prepare` の引数、`PlaySnapshotScene` の property 1 つ | AC4・AC5 | 312 → 332、116 → 122 |
| `SampleGame/DependOnAll/Editor/Build/PlayEntryInterceptor.cs`、`PlayEntryRecord.cs` | SampleGame の入口 | 印の規則（SceneResourceMap にある Scene 名）を閉包で `Prepare` に渡す（port の `Action` の形は変えない）。由来の label を `snapshot->other` に、Warning の文を「複製以外から読まれた」に改める | 既存 | 既存 | internal | AC6・AC7 | 219 → 227、303 → 305 |

- log の行（AC6 の観測が読む。接頭辞は固定し、B は欄を足してよいが、ここに書いた欄は変えない）: 配線の行（既存の `[AppInit] Content resolution: wired=true …`）は変えず、続けて `[AppInit] Content resolution: snapshot provider=<供給元の id> scenes=<宣言の件数> requested=<印の件数>` を出す。取り込みの行（既存の `[AppInit] Editor play snapshot intake: scene=… copy=… active=…`）の末尾に ` requested=true|false` を足す。起動時の要求は、要求ごとに `[AppInit] Editor play snapshot request: scene=<identity> stable=<true|false>` を出す。
- test（新規・追記。class の名前は B が決めて §6 に記録する）: `OneStarMaker/Tests/ContentResolution/`（AC1・AC2）、`OneStarMaker/Tests/Scene/`（`SceneDirectorContentLoaderTests` に追記）、`OneStarMaker/Tests/` の AssetManagement の test の folder（AC3）、`OneStarMaker/Tests/Editor/PlaySnapshot/`（AC4）、`SampleGame/Tests/Editor/Build/PlayEntryRecordTests.cs`（label）。AC5 の実機の test は、`SampleGame/Tests/Editor/Build/`（`RegisteredDirectoryRouteIntegrationTests` の隣。fixture の map・factory・`SceneBase` は test の中に持つ）か `OneStarMaker/Tests/Editor/` の、friend と asmdef の参照を足さずに組める方に置く（`SceneSnapshotWriter` は internal で、`SampleGame.Tests.Editor` からは見えない。複製は public の `PlaySnapshotSession.Prepare` で作る）。
- 変えない file: `IAssetManagement.cs`、`Internal/AssetRegistry.cs`、`Runtime/ContentResolution/Core/` の上の 3 file 以外、`IContentSourceBinding.cs`、`RegisteredDirectoryProvider.cs`、`BuildContent/` の全て、`SceneDirector.Unloading.cs`・`SceneDirector.FailedLoadCleanup.cs`（組の印で既存の分岐に乗せる）、`SnapshotRoot.cs`、`PlayEntryDecision.cs`、`PlayEntryTransaction.cs`、`PurposeLaunchSettings.cs`。
- 行数の警報:
  - `AbstractApplicationInitializer.cs` は既に 1197 行。足すのは stage の列の分岐と配線の数行で、判定は `EditorPlaySnapshotIntake` の純粋な関数に置く。この file の分割は `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` が持つので、本書では分けない。
  - `SceneDirector.Loading.cs` は既に 721 行。足すのは分岐 1 つと印 1 つで、新しい責務は入れない。
  - `AssetManagement.cs` は 500 行の閾値を数行超える見込み。預かる口の本体は partial の別 file に置いてあり、ここに足すのは分岐と解除の数行だけである。Phase C が実数を見る。
  - `EditorPlaySnapshotIntake.cs` は 50% を超えて増える。同じ責務（取り込みの純粋な判定）の関数の追加なので分けない。
- 配置の理由:
  - 供給元と預け元は、S3-1 の `Directory/` と同じく連携部分として `Runtime/ContentResolution/EditorSnapshot/` に置く。`Bootstrap` を参照せず、宣言は配線が値に写して渡す（`ContentResolution` → `Bootstrap` の辺を作らない）。
  - 預かる口は Scene の台帳の所有者（`AssetManagement`）に置く。台帳は snapshot を知らず、「外で読んだ Scene、その unload の手段、予約する identity」だけを知る。連携部分が使う `AssetManagement` の internal は、S3-1 の `IsInstalledContentDirectory` に、預け元を据える口と預かる口が加わる（harvest で [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.2 の棚卸しに足す）。
  - 供給元（policy）と預け元（Unity I/O）を分けるのは、変わる理由とテストの方法が違うため。
  - asmdef は全て既存の中で、参照を足さない。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (0) 製品の code を変える前の、Unity の読み直しの疎通は、Phase A で取って成立した（2026-11-20。§5 の未知の操作経路 (i) の記録）。B は取り直さずに (1) から始める。(1) 預け元の port、預け元を据える口（予約）、台帳の預かる口、`LoadContentSceneAsync` の予約の検査（AC3）。(2) 1 Scene の照合、預け元、供給元、loader の分岐（AC1・AC2）。(3) route の引数と `SceneDirector` の分岐と印（AC4 の `SceneDirectorContentLoaderTests`）。(4) 宣言の印、起動の計画、Editor の session（AC4）。(5) initializer の配線と stage、SampleGame の入口（AC6 の準備）。(6) 実機の test（AC5）。(7) `Core/` と `ScenePair` の doc comment。(8) 動作確認（§5 の検証経路）。
- Phase B から Phase A へ差し戻す条件: asmdef の参照か friend が要る。`IAssetManagement` か `Core/` の型・member・値を変える必要がある。§3 に無い public の型・member が要る。`AssetRegistry`、`SceneDirector.Unloading.cs`、`SceneDirector.FailedLoadCleanup.cs` を変える必要がある（組の印だけでは I-3 を満たせない）。Unity が同じ Play の中で複製を読み直せない（§2.6 NO-GO (i)）。snapshot の無い Play か、宣言に無い identity の分岐を変えないと I-1〜I-4 を満たせない。AC5 を、friend と asmdef の参照を足さずに組めない。
- 対象外を維持する方法: `Core/` の code の行、`BuildContent/`、`PlayEntryDecision.cs`、`SnapshotRoot.cs` を変えない。Object の要求に snapshot の供給元は `NotFound` を返す。入口の判定表の行と拒否の条件を変えない。bootstrap の読み込みを loader に通さない。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC4（Unity の build を伴わない EditMode）。offline の test（`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`）は `Core/` を変えないので非退行の確認に使う。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.ContentResolution`、`SceneDirectorContentLoaderTests`、`SceneVariantForwardingTests`、`EditorPlaySnapshotIntakeTests`、`SceneSnapshotWriterTests`、`PlayEntryRecordTests`、`PlayEntryTransactionTests`、`PlayEntryDecisionTests`、AC1〜AC4 の新しい test の class。
- 判定必須テスト（GO 候補 head）: 上の起点 filter、offline の test、AC5 の実機の test、`RegisteredDirectoryRouteIntegrationTests`、`OverlayRegistrationIntegrationTests`、最終の全 EditMode 回帰（空 filter。合否は「XML の failed 0 と root result `Passed`」で読む。`-nographics` の VFX 25 件の Inconclusive で `step.json` が `failed` になるため）、AC6 の観測。
- 全 EditMode 回帰の適用除外: なし。**Player の build は判定必須にしない**（§2.8 U8。代替証拠は AC7）。
- 統合・Unity テスト: AC5 は Content Directory の build を 1 回伴う。他の Unity が同時に動くと build を伴う test が失敗した既知の事例がある（program §8）ので、判定 C は他の Unity を止めて走らせる。build を伴う batch の後の Unity の終了 stall は既知（`tools/Harness/README.md`）で、XML の完成で判定する。coordinator の staging の `Directory.Move` が access denied で失敗する既知の不安定（program §8）に当たった時は、同じ head で走らせ直してよく、その事実を §7 に記録する。
- 操作・実行時・目視条件の検証経路（AC6）:
  - 担当: 実行担当（判定 C）。動作確認は B の出口で同じ手順を 1 回行う。環境: `unity/` の Editor（CLI の `open` で起動し、`tools/unity-editor.cmd eval`）。`eval` の前に Editor の window を前面にする（非フォーカスの Editor は main thread の操作が 5 秒で timeout する）。`eval` の code は method の本体として compile されるので、型は完全名で書く。
  - 準備: installed cache に `spring-full` の検証を通る候補が無ければ、`Verify Play` を 1 回実行して作る（Phase C の通常の Build）。Variant profile の選択を外し、入口の経路を `BuiltWithOpenScenes` にする（実行担当は元の値を記録して最後に戻す）。`Application.logMessageReceivedThreaded` に Error・Exception・Assert を貯める collector を AppDomain に置く（Console は Play ごとに clear される）。`Title.unity` の SHA-256 と `git status --short` を採る。
  - 操作（Play A。Domain Reload 無効 = この project の設定）: `Title` だけを開く → `EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play")` → 入口の record（`PlayEntryRecord.Load()`）の `state` が `EnteredPlay` / `Blocked` / `StartupFailed` になるまで短い `eval` で読む（移行中の `eval` は捨てられる）。`EnteredPlay` 以外で終われば、state と理由を記録して観測を止める。
  - 観測（構造と値で判断する。画面は見ない）: (1) `UnityEditor.LogEntries` から、§3 の log の行（`[AppInit] Content resolution: wired=…`、`[AppInit] Content resolution: snapshot …`、`[AppInit] Editor play snapshot intake: …`、`[AppInit] Editor play snapshot request: …`）。(2) `SampleGame.DependOnAll.AppInitializer.s_instance` から reflection で `_sceneDirector` を取り、`GetContentLoadRecord("Title")` の由来・根拠・経路・`SourceBindingVerified`・事実を読む。`SceneManager` の load 済みの Scene の名前と path を全て読み、`Title` の個数と path を見る。(3) `director.UnloadScene("Title")` を待ち、`Title` が load 済みに無いことを見てから `director.AddScene("Title", null, CancellationToken.None)` を待ち（どちらも `UniTaskExtensions.AsTask` を `AppDomain.SetData` に置き、後の `eval` で完了と例外を読む）、(2) と同じ読みと、入口の record の `Title` の由来を読む。(4) `director.AddScene("ConfirmDialog", null, CancellationToken.None)`（map にあり payload を持ち、親は `HpGauge`。どちらも開いていない。S3-1 の AC7 と同じ Scene）の record を読み、unload する。(5) `EditorApplication.ExitPlaymode()` → `isPlaying` と `isPlayingOrWillChangePlaymode` が false になってから、置き場の folder の有無、`EditorSceneManager.playModeStartScene`、`Title.unity` の SHA-256、開いている `Title` の Dirty、`git status --short` を読む。
  - Play B: `Title` を閉じ、`Title` 以外の、SceneResourceMap にあって payload と親を持つ Scene を 1 つだけ開いて Play し、その Scene と親の record と、load 済みの Scene の名前・path・個数を読む。Scene の名前は B が動作確認で選んで §6 に固定し、判定 C は同じ名前を使う。SampleGame の Scene の都合で起動しない Scene を選んだ時は、理由を §6 に書いて別の Scene を選ぶ。
  - Play C: `EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None`（Domain Reload 有効）にして Play A の (1)〜(3) を行い、設定を戻す。Unity が `EditorSettings.asset` に serialize の差を残した時は、追跡の内容へ戻す（S0-d の AC12 と同じ）。collector は reload で消えるので、Play C の Error は入口の record と `LogEntries` で読む。
  - Play D（失敗経路）: Play A と同じに起動し、`director.UnloadScene("Title")` の後に、宣言の `Title` の複製の file を `File.Delete` し、`director.AddScene("Title", …)` の task が失敗で終わること、例外の文、`Title` という名前の Scene が load 済みに無いことを読む。
  - 観測 O-2（合否に使わない。§2.2）: Play A とは別の Play で行う（Play A の合否の観測に混ぜない）。`Title` だけを開き、`Title` の Scene の中の object が直接参照していて、値を 1 つ読める asset（material の色、ScriptableObject の欄など）を 1 つ選ぶ（B が動作確認で選んで、asset の path・読む欄・元の値を §6 に固定し、判定 C は同じ物を使う）。`eval` でその asset の欄を、保存せずに in-memory で元と違う値に変える（`AssetDatabase.SaveAssets` を呼ばない）→ Play A と同じ操作で Play に入る → `Title` の複製の中の同じ参照から欄の値を読み（変えた値か、元の値か）、同じ asset を参照する object が directory から載った Scene にあれば、その値と、2 つが同じ instance かも読む → 停止 → `eval` で欄を元の値に戻し、`git status --short` が準備の時と同じであることを確かめる。該当する asset が無い時、値が読めない時は、理由を書いて「観測なし」とする（別の asset を作ったり、Scene を書き換えたりして補わない）。保存した編集の版は O-1 が合成 fixture で見る。
  - 観測窓: Play ごとに、Play の menu の実行から、停止の後に `isPlaying` と `isPlayingOrWillChangePlaymode` が偽になり、置き場の folder が消える（session の解除が終わる）まで。停止の途中の Error も数える。
  - 合否の読み方: (1) の配線の行が `wired=true` でない、snapshot の行の `requested` が 1 でない、取り込みの行（`requested=true`）と要求の行（`scene=Title`）が無い。(2)(3) で `Title` が 1 つでない、path が置き場の下でない、record が読めない、由来が `EditorSnapshot` でない、`SourceBindingVerified` が false。(3) の後に入口の record の由来が `snapshot` 以外になる。(4) の record が directory でない、identity が (1) の行と違う。(5) のどれかが Play の前と違う。Play B で開いた Scene の record が `EditorSnapshot` でない、親の record が directory でない。Play D で directory の `Title` が載る、または task が成功する。Play A〜C の観測窓の Error / Exception が 1 件以上。どれか 1 つで不合格。
  - 証拠: 条件ごとの観測と、実際に動いた `eval` を 1 file にして evidence bundle へ収録する。B が動作確認で使った `eval` を §6 に残し、判定 C はそれを使う。C / C' は bundle の file を読む。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: `eval` での Play の開始と停止、入口の record の読み取り、reflection での `SceneDirector` の取得と `AddScene` / `UnloadScene` は、S0-d（PR #176）の AC7・AC11 と S3-1（PR #197）の AC7 で成立した経路である。AC5 の build・publish・install・Play mode の読みは `RegisteredDirectoryRouteIntegrationTests` で成立した経路である。**確認済み（Phase A の疎通、2026-11-20。記録はこの項の下）:** (i) 同じ Play の中で、unload した複製の path を `EditorSceneManager.LoadSceneAsyncInPlayMode` でもう一度読めること。手順: base の製品 code のまま、§5 の検証経路の準備をし、`Title` と、map に無い保存済みの空の Scene（実行担当が untracked の folder に作り、最後に消す。S0-d の AC9 と同じ形）を additive に開いて経路 Built + open scenes で Play に入る。Play の中で `eval` から、空の Scene の複製（lifecycle に入っていない）を `SceneManager.GetSceneByPath(複製の path)` の handle で unload し、bridge の delegate（`EditorPlaySnapshotBridge.LoadScene`。reflection で取る）で同じ path を load し、完了の後に、その path の Scene が有効で `isLoaded` であること、名前が元と同じこと、同名の load 済みの Scene が 1 つであることを読む。これを同じ Play の中で 2 回繰り返し、観測窓の Error / Exception を数える。**結果は成立**（2 回とも）。確かめたのは lifecycle の外に居る空の Scene の複製である。lifecycle に入った中身のある Scene（`Title`）の読み直しと、台帳・`SceneDirector` を通した経路の証拠は AC5 の (4) と AC6 の (3) が持ち、そこで不成立なら §2.6 の NO-GO (i) で owner に返す。**未確認。初回確認は Phase B の動作確認と Phase C:**(ii) EditMode の `[UnityTest]` の中で `PlaySnapshotSession.Prepare`（`playModeStartScene` を空の開始 Scene にする）を呼んでから `EnterPlayMode` に入り、Play mode の中で bridge の delegate（`LoadSceneAsyncInPlayMode`）で fixture の複製を読めること、Play を出た後に session が置き場と `playModeStartScene` を戻すこと（AC5。既存の実機の test は Play の前に fixture の Scene を閉じていて、開いたまま Play に入る形は初めてである。成り立たなければ、AC5 を実 Editor の `eval` の観測へ移すかを Phase A へ戻す）。(iii) Domain Reload 有効の Play で、配線の時点（`SceneDirector` を作る前）に bridge の宣言が置き直されていること（S0-d の AC12 は取り込みの段の時点で確かめた。AC6 の Play C）。
  - 疎通 (i) の記録（2026-11-20。担当が Phase A で取った観測で、判定 C の入力にはしない）:
    - 環境: Unity 6000.6.9f1、この worktree の `unity/` の Editor（担当が `unity open` で起動し、終わりに正常終了した。Library は新規）。製品の code は base `a629f9c1` と同じ（HEAD `c0fb1d52` は文書だけの差）。Domain Reload は無効（project の設定）。
    - 準備で分かったこと: S0-d・S3-1 が使った installed の `spring-full` の 2 revision（Unity 6000.6.0f1 の build）は、今の Editor では「`Manifest is not compatible with this runtime.`」で検証を通らない（manifest の `unityVersion` の照合）。`Verify Play` `spring-full` を非対話で 1 回実行し（新しい Library で約 4 分）、cache root の既定（repo の外）に revision `20261119T154403156Z-dfbf…` を作った。この PC の Phase B・C は、Editor の版が同じ間はこの revision を使える。Variant profile の選択は無く、経路の保存値は未設定（既定 `Source`）だった。経路を `BuiltWithOpenScenes` にし、終わりに未設定へ戻した。
    - Editor を開いた時点で、Unity が `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` に serialize の差を残した（疎通の操作より前）。終わりに `git checkout` で追跡の内容へ戻した。`git status --short` の基準を採る担当は、これを準備で戻す。
    - 操作: `Title` と空の Scene `S33Probe`（`Assets/OsmS33Probe/`。確認の後に削除）を additive に開き、`Edit/Play Mode/Play`。入口の record は `EnteredPlay`、`rule=SnapshotRoute`、`intake=[Title, S33Probe]`、由来はどちらも `snapshot`。
    - 観測（2 回とも同じ）: unload の前は、`S33Probe` の複製が有効で load 済み、同名の load 済みの Scene は 1。unload の完了で、その path の Scene は無効、同名は 0、複製の file は残る。bridge の delegate で同じ path を load し、完了で、その path の Scene が有効で `isLoaded`、名前は `S33Probe`、path は複製の path、同名の load 済みの Scene は 1。`Title` の複製、`UIScene`、`OutGameScene`、空の開始 Scene は変わらない。
    - 観測窓（Play の menu の実行から、停止の後に置き場が消えるまで）の Error / Exception / Assert は 0。停止の後: `playModeStartScene` は null、置き場は無い、record は `copiesRemoved=yes`、`Title.unity` の SHA-256 は前と同じ、`git status --short` は基準と同じ。
    - 使った `eval`（Play の中で実行する。結果は `AppDomain` の data に貯め、後の `eval` で読む）:

      ```csharp
      var log = new System.Collections.Generic.List<string>();
      System.AppDomain.CurrentDomain.SetData("S33.probe", log);
      var sflags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
      var iflags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
      var bridge = typeof(OneStarMaker.Runtime.AbstractApplicationInitializer).Assembly.GetType("OneStarMaker.Runtime.EditorPlaySnapshotBridge");
      if (bridge == null) return "bridge type not found";
      var declaration = bridge.GetProperty("Declaration", sflags).GetValue(null) as System.Collections.IEnumerable;
      var load = bridge.GetProperty("LoadScene", sflags).GetValue(null) as System.Func<string, UnityEngine.AsyncOperation>;
      if (declaration == null || load == null) return "bridge is not set: declaration=" + (declaration != null) + " load=" + (load != null);
      string copyPath = null;
      var declared = new System.Text.StringBuilder();
      foreach (var item in declaration)
      {
          var identity = (string)item.GetType().GetProperty("Identity", iflags).GetValue(item);
          var path = (string)item.GetType().GetProperty("CopyPath", iflags).GetValue(item);
          declared.Append(identity).Append('=').Append(path).Append("; ");
          if (identity == "S33Probe") copyPath = path;
      }
      if (copyPath == null) return "S33Probe is not declared: " + declared;
      System.Func<string> snap = () =>
      {
          var sb = new System.Text.StringBuilder();
          for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
          {
              var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
              sb.Append(s.name).Append('|').Append(s.path).Append('|').Append(s.isLoaded).Append("; ");
          }
          return sb.ToString();
      };
      System.Func<int> sameName = () =>
      {
          var n = 0;
          for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
          {
              var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
              if (s.isLoaded && s.name == "S33Probe") n++;
          }
          return n;
      };
      System.Func<string> now = () => System.DateTime.Now.ToString("HH:mm:ss.fff");
      System.Action<int> cycle = null;
      cycle = n =>
      {
          var before = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(copyPath);
          lock (log) log.Add(now() + " cycle " + n + " before: valid=" + before.IsValid() + " isLoaded=" + before.isLoaded + " name=" + before.name + " sameNameLoaded=" + sameName() + " scenes=[" + snap() + "]");
          if (!before.IsValid()) { lock (log) log.Add("STOP: the copy is not loaded before unload"); return; }
          var unload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(before);
          if (unload == null) { lock (log) log.Add("STOP: UnloadSceneAsync returned null"); return; }
          unload.completed += _ =>
          {
              var gone = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(copyPath);
              lock (log) log.Add(now() + " cycle " + n + " unloaded: valid=" + gone.IsValid() + " sameNameLoaded=" + sameName() + " fileExists=" + System.IO.File.Exists(copyPath) + " scenes=[" + snap() + "]");
              UnityEngine.AsyncOperation op;
              try { op = load(copyPath); }
              catch (System.Exception ex) { lock (log) log.Add("STOP: load threw " + ex); return; }
              if (op == null) { lock (log) log.Add("STOP: the bridge delegate returned null"); return; }
              op.completed += __ =>
              {
                  var again = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(copyPath);
                  lock (log) log.Add(now() + " cycle " + n + " reloaded: valid=" + again.IsValid() + " isLoaded=" + again.isLoaded + " name=" + again.name + " path=" + again.path + " sameNameLoaded=" + sameName() + " scenes=[" + snap() + "]");
                  if (n < 2) cycle(n + 1);
                  else lock (log) log.Add("DONE");
              };
          };
      };
      cycle(1);
      return "started copy=" + copyPath + " declared=[" + declared + "]";
      ```
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし（準備の Content Directory の build は Phase C の通常の Build として 1 回行う）。
- 判定 C の照合 script の方式: なし（test の XML と、AC6 の観測 file を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC7 の `grep`。
- A0/A1 主担当・モデル・ベンダー: AI（Claude Code、Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: Codex CLI（OpenAI 系）。A0〜A2 と統合では使っていない（A2 は Anthropic 系だけ）。動かない時だけ cursor-agent の Grok 系。外部計画レビューの系列は owner が決めるので、A3 でその系列を記録し、C' の独立性を書き直す。
- 独立性の強化条件を満たせない場合の理由: A3 で記録する。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-19、同一 PC）。入力版は固定 base `a629f9c1` の clean な detached checkout と、HANDOFF copy（sha256 `c54325ec5b3a7cca22f6209a4b27c63821c7aa9899fec1ab355c198033d7fbf0`。A1 初稿）。4 件とも Claude Code の subagent（Anthropic 系。主担当と同じ系列で、主担当とは別のモデル）。互いの指摘は渡していない。
  - アーキテクチャゲート: HEAD・clean・hash の照合は一致。結論は blocker 0、major 3、minor 8。R2・E1・O1・L1・P1・S1・U7 と、供給元と預け元の分割・依存の向き・起動の順を妥当とした。§1 と §3 の行番号と行数は照合した範囲で全て正しかった。
  - 不変条件の反証と AC の読み違い: 照合は一致。結論は blocker 1、major 6、minor 7。I-1（resolver の経路）・I-2・I-5 は破れず、I-3 は「loader に尋ねる前の後始末が未要求の複製を名前で unload する」順で破れた。
  - 目的レビュー: §1 の目的と利用者の本文だけを渡した別の担当（道具を使わない）。入力の sha256 `110a9eb6ac1e69b5950b8a6d76290b89261311683fc3f15031b0e8f83b0b589b`。
  - A0 だけからの代替構成（risk `high` の追加分）: 目的レビューの回答を記録した後、同じ担当に A0 の packet（sha256 `59829f30a05ef6b1f34513ac2d1ee2d8a17408302fde50103ef5030cc3b0c641`。目的、利用者、対象外、現況、問い、誤って YES を出す道、実装制約）と固定 base を渡して続けた。初稿は読ませていない。目的レビューのやり直しに使える未関与の担当は残してある。
  - 目的レビューのやり直し（2026-11-19。A3 で目的・利用者の 2 欄を改めたため。1 回まで）: 改めた 2 欄の本文だけを渡した、目的・利用者以外の欄も初稿も読んでいない別の担当（Claude Code の subagent、Anthropic 系。道具を使わない）。入力の sha256 `cdae5daae15266ceeaaa303447632a3995ee96263100369552254a72f83d5fac`。
- A3 の分類（担当の案。owner の承認待ち）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | 反証 B1 | loader に尋ねる直前に組を印しても、それより前の後始末（`AddScene` の取消の窓、PreLoad の失敗・取消、自動で load される子）が未要求の複製を名前で unload する。直接の呼び手が預けた Scene では台帳と Unity の Scene が食い違う | 採用 | 宣言にある identity の組は作る時点で台帳経由の unload と印し、預け元の unload は無効な Scene を unload 済みと扱う（I-3 の (4)(6)、§1 の現況、AC4 の `SceneDirector` の単体、AC5 の (7)） |
  | ゲート M2 | 印を先に立てると、失敗の後始末が、直接の入口で読んだ他の所有者の token の Scene を identity で unload しうる | 採用 | 宣言の identity を予約し、宣言にある identity の台帳の entry を預かった Scene だけにした（I-1 の (6)、§2.4 D1、AC3、AC5 の (9)、§2.8 U10）。第 3 の状態は採らない（§2.4 L3） |
  | ゲート M3、反証 M2 | 預かる口の gate が決まっていない。別の gate だと `AssetRegistry.AddScene` の上書きで片方が台帳から漏れ、close が壊れる | 採用 | `_contentSceneLoadGate` を共有し、directory が受付中の時だけ預かる。close は預かった Scene を unload しない、と契約に書いた（I-3 の (8)(9)、§3、AC3） |
  | ゲート M1、反証 M4 | AC5 は `SceneSnapshotWriter`（internal）を `SampleGame.Tests.Editor` から呼べず、AC8 と食い違う | 採用 | 複製は public の `PlaySnapshotSession.Prepare` で作る。置き場は friend と参照を足さずに組める方（§3 の test、AC5、§5 の未知の経路 (ii)、§4 の差し戻す条件） |
  | 反証 M1 | AC5 の (7) の時点に未要求の複製が無く、空振りになる | 採用 | 最後まで未要求の `SnapD` を足し、PreLoad の取消、複製の file の削除、直接の入口の 3 つの失敗経路で残ることを見る（AC5） |
  | 反証 M3、ゲート minor 3 | 表現の指定が無い要求に「要求の表現をそのまま」返すと、`ContentCandidate` が空の表現を拒んで fault になる | 採用 | 指定が無い時は `Full` を申告する（I-1、AC1、§2.4 P1） |
  | 反証 M5 | `request-snapshot-scenes` の失敗の後に、複製と預かった Scene が load 済みのまま残る | 採用 | 計画の検査を取り込みの前に行い、stage の失敗で複製を unload する（I-2、I-3 の (11)、AC4、§3） |
  | 反証 M6 | AC6 の観測窓の終わりが決まっていない | 採用 | 窓は session の解除が終わるまで（AC6、§5） |
  | ゲート minor 1、反証 minor | loader に先に尋ねる分岐の位置（payload の無い論理 node の判定の後） | 採用 | I-1、AC4 |
  | ゲート minor 2 | snapshot の Play の全ての identity が loader へ先に行くと、宣言に無い同名の Scene が 2 つになる | 採用 | 宣言にある identity に限る（I-1、I-5、§2.4 L1） |
  | ゲート minor 4 | 1 Scene の照合が `EditorPlaySnapshotIntake.Verify` と 2 か所になる | 採用 | 取り込みの照合が新しい関数を使う（I-4、AC2、§3） |
  | ゲート minor 5 | 預け元の port が Unity の `Scene` を返すと fake を作れない | 採用 | port は `IBackendScene` を返す（§3） |
  | ゲート minor 6、代替構成 | `ContentLoadPath.AlreadyLoadedScene`・`OpenSceneSnapshot`・`ScenePair.BackendSceneLoaded` の doc comment が古くなる | 採用 | doc comment だけ直す（§2.8 U11、§3、AC7・AC8 の文言） |
  | ゲート minor 7 | 連携部分が使う `AssetManagement` の internal の棚卸しが増える | 採用 | harvest 先に足した（§0、§3） |
  | ゲート minor 8 | 封じていない `AssetManagement` では、Addressables の入口が台帳の預かった Scene を返す | 採用 | AC3 は directory を install して封じた状態で行う（snapshot の Play は常に封じる。I-1 の探索した境界） |
  | 反証 minor | Phase 2 と Phase 3 の間の再要求、同じ identity を 2 人が持つこと | 記録（変えない） | directory の Scene と同じ既存の制約（I-1 の既知の制約、I-3 の (10)、§2.3） |
  | 反証 minor | key が偽の扱い、`AssetOwner.Scene` の所有を見る、`UnloadScene` の待ち方、log の行の書式、`PrepareSnapshot` の port は `Action` のまま | 採用 | AC4、AC5 の (2)、§5 の (3)、§3 の log の行、§3 の SampleGame の行と AC7 |
  | 反証 (g) | 照合の不一致を実機で起こす条件が無い | 記録（変えない） | 不一致の網羅は単体（AC2）が持つ。実機と実 Editor は複製の file の削除で「採れない時に directory へ進まない」を見る（AC5 の (8)、AC6 の Play D） |
  | 反証・ゲートの未確認 | UICommon が map にあるか（あれば走査の停止で登録されなくなる） | 解消 | bootstrap の `UIScene` は SceneResourceMap に無い（ゲートが確認。入口も UICommon を名前と path で別に扱う。§1 の現況） |
  | 代替構成 | 預け元を据えて identity を予約し、直接の content 入口を拒む | 採用 | §2.4 D1 |
  | 代替構成 | 引数なしの `Prepare()` を残し、active の Scene だけを要求する | 保留（owner） | §2.8 U7 の代案 |
  | 代替構成 | 不達（bridge が無い、file が無い）を identity の無い `Rejected` で返す | 不採用 | 宣言は 1 回の Play の間変わらないので、供給元は常に `Available` を返し、採れないことは load の口の例外で言う（どちらも directory へ進まない。失敗の理由が load の段に 1 か所で出る） |
  | 代替構成 | key が真で配線しない Play は今の動きを残して log を出す | 不採用 | 起動の失敗にする（AC4、§2.3）。複製の経路を 1 つにする |
  | 代替構成 | 組に「何も取得していない」の印を足す | 不採用 | §2.4 L3 |
  | 代替構成 | 起動時の印、起動時の全ての load、台帳での所有、候補の値、適用する Play の範囲、置き場 | 初稿と一致 | R2・E1・O1・P1・S1、§3 |
  | 代替構成の検証 | 読み直し、directory の版への置き換わり、未要求の Scene と台帳の解放を最小の test で見る | 採用（初稿にある） | AC5 の (4)(8)(9)、(2)(5)(6)、AC3 |
  | 目的レビュー | 保証すべきこと 11 件 | 初稿にある | 複製の優先と 1 つだけ・振り分け・同じ API・同じ所有と解放・record・再要求・物理的な load と lifecycle の分離・明示の要求だけ・検証の非迂回・Editor の Play だけ、は I-1〜I-5 と S-1〜S-4。「複製は Play に入る時点の状態で、Play の終わりに破棄する」は S0-d の session の既存の契約で、本書は変えない（AC5・AC6 の停止の後の観測） |
  | 目的レビュー | 作らないべきもの 8 件 | 初稿と一致 | 開発者の UI・呼び手の供給元の指定・新旧の比較・黙った fallback・live sync・後続の本体・順序の設定は作らない。「開いている Scene を全て自動で lifecycle に入れる既定を作らない」は、Framework は既定を持たず（§2.8 U7）、全てを要求するのは SampleGame の入口の規則（§2.8 U3）という形で分けた |
  | 目的レビュー | 文言への指摘 8 件（Scene を何で同定するか、「1 つだけ実行」の主語、宣言と明示の要求の関係、load 済みの実体を使うか別の instance を作るか、「Play 前」の時点と未保存の変更、複製が検証の対象に読めること、宣言が無い時の既定、record の型の範囲） | 採用（owner、2026-11-19） | 2 欄を、結果を先に書き手段を後に並べる形に改め、8 件のそれぞれの答え（Scene の同定は Scene 名、「1 つだけ」は同じ Scene を 2 つ load しないこと、起動時の宣言と Runtime の要求の関係、load 済みの複製を採ること、Play 前の時点と未保存の変更、複製は検証の対象でないこと、Framework は既定を持たないこと、record の型を変えないこと）を文言に入れた（§1 の経緯、§2.8 U1）。改めた 2 欄で目的レビューをやり直した（下の 3 行） |
  | 目的レビューのやり直し | 保証すべきこと 10 件 | 初稿にある・1 件採用 | 版の対応が Play の終わりまで変わらない・複製は Play に入る時の 1 回の写し・複製を常に採る・build の版へ続けない・再要求も同じ・同じ契約と同じ record の型・directory の検証を変えない・load だけでは lifecycle に入らない・規則を渡さない入口は始められない・元の Scene と directory を変えない、は I-1〜I-5、AC4、S0-d の session の既存の契約。「採れない時の失敗の理由が、どの Scene の複製がなぜ採れないかを言う」を AC2 に足した |
  | 目的レビューのやり直し | 作らないべきもの 9 件 | 初稿と一致 | 開発者の設定・Framework の既定・build の版への fallback・新しい record の型や load の API・複製の digest の検証・Play の中の作り直し・複製の永続化・衝突を選ばせる UI・後続の本体は作らない（複製の SHA-256 は S0-d からある出所の手掛かりで、検証には使わない） |
  | 目的レビューのやり直し | 文言への指摘 9 件（同名で path の違う Scene の取り違え、複製を作る範囲、Play 全体として何が検証済みか、複製を保持する責任、規則の最小の形、開発者への知らせ方、「起動の後」の境目、元の Scene を保存しないこと、利用者 (d) の位置づけ） | 保留（owner の裁定。やり直しは 1 回までなので、これ以上は owner が文言を裁定して凍結する） | 答えは本文にある: 同名 2 つは入口が拒む（§1 の現況）。複製は開いている全ての Scene（§2.4 E1）。検証済みと言えるのは directory の Scene だけで、record が Scene ごとに言う（I-4）。複製の保持は Editor の session（S0-d）。印が 0 件の規則も許す（AC4）。何が要求されたかは起動の log の `requested=` に出て、表示は S5。「起動の後」は起動の stage の後の全ての要求。元の Scene の file と Dirty を変えない（I-5、AC6）。(d) は入力を持たない |

- 外部計画レビュー 1 巡目（2026-11-19、Draft PR #201、対象は commit `e3fff18a`。owner のアカウントの計画レビューと、同じ担当による第 1 指摘の訂正のコメント。系列は担当が確かめていない）。採否は担当の案で、owner の裁定待ち:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | Minor（訂正の後。初めは Major）: I-2 の「未要求の Scene は `AssetOwner.Scene` の所有に現れない」は、未要求の複製の `Awake` / `Start` を止めない E1 と、そのままでは両立しない（`AssetRegistry` は owner の Scene の登録を検査しない）。host の規約「lifecycle に明示的に入る前に `AssetOwner.Scene` で asset を取ってはならない」を一文足せば足りる。Framework の強制と E1 の変更は求めない | 採用 | I-2 の主張を「Framework は所有を作らない」に改め、host の規約と経路 (7) を足した。AC5 の (2) に、fixture が規約に従うことと、見ている範囲を書いた。code で `Acquire` / `TrackOwner` が検査しないことを確かめた |
  | Risk / sequencing: NO-GO の条件である「同じ Play の中の複製の読み直し」を、預かる口から入口まで実装した後に初めて確かめる順になっている。Phase B の最初に、既存の snapshot の作成と bridge で最小の疎通を取る | 採用 | §4 の順序に (0) を足し、§5 の未知の操作経路 (i) に手順を書いた（製品の code を変える前。不成立なら NO-GO (i)）。AC5・AC6 の証拠はそのまま。その後、owner（root）の指示（2026-11-20）で、凍結の前に Phase A のうちにこの疎通を取り、成立した（§5 の記録）。§4 の (0) は取得済みと書き改めた |
  | I-1 の、供給元の探索の停止に加えて `LoadContentSceneAsync` の予約まで含める形は妥当 | 記録 | — |

- A2 の再レビューの要否: 行わない案とする（owner の判断）。blocker への直し（組を作る時点の印）、予約、gate の共有は、A2 の指摘に沿って所有と後始末の穴を塞いだもので、新しい供給元・状態の種別・依存の辺は増えていない。増えたのは `AssetManagement.ContentDirectory.cs` の Editor の build だけの検査 1 つで、Draft PR の外部計画レビューで見てもらう。
- A3 統合担当・採否: 統合は AI（Claude Code、Anthropic）。owner の採否と凍結は未了。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
