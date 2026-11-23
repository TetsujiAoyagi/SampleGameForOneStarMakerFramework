# 遊べる Windows の Player に手元の上書きを重ねて起動する

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-2）
- status: `A`（Phase A の revision 0。A1 の初稿、A2 の独立レビュー、A3 の統合案まで。**凍結していない**。外部計画レビューの前。A1 の最初の gate（§5 の G0）は未実施）
- branch: `claude/player-content-overlay`（PR の base は `develop`）
- implementation base commit: `116b00918dbdefa9f2dff5aeec7e43ef4a8b4a30`（`develop` の head、2026-11-23。§1 の現況と A2 の固定 base もこれ）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `high`（Framework の起動（`AbstractApplicationInitializer`、`ContentOverlayStartup`）の Player の分岐と、Player が受け付ける config を変える。上書きの登録を Player で初めて製品の口から行う。独立して失敗しうる未知が複数ある（§5 の G0、u1〜u4））
- owner: root（依頼者）
- created: 2026-11-23
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [04 章](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.7「上書きの起動の設定」（Player の分岐と digest の key）、[13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」の「範囲」（Player の禁止を解いた範囲）、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」（Player の入口）、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（G0 と AC11 の実測）、repo の README の「試す」、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-2 の行と §2.3 の後続へ送る入力
- 関連: [program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-2 の行、「S4-0 の A1 が振り分けた入力」「S4-0 から移送した入力」「S4-1 から移送した入力」の S4-2 の項目、§5.3「上書きの方式を選ぶ理由と見直しの条件」「Windows の Player での上書きの実測」
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と program だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（外部計画レビューの指摘の採否、G0 の実行と結果の反映、owner の凍結判断）。
  - 次の作業: (1) Draft PR の外部計画レビューの指摘を §5.1 の A3 の表へ採否として足す（外部指摘で設計・受け入れ条件を変えたら、影響する A2 観点の再レビューの要否と理由も書く）。(2) §5 の G0 を実行担当の session で実行し、結果を §1 の現況と §2.5 に書く（G0 の答えが「同じ」でなければ凍結せず owner に返す。G0 (b) で組み込みの型の材料が無ければ §2.4 の owner の裁定へ）。(3) §2.5 の owner の判断が要る 5 点を owner に確かめる。凍結するなら A3 凍結記録・本票（次の担当を Phase B へ）・PR の状態ブロックを改めて、凍結版を commit する。
  - 読む範囲: 全節。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。
  - 必要な権限: (1)(3) は文書だけ。(2) は Unity の batch と Player の build・起動（[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md) と [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) を先に読む）。push は公開点の規則に従う。
  - 停止規則: 目的・利用者の欄を変える指摘は owner が裁定し、変えたら目的レビューをやり直す（§5.1）。G0 の答えが「同じ」でない時は凍結しない。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 編集する開発者が、検証済みの base（遊べる Player と同じ build の、検証済みの installed revision）を載せた Windows の遊べる Player に、手元の編集から作った上書きを 1 つ重ねて起動し、編集した Scene と asset を Player の上で確かめられるようにする。重ねられない時（上書きが base と合わない、上書きが build の後に書き換わった、上書きが Player に無い型を使う、script の code か field が Player を build した時と違う）は、base だけの起動に縮退せず、Player を起動する前か起動の失敗として理由を示す。動作確認の Player（BS4）の receipt と exit code の契約、Player の protected config、起動の fail-closed は弱めない。
- 利用者: Player で自分の編集を確かめたい編集する開発者（担当外の source は手元に無くてよい）。Player と base は、自分か CI が遊べる Player の build の menu で作ったものを使う。上書きを作って Player を起動するのは Editor の menu の 1 つの操作で、遊ぶのは起動した Player の窓の中の操作である。
- 対象外（所有する後続は §2.3）:
  - 上書きを 2 つ以上重ねること、base を Player と別の build にすること（Player の build identity と違う base）。
  - 動作確認の Player（`smoke`）に上書きを重ねること（今のまま拒む）。同梱の directory（installed の pair なし）に重ねること。
  - Player での要求時解決の配線と record（`WireContentResolution` は Player の構成では呼ばないまま。理由は §2.1 の D5）。
  - script の差を吸収する機構（field の migration、型の代替、missing script の stub）。差があれば Player の作り直しへ案内して止まる。
  - 不一致を警告に格下げして続ける・照合を飛ばす switch。base だけで起動する fallback。
  - Player の中で上書きを読み直すこと（hot reload）、Player の中で値を書き換えること。
  - Player や base を自動で作り直すこと。CI の build・取得の手順と「自分の base」を選ぶ規則（S4-3）。
  - Android・iOS（S4-5）。Mono、他の stripping の水準、development build の Player。
  - 上書きの登録の順・個数・時点・解除の契約そのもの（13 章のまま。Player でも同じ口を使う）。
- 現況（base `116b0091`）:
  - **上書きの起動の判定（`Runtime/Bootstrap/ContentOverlayStartup.cs:67-85`）:** key `editor:contentOverlayPath`・`editor:contentOverlayIdentity` が 1 つでもあれば、Player の構成か Editor の Play でない時は `Rejected("Content overlay is only available in Editor Play Mode.")`。Editor では mode が `directory`（か無し）で installed の pair がある時だけ `Overlay`。`ReadContentDirectoryConfiguration`（`AbstractApplicationInitializer.cs:1170-1241`）は Decide を Player の分岐より前に呼び、`Rejected` を `InvalidConfiguration` の例外にする。Player の分岐（`:1208-1217`）は `PlayerContentConfiguration.Resolve` の結果から `ContentDirectoryConfiguration` を作り、overlay を渡さない。
  - **Player の installed の照合（`Runtime/BuildContent/PlayerContentConfiguration.cs`）:** baked の 10 の protected key（S4-1 で `playerKind` を足した）を重ねた後の値と ordinal に比べ、installed の pair があれば `InstalledRevisionVerifier.Verify(root, digest, baked.ContentSet, baked.BuildIdentity, target)`。照合は receipt の revision が baked の build identity と同じことを求める（`InstalledRevisionVerifier.cs:78-83`）ので、Player に載せられる base は Player と同じ build の installed revision だけである。pair が無ければ exe の横の同梱 directory（検証なし）。
  - **登録（`AbstractApplicationInitializer.cs:817-831`）:** `InstallConfiguredContentDirectoryAsync` は configuration の `Overlay` があれば `ContentDirectorySession.RegisterVerifiedWithOverlay(verified, overlay.Path, overlay.Identity)`（base → 上書き、上書きの root の記録と base の 5 欄の照合、失敗は例外で base だけの session を返さない）を呼び、成功で `[AppInit] Content overlay registered: identity=… baseRevision=… path=…` を出す。Player の構成でも同じ method を通る（Player では AfterSceneLoad の `register-content-directory` の段）。登録は上書きの中身の hash を見ない（04 章 §4.7）。
  - **Player の Scene の load:** Player の構成では要求時解決を配線しない（`:415-421`）ので、`SceneDirector` は `IAssetManagement.LoadContentSceneAsync`（content 入口）で Scene を読む（`SceneDirector.Loading.cs:678-686`）。13 章は、層の選び方と id による影を loader と content 入口の両方が守ると書く。
  - **遊べる Player の build（`SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs:43-114`）:** `spring-full` を Full で build し、`artifacts/playable/transport/<identity>/` へ publish（`transport.json` は `files`・`sourceFiles`・`sourceClosure`・`index` 等を持つ）、`PlayerBuildProjectMutation` の中で IL2CPP・managed stripping High・development build でない Player を `previousBuildReportDirectories` に content の build report の folder を渡して build し、`artifacts/playable/players/<identity>/`（exe、同梱 `content/`、`build-receipt.json`）へ置く。Player と transport は同じ build identity を持つ。
  - **Editor の上書きの入口（`PlayEntryOverlay.cs`、`PlayEntryInterceptor`）:** 経路 Built + local edits は、Delivery で選んだ installed の base に対して `ContentOverlayPlanner.Plan`（base の `transport.json` の閉包の記録と手元の file から編集・上書きに入れる root・作り直せない root を決める）→ 認め → 再利用か build → 環境変数（`PurposeLaunchSettings.EnvironmentName`: `SAMPLEGAME_` + key の `:` を `__`、大文字）で key を渡して Editor の Play。作り直せない root（D）は止めずに示す。
  - **script の型:** S4-0 で、base の build report を渡して build した IL2CPP・High の Player に、base が使わない `ProbeOverlayOnly`（base が使う型と同じ assembly）が残った。別の assembly・組み込みの型・Player の build の後に足した script は測っていない。Unity の公開文書は、別に build した content は Player から strip された型を使えないと書く。content の root の JSON は `BuiltWithTypeTrees: true`。content の build report に object ごとの型の一覧があるか（`BuildReport.packedAssets`）は確かめていない（§5 の G0 (b)）。
  - **上書きの完全性:** 上書きは publish されず（`ContentTransportPublisher.Publish` が拒む）、file の一覧と hash を持つ manifest が無い。build の後に上書きの directory が書き換わっても Editor も Player も見分けない。
  - **game の log と終わり方:** S4-1 のとおり（game の log は exe の横の `OneStarMakerLocal/Log/`、遊べる Player の起動の失敗は receipt を書かずに Player の log の stage と例外と exit 1、窓を閉じると exit 0）。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 遊べる Player と base を用意する | menu `Tools/OSM/Content/Build Spring Playable Player`（既存）を選ぶ。base を Delivery で install して「次に使う」にする（既存の操作） | Player の構成、`player-compat.json`（§2.1 の D4）、transport | 開発者か CI の実行時の操作。他は自動導出 | 目的の「自分か CI が build の menu で作ったもの」。Delivery の install は既存の口で、base を検証済みにする唯一の手段 |
| UC2 上書きを重ねて Player を起動する | menu `Tools/OSM/Content/Play Playable Player With Local Edits`（新規、1 つ）を選ぶ。作り直せない root があれば認める（既存の Editor の入口と同じ確認） | base（Delivery の選択）、Player（base と同じ identity の `artifacts/playable/players/<identity>/`）、編集と上書きの root（計画）、上書きの再利用か build、script・型の照合、上書きの digest、起動の環境変数 | 選ぶのは実行時の操作。他は自動導出 | 目的の「上書きを作って Player を起動するのは Editor の menu の 1 つの操作」。Player・base・編集・hash を手で入れさせない（目的レビューの指摘）。作り直せない root の認めは Editor の入口と同じ D の扱いで、黙って通さないため |
| UC3 遊ぶ・閉じる | 窓の中の操作、窓を閉じる | なし | 実行時の操作 | 目的の「Player の上で確かめる」 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 編集する開発者が、検証済みの base を載せた遊べる Player に、自分の編集の上書きを重ねて起動でき、照合の失敗を base だけの起動に縮退せずに理由を示せるか（program §8 の S4-2 の行）。

### 2.1 進める最低条件

**設計の要点（A1 の決定。§3・§4 の根拠）:**

- **D1 base と Player:** base は遊べる Player と同じ build identity の installed revision だけ（今の照合のまま）。Player と base は同じ coordinator の 1 回の build が作る。別に build した base を Player に載せる形は作らない（S4-1 から移送した (1) への答え）。「別の `Library` の base」は、Player と base を別の checkout（別の `Library`）で build し、上書きを手元で build する形で確かめる（G0 と AC11）。
- **D2 Runtime の判定:** `ContentOverlayStartup.Decide` に Player の種類（検証済みの baked の `PlayerKind`。重ねた後の config の値は使わない）を渡し、Player の構成で種類が `playable`、installed の pair があり、baked の mode が `directory` の時だけ `Overlay` にする。上書きの 3 つの key（path・identity・digest）の**どれか 1 つでも**あれば判定に入る（digest だけの起動を `None` にしない。A2 の M-2）。`smoke` の Player、pair の無い Player、Editor の Play でも Player の構成でもない起動は今のまま `Rejected`。Player の分岐は `Overlay` を `ContentDirectoryConfiguration` に渡し、登録は Editor と同じ `RegisterVerifiedWithOverlay` を使う。key は `content:` の外のままなので protected の照合（`PlayerContentConfiguration.Resolve`）は変えない。`ContentOverlayPlayConfig` の remarks（Player の構成では起動の失敗）を直す。
- **D3 上書きの完全性（Player が正本）:** 上書きの content directory の全 file の相対 path と SHA-256 から 1 つの digest を作る internal の関数（`ContentOverlayDigest`、Runtime）を足し、key `editor:contentOverlayDigest` を Player の構成では必須にする。Player は登録の直前（`RegisterVerifiedWithOverlay` の前）に digest を取り直して比べ、違えば起動の失敗にする。入口は上書きを build した時の digest を入口の記録に残し、再利用の時はその記録と今の digest を比べ（再利用の照合は増えた file を拒まないので。`ContentOverlayReuse.cs:131`、A2 の M-5）、違えば build し直す。起動の直前にもう一度取り直して記録と違えば起動しない。Editor の Play では key を任意のままにし、key があれば照合する（無視しない。A2 の m-2）。
- **D4 script と型の照合（起動の前、入口が持つ）:** 遊べる Player の build が、Player の staging（`playerWork`）の中で publish の前に `player-compat.json` を書く（`smoke` では書かない。A2 の M-8）。中身は (a) script の指紋（Player の script の compile に入る `Assets/` と `Packages/` の `.cs`・`.asmdef`・`.asmref`・`.rsp`、`Assets/` の下の precompiled の `.dll`、`Packages/manifest.json`・`packages-lock.json`、Standalone の scripting define、Unity の版の相対 path と SHA-256 の列の digest。A2 の M-3 (b)）、(b) Player が持つ型の集合（材料は G0 (b) で確かめる）。入口は (a) を **`PlayEntryOverlay.Run` の準備（`Prepare`）より前に**取り直し（A2 の M-7）、違えば違う file の一覧と「遊べる Player を build し直す」案内を出して止める。上書きの build の時の指紋も入口の記録に残し、再利用する上書きの指紋が今と違えば build し直す（指紋の 3 点の一致。A2 の代替構成）。上書きの build report の型が (b) に無ければ、その型と root の一覧を出して止める。既存の script の class の strip（base が使わない class）は、遊べる Player の build の間だけ `PlayerBuildProjectMutation` が置く `link.xml` で SampleGame と OneStarMaker の runtime の assembly を全て保持して起こさない（指紋が同じなら class は Player にある。A2 の M-3 (a)。`smoke` には置かない）。Runtime は script と型の照合をしない（§2.5 の (3)）。
- **D5 要求時解決:** Player では配線しない（S4-1 から移送した (2) への答え）。content 入口が層と影を守る（13 章）ので、上書きの版を読むのに loader は要らない。record は付かず、由来は登録の 1 行の log で示す。配線なしで層が効くことは AC1 で確かめる（u5）。
- **D6 入口:** SampleGame の Editor の menu 1 つ（UC2）。順は (1) Delivery の「次に使う」の installed の base を得て、同じ identity の Player を手元の `artifacts/playable/players/<identity>/` から得る（CI の Player は取得した folder をそこへ置く。取得そのものは S4-3。A2 の M-6）。Player の `build-receipt.json` の `transportManifestSha256` と install の digest を比べる。(2) D4 の指紋。(3) 既存の `PlayEntryOverlay.Run` の段（準備・計画・認め・再利用か build）を、`suppliedSceneKeys` を空にして呼ぶ（Player には snapshot の供給元が無いので、開いている Scene の編集も上書きに入れる。A2 の代替構成。Dirty の Scene は既存の保存の確認に通す）。(4) Player が content から読まない root（bootstrap の Scene、SceneResourceMap と graph metadata、`Resources`）に編集が当たれば、その root と「遊べる Player を build し直す」案内を出して止める（Player はこれらを登録の前に同梱・baked から読むので、上書きに入れても効かない。A2 の B-2）。(5) D4 の型、D3 の digest。(6) 継承した `SAMPLEGAME_*` の環境変数を全て消してから決めた key だけを置き（A2 の M-1）、`-logFile` を入口の work の下に渡して Player の process を窓つきで起動する。(7) process が登録の 1 行と初回 Scene の行を出す前に終われば、Player の log から理由の行を拾って Console と dialog に出す。編集が無い（`NotRequired`）時は上書きの key を置かずに base だけで起動し、Console に「上書きなし（編集なし）」を 1 行出す（照合の失敗の縮退ではない）。作り直せない root だけの時（`UnbuildableOnly`）も同じで、Editor の入口と同じ通知を出す。

**不変条件（起きてはいけないこと）:**

- **I1 上書きの指定がある起動を base だけで続けない。** 上書きの key が 1 つでもある Player の起動は、上書きを全部重ねて動くか、`BeforeSceneLoad`・`AfterSceneLoad` の起動の失敗（Player の log に理由、exit 1、Title を出さない）かのどちらかになる。
  - 主張を崩しうる経路: `ContentOverlayStartup.Decide` の分岐、`ReadContentDirectoryConfiguration` の Player の分岐が overlay を落とす経路、`InstallConfiguredContentDirectoryAsync` が Overlay の null で base だけを登録する経路、`RegisterVerifiedWithOverlay` の巻き戻しの後に起動が続く経路（例外を握る `catch`）、SampleGame の失敗の override（`AppInitializer.OnPlayerContentStartupFailedAsync`）が遊べる Player を終わらせない経路、digest の key だけ欠けた起動、digest の key だけがある起動（Decide の最初の `None`。A2 の M-2）、Player が content から読まない root（bootstrap の Scene・map）の編集が登録されても効かない経路（入口の (4) で塞ぐ。A2 の B-2）、Editor の環境変数の継承で古い key が Player に渡る経路（入口の (6) で塞ぐ。A2 の M-1）。
  - 探索した境界: 上の 4 file と `ContentDirectorySession.RegisterVerifiedWithOverlay` の入口まで。`ContentDirectoryLayers` の中は S3-2 の検証に依る。
  - 未確認: IL2CPP・High で上書きの登録の経路の型が strip されて途中で例外の形が変わること（u2）。
- **I2 動作確認の Player の契約を変えない。** `smoke` の Player は上書きの key があれば今と同じく `before-scene-load` の失敗の receipt と exit 1、key が無ければ今と同じ receipt と exit 0。
  - 経路: Decide に足す種類の引数が `smoke` を `Overlay` にする経路、`ReadBakedPlayerKind` が baked を読む前に Decide が呼ばれる順序、coordinator の `player-compat.json` の書き込みが `smoke` の出力と `build-receipt.json` を変える経路。
  - 探索した境界: `AbstractApplicationInitializer` の `BeforeSceneLoad` の順と S4-1 の latch（`BuildConfig` の直後に種類を latch）。
- **I3 protected config を弱めない。** 上書きの key・digest の key は baked の 10 の値を変えられず、installed の pair の照合（同じ build identity）を迂回できない。
  - 経路: key が `content:` の名前空間に入る経路（大小文字・`.` と `:` の同一視）、Decide が Resolve より前に `Overlay` を返して Resolve を飛ばす経路、上書きの path に installed の base の path を渡す経路（gate の層の重複の拒否）。
- **I4 照合を通らない上書きを Player に登録しない。** base の記録の不一致、上書きの directory の書き換え（digest の不一致）、上書きの key の欠け・空は、native の登録より前に止まる。
  - 経路: digest を取る時点と native の登録の間の書き換え（窓は残る。受け入れる。§2.5 の (2)）、digest が content の folder の外の file・空の folder・大小文字の違う名前をどう数えるか、symlink・reparse point。
- **I5 script か型が Player と合わない上書きで Player を起動しない（入口の経路）。** 入口は D4 の照合に落ちたら Player の process を作らない。
  - 経路: `player-compat.json` が無い・壊れた Player（今までの Player を含む）を「照合なし」で通す経路、指紋の対象から漏れる script（`Packages/` の外の local package、`Assets/` の外の asmdef）、Unity が `.cs` を compile しない folder（`Editor/`、Editor 専用 asmdef）を指紋に入れて余計に止める経路（止める側の誤りは I5 を破らないが、使えなくなる）。
  - 経路（A2 で足した）: base が使わない既存の class が指紋が同じまま strip される経路（D4 の `link.xml` で塞ぐ）、古い script で作った上書きの再利用（D4 の 3 点の一致で塞ぐ）。
  - 未確認: 型の集合の材料（G0 (b)）。材料が無い時の扱いは §2.4。
- **I6 入口は base・installed cache・Player の出力・source を書き換えない。** 書くのは上書きの artifacts と入口の work の下だけ。観測の時点は入口が Player の process を作る直前までとし、Player が自分で書く `OneStarMakerLocal/` は除く（A2 の M-4）。

**成立条件（起きなければならないこと）:**

- **E1** 別の checkout（別の `Library`）で build した遊べる Player と base（installed）に、手元の Editor から入口で上書きを重ねて起動すると、上書きの登録の 1 行が出て、編集した Scene と asset が上書きの版で見え、Title から操作を続けられ、窓を閉じて exit 0 で終わる。
- **E2** I4・I5 の各拒否で、利用者は理由（どの照合か、違う欄・file・型）を、入口なら Console と dialog で、Player なら Player の log の 1 行で読める。

### 2.2 受け入れ条件

全ての Player の起動は窓を出した通常の起動（`-batchmode` なし）、制限 120 秒。判定は Player の log・game の log・exit code・入口の record（判定の test が書く JSON）と、AC1 だけ screenshot で行う。

| AC | 内容 | 検証する条件 |
|---|---|---|
| AC1 | 成功: 初期状態は、checkout X で build した遊べる Player の folder（`players/<id>`）と transport を checkout Y の `artifacts/playable/` に写し（CI からの取得の代わり）、Y の Delivery で transport を install して「次に使う」にした状態。Y の Unity を起動する前に、Title の Scene が読む UI の label の文字列（content の root の閉包にある asset）を token に変えておき、Y の入口を実行する。Player の log に `RegisteredLogMarker` の行（identity が入口の record の上書きの identity と同じ、baseRevision が Player の identity と同じ）、game の log に Title の `Loaded.`、screenshot に token の文字列。続けて S4-1 と同じく「スタート」を押す操作で Home に移り game の log に `OnStabledImpl HomeScene`。game の log の Error 以上の行が 0（`<unparsed>` の行も 0、S4-1 の注記）。Player の log の Error 以上の行は、S4-1 から移送した (8) の URP の post process の strip の行だけを除いて 0。窓を閉じて exit 0。 | E1、I1、D5 |
| AC2 | `smoke` の Player に上書きの 3 key を環境変数で渡して起動 → receipt の `succeeded=false`・stage `before-scene-load`・exit 1。key なしの起動は S4-1 と同じ receipt と exit 0。 | I2 |
| AC3 | 遊べる Player に、記録が違う base を指す上書きを渡す。作り方: 判定の test が、同じ checkout で `spring-full` をもう 1 回 build・publish・install した別の identity の base に対して上書きを作り、その上書きの正しい digest と一緒に Player へ渡す。照合の順は digest → base の記録なので、digest は通って `OverlayBaseMismatch` が出る → Player の log に `OverlayBaseMismatch` と違う欄、exit 1、Title の `Loaded.` が無い。 | I1、I4、E2 |
| AC4 | (a) 入口の port で、digest を取った後・process を作る前に上書きの 1 file を書き換えて差し込む単体テスト → 入口が digest の不一致を示して process を作らない。(b) 入口が作った上書きの 1 file を書き換え、古い digest のまま環境変数で直に起動 → Player の log に digest の不一致、exit 1、Title が無い。(c) digest の key を欠いた起動 → `InvalidConfiguration`、exit 1。(d) digest の key だけの起動（遊べる Player）→ `InvalidConfiguration`、exit 1。(e) 上書きの content に file を 1 つ足してから入口を実行 → 入口は再利用せずに build し直し、AC1 と同じく起動する。 | I1、I4、E2 |
| AC5 | Y の Unity を起動する前に、SampleGame の runtime の `.cs` に field を 1 つ足す（または新しい `MonoBehaviour` の script を足す。どちらも指紋で止まる）。入口を実行 → 入口が script の指紋の不一致と違う file を示し、Player の build し直しを案内し、`Prepare` を呼ばず、Player の process を作らない（record の pid なし）。 | I5、E2 |
| AC6 | base が使わない組み込みの component（G0 (b) で決める 1 種）を Title の閉包の prefab に足した上書き → 入口が型の一覧を示して起動しない。 | I5、E2 |
| AC7 | 遊べる Player を installed の pair なし（同梱の directory）で、上書きの key つきで起動 → `InvalidConfiguration`、exit 1。 | I1、I3 |
| AC8 | (a) 編集の無い Y で入口を実行 → 上書きの key なしで起動し、Console に「上書きなし」の 1 行、Player の log に上書きの登録の行が無く Title の `Loaded.`、窓を閉じて exit 0。(b) Editor の process の環境に古い上書きの 3 key を置いた状態で (a) → 結果は (a) と同じ。(c) 作り直せない root だけの計画（`UnbuildableOnly`。判定の test が Y の source から root の file を退かして作る）→ Editor の入口と同じ通知を出し、(a) と同じく起動する。 | E1（縮退との区別）、I1 |
| AC9 | 単体テスト: `ContentOverlayStartup.Decide` の表（Player の種類 × pair × mode × 3 key の有無。digest だけを Player と Editor の両方で含む）、`ContentOverlayDigest`（順・大小文字・空 folder・content の外の file）、指紋と型の照合の policy、`player-compat.json` の読み書き、Player が content から読まない root の分類。 | I1〜I5 |
| AC10 | 入口の実行の開始から Player の process を作る直前までに、base の installed の tree、`artifacts/playable/players/<identity>/`（`OneStarMakerLocal/` を除く）、`git status --porcelain`（上書きの artifacts・work と、S4-1 から移送した (6) の空の folder を除く）が変わらない。 | I6 |
| AC11 | AC1 の X と Y が別の `Library` を持つことを record に残す（両方の `Library` の path と、X の build の Unity の版と Y の上書きの build の Unity の版）。 | E1（別の `Library` の base） |
| AC12 | Y で bootstrap の UICommon の prefab（Player が同梱から読む root）を編集して入口を実行 → 入口がその root と「遊べる Player を build し直す」案内を示し、Player の process を作らない。 | I1、E2 |

### 2.3 ここでは答えない問いと所有する後続

- 別の PC で build した base（S4-3）。base の選び方（「自分の base」、同じ目的の複数 revision）と CI からの取得（S4-3）。
- Player での要求時解決の配線と record（Player の観測を record で揃えるか）: 所有の無い入力として program §10 の U11 に足す。
- digest の時点から native の登録までの間の書き換え（I4 の窓）: 開発用の Player では受け入れる。閉じる必要が出たら S4-3 の cache の lease と一緒に扱う。
- 上書きの登録の順・個数・時点の契約を Player で変えること、上書きを 2 つ以上: 13 章のまま。
- script の差を吸収する（Player を作り直さずに script の変更を確かめる）: 対象外。要求が出たら owner が新しい行を置く。
- S2-0b の「同じ file を持つ 2 つの directory」の結論を Player で測ること: 測らない。上書きの契約は id の違う物の 2 実体と同じ id の物の影で決まり、S4-0 が Player で同じ答えを得た。同じ bytes の 2 実体は開発の PC の重複として受け入れ済み（program §5.3 の当面の方針）。
- S4-1 から移送した (3)（設定を Addressables から読む Player の strip）: 遊べる Player は設定を Addressables から読まないので当たらない。G0 で S4-0 の harness を通す時に当たれば、harness の側の既知の形として記録する。
- S4-1 から移送した (6)〜(8)（空の folder の残り、URP の設定の書き換え、post process の strip）: U11 のまま。G0・判定 C の担当への注記として §5 に書く。
- Android・iOS（S4-5）。

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで、独立して失敗しうる未知が 3 つある（G0 (a) 別の `Library` の base の答え、G0 (b) 型の集合の材料、u1〜u4 の製品の口の Player での成立）。

判断: **分けない。** G0 (a)(b) は凍結の前に確かめる gate にして（§5）、答えが「同じ」でない時は凍結せずに owner へ返す。u1〜u5 は全て同じ AC1 の起動で確かめられ、どれか 1 つが落ちても他の判定に使う証拠は別の AC で取れる。G0 (b) で組み込みの型の材料が無いと分かった時は、HANDOFF の revision だけでは決めない。program の S4-2 の行が「base が使わない組み込みの型と module を起動の前に見分ける」を求めているので、owner の裁定（program の行を改めて型の検出を後続の行へ分ける、か、安全側の規則「上書きの閉包の型は base の閉包が使う型の部分集合に限る」を材料が取れる範囲で採る）を経て revision にする（A2 の M-3）。A0 だけからの代替構成は測定のスライス（S4-2a）と製品のスライスに分ける案だったが、測定は凍結の前の G0 として同じ役を果たし、製品の code を変えないので、スライスを分けずに済む。

### 2.5 未決事項（A3 で owner に確かめる）

1. **G0 の結果:** 未実施。凍結の前に実行する（§5）。
2. **上書きの digest を Editor の Play でも必須にするか:** A3 の案は Player だけ必須、Editor は key があれば照合（Editor の入口と Editor の起動の record を変えない。Editor は Play の直前に手元で作るので書き換えの窓が小さい）。目的レビューは「Player 側の照合を正本」とし、Player ではそうした。
3. **script と型の照合を Player（Runtime）にも置くか:** A3 の案は入口だけ（Runtime が Player の script と型の一覧を持つには baked に新しい protected の値が要り、`content:` の protected key を 1 つ増やす。A0 だけからの代替構成も、型の材料は Editor にしか無いので Player に型の判定は置かないとした）。入口を通さずに環境変数で直に起動した時、script の差は Player が見分けない（I5 は入口の経路だけ）。
4. **Framework の public の面:** D3 のため `OneStarMaker.Editor` の public の `ContentOverlayPlayConfig` に `OverlayDigestKey` と digest を取る static method を足す（host の入口が文字列と算法を複写しないため。S3-4b と同じ形）。13 章の「足した public の値」の一覧に加わる。
5. **digest から登録までの窓:** A0 だけからの代替構成は、上書きの検証を base と同じく予約（`ContentRevisionGate`）の中でやり直す案を出した。session と gate の層（13 章の登録の口）を変えるので A3 の案は採らず、窓を開発用の Player で受け入れる（§2.3）。

## 3. 責務マップ

| file | 責務（変更理由） | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/Bootstrap/ContentOverlayStartup.cs` | 上書きを重ねるかの判定（Player の種類の引数と digest の key を足す） | 状態なし | なし | internal | `ContentOverlayStartupTests`（表） | 91 → 125 |
| `OneStarMaker/Scripts/Runtime/BuildContent/ContentOverlayDigest.cs`（新規） | 上書きの directory の file の一覧と SHA-256 から digest を作る（算法の正本） | 状態なし | `System.IO`、`System.Security.Cryptography` | internal（Editor へ InternalsVisibleTo か public の写し。§2.5 (4)） | `ContentOverlayDigestTests`（一時 folder） | 0 → 70 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 起動の配線（Player の分岐で overlay を渡す、登録の前に digest を比べる） | App | 上の 2 つ | 変えない | 既存の起動のテストと AC1〜AC4・AC7 | 1425 → 1445。500 行を超えるが、起動の orchestration の 1 責務で、足すのは分岐の配線だけ（新しい判断は上の 2 file に置く）。非分割 |
| `OneStarMaker/Scripts/Editor/ContentOverlay/ContentOverlayPlayConfig.cs` | key と算法の public の写し | 状態なし | Runtime | public（`OverlayDigestKey`、`ComputeOverlayDigest`） | AC9 | 既存 → +15 |
| `SampleGame/DependOnAll/Editor/Build/PlayerCompatibility.cs`（新規） | Player の script の指紋と型の集合の記録の形と、手元・上書きとの比較（policy。I/O は引数の一覧で受ける） | 状態なし | なし（文字列の列だけ） | internal | `PlayerCompatibilityTests` | 0 → 140 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | 遊べる Player の build（`playerWork` の中で publish の前に `player-compat.json` を書く段を足す。`smoke` には書かない） | Editor の操作 | 上と build report | menu は変えない | AC9、判定の build | 229 → 265 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildProjectMutation.cs` | Player の build の間の project の書き換え（遊べる Player の時だけ、runtime の assembly を全て保持する `link.xml` を置いて戻す） | build の 1 回 | 既存 | 変えない | 判定の build、`git status` | 既存 → +25 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildPublisher.cs` | 変えない（staging の中身を原子的に置く。compat は staging に入れてから呼ぶ） | — | — | — | — | 変更なし |
| `SampleGame/DependOnAll/Editor/Build/PlayerOverlayLaunch.cs`（新規） | 入口の orchestration（D6 の (1)〜(7)。`SampleGameContentBuild` の上書きの port を `suppliedSceneKeys` 空で再利用し、Delivery の `ContentInstallResult` から base を得る） | Editor の操作 1 回 | `PlayEntryOverlay`、`PlayerCompatibility`、`ContentOverlayPlayConfig`、Delivery の選択 | menu 1 つ | port を差し替えた単体テスト（起動しない。AC4 (a)） | 0 → 240 |
| `SampleGame/Tests/Editor/Build/PlayerOverlayEndToEndTests.cs`（新規、`[Explicit]`、namespace `SampleGame.Tests.Editor.Build`） | 判定の Player の build・install・入口・起動・観測（S4-1 の `SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs` の形を写す） | test | 上の全て | test | — | 0 → 450（判定の case の列。1 責務の宣言的な列で非分割） |

`PlayerCompatibilityTests` と入口の port の test も `SampleGame/Tests/Editor/` に置く（`OneStarMaker.Tests.Editor` は SampleGame を参照しない。A2 の B-1）。

namespace・asmdef は既存の file と同じ（Runtime は `OneStarMaker.Runtime`／`.BuildContent`、SampleGame の Editor は既存の Editor の asmdef）。新しい asmdef 参照は足さない。

## 4. 実装計画

- 順序: (1) `ContentOverlayDigest` とテスト。(2) Decide の表の変更とテスト、`ContentOverlayPlayConfig` の remarks。(3) 起動の配線。(4) `PlayerCompatibility` とテスト、coordinator の `player-compat.json`、mutation の `link.xml`。(5) 入口。(6) 判定の test。
- Phase B から Phase A へ差し戻す条件: 型の集合を content の build report から取れない（G0 (b) と違う）、`RegisterVerifiedWithOverlay` を Player で通すのに session・gate の層の変更が要る、入口が Editor の Play の入口（`PlayEntryInterceptor`）の状態を変えないと動かない、新しい asmdef 参照が要る。
- B 適応の例: D4 の遊べる Player だけの `link.xml` で足りない engine 側の型だけを残す追加（結果に記録する）。`SampleGame/DependOnAll/link.xml` を変える時は動作確認の Player の build にも効くことを結果に記録する。
- 対象外を維持する方法: `smoke` の分岐、Editor の Play の Decide の結果、`PlayerContentConfiguration.Resolve`、`BuildReceipt` の欄、`WireContentResolution` の条件を変えない。

## 5. テストとレビュー計画

- **G0（A1 の最初の gate、凍結の前。未実施）:** 担当は Phase A の実行担当の session（Unity の batch と Player の起動）。
  - (a) 別の `Library` の base: S4-0 の harness（git の commit `b101b382` の `Tests/Editor/Spikes/PlayerOverlay/` と probe・runner の asmdef）を 2 つの新しい worktree（X・Y、それぞれの `Library`）に取り出し、base と Player を X で、上書きを Y で build するように harness の build の段だけを分けて（X の base の build report と directory を Y の上書きの build の入力にする）、S4-0 と同じ主の順の round を 3 process で走らせる。答えの読み方は S4-0 と同じ（「同じ」「違うが成り立つ」「成り立たない」）。差が出たら同じ harness の Editor の対照を走らせる（S4-0 から移送した (3)）。S4-1 の遊べる Player の構成は S4-0 と同じなので、構成の測り直しは要らない（program の S4-1 の行）。
  - (b) 型の集合の材料: (a) の X の content の build report と Player の build report を Editor で開き、`packedAssets` の各 object の型（`PackedAssetInfo.type`）が取れるか、`MonoBehaviour` の script の class の粒度まで分かるか、Player の build report の `strippingInfo`（含めた module）が取れるかを記録する。AC6 に使う「base が使わない組み込みの component」の候補を 1 種決める。
  - (a) の X から Y への受け渡しは、X の base の directory と build report を Y の harness の入力の path へ写す（path は G0 の record に残す）。
  - Project Auditor の post-build 監査で `step.json` が `failed` になりうる（S4-0 の J2）ので、合否は XML と record で読む。新しい worktree の最初の起動で URP の global settings が書き換わる（S4-1 から移送した (7)）ので戻してから build する。
- 単体テスト: `ContentOverlayStartupTests`、`ContentOverlayDigestTests`、`PlayerCompatibilityTests`、入口の port を差し替えたテスト、既存の `PlayerContentConfigurationTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.ContentOverlay.ContentOverlayStartupTests;OneStarMaker.Tests.BuildContent.PlayerContentConfigurationTests` と新しい 3 つの test class（名前は B が決め、§6 に書く）。
- 判定必須テスト（GO 候補 head）: 起点 filter、`PlayerOverlayEndToEndTests` の case（X の build、Y の install と AC1・AC3〜AC8・AC10〜AC12、AC2）を別の Unity process で、最終の全 EditMode 回帰（`pwsh tools/run-tests.ps1`）。全 EditMode は既知の VFX 25 件の Inconclusive と staging の `Directory.Move` で落ちうるので、合否は XML の total と失敗した test 名で読む（program の S5-0、既知の受容）。
- 全 EditMode 回帰の適用除外: なし。
- 操作・実行時・目視条件の検証経路: AC1〜AC8・AC10〜AC12 は上の判定の test が閉じる。担当は Phase C の実行担当。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、X と Y が clean、他の Unity と `SampleGame.exe` が動いていない。観測は record・Player の log・game の log・receipt・XML・AC1 の screenshot。事後に `git status --porcelain` と record の pid の終了を CIM で確かめる。証拠は `TestResults/s42/<run id>/`（Player の本体を除く）と XML を evidence bundle に入れる。AC1 の screenshot の token は C と C' が画像で読む。動作確認（Phase B の後）: `対象なし`（offline のテストで閉じない条件は全て Player の build を要し、Phase C が担当する）。
- 未知の操作経路の疎通: 次は未確認で、**初回確認は Phase C**（Unity の test と Build は Phase C の責任）。
  - (u1) 判定の test から 2 つの checkout をまたいで build・install・起動できるか（X の build は X の Unity process、Y の install と起動は Y の Unity process。record で X の出力の path を渡す）。不成立時: 環境の不備は実行担当が記録して owner へ返す。test の形で解けなければ Phase A へ戻す。
  - (u2) IL2CPP・High の Player で `RegisterVerifiedWithOverlay` の経路が strip で失敗しないか。不成立時: SampleGame・Framework の型の `link.xml` は B 適応、それ以外は Phase A。
  - (u3) Title の UI の label を変えた上書きが screenshot で見えるか（UI Toolkit の asset の上書き）。不成立時: 編集の対象を Title の Scene の別の物に変えるのは B 適応。
  - (u4) 入口が Player の process を窓つきで起動し、判定の test が閉じられるか（S4-1 の `WM_CLOSE` の形）。
  - (u5) 要求時解決を配線しない Player で、logical key の層の選択と id による影が content 入口で効くか（13 章の規則。確認地点 AC1）。不成立時: Phase A へ戻す（配線するかは設計判断）。
  - 編集を伴う AC（AC1・AC5・AC6・AC12）は、Y の Unity process を起動する前に編集し、別の process で入口を実行する（実行中の test の中で `.cs` を足すと compile と domain reload で test が切れる。A2 の M-7）。
- 人間の判断が必要な条件: なし（AC1 の token は AI が画像で読む）。
- 高コスト証拠: なし（Player の build は外部への作用が無く、この PC で繰り返せる。X の新しい worktree の import は約 11 分）。
- 判定 C の照合 script の方式: なし。判定 C は record と log と screenshot を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude Opus 5.5、Anthropic）。A0 は program §8 の S4-2 の行と S4-0・S4-1 から移送した S4-2 の項目、§5.3、04・13・18 章、実測の事実の Player の節を入力とした。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。A2 では Anthropic の系列だけを使う。
- 独立性の強化条件を満たせない場合の理由: 凍結前の外部計画レビューが使った系列は Phase A に関与した系列として §5.1 に記録する。C' の充足は起こす時に確かめて §8 に記録する。

### 5.1 A2 の独立レビューと A3 の採否

**A2 の独立レビュー（実施 2026-11-23。互いの指摘を見せていない。全て Anthropic の系列で、別ベンダーは外部計画レビューと C' 用に残した）:**

- (1) **目的レビュー:** 本 session が起動した新しい subagent（Claude Opus 5.5、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `79de02b9e4396881ff5087adf8af8c76e56c9af6eb6dad121396654c4ebf1ede`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。問いは「この目的のために何を保証すべきか」「何を作らないべきか」の 2 つだけ。
- (2) **A0 だけからの代替構成:** (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は A0 の packet（program の S4-2 の行、「S4-0 の A1 が振り分けた入力」「S4-0 から移送した入力」「S4-1 から移送した入力」、§5.3 の Player の実測の原文。sha256 `3c9d33ecd4507be1350202e48c60fcdb8fa73081b7dbd8781fb5954138157e2f`）と、固定 base `116b00918dbdefa9f2dff5aeec7e43ef4a8b4a30` の clean な detached checkout。本書は読んでいない。
- (3) **アーキテクチャゲート兼、各不変条件を破る順序・受け入れ条件の曖昧さ・検証経路の実行可能性・分割・未決事項:** 本 session が起動した別の新しい subagent（Claude Opus 5.5、Anthropic。A1 の主担当と同じモデルの別 session）。入力は A2 入力版の本書（内容 sha256 `416b02828ea53e5213fc3c558d27b67e14764d43d5d899298181ae19099215ba`）と (2) と同じ固定 base の checkout。枠は「§3・§4 の計画した code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。

**A3 の採否（統合担当: 本 session。目的と利用者の欄は A1 の後に変えていない。目的レビューのやり直しは無い）:**

(1) 目的レビュー。保証すべき 10 点と作らないべき 12 点のうち、初稿に無かった・弱かったもの:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 上書きの改ざんは Player が起動時に照合する（Editor だけで終わらせない） | 初稿のとおり | D3 は Player が正本。Editor の Play での必須化は §2.5 の (2) |
| script の field の差を既定値で読む縮退も失敗として扱う | 採用 | D4 の指紋は code と field の区別なく止める。型の欠けも止める |
| 理由が利用者に届く（exit code だけで終わらせない） | 採用 | D6 の (7) で Player の log の理由の行を Console と dialog に出す（(2) の代替構成とも一致） |
| 重ねた結果が見える（base の同名 asset が残らない） | 採用 | AC1 の screenshot の token。Player が content から読まない root の編集は止める（(3) の B-2） |
| base と Player の配置を書き換えない | 初稿のとおり | I6、AC10 |
| fallback・強制起動・検証の skip・hot reload・自動の作り直し・migration・Windows 以外を作らない | 初稿のとおり | §1 の対象外 |
| 上書きの起動用に別の receipt を作らない | 初稿のとおり | 遊べる Player は receipt を書かない（S4-1） |
| Player・base・hash を手で入れさせない | 初稿のとおり | UC2 は menu 1 つ |

(2) A0 だけからの代替構成。推奨案（同じ build の base に限る、Decide を `playable` と pair と key の時だけ開ける、Player が上書きの file の hash を検証する、script と型の照合は Editor が起動の前に行う、要求時解決は配線しない、menu 1 つ）は初稿と同じ骨格だった。違った点の採否:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 計画の `suppliedSceneKeys` を空にする（Player に snapshot の供給元が無い） | 採用 | D6 の (3) |
| script の指紋の 3 点の一致（Player の build 時・今・上書きの build 時。再利用は script を鍵に含まない） | 採用 | D4 |
| Player の `build-receipt.json` の `transportManifestSha256` と install の digest を突き合わせる | 採用 | D6 の (1) |
| Player の log から理由の行を拾って示す | 採用 | D6 の (7) |
| 指紋は assembly でなく source から取る（`.rsp`・define を含める） | 採用 | D4 (a)（(3) の M-3 (b) とも一致） |
| 型の材料が無い時は「base の閉包が使う型の部分集合」に落とすか分ける | 採用（owner の裁定を経る） | §2.4 |
| 配線なしで層と影が効くかを未知として確かめる | 採用 | u5（AC1） |
| Player の key を ticket（file の一覧・base の欄・`playerBuildGuid`・Unity の版）の path と SHA-256 の pair にする | 一部採用 | 完全性は D3 の digest 1 つで足りる。base の欄は上書きの root の記録と base の照合が既にある。`playerBuildGuid` の結び付けは script の照合が入口だけなので効きが薄く、Runtime の値が増える。ticket の file は作らない |
| 上書きの検証を予約（`ContentRevisionGate`）の中でやり直す | 不採用（owner に確かめる） | session と gate の層を変える。§2.5 の (5) |
| 測定のスライス（S4-2a）と製品のスライスに分ける | 不採用 | 凍結の前の G0 が同じ役を果たす（§2.4） |
| CI の Player の受け取り方（U8） | 後続 | 手元の `artifacts/playable/players/<id>/` に置く規則だけを決め、取得は S4-3（D6 の (1)） |
| 上書きの置き場の path の長さ（U7） | 採用（記録） | 判定の test の事前の `run-tests.ps1` の path の検査で見る。予算を超えたら B 適応で置き場を短くする |

(3) アーキテクチャゲート兼反証。blocker 2 件、major 8 件、minor 9 件。

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| B-1 | 判定の test を `OneStarMaker/Tests/Editor/` に置くと Framework から SampleGame への参照が要る（S4-1 の test は `SampleGame/Tests/Editor/Build/` にある） | 採用 | §3 の置き場を `SampleGame/Tests/Editor/Build/` に直した |
| B-2 | Player は bootstrap の UICommon と SceneResourceMap を登録より前に同梱・baked から読むので、それらの編集は上書きに入っても効かない（I1 が崩れる） | 採用 | D6 の (4) で止め、AC12 を足した |
| M-1 | Player が Editor の `SAMPLEGAME_*` の環境変数を継承する | 採用 | D6 の (6)、AC8 (b) |
| M-2 | digest の key だけの起動が Decide の最初の `None` で base だけで始まる | 採用 | D2、AC4 (d)、AC9 |
| M-3 | 既存の class の strip、指紋の対象の漏れ、材料が無い時の縮退案が program の行を満たさない | 採用 | D4 の `link.xml` と指紋の対象、§2.4 を owner の裁定に直した、G0 (b) の記録項目 |
| M-4 | Player の game の log は `players/<id>/` の中に書かれ AC10 と矛盾する | 採用 | I6・AC10 の観測の時点と除外、`-logFile` を work の下に渡す |
| M-5 | AC4 (a) は製品の入口では再現できない。再利用は増えた file を拒まない | 採用 | AC4 (a) を port の差し込みの test に、build の時の digest を記録して再利用で比べる（D3、AC4 (e)） |
| M-6 | 入口は Y の artifacts しか見ないので AC1 の Player を見つけられない | 採用 | D6 の (1) の規則と AC1 の初期状態 |
| M-7 | 実行中の test で `.cs` を足すと compile と domain reload で切れる。AC6 の前半は AC5 と同じ | 採用 | 指紋を `Prepare` の前に、編集は process の前に（§5）、AC5 と AC6 を統合した |
| M-8 | 責務マップに `PlayerBuildPublisher` が無い。compat を publish の後に足すと compat の無い Player が見える | 採用 | staging の中で書く（D4、§3） |
| m-1 | Decide に渡す種類は検証済みの baked | 採用 | D2 |
| m-2 | Editor でも digest の key があれば照合する | 採用 | D3 |
| m-3 | E1 の「操作を続けられ」を AC1 が見ていない | 採用 | AC1 に Home への遷移 |
| m-4 | AC3 の作り方と照合の順 | 採用 | AC3 |
| m-5 | AC1 の Error の行の log と URP の strip の行 | 採用 | AC1 |
| m-6 | `UnbuildableOnly` の AC が無い | 採用 | AC8 (c) |
| m-7 | `ContentOverlayPlayConfig` の remarks が偽になる | 採用 | D2、§4 |
| m-8 | I3 は破れなかった | 記録 | — |
| m-9 | 現況の引用は code と一致（B-1 を除く） | 記録 | — |

**外部計画レビュー:** 未実施（Draft PR で行う）。

## 6. Phase B 実装結果

（未着手）

## 7. Phase C

（未着手）

## 8. Phase C'

（未着手）

## 9. Phase D

（未着手）
