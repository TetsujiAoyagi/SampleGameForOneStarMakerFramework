# base と上書きの 2 つの Content Directory を Runtime で登録する（S3-2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S3-2）
- status: `C`（差し戻し 1 巡目の後の発見 C で C-1〜C-3 が閉じたことを確かめ、判定 C へ進めた。§7「発見 C（差し戻し 1 巡目の後）」）
- branch: `claude/runtime-overlay-registration`（PR の base は `develop`）
- implementation base commit: `b0bc9997`（`origin/develop` の head、2026-11-18。§1 の現況と §3 の行数はこの base）
- implementation head commit: 266e36bb2959eac4dd10aa63440af9ad8cf7e322
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: owner（root）、2026-11-19、Phase A revision 1。根拠: A2 の 4 件、外部計画レビュー 3 巡、設計レビュー 3 件、凍結前レビューの採否（§5.1）と §2.8 の U1〜U7 の決定を owner が確かめ、凍結前レビューの「設計は収束していて凍結してよい」を受けて凍結を決めた。program §5.3 の「上書きの方式を選ぶ理由と見直しの条件」「編集の種類ごとの扱い」も同じ時に確定した
- risk: `high`（program の行の値。所有者・寿命（native の登録と解除の順）、`ContentRevisionGate` の登録の基数、public の enum の値、BuildContentRoot の直列化の形を変える）
- owner: root（依頼者）
- created: 2026-11-18
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」（見出しの「Runtime の強制は未実装」を「強制は internal の登録の口にあり、入口は未配線（S3-4）」に改め、強制を確かめた設定（Domain Reload 無効、同じ PC と `Library`、loader の経路）と測り直しの結果を書く。「logical key の解決」の粒度（§2.8 U7）と、「測った設定」の Domain Reload 有効の送り先を直す。`RecordedBaseMatch` の保証の境界と 3 つの輪（§2.4）を書く。「logical key の解決」の項に id による影（上書きの root にある native の id を持つ base の entry は base から引けない）を書き、理由の句「移動・rename で id が変わった Scene のため」を A3 の測定（移動では id は変わらない）に合わせて直す。上書きのために足す public の値と面（§2.4「public の名前」）は、program §8 の S3-4 の縦断の実証まで暫定であり、方式を見直す時は外す、と書く）と「要求時の Content 解決」（上書きの供給元、record）、[18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md)「上書きの build と逆引き」（root の上書きの印）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（6000.6.9f1 の測り直し、移動した Scene の実測と「Scene の id は path から作られ」の訂正、native の登録と domain reload）、program §8・§10
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§4.1、§4.4、§5.3、§6、§8 の S3-2 と S3-1 からの移送）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成（使わない。発見 C は commit で固定した差分を読む）
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票（2026-11-19、差し戻し 1 巡目の後の発見 C から判定 C へ）:
  - 次の Phase・役割: 判定 C。GO 候補 head は上の implementation head（未解決の blocker は無い。§7「発見 C（差し戻し 1 巡目の後）」）。§5 の判定必須テスト（起点 filter、offline の test、AC6 の `OverlayRegistrationIntegrationTests`、S3-1 の `RegisteredDirectoryRouteIntegrationTests`、最終の全 EditMode 回帰（空 filter。合否は XML の failed 0 と root result `Passed` で読む））を GO 候補 head で走らせ、evidence bundle を生成して §2.6 で判定する。AC6 と全 EditMode 回帰は他の Unity を止めて走らせる（§5）。
  - 読む範囲: 既定（SKILL.md の表の Phase C の行。HANDOFF は §0〜§8）。
  - 凍結した Phase A: revision 1。引き継ぐ commit はこの票を含む commit で、その SHA は次の担当の起動指示（PR がある間は状態ブロックの `引き継ぐ commit`）で渡す。implementation head・base は上の欄。
  - 必要な権限: Unity のバッチテスト（`pwsh tools/run-tests.ps1` の sandbox 外の実行）、`pwsh` の起動（contract-audit・docs-audit・evidence の生成）、`dotnet`（offline の test）、`gh`（PR の timeline と状態ブロック）。
  - 停止規則: §2.6。
  - Evidence: Phase A snapshot・Phase B result snapshot は使わない。evidence bundle と C' blind bundle は判定 C が生成する（今は未生成）。
  - 引き渡さないもの: C' に §6 の差し戻しの経緯と §7 の所見を渡さない。

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
  - 探索した境界: `ContentDirectorySession.TryFindEntry` / `GetObjectKey` / `ResolveObject` / `ResolveScene`、`ContentDirectoryIndex` の解決と cache の key、`ContentEntryTable`、`RegisteredDirectoryProvider`、`RegisteredDirectoryRoute`、`ContentRequestLoader`、`ResolutionPolicy`（id の重複を拒む）、`ContentResolver` の固定表、`ContentCompatibilityRule`、`AssetManagement.LoadContentSceneAsync` / `LoadContentAssetAsync` / `EvictContentDirectoryCache`。未確認・既知の制約: 上書きの root に無い base の entry の依存と、上書きに root ではなく依存として入った同じ id の物（要求した物自身でありうる。例: Scene `S` だけを編集すると、`S` が参照する base の Object の root `Q` の複製が上書きに依存として入る。`Q` は上書きの root の entry ではないので影にならず、`Q` の要求は base の候補で通る。`LoadableObjectId` での load がどちらの directory の `Q` を返すかは未測定。同じ PC と `Library` では `Q` は編集していないので中身は同じで本書の GO に効かないが、別の PC の base では違いうる。S4 へ）のうち、上書きの directory に同じ id の file があるもの（base の root の entry から読んでも、依存は上書きの版になる。13 章が「上書きにある id の物の base の版を選べない」として受け入れた制約で、record は依存の由来を言わない）。上書きの層の load の失敗の例外は、session の `BuildIdentity`（base）を載せ、層は message で区別する（`IContentDirectoryBackend` の identity は 1 組のまま）。
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
| AC1 | I-1・I-2・I-4・I-5 | 単体（fake の native directory 2 つ（登録・解除を共通の log に書く）、temp の cache に実際に install した base、記録を差し替えられる fake の root）。登録の口は base の `VerifiedInstalledRevision` と上書きの path・identity を受け、記録が一致する時だけ層 2 つの session を返す。native の登録は base → 上書きの順に 1 回ずつ。照合の純粋な関数は、5 欄を 1 欄ずつ変えた記録（digest の大文字・前置きを含む）、宣言の片方だけ無い組を不一致と答える（Unity なしの単体）。**失敗経路:** 記録の 1 欄違い・宣言の片方だけ無い組は `OverlayBaseMismatch`、記録の無い root は `OverlayBaseMissing`、上書きの native の登録の失敗・root が 1 つでない・索引の作成の失敗はそれぞれの既存の値で拒まれ、巻き戻しの解除がどちらも成功した時は native の登録 0 件・gate の予約 0 件で終わる（解除は上書き → base）。巻き戻しの中で上書きの解除が失敗した時は base を解除せず予約を残し、次の登録の前の再試行で上書き → base の順に解除する。base の解除だけが失敗した時は、再試行は base だけを解除する。照合で拒んだ時は session を返さないので、供給元と route は作られない（照合は上書きを native に登録して root を読んだ後に行い、合わなければ load を受け付ける前に巻き戻す。記録は native の登録の前には読めない）。記録を持つ root を `Register`・`RegisterVerified` で単独で登録すると `InvalidRoot` で拒まれる。session は層を足す・外す member を持たない（code の検査） |
| AC2 | I-1 | 単体（Editor、adapter の test）。上書きの build は `BuildContentRoot` に base の記録（5 欄）・上書きの contentSet・Unity の版を書き、`AssetDatabase` で読み戻した root で同じ値が読め、Editor の `ContentOverlayBaseIdentity` からの変換が往復で一致する。上書きでない build の root は、読み戻しても「記録なし」と読める（Unity の直列化は class の欄を null にしないので、宣言の `IsDeclared` と同じく必須の欄が空なら記録なしと読む property で見る）。coordinator は書式の違う digest の上書きの build を report の前に拒む。記録を持たない既存の root の索引の作成は変わらない |
| AC3 | I-2・S-3 | 単体（層 2 つの session、AC1 の fake）。全ての token を返して `CloseAsync` すると、log は `[base 登録, 上書き登録, 上書き解除, base 解除]` で、gate の予約は最後に 1 回返る。**失敗経路:** 上書きの層の token だけ 0 にして受付を止めても、base の層の token が残る間はどちらも解除しない（逆も同じ）。上書きの解除が失敗すると base は解除されず、予約は残り、close の再試行で上書き → base の順に解除される。base の解除が失敗すると上書きは解除済みのまま、再試行で base だけ解除される。`CompletePlayStop` は token が残っていても上書き → base の順に解除し、上書きの解除が失敗すると base を解除せず、次の登録の前の巻き戻しの再試行で上書き → base の順に解除する。Play 停止の後に止まっていた close が再開しても、各層の成功した解除は 1 回だけ。`BeginSynchronousShutdown` も同じ順。途中の時点も見る: 各失敗の直後と各再試行の直後に、fake の log（どの層を何回解除したか）、gate の予約の有無、session が closed か、保留中の再試行の有無をそれぞれ確かめる。保留中の再試行が失敗すると次の登録は native の登録を始めずに拒まれる（既存の挙動） |
| AC4 | I-2・I-4・I-5 | 単体（`ContentRevisionGateTests` に追記）。層 2 つの登録は 1 件の登録として予約され（OS の lease は層ごとに取って合成する）、2 つの path と identity のどちらへの削除の許可も拒まれ、予約を返した後は両方とも許可される。拒否の値の優先: いずれかの層と path が同じで identity が違えば `PathInUse`、それ以外で当たれば `RevisionBusy`。層 2 つの登録の後の別の登録は今までどおり拒まれる。**失敗経路:** 上書きの path か identity が base と同じ組は予約の前に拒まれる。2 つ目の path の OS の lease の取得に失敗すると、1 つ目を返して予約 0 件で終わる。1 件の登録の既存の test は変更なしで通る |
| AC5 | I-3 | 単体（層 2 つの session と実物の `AssetManagement`、fake の native、route）。route は層 2 つの session から供給元 2 つ（上書き → base の順、id は別）の policy を作り、`Provider` は base の供給元のまま。I-3 の解決の規則どおりに次が採られる: 上書きにある (key, kind, 表現) は上書きの候補（由来・根拠は K1、identity は上書きの contentSet・build identity・target・空の digest、事実に base の記録 4 欄・Unity の版・root schema）、base にだけある物は base の候補（S3-1 と同じ値）。表現の 2 方向: 上書きに `K` の `Full` だけ・base に `K` の `Whitebox` と `Full` → `Whitebox` の要求は base の `Whitebox`、`Full` の要求は上書き。上書きに `K` の `Whitebox` だけ・base に `K` の `Full` → `Full` の要求は base、`Whitebox` の要求は上書き。層をまたぐ fallback: 両層に `K` の `Whitebox` が無く両層に別の `Full` がある時、`Whitebox`・fallback `Full` の要求は上書きの `Full` の候補・record・content 入口に渡す表現で採られる（base の `Full` が採られれば不合格）。上書きに `K` の `Full` も無ければ、同じ要求は base の `Full` で採られる。fallback を指定しない要求は採られない（期待する拒否）。id による影: base に `(K, Object, Whitebox)`、上書きに同じ native id の `(K, Object, Full)` がある時、`K/Whitebox` の要求は base から採られず（base の供給元は `NotFound`、理由は id による影）、base の expected identity で pin しても採られない。`K/Whitebox`・fallback `Full` の要求は上書きの `Full` で採られる。同じ組で `IAssetManagement.LoadContentAssetAsync(K, Whitebox)` を直接呼ぶと `EntryMissing` で拒まれ、native の load は 0 回。Scene でも同じ: base に `(K, Scene, Whitebox)`、上書きに同じ Scene の id の `(K, Scene, Full)` がある時、`K/Whitebox` の要求は base から採られず、base の pin でも採られず、`LoadContentSceneAsync(K, Whitebox)` の直接の呼び出しは `EntryMissing` で native の load は 0 回。record の `SourceBindingVerified` は true、probe に上書きの `NotFound` → base の採用の経緯が残る。cache: 両方の層から Object を載せて解放し cache に入れた後、実物の `AssetManagement` の close の退避（`EvictByPrefix` 1 回）で両方の層の entry が消える。**失敗経路:** 上書きにある物を base の expected identity で pin した要求は採られない。probe は上書きの `PinMismatch`（resolver は互換の規則より先に pin を見る）→ base の `NotFound`（上書きが選ばれた理由）で、要求は pin が未充足の失敗（`DecideFailureCode` の (1)）で終わる。上書きの候補の digest は空なので、上書きを pin で選ぶ要求は作れない（上書きの登録中に base の版を選べないことの帰結で、13 章が受け入れた制約）。base の供給元の候補を上書きの層の照合に、上書きの候補を base の照合に渡すと偽。上書きの候補は `ContentCompatibilityRule` で、§2.4 の上書きの分岐の表の各行に当たる事実・基準を 1 つずつ崩すと、その行の `ContentCompatibilityMismatch` の値で採られない（期待値は値で書く。offline の test を含む）。採った時の理由は `compat:Compatible:ContentOverlay`。照合を答えない test 用の供給元が `ContentOverlay` + `RecordedBaseMatch` と正しい形の事実を申告した候補は、実物の `ContentCompatibilityRule` が `Compatible` と答えても、loader は `SourceBindingUnavailable` で拒み、content 入口を呼ばない（`ContentRequestLoaderTests` の `ProviderWithoutBinding_IsRejectedBeforeLoad` と同じ形）。1 層の session の route は今までどおり供給元 1 つ（S3-1 の test が変更なしで通る） |
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
| Domain Reload 有効の Play（S2-1・S2-2・S3-1 から移送） | **本書の A3**（§1 の測定。native の登録は Play を終えても残り、script の reload で消える）。Play の入口から Domain Reload 有効で Play する確認は S3-4。Play に入る時の reload が前の Play の残りを消さないと分かれば、再試行の方式を本書の Phase A の新しい revision で決め直す。reload の後の OS の lease の file handle の扱いも同じ測定で S3-4 が確かめる |
| 上書きに root ではなく依存として入った同じ id の物（要求した物自身）を `LoadableObjectId` で読む時、どちらの directory の物が返るか（凍結前レビュー） | S4（別の PC の base では中身が違いうる） |
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
| K1 | `ContentSourceKind.ContentOverlay` と `ContentAcceptanceBasis.RecordedBaseMatch`（名前は下の「public の名前」で確定）を足し、resolver の固定表と規則に上書きの組を足す。identity は上書きの値、事実に base の記録 | public の enum の値と事実の予約 key の追加（S0-a・S1 の core の改訂）。表が 2 か所にある。record が上書きを読んだと正直に言う | **採る**（§2.8 U3） |
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
- **保証の境界:** 規則は記録の事実があることと基準との整合だけを見て、記録した base と実際に登録した base の一致そのものは確かめない（envelope に base の digest は無く、事実は供給元の申告である。`VerifiedDigest` でも core は digest を検証しないのと同じ契約）。一致を保証するのは次の 3 つの輪である: (1) 登録の口が、上書きの root の記録と base の検証済みの値を照合する（I-1、AC1）。(2) 上書きの供給元は、照合を通った session からしか作られない（供給元も route も internal）。(3) loader は、internal の照合（`IContentSourceBinding`）を答える供給元の候補だけを読み、外部の供給元の候補は `SourceBindingUnavailable` で読まない。`RecordedBaseMatch` の doc と 13 章にこの境界と 3 つの輪を書く。
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
- C' 用に予約した担当・モデル・ベンダー: xAI（Grok、cursor-agent。Grok 系のモデルを明示する）。Phase A は Anthropic 系（A1・A2・外部 1 巡目・設計レビュー 2 件目・凍結前レビュー）と OpenAI 系（外部 2・3 巡目・設計レビュー 1 件目と 3 件目）を使った。B・C も Grok を使わない。
- 独立性の強化条件を満たせない場合の理由: 満たす見込み（Grok は Phase A〜C に関与しない）。B か C が Grok を使えば `独立性制約あり` と記録する。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-18、同一 PC）。入力版は固定 base `b0bc9997` の clean な detached checkout と、HANDOFF copy（sha256 `ab5dcae7efe09ae9f4fc60d2b6a088e2d68c569df17dce85f4340a804ed491de`。A1 初稿に 6000.6.9f1 の測り直しの結果を足した版）。4 件とも Claude Code の subagent（Anthropic 系。主担当と同じ系列）。互いの指摘は渡していない。
  - アーキテクチャゲート: HEAD・clean・hash の照合は一致。結論は blocker 0、major 4、minor 4。L1・gate の基数・C1・K1 は妥当とした。
  - 不変条件の反証と AC の読み違い: 照合は一致。結論は blocker 0、major 6、minor 9。I-1・I-4・I-5 は破れず、I-2 は Play 停止の経路で本文と矛盾し、I-3 は key の単位の解決が 1 directory の時に読めた要求を後退させた。
  - 目的レビュー: §1 の目的と利用者の本文だけを渡した別の担当（道具を使わない）。入力の sha256 `e47b1cec69c7142ac335f8c351c2a4b888be9e79bc11c6b71e653c5c08d96a31`。
  - A0 だけからの代替構成（risk `high` の追加分）: 目的レビューの回答を記録した後、同じ担当に A0 の packet（sha256 `a296ee7d2694b21741f079f0126824e601e9176741b9387a7e7a9fa448eae430`。目的、利用者、対象外、現況、測り直し、問い、13 章の登録契約の原文、制約）と固定 base を渡して続けた。初稿は読ませていない。目的レビューのやり直しに使える未関与の担当は残してある。
- A3 の分類（担当の案を owner が 2026-11-18 に承認。目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは無い）:

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

- 外部計画レビュー 1 巡目（2026-11-18、Draft PR #200 の review と行コメント。担当は Claude（Anthropic 系の最上位のモデル。owner が依頼）。A1・A2 と同じ系列なので、系列の独立したレビューには数えない）。対象は統合案の commit。結論は、設計の本体（L1・C1・K1・G2、I-1〜I-5、AC1〜AC5）に破れは見つからず、凍結の前に直すのは GO が何を言うかの境界、というもの。採否（担当の案を owner が 2026-11-18 に承認）:

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
- 外部計画レビュー 2 巡目（2026-11-18、別の系列。担当は Codex CLI（OpenAI、`gpt-5.6-luna`、effort `medium`。owner が依頼）。入力は commit `91d2c8c6` の HANDOFF（sha256 `ec1e426ac3a1148c5db5d64cbc2df78b0f7ca19b81a58db24d7f02bb06d77469`、照合は一致）と固定 base の clean な checkout（照合は一致）。重点は I-2 と K1 の分岐）。結論は blocker 2・major 2。採否（担当の案を owner が 2026-11-18 に承認）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | blocker: Play 停止の解除の失敗の後、static の再試行は native と予約だけを解放し、元の session は closed にならず、後の close が二重に解除しうる。`??=` は 2 件目の失敗を失う | 採用（重大度は major と読む） | 1 層の今の code の読みとしては正しい。進行中の `CloseContentDirectoryAsync` は session を局所の変数に持つので、`AssetManagement` が session を手放すことは安全の根拠にならない（3 巡目の指摘）。安全の根拠は、層の列の再試行と session が同じ状態を共有し、どちらが先でも解除済みの層を呼ばず、全て済んだ時に closed と確定すること（I-2 の塞ぎ方に書いた）と、gate が登録を 1 件しか持たず次の登録が保留中の再試行を先に走らせて失敗すれば拒むので 2 件目の保留が作れないことである |
  | major: 既存の resolver は規則の前に pin を見るので、base で pin した要求で上書きの候補は `PinMismatch` になる。上書きだけ記録した base で pin を照らす規則を足す | 一部採用 | 挙動の読みは正しく（`PinMismatch` は探索を止めず次の供給元へ進む）、AC5 の失敗経路の probe と終わり方を書き直した。上書きを base の identity で pin に合格させる規則は不採用: record が「pin した base の版」と言いながら上書きの版を読むことになり、I-3 に反する |
  | major: AC1・AC3 が巻き戻しの途中の状態（直後の保留、一部の成功、再 close、2 件以上の保留）を見ない | 採用 | AC3 に途中の時点の 4 つの観測と、保留中の再試行が失敗した時の次の登録の拒否を足した |
  | blocker: U3〜U7 が owner の未決のまま | 記録 | 凍結の手順そのもの（owner が A3 で決める）。凍結記録に転記する |

- 外部計画レビュー 3 巡目（2026-11-18、Codex CLI（OpenAI、`gpt-6-astra`、effort `high`。risk `high` の計画なので上位のモデルで 1 巡追加した。owner が依頼）。入力は commit `49681e37` の HANDOFF（sha256 `7d3d9fedae4291777a3910dd18cf9095e3a1fc2e8e73711bf09b74423c70548c`、照合は一致）と固定 base の clean な checkout（照合は一致））。結論は major 2。I-2 は共有状態の設計を前提にすると破れる操作の列を作れず、K1 の固定表・空の digest による pin の不一致・修復を `VerifiedDigest` に限ること・load の経路は互いに整合している、とした。採否（担当の案を owner が 2026-11-18 に承認）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | major: 同じ native の id が base と上書きの別の (key, kind, 表現) に対応する時、G2 は base の entry を選ぶが native は上書きの版を読み、base の pin と record が嘘になる（依存ではなく要求した物自身の差し替わり） | 採用 | 登録を拒む案ではなく、上書きの root にある id を持つ base の entry を base の層から除く（id による影。13 章の「上書きにある id の物の base の版を選べない」と同じ向き）。I-3 の規則と経路 (8)、AC5 |
  | major: AC5・AC6 は完全一致だけで、層をまたぐ `Full` の fallback の順（上書き → base）を確かめない | 採用 | AC5 に、両層に別の `Full` がある組と、上書きに `Full` が無い組と、fallback なしの拒否を足した |
  | 2 巡目の指摘 1 の分類の理由（`AssetManagement` が session を手放す）は不十分で、進行中の close は session を局所の変数に持つ | 採用 | 2 巡目の表の理由を、共有状態と gate の基数に書き直した |
  | Domain Reload: script の reload と、Play に入る時の reload が前の Play の残りを消すかは別。本書の結論を Domain Reload 無効の確認の範囲に限るなら blocker にしない | 採用 | 下の「確かめた範囲」 |

- 設計レビュー（2026-11-18、PR #200 の review 2 件。owner が依頼。1 件目は OpenAI 系のモデルによる方式の選択への問い、2 件目は Claude（Anthropic 系、外部 1 巡目と同じ担当）の意見と提案）。対象は commit `bb37ef10`。採否（担当の案。2026-11-19 に owner が、SampleGame はサンプルなのでその所要の秒数は判断に意味が無いと示した）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | 本書の GO は「2 本を重ねて矛盾なく使える」で、「この方式を Framework の中心に据える価値がある」とは別。最小化する量、退けた代替の理由、方針転換の条件を残す | 採用 | 書く場所は program（方式を選んだのは program §5.3 で、HANDOFF は Phase D で消える）。program §5.3「上書きの方式を選ぶ理由と見直しの条件」に、制約・最小化する量・退けた代替・見直しの条件 (1)〜(3)（構造の条件だけ）・見直す時に外す物を書き、§8 の S3-4 の行の gate に入れた。本書の GO の意味は変えない |
  | 方式の実用をほぼ決める実データでの上書きの build の所要を、凍結の前に合否を持たない調査で測る | 不採用（owner） | SampleGame はサンプルで、その所要は実際に使う project の規模と構成を表さない（program §5.3 の当面の方針と同じ）。一度測ったが、数値は判断にも記録にも使わない |
  | 見直しの条件 (1) の閾値 N を数で書く | 不採用（owner） | 秒数の条件をやめ、見直しの条件は構造の 3 つ（開いている Scene だけの編集では build しない、編集の無い再 Play は build 0 回、core か Runtime の特例が増えれば見直す）にした |
  | `RecordedBaseMatch` の保証の境界: 規則は事実の有無と整合だけを見て、記録した base と実際の base の一致は確かめない | 採用 | §2.4 K1「保証の境界」と 3 つの輪、AC1 に「拒んだ時は session も供給元も作られない」、AC5 に「規則が `Compatible` でも照合を答えない供給元の候補を loader は読まない」、harvest to |
  | AC1 に「native の登録の前に拒否」を読める形で残す | 一部不採用 | 上書きの記録は native に登録した後でしか読めないので、「登録して読み、合わなければ load を受け付ける前に巻き戻す」と書いた（2 件目の訂正と同じ） |
  | core で一致を比べる設計（envelope に base の digest） | 不採用 | 事実は供給元の申告なので誤った供給元は防げず、public の面だけが増える（2 件目の意見と同じ） |
  | 上書きのために足す public の値と面は S3-4 の縦断の実証まで暫定 | 採用 | program §5.3、harvest to |
  | （担当の追加）広く共有された asset の編集の扱い。owner と検討した、編集の種類ごとのユースケースと困ることの組 | 採用（owner が 2026-11-19 に確定） | program §5.3「編集の種類ごとの扱い」。部分 checkout で手元に無い root に効く編集（D）を黙って混ぜないことを S3-4 の gate に、CI が作った build を自分の base に選ぶ規則を S4 に入れた。本書の範囲と凍結条件は変えない |

- 設計レビュー 3 件目と凍結前レビュー（2026-11-18、PR #200。対象は commit `9dbce4d8`）。3 件目は 1 件目と同じ OpenAI 系の担当で、前回の 2 点を解消とし、program §5.3 の D の文だけを指摘した（凍結を止めない）。凍結前レビューは Claude（Anthropic 系、外部 1 巡目と同じ担当）で、「設計は収束していて凍結してよい」とし、凍結の commit に入れる物を 6 つと、凍結を止めない物を 3 つ挙げた。採否（担当の案を owner が 2026-11-19 に承認）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | 3 件目・凍結前 5: D の「base の版のまま」は誤り。手元に無い root の直接参照は base の版、id で引く物は上書きの版で混在しうる。索引で検知した時も、索引が無く検知できない時も、完全に反映した Play として黙って通さない。小さい例（手元の A と手元に無い B が同じ asset を参照）で root の record と依存の版を別に観測する | 採用 | program §5.3 の D の行と S3-4 の行 |
  | 凍結前 1: id による影の case が Object だけ | 採用 | AC5 に Scene の case |
  | 凍結前 2: 13 章の harvest に、id による影と「id が変わった Scene のため」の句の訂正 | 採用 | §0 の harvest to |
  | 凍結前 3: 設計レビュー 1 件目の系列 | 採用 | OpenAI 系（owner が 2026-11-19 に回答）。C' の Grok の予約は保てる（§5） |
  | 凍結前 4: 再レビューを省く理由が、後から入った id による影を覆っていない | 採用 | 下の「A2 の再レビューの要否」 |
  | 凍結前 6: 「承認待ち」の残り、§0 の重複、K1 の「名前は仮」、C' の行 | 採用 | 各所を確定の文にした。program §5.3 の 2 つの項は、凍結の時に owner が確定した |
  | 凍結前（止めない）: I-3 の既知の制約は依存だけを言う。上書きに root ではなく依存として入った同じ id の物（要求した物自身）も同じ形で、どちらの directory の物を返すかは未測定 | 採用 | I-3 の探索した境界と §2.3（S4 へ） |
  | 凍結前（止めない）: I-2 の未確認の 2 つ目（reload の後の OS の lease の file handle）に送り先が無い | 採用 | §2.3 の Domain Reload の行（S3-4） |
  | 凍結前（止めない）: 最小化する量がどこでも観測されない。導入先が評価できるよう、入口が逆引きした root の数と build の所要を示し record に残す | 採用 | program §8 の S3-4 の行の入力（Framework は閾値を持たない） |
  | 凍結前（止めない）: 「D を見分けられるのは base の索引だけ」は手段を言い切っている | 採用 | program §5.3 を要件の文（base の build の時点の閉包の情報が要る。形は S3-4 の A1）に直した |

- 確かめた範囲: 本書の GO が言うのは、Domain Reload 無効の Play での登録・解決・解除である。Domain Reload 有効の Play は、§1 の測定（native の登録は Play を終えても残り、script の reload で消える）までを本書が持ち、Play に入る時の reload が残りを消すかと、Play の入口からの確認は S3-4 が持つ（§2.3）。

- 独立性: A1、A2 の 4 件、外部計画レビュー 1 巡目、設計レビュー 2 件目、凍結前レビューは Anthropic 系、外部計画レビュー 2・3 巡目と設計レビュー 1・3 件目は OpenAI 系である。A2 の後に G2・K1 の分岐・失敗の値・digest の定義が入り、A2 の再レビューは行っていない。2 巡目で I-2 と K1 の分岐を別の系列に見せた。C' は Phase A〜C に関与していない xAI（Grok、cursor-agent）に予約する。
- A2 の再レビューの要否: 行わない。解決の粒度（G2）、上書きの分岐の比較項目、失敗の値、digest の定義は、A2 の指摘に沿って曖昧さを決めたもので、新しい辺は増えていない。外部計画レビュー 3 巡目の後に入った id による影は、新しい解決の規則（base の entry を層から除く）で、3 巡目の指摘への対処である。影は凍結前レビューが反証の観点で 1 巡読み、record と実際に読む層が食い違う列を作れなかった。凍結前レビューは主担当と同じ Anthropic 系なので独立の再レビューには数えず、別の系列の読みは C'（Grok）で受ける。
- A3 統合担当・採否: 統合は AI（Claude Code、Anthropic）。§5.1 の各巡の採否案と §2.8 の U3〜U7 は、owner（root）が 2026-11-18・19 に推奨どおりと決め、2026-11-19 に revision 1 を凍結した（§0 の凍結記録）。

## 6. Phase B 実装結果

- 実装（§4 の順。行数は base `b0bc9997` → この head）:
  1. 記録と照合: `Runtime/BuildContent/BuildContentOverlayBase.cs`（新規 106 行）。public の `[Serializable]` の型で、base の 5 欄・上書きの contentSet・Unity の版を持ち、必須の欄が空なら `IsRecorded` が偽（記録無し）。constructor は digest を小文字 16 進 64 字（`IsManifestSha256`）に限る。照合は internal の純粋な関数 `FindMismatch`（`VerifiedInstalledRevision` と base の root の宣言の名前、または 5 つの値を受け、違う最初の欄の名前を返す）。`BuildContentRoot`（97 → 108）に `OverlayBase` と `Initialize` の任意引数 `overlayBase`。`ContentDirectoryFailureCode` に `OverlayBaseMismatch`・`OverlayBaseMissing` を末尾に足した。
  2. Editor の build: `IContentDirectoryAdapter.Build` に `ContentOverlayBaseIdentity? overlayBase, string contentSet` を足した。`UnityContentDirectoryAdapter`（245 → 261）が Editor の型から Runtime の記録へ変換し（`Application.unityVersion`）、読み戻した root の記録を全欄比べる。`BuildContentCoordinator`（215 → 221）は request の base と contentSet を adapter へ渡し、`ValidateOverlay` の最後に digest の書式を検査する。`ContentOverlayBaseIdentity` の `manifestDigest` の doc を transport manifest の SHA-256 に改めた（235 → 238）。
  3. 層の列: `Runtime/BuildContent/ContentDirectoryLayers.cs`（新規 276 行）。層ごとの path・identity・native・root・索引、予約、`Register`（保留中の再試行 → 層 2 つの予約 → 予約の中の再検証 → base → 上書きの native の登録・root・索引 → 記録を持つ base の root の拒否 → 上書きの記録の有無と照合 → id による影の計算。失敗は `TryUnregisterAll` で巻き戻し、巻き戻しの失敗は `DeferRollback`）、I-3 の解決 `Resolve`（`LayerResolution` に選んだ層・entry・「要求の表現の base の entry を影で除いた」を返す）、逆順の解除 `TryUnregisterAll`（層ごとの解除済みと予約の返却を保持し、失敗した層で止める）、process に 1 つの保留 `_pendingRollback`（層の列ごと）、cache の前置き。
  4. session: `ContentDirectorySession`（498 → 501）は token・処理・後始末の数え方と load の入口に留め、native の登録・解除は層の列へ移した。上書きを重ねた登録の口 `RegisterVerifiedWithOverlay(VerifiedInstalledRevision, string overlayPath, string overlayIdentity)` と、test が natives を注入する overload。load は層の解決が選んだ層の native で行い、token は載せた層の native を持つ。解除の 3 経路（`CloseNow`、`CloseNowForPlayStop`、登録の巻き戻し）は `TryUnregisterAll` を呼ぶだけ。
  5. gate: `ContentRevisionGate`（212 → 303）。internal の `Reserve(IReadOnlyList<ContentRevisionLayer>)` を足し、既存の `Reserve(identity, target, path)` は層 1 つで委ねる。層どうしの path か revision の重複は予約の前に `InvalidConfiguration`。OS の lease は層ごとに取り、途中の失敗では取れた分を逆順に返す。登録の台帳はどれかの層に当たれば当たったとし、拒否の値は「どれかの層と path が同じで revision が違えば `PathInUse`、それ以外は `RevisionBusy`」。
  6. 索引: `ContentDirectoryIndex`（159 → 189）に完全一致で引く `TryGetExact`、全 entry の一覧（影の計算用）、前置きを受ける `CacheKey(prefix, entry)`、上書きの層の前置き `OverlayLayerPrefix`（session の共通の前置きの後に `~overlay:` と path・identity。`~` は Base64 の字に無いので base の entry の key と衝突しない）。
  7. core: `ContentSourceKind.ContentOverlay = 4`、`ContentAcceptanceBasis.RecordedBaseMatch = 3`（doc に保証の境界と 3 つの輪）、`ContentCompatibilityFactKeys.OverlayBase*` の 4 つ、`ContentCompatibilityMismatch.OverlayBaseFactMissing = 14`、`ContentSetMismatch`・`RevisionMismatch`・`TargetMismatch` の doc に 1 句。`ContentResolver.RequiredBasis` と `ContentCompatibilityRule.EvaluateGeneric` に上書きの組を同時に足し、規則は §2.4 の上書きの分岐の表の順に判定し（engine と schema の判定は directory と共有の private method に括り出した）、host の述語を上書きにも当てる。採った時の理由は `compat:Compatible:ContentOverlay`。
  8. 供給元と route: `RegisteredDirectoryProvider`（130 → 190）に `OverlayId = "registered-content-overlay"` と層を指定する constructor `(session, bool overlay, string id)`（既存の `(session, id = DefaultId)` は base の層のまま）。候補は session の層の解決が自分の層を選んだ時だけ返し、base の供給元は上書きの層が選ばれた要求に `NotFound`（影なら理由に shadowed）。照合は由来・受け入れ根拠・identity まで比べる。`RegisteredDirectoryRoute`（102 → 121）は上書きを持つ session に供給元 2 つ（上書き → base）の policy を作り、`Provider` は base のまま、上書きの供給元は `OverlayProvider`。
  9. test（新しい class の名前）: `OneStarMaker.Tests.Editor.Build.ContentOverlaySessionTests`（AC1・AC3。共通の組み立ては同じ folder の `OverlayRegistrationFixture`）、`OneStarMaker.Tests.BuildContent.BuildContentOverlayBaseTests`（AC1 の照合の純粋な関数）、`OneStarMaker.Tests.Editor.Build.OverlayRootRecordTests`（AC2）、`OneStarMaker.Tests.BuildContent.ContentRevisionGateTests` に 3 件追記（AC4）、`OneStarMaker.Tests.Editor.Build.OverlayDirectoryProviderTests`（AC5）、offline の `OverlayTests.cs`（O1〜O8。`Program.cs` に `RunOverlayTests()` を 1 行足した）、実機の `OneStarMaker.Tests.Editor.Build.OverlayRegistrationIntegrationTests`（AC6。`SampleGame/Tests/Editor/Build/`、`[Timeout(720000)]`）。既存の test の変更は AC7 の例外 (1)(2) だけ（fake の adapter 3 つの `Build` の署名、`digest-1` を `ContentOverlaySelectionTests.BaseDigest`（SHA-256 の形の定数）に替えた）。
- internal の名前（§3 の仮の名前を B が決めた物）: `ContentDirectoryLayers`（`Layer`・`LayerResolution`・`Register`・`Resolve`・`TryUnregisterAll`・`DeferRollback`・`HasPendingRollback`・`CachePrefix`・`CacheKey`）、`ContentDirectorySession.RegisterVerifiedWithOverlay`・`OverlayLayer`・`ResolveEntry`・`IsClosed`、`ContentRevisionLayer`、`BuildContentOverlayBase.FindMismatch`・`IsManifestSha256`（後者は public。adapter の assembly が coordinator の検査に使う）、`RegisteredDirectoryProvider.OverlayId`、`RegisteredDirectoryRoute.OverlayProvider`。
- B 適応（凍結した契約と責務配置の範囲。Phase A は再開しない）:
  - **content 入口の影の扱い。** AC5 は、base に `(K, Whitebox)`・上書きに同じ native id の `(K, Full)` がある時、`LoadContentAssetAsync(K, Whitebox)` / `LoadContentSceneAsync(K, Whitebox)` の直接の呼び出しを `EntryMissing` と定める。一方 I-3 の解決の規則をそのまま content 入口に当てると、影で base の `Whitebox` を除いた後に上書きの `Full` へ fallback する。両方を満たすため、層の解決は規則どおりに上書きの `Full` を選び（供給元は上書きの `Full` の候補を返し、fallback を明示した要求で採られる）、content 入口（`ResolveForLoad`）だけが「要求の表現の base の entry を影で除いた上で、別の表現の entry を選んだ」時に `EntryMissing` で拒む。loader は採った候補の表現（`Full`）で content 入口を呼ぶので、loader の経路は変わらない。上書きを重ねない登録では影が無く、挙動は変わらない。
  - test の entry の native id は、`LoadableSceneId`・`LoadableObjectId` の非公開の field を reflection で設定して作った（`OverlayRegistrationFixture.SceneId`・`ObjectId`）。`LoadableSceneId` の constructor は internal で、層の解決と影の単体には任意の id の組（同じ・違う）が要り、Framework の test に Scene の asset を足さないため。native は fake なので id を読まない。Unity が field の名前を変えると fixture が `NullReferenceException` で落ちる（製品の code は依存しない）。
  - `ContentRevisionGate` の読みの台帳の要素の型を、登録の層の型（`Registration.Layer`。参照で外す class）にした。読みの調停の値と順は変えていない。
  - 予約の返却（`IDisposable.Dispose`）が投げた時、今までの 1 層の code は session を closed にしてから例外を close の失敗として返したが、層の列は予約を返すまで closed にせず、close の再試行で予約の返却だけをやり直す。最初の close が返す値（`OperationFailed`）は同じで、予約の返却は process 内の台帳と OS の file handle の解放なので実際には投げない。
- 行数の判断: `ContentDirectorySession.cs` は 501 行で 500 行を超えた。**非分割妥当**: 残っている責務は token・発行済みの処理・失敗した後始末の数え方と、それに従う load の入口という 1 つの変更理由で、層の列の責務（解決・cache の key・登録と逆順の解除・巻き戻しの再試行）は `ContentDirectoryLayers.cs` に移した。超過は test が途中の状態を見る `IsClosed` の 3 行で、§3 が置き場を今決めると定めた。`ContentRevisionGate.cs` は 212 → 303 行（+43%。見込みは 250）。層の型・lease の合成・拒否の値の優先を足した分で、責務は登録・読み・削除の調停のまま。
- HANDOFF との差: なし（§3 の file と test の folder の外、asmdef の `references`、`AssemblyInfo.cs`、`SceneState`、`tools/Artifacts`、SampleGame の製品 code、`IAssetManagement`・`AssetManagement` と `AssetManagement.ContentDirectory.cs`・`IContentDirectoryBackend`・`UnityContentDirectoryBackend`・`ContentRequestLoader.cs`・`AbstractApplicationInitializer`・`PlayerContentConfiguration`・`Distribution/` は変えていない）。
- 実施した確認: Editor（6000.6.9f1、この worktree、StandaloneWindows64 Player）を起動して compile（`compilationFailed:false`、console の error は Pipeline の eval の timeout の 1 件だけで compile の error は 0。変更・新規の file の warning は 0）。`dotnet run --project tools/ContentResolutionOfflineTests`（72 passed、0 failed、exit 0。うち上書きの O1〜O8 は 8 件）。`pwsh tools/contract-audit.ps1`（errors=0 warnings=0）。`pwsh tools/docs-audit.ps1`（errors=0。警告 1 件は `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest 時期で本スライスと無関係）。AC7 の 3 つの `grep`（`Runtime/AssetManagement/` → `ContentResolution`、`Runtime/ContentResolution/Core/` と `ContentRequestLoader.cs` → `BuildContent`・`UnityEngine`、`Runtime/BuildContent/` → `ContentResolution`）は全て一致なし。AC7・AC8 の変えない file は `git diff --stat b0bc9997` に出ない。起動した Editor は未保存の Scene が無いことを確かめて正常終了させた。
- 未実行: `pwsh tools/run-tests.ps1` の全て（起点 filter、AC1〜AC5 の新しい class、AC6 の実機の test、`RegisteredDirectoryRouteIntegrationTests`、全 EditMode 回帰）と Content Directory の build。Unity のバッチテストは Phase C が行う。新しい test は compile しただけで、一度も走らせていない。
- implementation head commit: `9638a4af`（1 回目の B。実装と票を 1 つの commit で切った。今の値の正本は §0 の欄）。
- Phase B 担当・モデル・ベンダー: AI（Claude Code、Anthropic）。

### 差し戻し 1 巡目（2026-11-19、§7 の C-1〜C-3）

- 開始時の照合: 起動指示の引き継ぐ commit `0166d8b3` が HEAD と一致し、作業ツリーは clean。implementation head 欄の `9638a4af` は HEAD の祖先で、`git diff --stat 9638a4af HEAD` は本書だけ。PR #200 の状態ブロックの `引き継ぐ commit` と implementation base / head は起動指示と §0 に一致した（remote の head も `0166d8b3`）。
- 直した物（3 件とも B 適応。製品の code の契約・責務配置・public の名前、I-2 の再試行の方式は変えていない）:
  - **C-3**（`ContentOverlaySessionTests.SessionAndLayers_HaveNoMemberToAddOrRemoveALayer`）: field が readonly であることの検査を instance の field に限った。static の `ContentDirectoryLayers._pendingRollback` は、巻き戻しに失敗した層の列を process に 1 つ保留して次の登録の前に再試行する書き換える field（I-2・§3）で、どの session の層の列にも層を足さないため検査から外し、その理由を test の comment に書いた。method の名前の動詞の検査は static を含めたまま。`_pendingRollback` は readonly にしていない。
  - **C-1**（`Runtime/BuildContent/ContentDirectoryIndex.cs`）: `ContentEntryTable.TryGetExact` を `TryResolve` の fallback の comment の前へ移し、`TryGetExact` 自身に「実効表現の完全一致だけを引き、fallback しない。上書きを重ねた session の層の解決が要求の表現を両層で引いてから Full を両層で引くため」の comment を付けた。code の挙動は変わらない。
  - **C-2**（`OverlayRegistrationIntegrationTests`）: 記録の対象を、移動の前後の `LoadableSceneIdEditorUtility.CreateLoadableSceneId` の値から、登録した session の base と上書きの root の `Moved` の entry（`Full`）の `SceneId` に替えた（`LogMovedSceneIds`。登録の直後、route を作る前に読む）。base の層は「base の root にだけある `Stay` の解決が返す層」で取り、製品の code に accessor を足していない。比べた結果は `[S32 overlay] Moved entry scene id in base root: …, in overlay root: … (same|different)` の行で書き、合否にしない（両方の root に `Moved` の entry があることだけは前提として assert する。上書きの root にあることは逆引きの assert が既に求めている）。
- 実施した確認: Editor（6000.6.9f1、この worktree）を起動し、`recompile_status` / `console_status` で `compilationFailed:false`、console の error 0。変更した 3 file の assembly（`OneStarMaker.Runtime`・`OneStarMaker.Tests.Editor`・`SampleGame.Tests.Editor`）の DLL は編集より後に compile し直されていた。console の warning 14 件は全て変更外の既存 file（`ISceneController.cs` の CS8632、`ContentManifestValidation.cs` の CS8602）と Package Manager の deprecation。`pwsh tools/contract-audit.ps1` は errors=0、warnings=0。`pwsh tools/docs-audit.ps1` は errors=0、warnings=1（`PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の時期で、1 回目の B と同じ。本書と無関係）。core と offline の test の対象は変えていないので、offline の test は走らせ直していない。起動した Editor は未保存の Scene が無いことを確かめて `EditorApplication.Exit(0)` で終了させた。
- 未実行: `pwsh tools/run-tests.ps1` の全て（C-3 の test を含め、直した test は compile しただけ）。Unity のバッチテストは発見 C が行う。
- 行数: `ContentDirectorySession.cs` は変えていない（501 行のまま）。`ContentDirectoryIndex.cs` は 189 → 191。
- 担当・モデル: AI（Claude Code、Anthropic）。

## 7. Phase C

### 発見 C（1 回目、2026-11-19。未完了: Unity のテストが未実行）

- 種別: 発見 C。GO 判定はしない。C' は起動しない。
- 対象: implementation base `b0bc9997`、implementation head `9638a4af`（起動指示の引き継ぐ commit と同じ）。開始時に、HEAD が起動指示の SHA と一致し作業ツリーが clean であること、implementation head 欄が「この票を含む commit」で `git diff --stat HEAD^ HEAD` に `docs/handoff/` 以外の file があること（implementation head は HEAD 自身）を照合した。PR の有無と timeline は、`gh` の実行が承認されず確かめていない。
- 使った evidence: commit で固定した差分（`git diff b0bc9997 9638a4af`）だけ。evidence bundle は未生成（判定 C が生成する）。
- 構造適合（§3 との照合）: 変更した file は §3 の file とその `.meta`、§3 の test の folder、`docs/handoff/` だけ（`git diff --name-status`）。責務・置き場・依存は §3 のとおり: 層の列（解決・影・cache の key・登録と逆順の解除・巻き戻しの再試行）は `ContentDirectoryLayers` 1 か所にあり、session の解除の 3 経路と再試行は `TryUnregisterAll` を呼ぶだけ。token・処理・後始末は session に 1 組。gate は 1 件の登録が層の配列を持つ形で、既存の `Reserve(identity, target, path)` は層 1 つで委ねる。供給元は層ごとに 1 つ・id は別、route の `Provider` は base のまま。記録を互換の事実に変える処理は `Directory/` の供給元にある。行数の超過（session 501 行、gate +43%）は §6 の判断（非分割妥当、責務は調停のまま）で妥当と読んだ。`BuildContentOverlayBase.IsManifestSha256` が public なのは、Editor の coordinator から使うためで friend を足さない代わり（§6 に記録あり。§3 の public の型の member で、規則を 2 か所に書かない）。
- AC ごとの読み（code と test。test は未実行）:
  - AC1: 登録の口は base の `VerifiedInstalledRevision` と上書きの path・identity だけを受け、上書きの target は base の検証済みの target。予約の中の再検証 → base の native の登録・root・索引 → 記録を持つ base の root の拒否（`InvalidRoot`）→ 上書きの登録・root・索引 → 記録の有無（`OverlayBaseMissing`）→ 照合（`OverlayBaseMismatch`、欄の名前を理由に）の順。失敗は `TryUnregisterAll` で上書き → base に巻き戻し、巻き戻しの失敗は予約を残して `DeferRollback`。`ContentOverlaySessionTests` と `BuildContentOverlayBaseTests` が AC1 の各経路（5 欄・宣言の片方だけ・digest の大文字と前置き・無効な handle・root の数・索引・同じ path / identity・巻き戻しの層ごとの失敗・保留中の再試行の失敗・単独の登録の `InvalidRoot`・層を足す member の無いこと）を持つ。
  - AC2: adapter が Editor の型を Runtime の記録へ変換し（1 か所）、読み戻した root の全欄を比べる。coordinator の `ValidateOverlay` が digest の書式を report の前に検査する。`OverlayRootRecordTests` が持つ。
  - AC3: `ContentOverlaySessionTests` が close・自動の解除（両方の順）・層ごとの解除の失敗と再試行・Play 停止（token あり、上書きの失敗の後の次の登録の再試行、再開した close で各層 1 回）・同期終了を、途中の時点（log・予約・closed・保留）とともに持つ。
  - AC4: gate の層の予約・lease の合成と途中の失敗の返却・拒否の値の優先は code で読んだ。test は `ContentRevisionGateTests` の追記 3 件。
  - AC5: 層の解決（`Resolve`）は I-3 の規則（要求の表現を上書き → base（影を除く）、次に `Full` を同じ順）どおり。content 入口だけが影の後に別の表現へ読み替えた時に `EntryMissing`（§6 の B 適応。凍結した AC5 の期待と I-3 の両方を満たす読みとして妥当）。規則の上書きの分岐は §2.4 の表の順。`OverlayDirectoryProviderTests` と offline の O1〜O8 が AC5 の項目を持つ。
  - AC6: `OverlayRegistrationIntegrationTests` は AC6 の手順（開始時の一覧 0 件、base の build・publish・install・検証、`Tex` の pixel・`Moved` の移動・`Added`、逆引きと上書きの build、層 2 つの登録、record と pixel、close の後の一覧 0 件、観測窓の Error / Exception 0 件、`[Timeout(720000)]`）を持つ。指摘 C-2 を参照。
  - AC7: 下の機械検査。変えない file は diff に無い。既存の test の変更は例外 (1)(2) だけ（fake の adapter 3 つの署名と `digest-1` の置き換え）。
  - AC8: `git diff --name-status b0bc9997 9638a4af` の file は全て §3 の範囲。asmdef・`AssemblyInfo.cs`・`SceneState`・`tools/Artifacts`・SampleGame の製品 code に変更なし。
- 現在の問いを阻害する欠陥: なし（code と test の読みの範囲。test の実行結果は未取得）。
- 阻害しない指摘（どの凍結済み条件・常時契約の違反でもない。次に B へ戻る巡があればそこで直す。B へ戻る巡が無ければ判定 C が扱いを決める）:
  - **C-1**（低、obvious、unique）: `Runtime/BuildContent/ContentDirectoryIndex.cs` 172〜177 行。`ContentEntryTable.TryResolve` の「Full へ fallback する理由」の comment の直後に `TryGetExact` が挿入され、comment が fallback しない `TryGetExact` の説明に読める。`TryGetExact` を comment の前へ移す。
  - **C-2**（低、semantic、unique）: AC6 は「`Moved` の上書きと base の entry の Scene の id は比べて記録する（合否にしない）」と定めるが、test が記録するのは移動の前後の `LoadableSceneIdEditorUtility.CreateLoadableSceneId` の値で、base と上書きの root の `Moved` の entry の id ではない（`OverlayRegistrationIntegrationTests.cs` 89・104〜107 行）。合否に効かないので blocker にしないが、記録の対象を AC6 の文言に合わせる（例: Play の前に base と上書きの root を読み、`Moved` の entry の `SceneId` を比べて書く）。
- 後続スライスの入力: なし。
- 実行した検査とテスト:
  - `dotnet run --project tools/ContentResolutionOfflineTests`: 72 passed、0 failed、exit 0（上書きの 8 件を含む）。
  - AC7 の参照の検査（Grep）: `Runtime/AssetManagement/` → `ContentResolution`、`Runtime/ContentResolution/Core/` → `BuildContent`・`UnityEngine`、`ContentRequestLoader.cs` → `BuildContent`、`Runtime/BuildContent/` → `ContentResolution` は全て一致なし。
  - 常時契約の hard gate: 新規・編集の Unity 側 `.cs` 28 個は全て先頭が `#nullable enable`。新規の test に `record`・`Task.Delay`・`Thread.Sleep` は無い。
- 未実行（承認が得られなかった）: `pwsh tools/contract-audit.ps1`・`pwsh tools/docs-audit.ps1`（pwsh の起動の承認）、`pwsh tools/run-tests.ps1` の全て（sandbox 外の実行の承認。phase-c の規則に従いテスト未実行として止めた。Unity は起動していない）。このため、起点 filter・AC1〜AC5 の新しい class・AC6・`RegisteredDirectoryRouteIntegrationTests` は B・C のどちらも一度も走らせていない。
- 判定必須のうち未実行: 全て（Unity のテスト一式と全 EditMode 回帰）。
- 担当・モデル: AI（Claude Code、Anthropic）。B と同じモデルで行った（`独立性制約あり`。owner の方針で、独立性のためにモデルを替えない）。

### 発見 C（続き、2026-11-19。Unity のテストを実行し、B へ差し戻した）

- 種別: 発見 C の続き。GO 判定はしない。C' は起動しない。構造レビューはやり直していない（差分は 1 回目と同じ）。
- 対象と開始時の照合: implementation base `b0bc9997`、implementation head `9638a4af`。起動指示の引き継ぐ commit `a7631e5b` が HEAD と一致し、作業ツリーは clean、`9638a4af` は HEAD の祖先で、`git diff --stat 9638a4af HEAD` は本書だけだった。PR #200 は open（base `develop`）で、remote の head は A3 凍結の `7d626349` のまま（B と発見 C の commit は未 push）、凍結の後の review・comment の新着は無い。**状態ブロックの `引き継ぐ commit` は `9638a4af`（B の出口の値）で、起動指示の `a7631e5b` と一致しなかった。** 1 回目の発見 C が `gh` の承認を得られず状態ブロックを更新しなかったためで、`9638a4af` は `a7631e5b` の親、その間の差分は本書だけなので、引き継ぎの連鎖は壊れていないと読み、止めずに進めた。状態ブロックは更新していない（PR の本文の更新は owner に確かめる）。
- 実行の条件: この worktree、Unity 6000.6.9f1、Editor を閉じ他の Unity が無い状態（Hub と Licensing Client だけ）、active build target は B が確かめた StandaloneWindows64 の Player（`EditorUserBuildSettings.asset` は 11-17 から変わっていない）。`pwsh tools/run-tests.ps1` を sandbox 外で起動し、両 process ともライセンスは `LicenseClient-void-1.19.0` に接続した。
- 起点 filter の変更（受け入れ条件の追加ではない）: §5 の起点 filter と AC1〜AC5 の新しい 4 class に、B が AC7 の例外 (1)(2) で変えた既存の `ContentOverlaySelectionTests` と `BuildContentProjectionTests` を足した（変えた test が今も通ることを見るため）。
- process 1（起点 filter と AC1〜AC5。12 class を `;` で並べた 1 つの `-Filter`）: XML の root は `Failed(Child)`、total 171、passed 170、failed 1、skipped 0。class ごとの件数（total / failed）: `BuildContentOverlayBaseTests` 15/0、`ContentRevisionGateTests` 7/0、`ContentRequestLoaderTests` 15/0（`OneStarMaker.Tests.ContentResolution` の名前空間で当たったのはこの class だけ）、`BuildContentFullModeTests` 25/0、`BuildContentProjectionTests` 4/0、`ContentDirectorySessionTests` 21/0、`ContentOverlaySelectionTests` 13/0、`ContentOverlaySessionTests` 26/1、`OverlayDirectoryProviderTests` 13/0、`OverlayRootRecordTests` 5/0、`RegisteredDirectoryProviderTests` 23/0、`UnityContentDirectoryAdapterTests` 4/0。step は `failed`（exit 1）、Unity の exit code は 2（test の失敗）で自然に終わった。`error CS` は 0 件。
- process 2（AC6。`OverlayRegistrationIntegrationTests` だけ）: XML の root は `Passed`、total 1、passed 1、所要 73.8 秒。step は `passed`（exit 0）、Unity の exit code 0 で自然に終わり、終了 stall も staging の `Directory.Move` の失敗も無かった。test の出力: `Moved scene id before move: 2196e4152bccf8a4a9604904b18166c6, after move: 2196e4152bccf8a4a9604904b18166c6 (same)`（A3 の測定と同じ。記録の対象は C-2）。log の build の段に Pipeline の設定の Warning（`No RuntimePipelineConfig asset found`）が build ごとに出た（A1 の測り直しと同じ）。§2.6 NO-GO (i) に当たる観測は無い。
  - 差し戻しが決まった後に AC6 を走らせた理由: 票が AC6 を別 process で走らせることを求めており、AC6 は B・C のどちらも一度も走らせていない唯一の実機の test で、NO-GO (i) を見られるのはこれだけだから。失敗すれば同じ巡で B に直させられる。判定必須の残り（`RegisteredDirectoryRouteIntegrationTests` と全 EditMode 回帰）は走らせていない。
- 機械検査: `pwsh tools/contract-audit.ps1` は errors=0、warnings=0。`pwsh tools/docs-audit.ps1` は errors=0、warnings=1（`PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の時期で、B の記録と同じ。本書と無関係）。
- 現在の問いを阻害する欠陥:
  - **C-3**（中、test の欠陥、unique）: `ContentOverlaySessionTests.SessionAndLayers_HaveNoMemberToAddOrRemoveALayer` が失敗する（`ContentDirectoryLayers._pendingRollback`、Expected: True But was: False。`ContentOverlaySessionTests.cs` 242 行）。test は型が `ContentDirectoryLayers` / `ContentDirectoryLayers.Layer[]` の field を static を含めて全て `IsInitOnly` と求めるが、`ContentDirectoryLayers.cs` 34 行の `private static ContentDirectoryLayers? _pendingRollback` は、I-2 の塞ぎ方と §3 が「process に 1 つだけ保留できる、層の列ごとの巻き戻しの再試行」として書き換える field と定めたもので、製品の code は契約どおり。違反の根拠は AC1 の「session は層を足す・外す member を持たない（code の検査）」が通らないこと（§2.6 GO は AC1〜AC8 が全て成り立つことを要求する）。直す先は test の検査の範囲（例: instance の field に限る、または保留の field を名前で除き、その理由を comment に書く）。分類は B 適応（契約・責務配置を変えない）。同じ test の中の動詞の検査（`Add`・`Remove` など）は両方の型で通った（失敗した field の検査は動詞の検査の後にある）。
- 阻害しない指摘: 1 回目の C-1・C-2 はそのまま有効。差し戻しの巡ができたので、同じ巡で B に直させる。
- 差し戻しの後の発見 C で走らせる範囲（案）: `ContentOverlaySessionTests`（C-3 の確認）と、C-1 が触れる `ContentDirectoryIndex` を使う `OverlayDirectoryProviderTests`・`RegisteredDirectoryProviderTests`。C-2 は AC6 の test の記録だけを変えるので、AC6 は判定 C で GO 候補 head とまとめて走らせてよい（発見 C で走らせるかは次の担当が根拠を書いて決める）。
- 後続スライスの入力: なし。
- 担当・モデル: AI（Claude Code、Anthropic）。B と同じ系列（`独立性制約あり`。owner の方針）。

### 発見 C（差し戻し 1 巡目の後、2026-11-19。C-1〜C-3 は閉じた）

- 種別: 発見 C（B と発見 C の往復の 1 巡目の戻り）。GO 判定はしない。C' は起動しない。
- 対象と開始時の照合: implementation base `b0bc9997`、implementation head `266e36bb`（差し戻し 1 巡目の B の commit）。起動指示の引き継ぐ commit `266e36bb2959eac4dd10aa63440af9ad8cf7e322` が HEAD と一致し、作業ツリーは clean。implementation head 欄は「この票を含む commit」で、`git diff --stat HEAD^ HEAD` は本書のほかに `ContentDirectoryIndex.cs`・`ContentOverlaySessionTests.cs`・`OverlayRegistrationIntegrationTests.cs` を変えるので、implementation head は HEAD 自身。`9638a4af` は HEAD の祖先。PR #200 は open（base `develop`）、remote の head は `266e36bb`、状態ブロックの `引き継ぐ commit` と implementation base / head は起動指示と一致した。review・comment の最新は凍結前（2026-11-18）の review 3 件で、新着は無い。
- 使った evidence: commit で固定した差分（`git diff 0166d8b3 266e36bb` の code 3 file。1 巡目の差分全体の構造レビューは 1 回目の発見 C のまま）。evidence bundle は未生成（判定 C が生成する）。
- 構造適合: 差し戻しの差分は §3 の file（`ContentDirectoryIndex.cs`）と §3 の test の folder の 2 file だけで、責務・置き場・依存・public の面は変わらない。製品の code の変更は `TryGetExact` の位置と comment だけ（挙動は同じ）。
- 指摘ごとの確認:
  - **C-3（閉じた）:** `SessionAndLayers_HaveNoMemberToAddOrRemoveALayer` は、readonly の検査を `BindingFlags.Instance` の field に限り、static を外した理由（`_pendingRollback` は I-2 の保留で、層を足さない）を comment に書いた。method の名前の動詞の検査は static を含めたまま。検査が空振りでないことを code で確かめた: 対象の instance の field は `ContentDirectorySession._layers` と `ContentDirectoryLayers._layers` の 2 つで、どちらも `readonly`。製品の `_pendingRollback` は readonly にされていない（I-2・§3 のとおり）。test は Passed（下の process 1）。
  - **C-1（閉じた）:** `TryGetExact` は `TryResolve` の fallback の comment の前に移り、自身の comment（完全一致だけを引き fallback しない理由）を持つ。fallback の comment は `TryResolve` の直前にある。
  - **C-2（閉じた）:** AC6 の test は、登録した session の base と上書きの root の `Moved` の entry（`Full`）の `SceneId` を比べて記録する（`LogMovedSceneIds`）。base の層を `Stay` の解決で取る方法は、id による影が索引ではなく層の列の別の集合（`ContentDirectoryLayers._shadowed`）なので、影になった base の entry も索引の `TryGetExact` で引ける。id の比較は合否にせず、前提の assert は両方の root に `Moved` があることだけ。記録の行は `[S32 overlay] Moved entry scene id in base root: db451c49c87936a498205e1db11c6d74, in overlay root: db451c49c87936a498205e1db11c6d74 (same)`（A3 の測定と同じく、移動しても id は同じ）。
- 走らせた範囲と根拠（起点 filter の変更。受け入れ条件の追加ではない）:
  - process 1: 前の発見 C の案の 3 class に `ContentDirectorySessionTests` を足した。C-1 で動かした `TryGetExact` は層の列の解決（`ContentDirectoryLayers.Resolve`）が上書きの無い 1 層の session でも毎回呼ぶので、I-5 の 1 層の経路も見るため。
  - process 2: AC6 を発見 C で走らせた。C-2 の修正は AC6 の test に前提の assert と記録の行を足したもので、閉じたかは AC6 の出力でしか確かめられず、足した assert が AC6 を落とさないかもこの test でしか見られないため（前の巡の実行は 73.8 秒）。判定 C は GO 候補 head でもう一度走らせる。
  - 実行の条件: この worktree、Unity 6000.6.9f1、Editor を閉じ他の Unity が無い状態（Hub と Licensing Client だけ）。`pwsh tools/run-tests.ps1` を sandbox 外で起動し、両 process ともライセンスは `LicenseClient-void-1.19.0` に接続した。
- process 1（`ContentOverlaySessionTests`・`OverlayDirectoryProviderTests`・`RegisteredDirectoryProviderTests`・`ContentDirectorySessionTests` を `;` で並べた 1 つの `-Filter`）: XML の root は `Passed`、total 83、passed 83、failed 0、skipped 0。class ごとの件数（total / failed）: `ContentDirectorySessionTests` 21/0、`ContentOverlaySessionTests` 26/0、`OverlayDirectoryProviderTests` 13/0、`RegisteredDirectoryProviderTests` 23/0（前の巡と同じ件数）。step は `passed`（exit 0）、Unity の exit code 0 で自然に終わった。compile error は 0。
- process 2（`OverlayRegistrationIntegrationTests` だけ）: XML の root は `Passed`、total 1、passed 1、XML の所要 61.1 秒。step は `passed`（exit 0）、Unity の exit code 0 で自然に終わり、終了 stall も staging の `Directory.Move` の失敗も無かった。§2.6 NO-GO (i) に当たる観測は無い。
- 機械検査: `pwsh tools/contract-audit.ps1` は errors=0、warnings=0。`pwsh tools/docs-audit.ps1` は errors=0、warnings=1（`PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の時期で、前の記録と同じ。本書と無関係）。
- 現在の問いを阻害する欠陥: なし。
- 阻害しない指摘: なし。
- 後続スライスの入力: なし。
- 往復の数: B と発見 C の差し戻しは 1 巡（上限 4 巡）。
- 判定必須のうち未実行: offline の test（core と offline の test の対象はこの巡で変わっていない）、起点 filter の残り（`ContentRevisionGateTests`・`UnityContentDirectoryAdapterTests`・`BuildContentFullModeTests`・`OneStarMaker.Tests.ContentResolution` と AC1〜AC5 の新しい class のうち `BuildContentOverlayBaseTests`・`OverlayRootRecordTests`）、`RegisteredDirectoryRouteIntegrationTests`、全 EditMode 回帰。どれも判定 C が GO 候補 head で走らせる。
- 担当・モデル: AI（Claude Code、Anthropic）。B と同じ系列（`独立性制約あり`。owner の方針で、独立性のためにモデルを替えない）。

## 8. Phase C'

未着手

## 9. Phase D

未着手
