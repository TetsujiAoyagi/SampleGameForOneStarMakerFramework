# base と上書きの 2 つの Content Directory を Runtime で登録する（S3-2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S3-2）
- status: `A`（A3 の統合案。A2 の 4 件と外部計画レビュー 3 巡（Anthropic 系 1、OpenAI 系 2）の採否を §5.1 に記録し、§2.8 の U3〜U7 と §5.1 の採否案を owner が 2026-11-18 に推奨どおりと決めた。owner の外部レビューと凍結の前。未凍結）
- branch: `claude/runtime-overlay-registration`（PR の base は `develop`）
- implementation base commit: `b0bc9997`（`origin/develop` の head、2026-11-18。§1 の現況と §3 の行数はこの base）
- implementation head commit: `未固定`
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: `未凍結`
- risk: `high`（program の行の値。所有者・寿命（native の登録と解除の順）、`ContentRevisionGate` の登録の基数、public の enum の値、BuildContentRoot の直列化の形を変える）
- owner: root（依頼者）
- created: 2026-11-18
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」（見出しの「Runtime の強制は未実装」を「強制は internal の登録の口にあり、入口は未配線（S3-4）」に改め、強制を確かめた設定（Domain Reload 無効、同じ PC と `Library`、loader の経路）と測り直しの結果を書く。「logical key の解決」の粒度（§2.8 U7）と、「測った設定」の Domain Reload 有効の送り先を直す）と「要求時の Content 解決」（上書きの供給元、record）、[18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md)「上書きの build と逆引き」（root の上書きの印）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（6000.6.9f1 の測り直し、移動した Scene の実測と「Scene の id は path から作られ」の訂正、native の登録と domain reload）、program §8・§10
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§4.1、§4.4、§5.3、§6、§8 の S3-2 と S3-1 からの移送）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A3（外部計画レビュー、owner の採否、凍結）。凍結の後に Phase B。
  - 次の作業: 外部計画レビュー 3 巡（Anthropic 系 1、OpenAI 系 2）の反映と、owner の U3〜U7・採否の決定が済んだ。owner がもう 1 巡外部レビューを行う。owner が §2.8 の U3〜U7 と §5.1 の採否を決め、凍結判断を上の凍結記録に書いて凍結版を commit する。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結の前に Phase B を始めない。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 検証済みの installed revision から登録した目的 build（base）に、手元で build した上書きの Content Directory を 1 つ重ねて登録できるようにする。重ねてよいのは、上書きを作った時に記録した base と一致する base だけで、登録・解除の順と個数は 13 章「base と上書きの登録契約」のとおりに Framework が強制し、呼び手に守らせない。要求時解決は両方の root から logical key を引き、得た物の所有と解放は既存の契約のまま、record はどちらの directory の root の entry を読んだかを指す。
- 利用者: (a) 上書きを Play に載せる Editor の入口を実装する者（後続のスライスと host の Editor code）。base の installed revision と上書きの directory を渡すだけで、順・照合・解除の順を書かない。(b) 要求時解決の loader と SceneDirector で Scene / asset を読む host の code（S3-1 の利用者）。新しい入力は無く、上書きが重なっていれば record がそれを言う。(c) 後続（snapshot の候補、Local / LAN）で供給元を足す実装者。
- 目的・利用者の文言の経緯: 上の 2 欄は program §8 の S3-2 の行、13 章の登録契約、S3-1 から移送した入力から本書で初めて書いた。A2 の前に owner（root）が 2026-11-18 に承認した（§2.8 U1）。同じ時に分割（§2.5）も承認した。
- 対象外:
  - **上書きを Play に載せる入口**（どの編集から上書きを作るか、いつ build するか、起動の設定の key、`AbstractApplicationInitializer` の配線、Play の入口の UX、Domain Reload 有効の Play での確認、実 Editor の Play での確認）。S3-4（§2.5。owner が 2026-11-18 に新設を承認）。
  - 上書きを 2 つ以上重ねること、base を 2 つ登録すること、目的 build 同士を同時に登録すること（13 章の契約が禁止。program §5.3）。
  - load の後に上書きを足すこと、Play の途中で上書きだけを外すこと（13 章の契約が禁止）。
  - 検証済みでない base（`directoryPath` の明示、Player に同梱した directory）に上書きを重ねること。base の identity が無く、照合できない。
  - Player で上書きを使うこと（13 章の契約が Editor の Play だけと定める）。
  - 共有 asset の実体の重複を減らすこと（上書きの preflight が一覧を出す。program §5.3 の当面の方針で開発時は許す）。
  - Editor snapshot の候補（S3-3）、要求の時点の install（S4）、観測の UI（S5）。
- 現況（base `b0bc9997`。code の読みと `grep` で確かめた。この節と §3 の path は `unity/Assets/OneStarMaker/Scripts/` からの相対で、test だけ `unity/Assets/` からの相対）:
  - **登録できる directory は process に 1 つ。** `AssetManagement.InstallContentDirectory`（internal、`AssetManagement.ContentDirectory.cs` 20〜25 行）は 2 件目を例外で拒む。`ContentRevisionGate.Reserve`（`Runtime/BuildContent/ContentRevisionGate.cs` 77〜97 行）は static な `_registration` 1 つを持ち、2 件目の予約を `RevisionBusy` / `PathInUse` で拒む。削除の許可（`TryAcquireDelete`）も、この 1 件と読みの lease の一覧と照合する。
  - **session は native の登録 1 つを所有する。** `ContentDirectorySession`（498 行）は path・identity・native の handle・索引・token の数・予約を 1 組持ち、受付を止めた後に token が 0 になると自分で unregister する（`ReleaseToken` → `CloseNow`）。Play 停止（`CompletePlayStop`）は token を待たずに unregister する。登録の巻き戻しに失敗した時の再試行は static な `_pendingRollback` 1 つに置く。
  - **上書きの base は Runtime から読めない。** 上書きの build（`BuildContentCoordinator.Build` に `ContentOverlayBaseIdentity` を渡す）は、`manifestDigest` を「呼び手が決めた hash の文字列」（`ContentOverlaySelection.cs` 58 行。test は `digest-1`）として受け、base の contentSet・revision・target・manifest の digest・宣言の名前を、directory の外の `reports/<identity>/preflight.json` の `overlayBase` にだけ書く。directory の中の `BuildContentRoot`（`Runtime/BuildContent/BuildContentRoot.cs`）は上書きの印を持たない。上書きの directory を今の `content:directoryPath` で単独で登録すると、普通の directory として通る。
  - **上書きの contentSet は base と違う。** coordinator は base と同じ contentSet（大文字小文字を無視）、base と違う宣言の名前、base と違う target を拒む（18 章「上書きの build と逆引き」）。上書きの directory は `artifacts/bs2b` か `TestResults/bs3-fixture` の下の `content/<build identity>` にでき、install も publish もされない（publish は上書きを拒む）。
  - **上書きを作る code を呼ぶのは test だけ。** `ContentOverlaySelection` と上書きの build を呼ぶ製品の入口（menu、Play の入口）は無い。
  - **供給元は 1 つ。** `RegisteredDirectoryRoute.TryCreate`（`Runtime/ContentResolution/Directory/RegisteredDirectoryRoute.cs`）は検証済みの session 1 つから供給元 1 つの policy を作る。`RegisteredDirectoryProvider` の id は固定値 `registered-content-directory`。照合（`IsLoadedBy`）は「実物の `AssetManagement` が今登録している directory がこの session か」を参照の同一で見る。
  - **record の由来の表は 2 組。** `ContentResolver.RequiredBasis` と `ContentCompatibilityRule.EvaluateGeneric` は、由来と受け入れ根拠の組を `ContentDirectory` + `VerifiedDigest` と `EditorSnapshot` + `OpenSceneSnapshot` だけ受け付け、他は採らない（`NoAcceptanceBasis` / `UnsupportedSource`）。
  - **resident cache の key は directory の path・identity・target を前置きにする。** `ContentDirectoryIndex.CacheKey`（99〜103 行）。close と Play 停止は session の `CachePrefix` で cache を退避する（`AssetManagement.EvictContentDirectoryCache`）。
  - **Unity の挙動（13 章と実測の事実。6000.6.0f1 の実測。6000.6.9f1 の測り直しは下）:** 同じ id の物は後から登録した directory の版が出る（どちらの root の id から要求しても同じ）。Scene の直接参照は Scene のある directory の中で解決する。後から登録した directory の解除は、先に登録した directory から `Loadable` で載せた物を Error なしに壊す（逆は壊さない）。
  - **6000.6.9f1 での測り直し（A1、2026-11-18。program §8 の Unity 更新からの移送が A1 に求めた）:** 下記（§1 末尾「測り直しの結果」）。
- UC ごとの利用者の入力:

  **UC-1 base に上書きを重ねて登録する**（利用者 (a)。本書では Framework の internal の登録の口を呼ぶ code。Play の入口からの呼び出しは S3-4）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | base の検証済みの installed revision | 呼び手が実行時に渡す（既存の `VerifiedInstalledRevision`。S3-1 と同じ） | 目的の「検証済みの installed revision から登録した base」そのもの |
  | 上書きの directory の path と build identity | 呼び手が実行時に渡す（上書きの build の結果の `ContentPath` と identity） | 目的の「手元で build した上書き」を指す。identity は gate の予約と root の照合に要る（既存の `Register` と同じ組） |

  機械が決めるもの: 重ねてよいかの照合（上書きの root に build 時に書いた base の記録と、base の検証済みの値）、上書きの target（base の検証済みの target）、上書きの contentSet（root の記録）、登録の順（base → 上書き）、解除の順（上書き → base）、個数（base 1 と上書き 0〜1）、key の解決の優先（同じ key・kind・実効の表現は上書き）、供給元の順、record。呼び手は順と照合を書かない（目的の「呼び手に守らせない」）。

  **UC-2 loader / SceneDirector で読む**（利用者 (b)）: 新しい入力なし。S3-1 の UC-1・UC-2 と同じ入力で、上書きの有無は機械が決める。

  利用者 (c) は開発者としての操作を持たない。

### 測り直しの結果

2026-11-18、この worktree（base `b0bc9997`、Unity 6000.6.9f1、Active Build Target は StandaloneWindows64 の Player、Enter Play Mode は Domain Reload 無効）で、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「測り直し」の手順どおりに harness を git の commit から取り出して未追跡の file として置き（製品 code は無改変）、各 filter を別々の Unity process で 1 回ずつ実行した。harness の file は測り直しの後に消し、commit しない。record は repository の外へ出していない（`TestResults/` の下。git 管理外）。

- **上書きの登録（commit `a304963b` の harness、7 process）:** `OverlayVerdictTests`・`OverlayBuilds.FirstProcess`・`OverlayRounds` の `ClassFirst`・`LifetimeFirst`・`ReadFirst`・`ControlClassFirst`・`ControlReadFirst` が全て exit 0。record の観測の行は 5 つの観測の process で同じ値になり、6000.6.0f1 の事実の節と全て一致した。
  - 登録の順が base → 上書き: `SceneEdit` と `EditedTex` は、base の root の id でも上書きの root の id でも上書きの版（v2）、`EditedTex` の実体は 1 つ、2 つの root の Scene の id と `LoadableObjectId` は等しい。
  - class: base だけの Scene・上書きの Scene・base の prefab の component は全て正しい class と値で、Missing は 0（両方の順）。
  - 実体の数（base → 上書き、両方の Scene と prefab を保持した時点）: 共通の texture・material・shader と、編集した texture・material が 2 つずつ、base だけの prefab と texture が 1 つずつ。
  - 寿命: Scene の unload と load し直し（間に `UnloadUnusedAssets`）の 5 つの checkpoint で、保持している物の破損は 0。最後に上書き → base の順で解除し、登録は 0 件。
  - 登録の順が上書き → base（対照）: どちらの root の id からも base の版（v1）。
  - build の出力: base と上書きで id が同じで中身が違う file は 3 つ（script をまとめた file、`EditedTex`、`SceneEdit`）。
  - Error / Exception の log は 0 件（build の段に Pipeline の設定の Warning が 2 件）。
  - harness の `verdict` の行は `UNDECIDED` だった。理由は対照の順の `AC6-class` を上書きの版が出る前提で読む補助の判定 code の既知の誤り（事実の文書の「測り直し」の末尾が予告している）で、観測の値は上のとおり 6000.6.0f1 と同じである。
- **同じ file を持つ 2 つの directory の登録（commit `444651ed` の harness、3 process）:** `RegistrationVerdictTests`・`RegistrationBuilds.FirstProcess`・`RegistrationRounds.SecondProcess` が exit 0。harness の判定は `GO` で、答えは 6000.6.0f1 の事実の節と全て一致した: `Loadable` からは prefab・material・texture とも実体 1（両方の順、2 回の繰り返し）。Scene の直接参照からは material と texture が 2 つずつで、`A` の root の prefab の material は登録の順 `A`→`B` で `A` の Scene と別の実体、`B`→`A` で同じ実体。解除の 7 case のうち、後から登録した側を解除した `U3`・`U4`・`U6` は先に登録した側から `Loadable` で載せた物が壊れ（Error の log なし）、`U1`・`U2`・`U5`・`U7` は無傷。Error / Exception の log は 0 件。
  - 第 1 process（`RegistrationBuilds.FirstProcess`）は、XML（`Passed`、1 件）と record の `process-end` を 16:58 に書いた後、Unity が終わらなかった（[Harness README](../../tools/Harness/README.md) の既知の終了 stall と同じ症状）。停止規則に従って原因は調べず、この測り直しのために起動したその Unity process だけを約 4 時間後に強制終了した。強制終了した process の終了は成功の証拠にせず、測り直しの答えは XML と record の終了行で読んだ。
- **A3 の追加の測定（2026-11-18、外部計画レビューの勧め）:** 同じ worktree・同じ設定で、製品 code を無改変のまま一時の EditMode の test（`SampleGame/Tests/Editor/Build/` に置き、測った後に消した。commit しない）を 3 つ、別々の Unity process で 1 回ずつ実行した。record は `TestResults/` の下（git 管理外）。
  - **移動した Scene（`Moved`）:** S3-1 の統合 test と同じ組み立てで、Scene 2 つ（`Stay`・`Moved`）の目的を製品の coordinator で `Full` build した後、`AssetDatabase.MoveAsset` で `Moved` を下の folder へ移し、中の物の名前を変えて保存し、移動後の path を編集の集合にして `ContentOverlaySelection.Select` → 上書きの build を行った。逆引きは `Moved` だけを選び、上書きの build は成功した。`LoadableSceneIdEditorUtility.CreateLoadableSceneId` の値は移動の前後で同じで、Scene の GUID と同じ文字列だった。base と上書きの root の `Moved` の entry の Scene の id も等しかった。base → 上書きの順に native で登録すると、上書きの entry から読んでも base の entry から読んでも上書きの版（中の物の名前が編集後）が載った。解除の後の登録の一覧は 0 件。**実測の事実の「Scene の id は path から作られ」（6000.6.0f1 の harness の注）は、この測定と合わない**（harvest で直す）。
  - **domain reload と native の登録（Domain Reload 無効の Play）:** 上書きの harness の base と上書きの directory を Play の中で native に 2 つ登録し、解除せずに Play を終えると、一覧に 2 件残った。続けて script の reload（`EditorUtility.RequestScriptReload`）を起こすと、reload の後の一覧は 0 件だった。
  - **Domain Reload 有効の Enter Play Mode:** 設定を一時に Domain Reload 有効にし、Play の中で 2 つ登録して解除せずに Play を終えると、一覧に 2 件残った。unity.log では、domain reload は Play に入る時に起き、Play を終える時には起きていなかった。残った 2 件は、一覧から取った handle の `UnregisterContentDirectory` で解除でき、一覧は 0 件になった。設定は測定の後に git の版へ戻した。
  - 測っていないもの: Play に入る時の domain reload が、前の Play で残った native の登録を消すか（script の reload が消すことだけを見た）。reload で static が消えた後の OS の lease の file handle の扱い。
- **結論:** 13 章「base と上書きの登録契約」が依拠する実測（登録の順、同じ id の物、解除の順で壊れること、実体の重複）は、6000.6.9f1 で変わっていない。契約の行は直さない。harvest で [実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md) の 2 つの節に「6000.6.9f1 で測り直して同じ」と書き、補助の判定 code の既知の誤りの注記はそのまま残す。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **検証済みの installed revision から登録した base に、記録した base と一致する上書きを 1 つ重ねた時、Framework が 13 章「base と上書きの登録契約」の順・個数・時点・照合・解除の順を強制して登録・解決・解除でき、要求時解決の record が、読んだ entry が上書きと base のどちらの root の物かを指すと言えるか。**
- 誤って YES を出す道（各 AC はこれを塞ぐ形にする）:
  - (a) fake の native directory だけで確かめ、Unity が後から登録した側の版を出すことと、後から登録した側の解除が先の側の物を壊すことを見ない。→ 製品の build 経路で作った base と上書きを実機で登録して読む（AC6）。
  - (b) 照合を、呼び手が申告した base の値と比べるだけにする。→ 照合は上書きの directory の root が持つ記録と、base の検証済みの値の比較にする（AC1・AC2）。照合が保証するのは「記録した base 以外に重ねない」ことで、上書きがその base の内容から作られたことではない（記録は上書きの build の呼び手が渡した値の写し）。
  - (c) 上書きの root にある key だけを試し、base だけにある key、上書きにだけある key、移動した Scene、表現が片方の層にだけある key を試さない。→ AC5・AC6 に全てを入れる。
  - (d) 成功の経路だけ見て、解除の順と、片方の解除・巻き戻しの失敗の後を見ない。→ AC1・AC3・AC6。
  - (e) record が `SourceBindingVerified` を true で返すことだけを見て、どちらの root の entry かを見ない。→ AC5・AC6 で record の由来と identity を key ごとに見る。
  - (f) base の版を pin した要求が、上書きの登録中に base の候補で成功する（実際には上書きの版が出る）。→ AC5 の失敗経路。
  - (g) 上書きの候補の互換の判定が常に通る（事実と基準を同じ出所から作る）。→ AC5 で各欄の不一致が落ちることを見る。

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 上書きは、作った時に記録した base と一致する base にだけ重なる。** 上書きの登録が成功した時、上書きの root が持つ base の記録（contentSet・revision・target・transport manifest の SHA-256・宣言の名前）は、同じ時に登録した base の検証済みの値（`VerifiedInstalledRevision` の contentSet・identity・target・`ManifestSha256` と、base の root の宣言の名前）と 5 欄とも ordinal で一致する。一致しない組、記録の無い directory を上書きにする組、記録を持つ directory を base や単独の directory として登録する組は、どれも native の登録と gate の予約を残さずに拒む（巻き戻しの失敗は I-2 の再試行に残す）。記録の digest は transport manifest の bytes の SHA-256 の小文字 16 進 64 字（receipt の `manifestSha256` と同じ形）で、上書きの build がこの形でない値を拒む。
  - 主張を崩しうる経路: (1) 呼び手が渡す値だけで照合する実装。(2) 上書きの build が root に記録を書かない（今の adapter）。(3) 上書きの directory を今の `Register(path, identity, target)`（`content:directoryPath`）で単独で登録する。(4) 上書きの directory を base として渡す。(5) 検証済みでない base（digest が無い）に重ねる。(6) 宣言の名前の照合を、base の root の宣言が無い時に飛ばす。(7) 照合の後、native の登録の前に base の directory が入れ替わる。(8) digest の書式の違い（大文字、`sha256:` の前置き）で、正しい組が落ちる・別の値が通る。(9) junction・8.3 名で base の content を上書きとして指す（A2 の反証で、記録が無いので照合で落ちることを確かめた）。
  - 塞ぎ方: 上書きの build が `BuildContentRoot` に base の記録と上書き自身の contentSet・build した Editor の Unity の版を書く（Editor の adapter。Editor の `ContentOverlayBaseIdentity` から Runtime の記録への変換は adapter の 1 か所だけ）。coordinator の `ValidateOverlay` が digest の書式を検査する。登録の口は base の `VerifiedInstalledRevision` と上書きの path・identity だけを受け、上書きの target は base の検証済みの target とする。base の予約の中の再検証（`VerifyInsideLease`）の後、上書きの native の登録の後に root を読み、照合の純粋な関数（記録の型の隣に置く）で比べ、合わなければ上書き → base の順に巻き戻して新しい失敗の値 `OverlayBaseMismatch` で拒む（理由の文に違う欄の名前を入れる）。記録の無い root は `OverlayBaseMissing`。記録を持つ root は、上書きの層以外の登録（`Register`・`RegisterVerified`・base の層）で `InvalidRoot` で拒む。
  - 探索した境界: `ContentDirectorySession.Register` / `RegisterVerified` / `RegisterCore`、`InstalledRevisionVerifier.Verify` / `VerifyInsideLease`、`AbstractApplicationInitializer.ReadContentDirectoryConfiguration` と `InstallConfiguredContentDirectoryAsync`、`PlayerContentConfiguration`、`BuildContentCoordinator.Build` / `ValidateOverlay`、`UnityContentDirectoryAdapter.Build`、`ContentRevisionGate.TryNormalize`。未確認: SampleGame の Editor の入口（`ContentDeliveryPlayBridge`・`PlayEntryDecision`）が `content:directoryPath` に入れうる値の全経路（利用者が手で設定を書く道は、(3) の root の拒否で塞ぐ）。
- **I-2 base → 上書きの順に登録し、上書き → base の順に解除する。上書きの解除が成功する前に base を解除しない。** native の登録は base が先で、上書きの登録と照合が成功するまで load を受け付けない。native の解除は層の列の逆順で、ある層の解除が失敗したらそれより前の層を解除しない。各層の成功した解除は高々 1 回。明示 close と自動の解除（受付停止後に token と処理が 0 になった時）では、どちらの層から載せた token・発行済みの処理・失敗した後始末が 1 つでも残る間は、どちらの層も解除しない。**Play 停止（`CompletePlayStop`）は例外:** Unity が Play mode ごと解体するので、今までどおり token も失敗した後始末も待たずに解除する。その場合も順（上書き → base）と「上書きの解除が失敗したら base を解除しない」は守る。
  - 経路: (1) 層ごとに token を数え、上書きの token が 0 になった時点で上書きを自動で解除する。(2) Play 停止が層ごとに unregister を呼ぶ順。(3) 上書きの解除の失敗の後に base を解除する。(4) 登録の途中の失敗（上書きの native の登録、root、索引、照合の失敗）の巻き戻しで base を先に解除する。(5) 巻き戻しの失敗の再試行（static の `_pendingRollback` は 1 つの処理しか持てない）。(6) 同期終了（`BeginSynchronousShutdown`）。(7) 明示 close が `ResourcesInUse` で止まった後の再試行。(8) gate の予約を層ごとに返し、base の予約が上書きより先に外れる。(9) Play 停止の解除の失敗の後に、止まっていた明示 close が再開して同じ層を 2 回解除する（A2 の反証。今の 1 層の code でも `_closed` が false のまま `CloseNow` へ進みうる）。
  - 塞ぎ方: 1 つの session が層（base と、任意の上書き）を順のある列で所有し、token・発行済みの処理・失敗した後始末を session 全体で 1 組数える（L1）。「列の逆順に 1 層ずつ解除し、失敗したらそこで止め、どの層まで解除したかを保持し、全ての層を解除したら予約を返す」操作は層の列（`ContentDirectoryLayers`）の 1 つの method にし、session の解除の 3 経路（`CloseNow`、Play 停止、登録の巻き戻し）と巻き戻しの再試行は、その method を呼ぶだけにする。巻き戻しの再試行（static）は層の列ごと 1 つの処理として持ち、session と同じ層の列の状態（層ごとの「解除済み」と予約を返したか）を共有する。再試行と session の close のどちらが先に走っても、解除済みの層を再び呼ばず、全ての層の解除と予約の返却が済んだ時に session を closed と確定する。保留できる再試行は process に 1 つだけで、それで足りる理由は 2 つ: gate は登録を 1 件しか持たない。次の登録は native の登録の前に保留中の再試行を走らせ、失敗すれば登録を拒む（今の `RegisterCore` と同じ）。gate の予約は session の全ての層を 1 つの登録として持つ。
  - 探索した境界: `ContentDirectorySession` の全ての解除の経路（`CloseAsync`、`CloseNow`、`CompletePlayStop` / `CloseNowForPlayStop`、`BeginSynchronousShutdown`、`ReleaseToken`、`CompleteOperation`、登録の巻き戻し）、`AssetManagement.CloseContentDirectoryAsync` / `CompleteContentDirectoryPlayStop` と `ReleaseAll` からの呼び出しの順、`ContentRevisionGate` の予約と削除の許可。実測（§1「A3 の追加の測定」）: native の登録は Play を終えても Unity が自分では解除しない（Domain Reload の有無によらない）。script の reload の後は一覧が 0 件になる。このため、巻き戻しの再試行（static）が reload で消えても、その時点で native の登録は残っておらず、再試行は static のままでよい。Domain Reload 有効の Play は Play を終える時に reload しないので、Play 停止の解除の失敗の後も static の再試行は次の Play に入るまで残る。未確認: Play に入る時の reload が前の Play の残りを消すか、reload の後の OS の lease の file handle。前者が消さないと分かった場合は、再試行の方式（登録の前に native の残りを一覧で見て片付けるか、残りがあれば拒むか）を Phase A の新しい revision で決め直す（§2.3）。
- **I-3 record は、読んだ entry の root の directory を指す。** 層の解決の前に、base の root の entry のうち、native の id（Scene の id か Object の `LoadableObjectId`）が上書きの root のどれかの entry と同じ物を base の層から除く（id による影。native の load は directory を指定せず id で読むので、その entry を base から読んでも上書きの版が出るため）。層の解決は (logical key, kind, 実効の表現) の単位で行う: 要求の表現 R について、上書きの root に R の entry があれば上書き、無ければ base の root の R の entry、どちらにも無く要求が R を指定していれば上書きの `Full`、無ければ base の `Full`（表現の指定が無い要求は `Full` だけを同じ順で引く。空の表現の entry は `Full`）。loader が成功として返す record の候補は、この解決が選んだ層を由来・provider の id・identity で言う。上書きの登録中に base の候補として成功する要求は、上書きの root に同じ (key, kind, 実効の表現) が無い要求だけである。上書きの候補は検証済みの digest を受け入れ根拠と言わない。
  - 経路: (1) 両方の root を同じ供給元が引き、どちらから引いたかを record に残さない。(2) base の供給元が、上書きの root にもある (key, kind, 表現) に候補を返す（Unity は上書きの版を出すので、record が base を言い、pin した base の版の要求が成功する）。(3) 解決を key だけの単位にすると、上書きの root に `K` の `Whitebox` だけがある時、編集と関係の無い base の `K` の `Full` の要求が失敗し、逆向きでも fallback なしの要求が失敗する（A2 の反証。1 directory の時に読めた要求が上書きで後退する）。(4) 上書きの候補を `ContentDirectory` + `VerifiedDigest` で申告する。(5) 照合（`IsLoadedBy`）が層を見ず、base の供給元の候補に上書きの層を合格させる。(6) resident cache の key が層を区別しない、または退避が片方の層に届かない。(7) Scene の台帳（identity ごとに 1 つ）が、上書きの登録より前に載った同じ identity の Scene を再利用する。(8) 同じ asset が base と上書きで別の (key, kind, 表現) を持つ（例: 表現の tag を `Whitebox` から `Full` に変えて上書きを作った。GUID と local file id は同じなので native の id も同じ）。base の `(K, Whitebox)` を選ぶと native は上書きの版を読み、record と base の pin が嘘になる（外部計画レビュー 3 巡目）。
  - 塞ぎ方: 層の解決は層の列の 1 か所（上の規則）で決め、load の入口・2 つの供給元・照合が共有する。供給元は層ごとに 1 つで id は別（base は今の `registered-content-directory`、上書きは `registered-content-overlay`）、policy の順は上書き → base。各供給元は解決が自分の層を選んだ時だけ候補を返し、base の供給元は上書きの層が選ばれた時に `NotFound`（理由で上書きを指す）を返す。上書きの候補は新しい由来・受け入れ根拠の組（§2.4 K1）で申告し、load の経路は base と同じ `ContentLoadPath.ContentDirectory` にする（loader はこの経路の候補だけを content 入口に渡すので、loader を変えずに済む）。照合は層まで比べる。(8) は上の id による影で塞ぐ（影になった base の entry は、loader でも `IAssetManagement` の content 入口でも base から引かれない。content 入口は今の索引の失敗と同じ `EntryMissing` で拒む）。cache の key は session に共通の前置き（今の base の `CachePrefix`）の後に層の部分（上書きは path・identity）を続ける形にし、`AssetManagement` の `EvictByPrefix` 1 回で両方の層が消える。(7) は、上書きは session の作成の時に base と一緒に登録し、後から足す口を作らない（I-4）ことで、上書きより前に載った Scene が無い。
  - 13 章との関係: 13 章「logical key の解決」の「同じ key が両方にあれば上書きの entry を採る」を、harvest で「同じ key・kind・実効の表現」と書き改める（設計上の規則の粒度の確定で、実測には依らない。§2.8 U7）。
  - 探索した境界: `ContentDirectorySession.TryFindEntry` / `GetObjectKey` / `ResolveObject` / `ResolveScene`、`ContentDirectoryIndex` の解決と cache の key、`ContentEntryTable`、`RegisteredDirectoryProvider`、`RegisteredDirectoryRoute`、`ContentRequestLoader`、`ResolutionPolicy`（id の重複を拒む）、`ContentResolver` の固定表、`ContentCompatibilityRule`、`AssetManagement.LoadContentSceneAsync` / `LoadContentAssetAsync` / `EvictContentDirectoryCache`。未確認・既知の制約: 上書きの root に無い base の entry の依存のうち、上書きの directory に同じ id の file があるもの（base の root の entry から読んでも、依存は上書きの版になる。13 章が「上書きにある id の物の base の版を選べない」として受け入れた制約で、record は依存の由来を言わない）。上書きの層の load の失敗の例外は、session の `BuildIdentity`（base）を載せ、層は message で区別する（`IContentDirectoryBackend` の identity は 1 組のまま）。
- **I-4 上書きは最初の load より前に揃い、個数は 0 か 1 で、途中で増減しない。** session が load を 1 つでも受け付けた後に層を足す・外す口は無い。上書きを 2 つ以上、base を 2 つ持つ session は作れない。
  - 経路: (1) session に層を足す method。(2) `AssetManagement.InstallContentDirectory` を 2 回呼ぶ（今も拒む）。(3) gate に 2 つ目の登録を予約する（今も拒む。層を持つ登録の後も拒む）。(4) 上書きの root を持つ directory を、上書きを重ねた session の base にする（I-1 の (4)）。
  - 塞ぎ方: 層は登録の口の引数だけで決まり、session は層の列を変える member を持たない。登録の口は base 1 つと上書き 0 か 1 つだけを受ける。
- **I-5 1 つの directory の利用と既存の契約を変えない。** 上書きを渡さない登録は、今の `Register` / `RegisterVerified` と同じ値・同じ失敗・同じ解除の挙動で、`IAssetManagement`、`AssetManagement` の public の面と `AssetManagement.ContentDirectory.cs`、`IContentDirectoryBackend`、`InstallContentDirectory` の 2 件目の拒否、起動の配線、Player の起動経路、Addressables の経路は変わらない。`Runtime/AssetManagement/` は `ContentResolution` の型を参照しない。上書きを重ねた登録の失敗の後に、base だけで Play を続ける道を作らない。
  - 経路: (1) session の層の化で 1 層の解除・token・cache の挙動が変わる。(2) gate の登録の形の変更で、1 件の登録と削除の許可の競合の結果（`RevisionBusy` / `PathInUse` / `DeletionInProgress`）が変わる。(3) BuildContentRoot の欄の追加で、記録の無い既存の root（全ての base、旧 build）の索引の作成が変わる。(4) 上書きの照合の失敗を、呼び手が握って base だけで登録し直す（本書は起動を配線しないので、呼び手は test と後続の S3-4）。(5) route の `Provider` の意味の変更で、起動の log と既存の test が変わる。
  - 塞ぎ方: 既存の test が変更なしで通る（AC7）。route の `Provider` は base の供給元の意味のまま残し、上書きの供給元は別の property にする。登録の口は失敗を例外で返し、base だけの session を返さない（AC1）。S3-4 への入力として「照合の失敗を base だけの Play に縮退しない」を移送する（§2.3）。

**成立条件（起きなければならないこと）**

- **S-1** 製品の build 経路で作った base（build → publish → install → 検証）と、その base を記録した上書き（逆引き → 上書きの build）を登録の口に渡すと、1 つの session が base → 上書きの順に登録し、`AssetManagement` に install できる。
- **S-2** その session で loader が、上書きの root にある (key, kind, 表現) は上書きの版を読んで上書きを指す record を返し、base の root にだけある物は base の entry を読んで base を指す record を返し、上書きの root にだけある key（新しい root）と、移動した Scene の key も上書きから読める。
- **S-3** 全ての物を既存の API で離した後、`CloseContentDirectoryAsync` が上書き → base の順に解除し、登録が 0 件になり、gate の予約が返る。Play 停止の経路も同じ順で解除する。

### 2.2 受け入れ条件

fixture は SampleGame を使わない合成（program D10）。実機は S3-1 の `RegisteredDirectoryRouteIntegrationTests` と同じ手順（製品の coordinator で `Full` build → publish → Local の供給元から install → 検証）で base を作り、fixture の source を変えた後に製品の `ContentOverlaySelection` と上書きの build で上書きを作る。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | I-1・I-2・I-4・I-5 | 単体（fake の native directory 2 つ（登録・解除を共通の log に書く）、temp の cache に実際に install した base、記録を差し替えられる fake の root）。登録の口は base の `VerifiedInstalledRevision` と上書きの path・identity を受け、記録が一致する時だけ層 2 つの session を返す。native の登録は base → 上書きの順に 1 回ずつ。照合の純粋な関数は、5 欄を 1 欄ずつ変えた記録（digest の大文字・前置きを含む）、宣言の片方だけ無い組を不一致と答える（Unity なしの単体）。**失敗経路:** 記録の 1 欄違い・宣言の片方だけ無い組は `OverlayBaseMismatch`、記録の無い root は `OverlayBaseMissing`、上書きの native の登録の失敗・root が 1 つでない・索引の作成の失敗はそれぞれの既存の値で拒まれ、巻き戻しの解除がどちらも成功した時は native の登録 0 件・gate の予約 0 件で終わる（解除は上書き → base）。巻き戻しの中で上書きの解除が失敗した時は base を解除せず予約を残し、次の登録の前の再試行で上書き → base の順に解除する。base の解除だけが失敗した時は、再試行は base だけを解除する。記録を持つ root を `Register`・`RegisterVerified` で単独で登録すると `InvalidRoot` で拒まれる。session は層を足す・外す member を持たない（code の検査） |
| AC2 | I-1 | 単体（Editor、adapter の test）。上書きの build は `BuildContentRoot` に base の記録（5 欄）・上書きの contentSet・Unity の版を書き、`AssetDatabase` で読み戻した root で同じ値が読め、Editor の `ContentOverlayBaseIdentity` からの変換が往復で一致する。上書きでない build の root は、読み戻しても「記録なし」と読める（Unity の直列化は class の欄を null にしないので、宣言の `IsDeclared` と同じく必須の欄が空なら記録なしと読む property で見る）。coordinator は書式の違う digest の上書きの build を report の前に拒む。記録を持たない既存の root の索引の作成は変わらない |
| AC3 | I-2・S-3 | 単体（層 2 つの session、AC1 の fake）。全ての token を返して `CloseAsync` すると、log は `[base 登録, 上書き登録, 上書き解除, base 解除]` で、gate の予約は最後に 1 回返る。**失敗経路:** 上書きの層の token だけ 0 にして受付を止めても、base の層の token が残る間はどちらも解除しない（逆も同じ）。上書きの解除が失敗すると base は解除されず、予約は残り、close の再試行で上書き → base の順に解除される。base の解除が失敗すると上書きは解除済みのまま、再試行で base だけ解除される。`CompletePlayStop` は token が残っていても上書き → base の順に解除し、上書きの解除が失敗すると base を解除せず、次の登録の前の巻き戻しの再試行で上書き → base の順に解除する。Play 停止の後に止まっていた close が再開しても、各層の成功した解除は 1 回だけ。`BeginSynchronousShutdown` も同じ順。途中の時点も見る: 各失敗の直後と各再試行の直後に、fake の log（どの層を何回解除したか）、gate の予約の有無、session が closed か、保留中の再試行の有無をそれぞれ確かめる。保留中の再試行が失敗すると次の登録は native の登録を始めずに拒まれる（既存の挙動） |
| AC4 | I-2・I-4・I-5 | 単体（`ContentRevisionGateTests` に追記）。層 2 つの登録は 1 件の登録として予約され（OS の lease は層ごとに取って合成する）、2 つの path と identity のどちらへの削除の許可も拒まれ、予約を返した後は両方とも許可される。拒否の値の優先: いずれかの層と path が同じで identity が違えば `PathInUse`、それ以外で当たれば `RevisionBusy`。層 2 つの登録の後の別の登録は今までどおり拒まれる。**失敗経路:** 上書きの path か identity が base と同じ組は予約の前に拒まれる。2 つ目の path の OS の lease の取得に失敗すると、1 つ目を返して予約 0 件で終わる。1 件の登録の既存の test は変更なしで通る |
| AC5 | I-3 | 単体（層 2 つの session と実物の `AssetManagement`、fake の native、route）。route は層 2 つの session から供給元 2 つ（上書き → base の順、id は別）の policy を作り、`Provider` は base の供給元のまま。I-3 の解決の規則どおりに次が採られる: 上書きにある (key, kind, 表現) は上書きの候補（由来・根拠は K1、identity は上書きの contentSet・build identity・target・空の digest、事実に base の記録 4 欄・Unity の版・root schema）、base にだけある物は base の候補（S3-1 と同じ値）。表現の 2 方向: 上書きに `K` の `Full` だけ・base に `K` の `Whitebox` と `Full` → `Whitebox` の要求は base の `Whitebox`、`Full` の要求は上書き。上書きに `K` の `Whitebox` だけ・base に `K` の `Full` → `Full` の要求は base、`Whitebox` の要求は上書き。層をまたぐ fallback: 両層に `K` の `Whitebox` が無く両層に別の `Full` がある時、`Whitebox`・fallback `Full` の要求は上書きの `Full` の候補・record・content 入口に渡す表現で採られる（base の `Full` が採られれば不合格）。上書きに `K` の `Full` も無ければ、同じ要求は base の `Full` で採られる。fallback を指定しない要求は採られない（期待する拒否）。id による影: base に `(K, Object, Whitebox)`、上書きに同じ native id の `(K, Object, Full)` がある時、`K/Whitebox` の要求は base から採られず（base の供給元は `NotFound`、理由は id による影）、base の expected identity で pin しても採られない。`K/Whitebox`・fallback `Full` の要求は上書きの `Full` で採られる。同じ組で `IAssetManagement.LoadContentAssetAsync(K, Whitebox)` を直接呼ぶと `EntryMissing` で拒まれ、native の load は 0 回。record の `SourceBindingVerified` は true、probe に上書きの `NotFound` → base の採用の経緯が残る。cache: 両方の層から Object を載せて解放し cache に入れた後、実物の `AssetManagement` の close の退避（`EvictByPrefix` 1 回）で両方の層の entry が消える。**失敗経路:** 上書きにある物を base の expected identity で pin した要求は採られない。probe は上書きの `PinMismatch`（resolver は互換の規則より先に pin を見る）→ base の `NotFound`（上書きが選ばれた理由）で、要求は pin が未充足の失敗（`DecideFailureCode` の (1)）で終わる。上書きの候補の digest は空なので、上書きを pin で選ぶ要求は作れない（上書きの登録中に base の版を選べないことの帰結で、13 章が受け入れた制約）。base の供給元の候補を上書きの層の照合に、上書きの候補を base の照合に渡すと偽。上書きの候補は `ContentCompatibilityRule` で、§2.4 の上書きの分岐の表の各行に当たる事実・基準を 1 つずつ崩すと、その行の `ContentCompatibilityMismatch` の値で採られない（期待値は値で書く。offline の test を含む）。採った時の理由は `compat:Compatible:ContentOverlay`。1 層の session の route は今までどおり供給元 1 つ（S3-1 の test が変更なしで通る） |
| AC6 | S-1・S-2・S-3・I-2・I-3 | 実機（Content Directory の build を伴う EditMode の `[UnityTest]`。Play mode に入って読む）。合成の目的: Scene `Entry`（`Full` と `Whitebox` の両方の Scene が、Read/Write を有効にした texture `Tex` を material で直接参照）、Scene `Stay`（`Full`、`Tex` を参照しない）、Scene `Moved`、Object `Prop`（`Tex` を参照する material を持つ prefab）。手順: 登録の一覧が 0 件であることを見る → base を build・publish・install・検証する → fixture を残したまま `Tex` の pixel を変え、`Moved` を別の folder へ移し（`AssetDatabase.MoveAsset`）、新しい Scene `Added` を root に足し、編集の集合（`Tex`、`Moved` の新しい path、`Added`）で逆引きして上書きを build する → 登録の口で層 2 つを登録し、route の loader で読む。`Entry` の `Whitebox`・`Prop`・`Moved`・`Added` は上書きの版（`Tex` の pixel は編集後）で載り、record は上書きを指す。`Moved` の上書きと base の entry の Scene の id は比べて記録するが合否にしない（A3 の測定では移動の前後で同じ。同じなら「同じ id は上書きの版」、違えば「key で上書きの entry を採る」で、どちらも契約の内）。`Stay` は base を指す record で載る。全ての record の identity は、base なら install した manifest の値と digest、上書きなら上書きの build の identity と等しく、`SourceBindingVerified` は true。全てを離した後 `CloseContentDirectoryAsync` が `ResourcesInUse` を出さずに完了し、`ContentLoadManager.GetContentDirectories` が 0 件になる。観測窓（登録から close まで）の Error / Exception は 0 件。test の `Timeout` は build 2 回分にする（S3-1 の 360 秒を超えてよい）。照合の失敗経路は AC1 が持つ（root の記録は製品の API で書き換えられないため） |
| AC7 | I-5 | 既存の test と検査。`IAssetManagement`・`AssetManagement` の public の member と `AssetManagement.ContentDirectory.cs`、`IContentDirectoryBackend`、`ContentRequestLoader.cs`、`AbstractApplicationInitializer`、`PlayerContentConfiguration` は変わらない（diff）。次の参照が無い（`grep` と C の構造レビュー）: `Runtime/AssetManagement/` → `ContentResolution`、`Runtime/ContentResolution/Core/` と `ContentRequestLoader.cs` → `BuildContent`、`Runtime/BuildContent/` → `ContentResolution`（root の記録を互換の事実に変える処理は `Directory/` の供給元に置く）。既存の `ContentDirectorySessionTests`・`ContentRevisionGateTests`・`RegisteredDirectoryProviderTests`・`ContentRequestLoaderTests`・`RegisteredDirectoryRouteIntegrationTests`・`PurposeBuildIndexIntegrationTests`・`UnityContentDirectoryAdapterTests`・offline の test は変更なしで通る。例外は次だけで、どれも期待する結果を変えない: (1) `IContentDirectoryAdapter` を実装する test の fake 3 つ（`BuildContentFullModeTests.WritingAdapter`・`BuildContentProjectionTests.FailingAdapter`・`ContentOverlaySelectionTests.ManifestAdapter`）の `Build` の署名。(2) 上書きの test が使う digest の文字列（`BuildContentFullModeTests` の `digest-1` と `ContentOverlaySelectionTests.BaseIdentity`）を、書式の検査（AC2）に合わせて SHA-256 の形の値に替える。既存の internal の `ContentRevisionGate.Reserve(identity, target, path)` と `RegisteredDirectoryProvider(session)` の constructor は、今の署名と意味のまま残し（層 2 つの予約と層の指定は別の overload）、それらを呼ぶ既存の test は変えない。offline の C17（由来と根拠の表の一致）は変えず、上書きの組を含む表の一致は新しい test で見る |
| AC8 | 範囲 | Phase B の commit が変える file は、§3 の file とその `.meta`、§3 の test の folder の file、`docs/handoff/` の文書だけで、asmdef の `references`、`AssemblyInfo.cs` の friend、`SceneState`、`tools/Artifacts`、SampleGame の製品 code に変更が無い |

### 2.3 ここでは答えない問いと所有する後続

program と S3-1 が「S3-2」へ送った入力の引き取りと送り先を、ここに全て書く。

| 問い（出所） | 所有 |
|---|---|
| base → 上書きの登録の順・個数・時点・解除の順、重ねてよい組の登録時の照合、両方の root からの key の解決（S2-1・S2-2、13 章） | **本書**（I-1〜I-4、AC1〜AC6） |
| directory が 2 つの時に record がどちらを指すか（S3-1） | **本書**（I-3、AC5・AC6） |
| 移動・rename で id が変わった Scene の key の解決の実測（13 章「logical key の解決」） | **本書**（AC6 の `Moved`。A3 の測定で、移動では Scene の id は変わらなかった。rename は測っていない） |
| Domain Reload 有効の Play（S2-1・S2-2・S3-1 から移送） | **本書の A3**（§1 の測定。native の登録は Play を終えても残り、script の reload で消える）。Play の入口から Domain Reload 有効で Play する確認は S3-4。Play に入る時の reload が前の Play の残りを消さないと分かれば、再試行の方式を本書の Phase A の新しい revision で決め直す |
| resident cache の key の全経路（S3-1 の未確認） | **本書**（I-3 の経路 (6)、AC5）。A1 で `CacheKey` / `CachePrefix` の呼び出し元を `grep` で全て読んだ（`ContentDirectorySession.GetObjectKey`・`CachePrefix` と `AssetManagement.EvictContentDirectoryCache` だけ）。cache の内部（`AssetResidentCache`）は key を不透明な文字列として扱う |
| 13 章の登録契約が依拠する実測の 6000.6.9f1 での測り直し（Unity 更新） | **本書の A1**（§1「測り直しの結果」） |
| 上書きの base の選択に索引を使う接続（S2-3） | S4（base の目的 build を選ぶのは供給元が候補の一覧を持つ時で、本書は呼び手が渡す base を照合するだけ） |
| Editor の Play の結論を Player で確かめ直すか（S2-0b・S2-1） | 不要（上書きは Editor の Play だけ。13 章の契約）。Player の 1 directory の経路の非退行は §2.8 U4 |
| 上書きを Play に載せる入口、起動の設定の key と配線、照合の失敗を base だけの Play に縮退しないこと、上書きを重ねた時の SceneDirector の経路、実 Editor の Play での確認、上書きの build の呼び手が `ContentOverlayBaseIdentity` に install した base の値（receipt の `manifestSha256`）を渡すこと | S3-4（§2.5、§2.8 U2） |
| S3 の gate「Player の非退行」（S3-1 の配線と本書の session・gate の層の化を含む） | S4（§2.8 U4。program §8 の S4 の行の gate に足した） |
| 共有 asset の実体の重複を減らすこと | 対象外（program §5.3 の当面の方針） |
| 片方の directory の file が disk から消えた時、どちらの file を読むか（S2-0b） | S4（program の割り当てのまま） |

### 2.4 代替案の比較

**層の持ち方**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| L1 | 1 つの session が層（base と任意の上書き）を順のある列で所有する。`AssetManagement` は今までどおり session 1 つを install する | `AssetManagement`・`IContentDirectoryBackend` を変えない。token と解除の順を 1 か所で数えられ、I-2 の (1)(8) が構造で塞がる | **採る** |
| L2 | session を 2 つ作り、`AssetManagement` が 2 つ install する | `InstallContentDirectory` の 2 件目の拒否、load の入口、cache の退避、Play 停止、照合の全てに 2 つ目の分岐が要る | 採らない |
| L3 | 積み重ねの型が session 2 つを包んで `IContentDirectoryBackend` を実装し、base の session の自動解除を「close hold」の token で止める（A2 の代替構成の推奨） | `AssetManagement` を変えない点は L1 と同じ。token の数え方が 2 組のままで、hold の取り外しと巻き戻しの順を session の外で調停する。Release・Unload を発行元の session へ振り分ける判定が要る | 採らない（解除の順の所有者が 2 つになる。A2 のゲート M4 の指摘と逆向き） |

**照合の出所**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| C1 | 上書きの build が `BuildContentRoot` に base の記録を書き、登録が root を読んで base の検証済みの値と比べる | root の直列化に任意の欄が 1 つ増える（記録の無い root は今までどおり読める）。directory だけで照合が閉じ、上書きの単独の登録も root で拒める | **採る** |
| C2 | Runtime が directory の外の `reports/<identity>/preflight.json` を読む | Runtime が Editor の report の置き場と形を知る。directory を移すと照合できない | 採らない |
| C3 | 呼び手が上書きの base を申告し、登録はそれと base を比べる | 上書きが本当にその base から作られたかを確かめない（道 (b)） | 採らない |

**上書きの候補の由来**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| K1 | `ContentSourceKind.ContentOverlay` と `ContentAcceptanceBasis.RecordedBaseMatch`（名前は仮）を足し、resolver の固定表と規則に上書きの組を足す。identity は上書きの値、事実に base の記録 | public の enum の値と事実の予約 key の追加（S0-a・S1 の core の改訂）。表が 2 か所にある。record が上書きを読んだと正直に言う | **採る**（§2.8 U3） |
| K2 | 上書きの候補を `ContentDirectory` + `VerifiedDigest` と base の identity で申告し、事実に上書きを書く | core を変えない。record の主な欄が「digest で検証した base」と言い、上書きの版の内容と食い違う（道 (e)） | 採らない |
| K3 | 上書きを供給元にせず、base の供給元が層を区別しない | record がどちらを読んだかを言えない | 採らない |

**K1 の互換の規則（上書きの分岐）:** 由来と根拠の組が `ContentOverlay` + `RecordedBaseMatch` の候補について、規則は表の上から順に見て、最初に当たった値を返す。digest と identity の完全さは要求しない（`IncompleteIdentity` は上書きの候補に返さない。digest が空なのが正常）。事実は `Directory/` の供給元が root の記録から作る。

| 順 | 上書きの分岐の検査 | 返す値 |
|---|---|---|
| 1 | 由来と根拠の組が表に無い | `UnsupportedSource`（既存） |
| 2 | 要求の expected identity と 4 欄が一致しない（上書きの digest は空なので、pin した要求は採られない。resolver を通す要求では、resolver が規則の前に `PinMismatch` で落とすので、この行に届くのは規則を単独で呼ぶ時だけ） | `ExpectedIdentityMismatch`（既存） |
| 3 | base の記録の事実（下の 4 つの key のどれか）が無い・空白だけ | **`OverlayBaseFactMissing`（新規）**。欠けた key を理由の文に書く |
| 4 | 事実の base の contentSet が基準と違う | `ContentSetMismatch`（既存） |
| 5 | 基準が revision を固定していて、事実の base の revision が違う | `RevisionMismatch`（既存） |
| 6 | 事実の base の target、または候補の identity の target が基準と違う | `TargetMismatch`（既存。どちらかを理由の文に書く） |
| 7 | 事実の Unity の版（上書きの root に build した Editor が書いた値）が無い・基準（`ContentManifestValidation` の定数）と違う | `EngineVersionMissing` / `EngineVersionMismatch`（既存） |
| 8 | 事実の root schema（上書きの root の `SchemaVersion`）が無い・壊れている・基準と違う | `SchemaVersionMissing` / `SchemaVersionMalformed` / `SchemaVersionMismatch`（既存） |
| 9 | host の述語が偽（`ContentDirectory` と同じく当てる。宣言の不一致を見る host の判定が上書きで素通りしないため） | `HostPredicateRejected`（既存） |

- 採った時の理由は `compat:Compatible:ContentOverlay`（今の `nameof` の形）。
- `ContentSetMismatch`・`RevisionMismatch`・`TargetMismatch` の doc に「上書きの候補では記録した base の値を比べる」を 1 句足す（意味を広げるのはこの 3 値）。

**public の名前（契約。変えるなら Phase A へ戻す）:** `ContentSourceKind.ContentOverlay = 4`、`ContentAcceptanceBasis.RecordedBaseMatch = 3`、`ContentCompatibilityMismatch.OverlayBaseFactMissing`、`ContentCompatibilityFactKeys.OverlayBaseContentSet` / `OverlayBaseRevision` / `OverlayBaseTarget` / `OverlayBaseManifestSha256`（値は `osm.compat.overlayBaseContentSet` などの既存の形）、`ContentDirectoryFailureCode.OverlayBaseMismatch` / `OverlayBaseMissing`、`BuildContentRoot.OverlayBase`（型 `BuildContentOverlayBase`。`Declaration` / `BuildContentDeclaration` と同じ並び）、供給元の id `registered-content-overlay`。どの enum も既存の値を動かさず末尾に足す。internal の型・member の名前は B が決めて §6 に記録する。

**層の解決の粒度**

| 案 | 内容 | 推奨 |
|---|---|---|
| G1 | key（と kind）の単位。上書きの root に key があれば表現を問わず上書き（初稿） | 採らない。上書きに一方の表現だけがある時、編集と関係の無い他方の表現の要求が失敗する（A2 の反証） |
| G2 | (key, kind, 実効の表現) の単位。要求の表現を両層で引いてから、`Full` への fallback を両層で引く（I-3） | **採る** |

### 2.5 スライス分割の判断

- 契機: program の S3-2 の行には、独立して失敗しうる未知が 2 群ある。(1) Runtime の登録・解決・解除と record（Unity の登録の挙動、session・gate の基数、core の由来の値）。(2) 上書きを Play に載せる入口（どの編集から上書きを作るか、build の時点、起動の設定と配線、Play の入口の UX、Domain Reload 有効の Play）。(2) は (1) が無いと確かめられず、(1) は (2) が無くても実機の test で確かめられる。(2) には利用者の入力（編集の集合をどう得るか）の設計が要り、program §4.3 の Play の入口の要求に当たる。
- 判断: **分ける。** owner（root）が 2026-11-18 に承認した（§2.8 U2）。本書は (1)。(2) は S3-4（仮称 `OVERLAY_PLAY_ENTRY`）として program §8 に行を足し、本書の Phase D の後に A1 を始める。program §8 の行は本書の A3 の commit で改めた。
- 本書の中: 分けない。照合・層・解除の順・供給元・record は、1 つの問いに要る。単体で閉じる部分（AC1〜AC5）と実機（AC6）は別々に判定する。

### 2.6 判定定義と停止規則

- 判定定義:
  - **GO:** AC1〜AC8 が全て成り立ち、判定必須テスト（§5）が通る。
  - **NO-GO（owner へ返す）:** (i) AC6 で、製品の build 経路で作った base と上書きを契約の順に登録しても、上書きの版が出ない、または上書きの解除が base の物を壊さずに済む順が無い（6000.6.9f1 の測り直しと違う）。何がどの段で違ったかを記録して返す。(ii) I-1〜I-5 を、`AssetManagement` か `IContentDirectoryBackend` を変えずには満たせない。
  - それ以外の不成立は通常の差し戻しで B へ戻す。
  - スパイクではないので CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の `references` と `InternalsVisibleTo` を足さない（足す必要が出たら Phase A へ戻す）。
- アセットは `IAssetManagement` 経由で、`AssetOwner` で寿命を宣言する。`IAssetManagement` に member を足さない。
- `SceneState` の 14 値を変えない。
- 公開 API のログは `ILogger<T>`。`ZLogger*` 型を公開面へ出さない。
- 新規・編集する Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。Editor の code を Runtime の assembly に置かない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない。
- repo 相対 path は `tools/path-budget.json` の予算に収める。
- `Runtime/ContentResolution/Core/` は Unity と `BuildContent` を参照しない（`tools/ContentResolutionOfflineTests` が source を直接 compile する）。由来の値を足す時は、`ContentResolver.RequiredBasis` と `ContentCompatibilityRule.EvaluateGeneric` の 2 か所を同時に直し、offline の test が一致を確かめる。
- loader は AssetManagement の public だけを使い、同じ assembly の internal を呼ばない。照合と content 入口の呼び出しの間に `await` を置かない（S3-1 の契約）。
- 封じ（`SealAddressables`）、protected config の照合、起動の fail-closed、`CompleteContentDirectoryPlayStop` の同期の順序（`UniTask.GetResult` で待たない、native の Unload を待たない）を変えない。
- `UnityContentDirectoryBackend`（native の port の実装）を変えない。層ごとに instance を 1 つ持ち、登録の口は test が 2 つの instance を注入できる形にする。
- 機械向けの記録を git に入れない。

### 2.8 owner の判断を求める点（未決事項）

担当の推奨を付ける。全て決定済み（U1・U2 は A2 の前、U3〜U7 は A3 で owner（root）が 2026-11-18 に推奨どおりと決めた）。

- **U1 目的と利用者の文言（§1）。** 決定: 承認（owner、2026-11-18）。
- **U2 分割（§2.5）。** 決定: S3-4 に分ける（owner、2026-11-18）。
- **U3 上書きの由来を core に足す（§2.4 K1 と上書きの分岐）。** 決定: 推奨どおり（owner、2026-11-18）。推奨: K1。`ContentSourceKind` と `ContentAcceptanceBasis` に 1 値ずつ、`ContentCompatibilityFactKeys` に base の 4 欄の予約 key を足す（S0-a・S1 で凍結した core の契約の追加）。
- **U4 Player の build を判定必須にしない。** 決定: 推奨どおり（owner、2026-11-18）。推奨: しない。上書きは Player で使わず、Player の起動経路は変えない。session と gate の 1 層の挙動は既存の test が変更なしで通ることで見る（AC7）。program の S3 の gate「Player の非退行」の実機は S4（`LOCAL_LAN_PROVIDER`。要求の時点の install と登録で Player の起動経路に触れうる最初の行）が引き取り、S3-1 の配線と本書の session・gate の層の化を含めて確かめる（program §8 の S4 の行に書いた）。S4 までの間、Player は層の列を通る 1 層の経路を実機で確かめないまま develop に載る。owner はこの期間を受け入れた（2026-11-18）。
- **U5 上書きの記録を root に書く（§2.4 C1）。** 決定: 推奨どおり（owner、2026-11-18）。推奨: C1。root の直列化に任意の欄を足す。root の schema の版は上げない（記録の無い root は今までどおり読める。publish は上書きを拒むので、記録を知らない古い runtime が記録つきの root を単独の directory として読む経路も無い）。古い上書き（記録が無い）は作り直す。
- **U6 新しい失敗の値。** 決定: 推奨どおり（owner、2026-11-18）。推奨: `ContentDirectoryFailureCode`（public の enum、`Runtime/AssetBuildContracts/`）に `OverlayBaseMismatch` と `OverlayBaseMissing` を足す（目的レビューの「理由を区別して返す」。`InvalidConfiguration` と message にすると、S3-4 の「縮退せずに理由を示す」が message の解釈に依る）。互換の規則の値は `OverlayBaseFactMissing` の 1 つだけ足し、他は既存の値を使う（§2.4 の表）。
- **U7 13 章の「logical key の解決」の粒度（§2.4 G2）。** 決定: 推奨どおり（owner、2026-11-18）。推奨: 「同じ key・kind・実効の表現」に書き改める。13 章の文は S2-2 で定めた設計上の規則で、実測に依らない。

## 3. 責務マップ

行数は base `b0bc9997` の `wc -l`。増分は見込み。public の名前は §2.4 のとおりで、変えるなら Phase A へ戻す。internal の型・member の名前は仮で、B が決めて §6 に記録する（責務・置き場・file は変えない）。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/BuildContent/BuildContentRoot.cs` | directory の root の直列化 | 上書きの記録の任意の欄 | 値 | 下の記録の型 | public の property 1 つ（記録が無ければ null を返す）と、`Initialize` の任意引数 | AC2 | 97 → 115 |
| `Runtime/BuildContent/BuildContentOverlayBase.cs`（新規） | 上書きの記録の値（base の 5 欄、上書きの contentSet、Unity の版）と、base の検証済みの値との照合の純粋な関数 | 新しい値と照合の規則 | 値 | `VerifiedInstalledRevision`（値の比較だけ） | public の `[Serializable]` の型（root が public なので）。照合は internal | AC1（Unity なし）・AC2 | 0 → 90 |
| `Runtime/AssetBuildContracts/ContentDirectoryException.cs` | 失敗の値 | `OverlayBaseMismatch`・`OverlayBaseMissing`（§2.8 U6） | enum | なし | public の enum の値の追加 | AC1 | 既存 + 2 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs`、`BuildContentCoordinator.cs`、`ContentOverlaySelection.cs` | 上書きの build | adapter が root に記録を書く（Editor の型からの変換はここだけ）。coordinator は request の上書きの base を adapter へ渡し（internal の `IContentDirectoryAdapter.Build` の引数。test の fake の実装も直す）、`ValidateOverlay` で digest の書式を検査する。`ContentOverlayBaseIdentity` の doc を「transport manifest の SHA-256」に改める | 既存 | 既存 | internal と doc | AC2 | 245 → 260、既存 + 8、235 → 236 |
| `Runtime/BuildContent/ContentDirectoryLayers.cs`（新規） | session が所有する層の列: 層ごとの path・identity・native の handle・索引、I-3 の解決の規則（id による影を含む）、層の部分を持つ cache の key、登録の順と、逆順の解除（どこまで解除したかを保持する 1 つの method）、巻き戻しの再試行（今の session の static の `_pendingRollback` をここへ移し、層の列ごと 1 つの処理として持つ） | 新しい責務（順のある層の列の寿命） | session と同じ寿命 | `ContentDirectoryIndex`、`IContentNativeDirectory`、`BuildContentOverlayBase` | internal | AC1・AC3・AC5 | 0 → 220 |
| `Runtime/BuildContent/ContentDirectorySession.cs` | 登録した directory の token・発行済みの処理・後始末 | 層の列を使う。上書きを重ねた登録の口を足す。記録を持つ root の単独の登録を拒む。解除の 3 経路は層の列の method を呼ぶだけにする | 既存 | 上の層の列 | internal のまま | AC1・AC3・AC7 | 498 → 480 |
| `Runtime/BuildContent/ContentRevisionGate.cs` | 登録・読み・削除の調停 | 1 件の登録が層 2 つの (identity, path) と OS の lease を持てる。削除の許可の拒否の値の優先 | static | 既存 | public の面は変えない（層 2 つの予約は internal の overload。既存の `Reserve(identity, target, path)` は残す） | AC4 | 212 → 250 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | root の索引 | 層の解決に要る「表現を完全一致で引く」口と、層の部分を持つ cache の key | 既存 | 既存 | internal | AC5 | 159 → 175 |
| `Runtime/ContentResolution/Core/ContentSourceKind.cs`、`ContentAcceptanceBasis.cs`、`ContentCompatibilityFactKeys.cs` | 由来・受け入れ根拠・事実の予約 key | 上書きの値と base の 4 欄の key（§2.4 K1） | enum・定数 | なし | public の enum の値と定数の追加 | AC5 | 26 → 32、22 → 28、既存 + 12 |
| `Runtime/ContentResolution/Core/ContentResolver.cs`、`ContentCompatibilityRule.cs`、`ContentCompatibilityMismatch.cs` | 固定表と互換の判定 | 上書きの組と、§2.4 の上書きの分岐。`OverlayBaseFactMissing` を足し、3 値の doc に 1 句足す | static な処理・enum | なし | public の enum の値 1 つの追加 | AC5（offline を含む） | 325 → 330、228 → 275、既存 + 4 |
| `Runtime/ContentResolution/Directory/RegisteredDirectoryProvider.cs` | 層 1 つの供給元 | 層と id を受ける（既存の `(session)` の constructor は base の層・既定の id のまま残す）。解決が自分の層を選んだ時だけ候補を返す。上書きの候補の値と、root の記録から作る事実。照合で層を比べる | session と同じ | 既存 | internal | AC5 | 130 → 200 |
| `Runtime/ContentResolution/Directory/RegisteredDirectoryRoute.cs` | 供給元・基準・policy・loader の組み立て | 層 2 つの session には供給元 2 つ（上書き → base）の policy。`Provider` は base の意味のまま、上書きの供給元は別の property | static | 既存 | internal | AC5 | 102 → 130 |
| test（単体は `OneStarMaker/Tests/Editor/Build/`・`OneStarMaker/Tests/BuildContent/`・`OneStarMaker/Tests/ContentResolution/`、offline は `tools/ContentResolutionOfflineTests/`、実機は `SampleGame/Tests/Editor/Build/`） | — | — | — | — | — | AC1〜AC6 | 新規と追記 |

- 変えない file: `IAssetManagement.cs`、`AssetManagement.ContentDirectory.cs`（`IsInstalledContentDirectory` は session の参照で答え、退避は session の共通の前置き 1 つで両方の層に届く）、`Internal/IContentDirectoryBackend.cs`、`UnityContentDirectoryBackend.cs`、`ContentRequestLoader.cs`（供給元の id が別なので照合の相手を取り違えない）、`Bootstrap/AbstractApplicationInitializer.cs`（route の `Provider` が base の意味のまま）、`BuildContent/PlayerContentConfiguration.cs`、`Distribution/` の全て、SceneSystem、SampleGame の製品 code。
- 行数の警報:
  - `ContentDirectorySession.cs` は既に 498 行。層の列の責務（解決・cache の key・登録と逆順の解除・巻き戻しの再試行）を `ContentDirectoryLayers.cs` に移し、session は token と処理の数え方と既存の load の入口に留める。partial にも別の型にも分けない（置き場は今決める。行数で置き場を変えない）。上書きの登録の口、記録を持つ root の拒否、選ばれた層の native への振り分けを足して 500 行を超えたら、B は §6 に `非分割妥当` と根拠（token と処理の数え方という 1 つの変更理由）を書く。
  - `ContentDirectoryLayers.cs` は 1 つの責務（順のある層の列の寿命）で 220 行の見込み。照合の規則は I/O が要らず変わる理由が違うので、記録の型の file に置く。
- 配置の理由:
  - 層は native の登録と root を所有する BuildSystem の Runtime（`Runtime/BuildContent/`）に置く。登録・解除の順は native の handle の寿命の問題で、session と同じ所有者が持つ。`AssetManagement` は session 1 つを install するまま（L1）。
  - 上書きの記録は root（`BuildContentRoot`）に持たせる（C1）。今の宣言（`BuildContentDeclaration`）と同じ置き方。
  - 供給元と route は S3-1 と同じく連携部分（`Runtime/ContentResolution/Directory/`）。記録を互換の事実に変える処理もここに置き、`Runtime/BuildContent/` → `ContentResolution` の辺を作らない。
  - asmdef は全て既存の中で、参照を足さない。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (1) 記録の型と照合の関数、root の欄、adapter と coordinator（AC1 の照合の単体・AC2）。(2) 層の列と session（AC1・AC3）。(3) gate（AC4）。(4) core の由来の値と規則（AC5 の offline）。(5) 供給元と route（AC5）。(6) 実機の test（AC6）。
- Phase B から Phase A へ差し戻す条件: asmdef の参照か friend が要る。`IAssetManagement`・`AssetManagement.ContentDirectory.cs`・`IContentDirectoryBackend`・`UnityContentDirectoryBackend` を変える必要がある。§3 に無い public の型・member が要る。起動の配線を変える必要がある。AC6 で Unity の挙動が 13 章の契約と違う（§2.6 NO-GO (i)）。
- 対象外を維持する方法: `AbstractApplicationInitializer`、`PlayerContentConfiguration`、`Distribution/`、SceneSystem、SampleGame の製品 code、`ContentTransportPublisher` を変えない。起動の設定の key、menu、Play の入口を作らない。層を後から足す口を作らない。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC5（Unity の build を伴わない EditMode）と、AC5 の core の部分の offline（`dotnet run --project tools/ContentResolutionOfflineTests`。全行 `PASS` で exit 0）。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.Editor.Build.ContentDirectorySessionTests`、`OneStarMaker.Tests.BuildContent.ContentRevisionGateTests`、`OneStarMaker.Tests.Editor.Build.RegisteredDirectoryProviderTests`、`OneStarMaker.Tests.Editor.Build.UnityContentDirectoryAdapterTests`、`OneStarMaker.Tests.Editor.Build.BuildContentFullModeTests`、`OneStarMaker.Tests.ContentResolution`、AC1〜AC5 の新しい test の class（B が名前を決め、§6 に記録する）。
- 判定必須テスト（GO 候補 head）: 上の起点 filter、offline の test、AC6 の実機の test、S3-1 の `RegisteredDirectoryRouteIntegrationTests`、最終の全 EditMode 回帰（空 filter。合否は「XML の failed 0 と root result `Passed`」で読む。`-nographics` の VFX 25 件の Inconclusive で `step.json` が `failed` になるため）。
- 全 EditMode 回帰の適用除外: なし。**Player の build は判定必須にしない**（§2.8 U4。代替証拠は AC7）。
- 統合・Unity テスト: AC6 は Content Directory の build を 2 回（base と上書き）伴う。他の Unity が同時に動くと build を伴う test が失敗した既知の事例がある（program §8）ので、判定 C は他の Unity を止めて走らせる。build を伴う batch の後の Unity の終了 stall は既知（`tools/Harness/README.md`。本書の A1 の測り直しでも 1 回起きた）で、XML の完成で判定する。coordinator の staging の `Directory.Move` が access denied で失敗する既知の不安定（program §8）に当たった時は、同じ head で走らせ直してよく、その事実を §7 に記録する。
- 操作・実行時・目視条件の検証経路: 対象なし（起動・Play の入口を変えない。実機の確認は AC6 の `[UnityTest]` で閉じる）。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: AC6 の build・publish・install・Play mode の読みは、S3-1 の `RegisteredDirectoryRouteIntegrationTests` と S2-3 の `PurposeBuildIndexIntegrationTests` で成立した経路で、上書きの build は S2-2 の製品の経路である。A2 の反証が、上書きの記録の値（install した値と `ContentDeliveryFiles.Sha256(manifest)`）、`Added` の逆引き、`ContentLoadManager.GetContentDirectories` の読みが製品の API で作れることを code で確かめた。EditMode の `[UnityTest]` の中で fixture の Scene を移動（`AssetDatabase.MoveAsset`）した後の逆引きと上書きの build は、A3 の測定（§1）で成り立った。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（test の XML を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC7 の 3 つの `grep`。
- A0/A1 主担当・モデル・ベンダー: AI（Claude Code、Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: xAI（Grok、cursor-agent。Grok 系のモデルを明示する）。Phase A は Anthropic 系（A1・A2・外部 1 巡目）と OpenAI 系（外部 2 巡目）を使った。B・C も Grok を使わない。
- 独立性の強化条件を満たせない場合の理由: 満たす見込み（Grok は Phase A〜C に関与しない）。B か C が Grok を使えば `独立性制約あり` と記録する。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-18、同一 PC）。入力版は固定 base `b0bc9997` の clean な detached checkout と、HANDOFF copy（sha256 `ab5dcae7efe09ae9f4fc60d2b6a088e2d68c569df17dce85f4340a804ed491de`。A1 初稿に 6000.6.9f1 の測り直しの結果を足した版）。4 件とも Claude Code の subagent（Anthropic 系。主担当と同じ系列）。互いの指摘は渡していない。
  - アーキテクチャゲート: HEAD・clean・hash の照合は一致。結論は blocker 0、major 4、minor 4。L1・gate の基数・C1・K1 は妥当とした。
  - 不変条件の反証と AC の読み違い: 照合は一致。結論は blocker 0、major 6、minor 9。I-1・I-4・I-5 は破れず、I-2 は Play 停止の経路で本文と矛盾し、I-3 は key の単位の解決が 1 directory の時に読めた要求を後退させた。
  - 目的レビュー: §1 の目的と利用者の本文だけを渡した別の担当（道具を使わない）。入力の sha256 `e47b1cec69c7142ac335f8c351c2a4b888be9e79bc11c6b71e653c5c08d96a31`。
  - A0 だけからの代替構成（risk `high` の追加分）: 目的レビューの回答を記録した後、同じ担当に A0 の packet（sha256 `a296ee7d2694b21741f079f0126824e601e9176741b9387a7e7a9fa448eae430`。目的、利用者、対象外、現況、測り直し、問い、13 章の登録契約の原文、制約）と固定 base を渡して続けた。初稿は読ませていない。目的レビューのやり直しに使える未関与の担当は残してある。
- A3 の分類（担当の案。owner の承認待ち。目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは無い）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | 反証 major 1 | key の単位の解決は、上書きに一方の表現だけがある時に他方の表現の要求を失敗させる | 採用 | 解決を (key, kind, 実効の表現) の単位にした（I-3、§2.4 G2、AC5 の 2 方向、§2.8 U7） |
  | ゲート M1、反証 major 2 | cache の退避は前置き 1 つ。層ごとの前置きでは変えない 2 file と食い違う | 採用 | 共通の前置きの後に層の部分を続ける形（I-3、§3、AC5 は実物の退避で見る） |
  | 反証 major 3 | I-2 の無条件の文と Play 停止の経路が矛盾する | 採用 | Play 停止の例外を I-2 に書いた（順と base を解除しない条件は守る） |
  | 反証 major 4、minor 11 | 登録の途中の巻き戻しの失敗の AC が無い。各層の解除は 1 回 | 採用 | AC1 の失敗経路、AC3 の「1 回だけ」、I-2 の経路 (9) |
  | 反証 major 5 | 実機で root を差し替える失敗経路は製品の API でできない | 採用 | 照合の失敗経路を AC1 に移した（AC6） |
  | ゲート M2、反証 major 6、minor 9 | 上書きの contentSet・engine・schema の事実の出所、規則の比較項目、予約 key の file、host の述語、上書きの target | 採用 | 記録に上書きの contentSet と Unity の版、§2.4 の上書きの分岐の 6 項目、`ContentCompatibilityFactKeys.cs` を §3 に、host の述語を当てる、target は base の値（UC-1） |
  | ゲート M3 | 照合を純粋な関数に分けて Unity なしで網羅する | 採用 | `BuildContentOverlayBase.cs`（§3）、AC1 |
  | ゲート M4 | 逆順の解除と途中の状態の所有者を 1 つに。partial は行数のための分割に読める | 採用 | 層の列の 1 つの method（I-2、§3）。partial にしない判断と代替を §3 に書いた |
  | ゲート m1 | 失敗の値・`Initialize`・route の `Provider`・供給元の id・native の port の注入 | 採用 | §2.8 U6、§3、§2.7、I-5 |
  | ゲート m2、反証 minor 14 | Unity の直列化は class の欄を null にしない | 採用 | AC2 の「記録なし」の読み方 |
  | ゲート m3、反証 minor 8、代替構成 | `manifestDigest` は「呼び手が決めた文字列」で、照合の前提にならない。5 欄の重複 | 採用 | digest を transport manifest の SHA-256 の小文字 16 進と定め、coordinator が検査する（I-1、AC2、§3）。変換は adapter の 1 か所。AC7 の例外（`digest-1` の test の値） |
  | ゲート m4 | 上書きの層の失敗の診断が base を名指しする | 採用（既知の制約として） | I-3 の探索した境界 |
  | 反証 minor 7 | 供給元の id の値 | 採用 | I-3 |
  | 反証 minor 10 | gate の拒否の値の優先、2 つ目の lease の失敗 | 採用 | AC4 |
  | 反証 minor 12 | AC6 の fixture の表現・Read/Write・`Moved` の id の見方・0 件の基準の時点・fixture を残す | 採用 | AC6 |
  | 反証 minor 13 | 照合が保証する範囲 | 採用 | 道 (b) |
  | 反証 minor 15 | partial の file 名が AC8 の範囲に無い | 解消 | partial にしない（§3） |
  | 反証の結論 | I-1・I-4・I-5 は破れなかった | 記録 | — |
  | 目的レビュー | 保証すべきこと 11 件 | 初稿にある・採用 | 照合・順・個数・逆順・半端な状態にしない・優先規則・record・所有・上書きなしは不変は I-1〜I-5 と S-1〜S-3。(9) 保持中の解除は `ResourcesInUse` と I-2（明示 close）と Play 停止の例外で 1 つに決めた。(11) 供給元を足す境界は、層ごとの供給元と policy の順で足りる（hook を作らない） |
  | 目的レビュー | 作らないべきもの 10 件 | 初稿と一致・一部不採用 | (7)「build pipeline を変えない」は不採用: 照合の材料を directory に持たせるには、上書きの build が root に記録を書く最小の変更が要る（C1。逆引きと差分の作り方は変えない）。他は対象外と §4 に同じ |
  | 代替構成 | 積み重ねの型が session 2 つを包む（close hold） | 不採用 | §2.4 L3 |
  | 代替構成 | `HasStartedOperations` で load の後の登録を拒む | 不採用 | 層は session の作成の時だけ決まり、後から足す口が無い（I-4）ので要らない |
  | 代替構成 | 上書きの build identity を directory の名前から取る | 不採用 | 呼び手が build の結果の identity を渡し、索引の作成が root の identity と照らす（今の `Register` と同じ） |
  | 代替構成 | 記録に上書きの contentSet と Unity の版・root schema を事実に | 採用 | §2.4 の上書きの分岐 |
  | 代替構成の検証 | 解除の順の逆転と、pin による由来の嘘を最小の test で見る | 採用 | AC3、AC5 の失敗経路 |

- 外部計画レビュー 1 巡目（2026-11-18、Draft PR #200 の review と行コメント。担当は Claude（Anthropic 系の最上位のモデル。owner が依頼）。A1・A2 と同じ系列なので、系列の独立したレビューには数えない）。対象は統合案の commit。結論は、設計の本体（L1・C1・K1・G2、I-1〜I-5、AC1〜AC5）に破れは見つからず、凍結の前に直すのは GO が何を言うかの境界、というもの。採否（担当の案。owner の承認待ち）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | PR 状態ブロックの `引き継ぐ commit` が実在しない SHA | 採用（担当の誤り） | 状態ブロックを実在の head の SHA に直した |
  | I-2 の再試行は static で domain reload で消える。Domain Reload 有効が S3-4 へ束ねて送られている。推奨は凍結前に 1 回測る | 採用 | §1「A3 の追加の測定」で測った。I-2 の探索した境界に結果と、static の再試行でよい理由と未確認を書いた。§2.3 で独立の行にした |
  | 上書きの分岐が返す `ContentCompatibilityMismatch` の値が未定。AC5 の失敗経路を値で書く | 採用 | §2.4 の表（新しい値は `OverlayBaseFactMissing` の 1 つ）、§3 に file、AC5 |
  | `Moved` は未測定の事実（移動で id が変わるか）を合否に入れている。不成立の扱いが凍結されない。推奨は凍結前に 1 回測る | 採用 | §1 で測った（id は変わらず、逆引きと上書きの build は成り立った）。id の異同は合否にしない（AC6）。§5 の「初回確認は Phase C」を消した |
  | Player の非退行の送り先に名前が無い。推奨は S4 | 採用 | §2.3、U4、program §8 の S4 の行 |
  | SceneDirector の経路が受け入れ条件にも移送にも無い | 採用 | §2.3 の S3-4 の行に明記 |
  | harvest の文言（強制は internal の口で入口は未配線、確かめた設定、送り先の訂正） | 採用 | §0 の harvest to |
  | public の名前を A3 で決める | 採用 | §2.4「public の名前」、§3 の冒頭 |
  | static の置き場を行数で決めない | 採用 | §3（`ContentDirectoryLayers` に置く。500 行を超えたら `非分割妥当` と根拠） |
  | U5 の理由に「publish が上書きを拒むので古い runtime が読む経路が無い」 | 採用 | U5 |
  | U6 の理由（message の解釈に依らない） | 採用 | U6 |
  | 凍結の前に別の系列で I-2 と K1 の分岐を見せる | 保留（owner が決める） | 下の「独立性」 |

- 担当の見直し（外部計画レビューの後、Phase C で突っ込まれうる点を code と突き合わせた）:
  - AC7 の「既存の test は変更なしで通る」は、`IContentDirectoryAdapter` の署名を変えると fake 3 つ、digest の書式を検査すると上書きの test の値 2 か所が変わる。例外として列挙した（AC7）。gate の `Reserve` と供給元の constructor は、既存の署名を残すと書いた（AC7・§3）。
  - 上書きの候補の load の経路を `ContentDirectory` と明記した（loader は他の経路の候補を `LoadPathUnsupported` で拒むので、書かないと loader を変える実装になりうる。I-3）。
  - 上書きの root に書く Unity の版は `Application.unityVersion`、基準は `ContentManifestValidation.SupportedUnityVersion`（今は `6000.6.9f1` で一致する）。両者が食い違う Editor では AC6 の上書きの候補が `EngineVersionMismatch` になる。これは規則の意図どおりで、Unity の版を上げる PR が literal を揃える既存の運用に従う。
  - AC6 は build 2 回を伴うので、test の `Timeout` を S3-1 の 360 秒より長くしてよいと書いた。
  - 実測の事実の「Scene の id は path から作られ」の訂正を harvest に入れた。
- 外部計画レビュー 2 巡目（2026-11-18、別の系列。担当は Codex CLI（OpenAI、`gpt-5.6-luna`、effort `medium`。owner が依頼）。入力は commit `91d2c8c6` の HANDOFF（sha256 `ec1e426ac3a1148c5db5d64cbc2df78b0f7ca19b81a58db24d7f02bb06d77469`、照合は一致）と固定 base の clean な checkout（照合は一致）。重点は I-2 と K1 の分岐）。結論は blocker 2・major 2。採否（担当の案。owner の承認待ち）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | blocker: Play 停止の解除の失敗の後、static の再試行は native と予約だけを解放し、元の session は closed にならず、後の close が二重に解除しうる。`??=` は 2 件目の失敗を失う | 採用（重大度は major と読む） | 1 層の今の code の読みとしては正しい。進行中の `CloseContentDirectoryAsync` は session を局所の変数に持つので、`AssetManagement` が session を手放すことは安全の根拠にならない（3 巡目の指摘）。安全の根拠は、層の列の再試行と session が同じ状態を共有し、どちらが先でも解除済みの層を呼ばず、全て済んだ時に closed と確定すること（I-2 の塞ぎ方に書いた）と、gate が登録を 1 件しか持たず次の登録が保留中の再試行を先に走らせて失敗すれば拒むので 2 件目の保留が作れないことである |
  | major: 既存の resolver は規則の前に pin を見るので、base で pin した要求で上書きの候補は `PinMismatch` になる。上書きだけ記録した base で pin を照らす規則を足す | 一部採用 | 挙動の読みは正しく（`PinMismatch` は探索を止めず次の供給元へ進む）、AC5 の失敗経路の probe と終わり方を書き直した。上書きを base の identity で pin に合格させる規則は不採用: record が「pin した base の版」と言いながら上書きの版を読むことになり、I-3 に反する |
  | major: AC1・AC3 が巻き戻しの途中の状態（直後の保留、一部の成功、再 close、2 件以上の保留）を見ない | 採用 | AC3 に途中の時点の 4 つの観測と、保留中の再試行が失敗した時の次の登録の拒否を足した |
  | blocker: U3〜U7 が owner の未決のまま | 記録 | 凍結の手順そのもの（owner が A3 で決める）。凍結記録に転記する |

- 外部計画レビュー 3 巡目（2026-11-18、Codex CLI（OpenAI、`gpt-6-astra`、effort `high`。risk `high` の計画なので上位のモデルで 1 巡追加した。owner が依頼）。入力は commit `49681e37` の HANDOFF（sha256 `7d3d9fedae4291777a3910dd18cf9095e3a1fc2e8e73711bf09b74423c70548c`、照合は一致）と固定 base の clean な checkout（照合は一致））。結論は major 2。I-2 は共有状態の設計を前提にすると破れる操作の列を作れず、K1 の固定表・空の digest による pin の不一致・修復を `VerifiedDigest` に限ること・load の経路は互いに整合している、とした。採否（担当の案。owner の承認待ち）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | major: 同じ native の id が base と上書きの別の (key, kind, 表現) に対応する時、G2 は base の entry を選ぶが native は上書きの版を読み、base の pin と record が嘘になる（依存ではなく要求した物自身の差し替わり） | 採用 | 登録を拒む案ではなく、上書きの root にある id を持つ base の entry を base の層から除く（id による影。13 章の「上書きにある id の物の base の版を選べない」と同じ向き）。I-3 の規則と経路 (8)、AC5 |
  | major: AC5・AC6 は完全一致だけで、層をまたぐ `Full` の fallback の順（上書き → base）を確かめない | 採用 | AC5 に、両層に別の `Full` がある組と、上書きに `Full` が無い組と、fallback なしの拒否を足した |
  | 2 巡目の指摘 1 の分類の理由（`AssetManagement` が session を手放す）は不十分で、進行中の close は session を局所の変数に持つ | 採用 | 2 巡目の表の理由を、共有状態と gate の基数に書き直した |
  | Domain Reload: script の reload と、Play に入る時の reload が前の Play の残りを消すかは別。本書の結論を Domain Reload 無効の確認の範囲に限るなら blocker にしない | 採用 | 下の「確かめた範囲」 |

- 確かめた範囲: 本書の GO が言うのは、Domain Reload 無効の Play での登録・解決・解除である。Domain Reload 有効の Play は、§1 の測定（native の登録は Play を終えても残り、script の reload で消える）までを本書が持ち、Play に入る時の reload が残りを消すかと、Play の入口からの確認は S3-4 が持つ（§2.3）。

- 独立性: A1、A2 の 4 件、外部計画レビュー 1 巡目は Anthropic 系、2・3 巡目は OpenAI 系（Codex。2 巡目と 3 巡目は同じ系列の別のモデル）である。A2 の後に G2・K1 の分岐・失敗の値・digest の定義が入り、A2 の再レビューは行っていない。2 巡目で I-2 と K1 の分岐を別の系列に見せた。C' は Phase A〜C に関与していない xAI（Grok、cursor-agent）に予約する。
- A2 の再レビューの要否: 行わない。解決の粒度（G2）、上書きの分岐の比較項目、失敗の値、digest の定義は、A2 の指摘に沿って曖昧さを決めたもので、新しい辺は増えていない。Draft PR の外部計画レビューで見てもらう。
- A3 統合担当・採否: 統合は AI（Claude Code、Anthropic）。§5.1 の各巡の採否案と §2.8 の U3〜U7 は、owner（root）が 2026-11-18 に推奨どおりと決めた。凍結は未了（owner の外部レビューの後）。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
