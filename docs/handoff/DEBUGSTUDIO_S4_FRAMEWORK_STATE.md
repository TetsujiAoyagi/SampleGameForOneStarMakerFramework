# DebugStudio S4 — SceneDirector の Scene の状態を CLI から読む `osm.scenes`

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `判定 C 完了`（A3 凍結済み、revision 8。GO 候補 head は implementation head。判定必須テストと AC11 の V0〜V6 の証拠が GO 候補 head で揃った。§7。次は C'。C' の blind bundle の stage は task の登録の後）
- branch: `claude/magical-gauss-7rycuj`（Cloud の計画 session の branch。PR の base は `develop`。Draft PR で外部計画レビュー中）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`origin/develop` の head、2026-11-25。本書の実測は全てこの base）
- implementation head commit: `0b933a5d8f3a90cd2e589a3a267c545e3483e286`
- A3 凍結記録: 承認者は owner（root、依頼者）。2026-11-25（UTC）。対象は revision 8（commit `d151fdc8` の本文に、この凍結の記録と引継ぎ票を足した版）。根拠: owner が本 session で「それ終わったら凍結しよ」と指示した（主担当の自己点検の反映の後）。PR #216 の外部計画再レビューは「文書の修正と既定の docs-audit が済めば A3 凍結に進んでよい」とし、docs-audit と contract-audit は head `d151fdc8` の CI の `audit` ジョブ（`offline-checks.yml`）で実行されている。次の担当は Phase B で、owner の指示により Cloud の session が担う（「ローカルが長めの試験やる予定なので実装自体はクラウドでやりたい」。下の引継ぎ票の例外）。
- risk: `normal`（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または A3 の凍結で置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/05-scene.md` §5.12（外から読める Scene の状態、出力の意味、観測が状態と寿命を変えないこと。あわせて §5.12 の `SceneEvent` の欄の記述が code に遅れている点を直す）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（Framework が Editor でだけ持つ内部の catalog と振り分けの dispatcher）、`tools/DebugStudio/src/DebugStudio.Cli/README.md`（`osm.scenes` の送り方と出力の読み方）
- 関連: [DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md)（デバッグコマンドの基盤の構想。未承認。本スライスが作らない物と、将来の観測の形の置き場）
- Phase A snapshot の取得案内・状態: 不要（A3 の時点に機械向けの期待値・合成 fixture・script は無い。判定 C の offline 実行。§5）。凍結版は commit で固定し、その SHA を tree の外で渡す
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: いずれも未生成（未保存）。中身は進行役が Git 外にローカル保全している（Phase B result は implementation head の完全 diff と audit の log。evidence bundle は判定 C の判定必須テストの XML・log と V0〜V6 の原記録、判定 C の照合 script と結果。C' blind bundle はまだ組んでいない）。task の登録の後に implementation head から stage し、取得案内を残す。引渡し先・担当: evidence bundle は Phase D・owner（C' の後の突き合わせ）、C' blind bundle は C' の担当
- task: 未登録（owner が start を実行したと伝えたが、`workflow-task.ps1 status -Task debugstudio-s4` は `unregistered`。進行役の start は自動の許可判定に拒否された。2026-11-27。owner の確認待ち）
- 引継ぎ票（判定 C の出口、2026-11-27）:
  - 次の Phase・役割: C'（GO 候補 head = implementation head の独立監査）。その前に進行役が Phase B result snapshot・evidence bundle・C' blind bundle を stage する。
  - 次の作業: task の登録の後、implementation head から Phase B result snapshot（所見を含まない）、evidence bundle（判定 C と同じ原記録と、判定 C の照合 script と結果）、C' blind bundle（凍結した Phase A、所見を含まない Phase B 結果、本書を除いた base から head までの diff、判定 C と同じ原記録。発見 C・判定 C の所見、照合 script と結果は入れない）を固定 manifest つきで stage し、別の process の取得で全 file の一致を確かめて §0 に取得案内を残す。C' の担当の起動は owner に確かめてから行う。C' は本書と PR を読まない。進行役は C' の結果を §8 に転記する。
  - 読む範囲: 進行役は §0・§7・§8。C' の担当は blind bundle だけ。
  - 必要な権限: Artifact Storage の stage と取得（task の登録の後）。Unity は起動しない（原記録は揃っている）。
  - 取得案内の所在: 上の欄のとおり未生成。
  - task: `debugstudio-s4`（未登録。stage はその解消の後）。
  - 停止規則: 実装を直さない。判定をやり直さない。C' blind bundle に発見 C・判定 C の所見、照合 script と結果を入れない。bundle の file を加工しない。
  - 引き渡さないもの: 発見 C・判定 C の所見と結論（C' へ）。
### 実測の方法

- base `07cc2c97` の code と文書を `grep`・`wc -l`・行範囲の読み取りで確かめた。Unity も .NET も起動していない。
- 本書の作成環境（Cloud の Linux container）に `pwsh`・`dotnet`・Unity が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。

### 改訂の経緯

- revision 1（commit `f662d9ae`）: 状態の source の表と、Framework のコマンドを Game の dispatcher の前で振り分ける合成。
- 分割版（commit `1d1ad645`）: コマンドの口（宣言・一覧・予約の強制）を S4a、Scene の読み取りを S4b に分けた。
- revision 2（commit `b3f06b7f`）: 1 スライスに戻し、hook の置き換えを共通の口とし、デバッグコマンドの将来の契約を「今決める D1〜D9 / 後から足す L1〜L10」に分けた。
- revision 3（commit `7948b6a7`）: owner の補足（2026-11-25、§1.1）で目的を「Hierarchy と同じように Scene の存在・状態・寿命を観測する方向の最初の一歩」に戻し、基盤の整備を目的から外した。owner の判断「共通の口にしたのが無理をさせてるならやめてもいい」（2026-11-25）で共通の口（hook の置き換え）をやめ、revision 1 と同じ形の内部の振り分けに戻した。これにより、owner が 2026-11-25 に決めた「既存の `CreateDebugCommandDispatcher()` の override は新しい hook へ移して廃止」は取り消した。D1〜D9 / L1〜L10 の表は [DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md) へ移した。
- revision 4（commit `c79bc8f9`）: owner が渡した最終の提言（2026-11-25。§1.5 の意見 Z）を反映した。§1.9 の U1・U2 と risk を確定した。目的の「表示のための GameObject や MonoBehaviour を作らずに」を、本スライスの範囲の記述に改め、Framework の設計原則や将来の制約として読めないようにした（§1.1）。Scene の存在の意味を写しと event で分けて書いた（§1.3、§1.6）。旧 service の handler が新しい Play の SceneDirector を読みうることを、解決ではなく読み取りに限って許容した制約として書き直した（§2.2）。cancel 済みの Framework のコマンドを Game へ渡さないように振り分けを改めた（§2.1 の 1）。
- revision 5（commit `748d4c8e`。A3 統合案）: A2 の目的レビューとアーキテクチャゲートの指摘を §5.1 の採否のとおり反映した。主な変更: Editor / Player の判定と Framework のコマンドの登録を構成の関数 `FrameworkDebugCommands.Compose` に切り出した、新規 file の namespace を folder で分けない、振り分けは内部の `CatalogDebugCommandDispatcher` へ委譲する、出力の整形を純関数 `Format` にした、`ReleaseAll` の null 化を AC にした、dispatcher の鎖を通した同一性のテストを足した。A3 で固定する物（oracle の導出の規則、比較の規則、V3 の操作、V5 の識別の条件）を §5 に固定した。
- revision 6（commit `0e800529`）: PR #216 の外部計画レビュー 1 巡目（2026-11-25、owner のアカウントのコメント）を §5.1 の採否のとおり反映した。unload の待ちが true になる場面の単体テスト（AC4b）、比較の規則の強化（存在の条件・形・件数・一意性・順・保留と未判定の扱い）、V3 の完了の観測、I1 の主張と証拠の分担と oracle の観測項目の追加、名前の振り分けが完全一致であることの明記、組込みのコマンドの確認。
- revision 7（commit `9c90c5fb`）: PR #216 の外部計画再レビュー（2 巡目）を反映した。V3 の完了の観測が不成立なら未判定で止めること（E2 を狭めない）と印の扱い、V6 の復元、V2 の変化の扱い。
- revision 8（本書。A3 凍結版）: 主担当が Phase C の観点で読み直した自己点検を §5.1 のとおり反映した。V3 の切り替え先を `OutGameSession` に変えた（`HomeScene` は `Stable` で InGame へ自動で進むため）、V1 の Play の入口と初期状態、E3 の根拠、V4 の root の選び方、oracle の自動 property の読み方、AC9 の呼び方。

## 1. 目的と対象外

### 1.1 目的と利用者（A2 の目的レビューへ渡す原文の候補）

- 目的: 既存の Hierarchy で GameObject の存在や状態を観測できるのと同様に、SceneDirector が管理する Scene の存在・状態・ライフサイクルを DebugStudio から観測できるようにする。その最初の一歩として、Editor の通常の Play 中に、SceneDirector が管理する Scene の一覧と各 Scene の `SceneState`、遷移の履歴を、DebugStudio の CLI からその時点の値として読めるようにする。最初の一歩は、ライフサイクルの変化を観測する将来の形とも、Unity の Hierarchy や Inspector など別の観測手段とも矛盾しないこと。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント（検証の driver を書く担当と、判定 C の実行担当を含む）。
- 文言の出どころ（owner、2026-11-25）: 「既存の Hierarchy で GameObject の存在や状態を観測できるのと同様に、SceneDirector が管理する Scene の存在・状態・ライフサイクルを DebugStudio から観測できるようにしたい、というのが本来の目的である。デバッグコマンド基盤の整備自体が目的ではない」「最初のスライスは CLI から Scene の現在状態を読めるだけでも構わない。ただし、Scene の寿命を可視化するという本来の方向性と矛盾しないこと」。それ以前に owner が決めたこと: 表示先は CLI の JSON だけ、実行形態は Editor の Play だけ。revision 4 で改めた文言（owner の提言、2026-11-25）: revision 3 の目的は「表示のための GameObject や MonoBehaviour を作らずに、DebugStudio の CLI から…」だった。owner は「DebugStudio を唯一の観測手段とするつもりはない」「『表示のための GameObject や MonoBehaviour を作らない』という記述は今回の実装範囲を示すものであり、Framework 全体の設計原則や将来的な制約にはしないでほしい」と示したので、その句を目的から外し、本スライスで Unity 向けの表示を作らないことは §1.2 の対象外に書いた。利用者の入力は変わらない。

### 1.2 対象外

- デバッグコマンドの共通の登録の口、宣言・一覧・名前空間の強制、Player への公開、Scene 寿命のコマンド、read の出力の形の統一（[DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md) の構想。本スライスでは作らない）。
- ライフサイクルの変化の観測（`OnSceneEvent` の流れの配信、Hierarchy と同じ push と WPF の保持、時系列の表示）。本スライスは写しだけを作り、将来の形と矛盾しない条件（§1.6）だけを守る。
- WPF の表示、protocol・wire の変更、CLI の新しいサブコマンド。既存の `send` に載せる。
- Unity の Hierarchy・Inspector から Framework の状態を見るための表示（DontDestroyOnLoad に置くデバッグ表示用の GameObject と MonoBehaviour など）。本スライスでは作らないが、将来の観測手段として否定しない。同じ写しのモデル（§1.6 の C1）を使える形にしておく。
- Player（Build した実行ファイル）と遠隔接続。Player では何も変えない。
- 書き込み。Scene の load・unload・遷移、`SceneState` の変更、履歴の変更。
- `osm.scenes` の追加の項目（静的な木の parent / children / loadType、進行中の操作、取消の窓、要求時解決の記録）。足せる形にはする（§2.1 の 5）。
- Scene 以外の状態（AssetManagement、UpdateSystem など）。
- SampleGame の code と asset、`DebugCommandCatalog`、組込みの `ping` / `runtime-diagnostics` の変更。
- `SceneState` の値・順序、`SceneLifecycleManager` の遷移規則、SceneDirector の挙動（履歴の Push の時点を含む）。
- hierarchy に DontDestroyOnLoad を出すこと、Play をまたぐ NodeId の識別、inspect の自己応答保証（S3 から移送された項目）。

### 1.3 現況（base `07cc2c97`）

**Hierarchy の観測の仕組み（比較の基準）:**

- Runtime の `DebugSocketService` が `SceneManager` の読み込み済みの Scene を走査して GameObject の木を写し（`DebugSocketServices/Hierarchy/DebugSocketHierarchyPublisher.cs`、320 行）、接続の握手の後に全体の写しを、`sceneLoaded` などの Unity の callback の後に revision 付きの差分を WPF へ push する。
- WPF は受けた写しを `HierarchyStore` に保持し、CLI の `hierarchy` は Unity に問い合わせずに保持分を `status/reason/data` の JSON で返す（`hasSnapshot`、revision、capture の時刻、接続の状態を含む）。`inspect` は Unity へ問い合わせる。
- 正本は Unity の Scene と Transform で、Framework の外の構造。NodeId は service ごとの採番で Play をまたいで同じ番号が別の object を指しうる。

**Scene の状態（全て `Runtime/SceneSystem/`）:**

- 実行時の状態は `SceneDirector.cs`（336 行。ほかに partial の `.Loading.cs` 747・`.Unloading.cs` 478・`.Transitions.cs` 256・`.FailedLoadCleanup.cs` 349）の private field: `_currentScenes`（`Dictionary<string, ScenePair>`、:77。親子の link は無い）、`_pendingUnloads`（`HashSet<string>`、:78）、進行中の操作の 4 つの辞書（:79〜82）、`_sceneHistory`（`Stack<SceneHistoryEntry>`、:104。上限 8）。
- 各 Scene の `SceneState` は `SceneBase.Lifecycle`（`internal SceneLifecycleManager Lifecycle => _lifecycle;`、`SceneBase.cs:221`）の `State`（`public SceneState State => _state;`、`SceneLifecycleManager.cs:20`）。どちらも field を返すだけ。`SceneLifecycleManager` だけが `_state` を書く。
- 読み取りの口: 公開は `ISceneQuery`（`GetLoadedScene`・`IsSceneLoaded`・`IsSceneStable`・`GetContentLoadRecord`）、`CanGoBack`、`OnSceneEvent`。internal に「テスト用」の `ContainsScene`・`GetSceneState`・`HasPendingUnload`・`SceneHistoryCount`（:271〜283）。**全 Scene を列挙する口と履歴の中身を読む口は無い。**
- 履歴: `SwitchScene` は PoNR を過ぎた後、`UnloadScene` と `AddScene` の前に `_sceneHistory` へ Push する（`SceneDirector.Transitions.cs` の「履歴記録（PoNR 通過後に記録するため…）」の箇所）。**履歴は成功した遷移だけの記録ではなく、戻り先の管理用の stack である。** `GoBack` は遷移の成功の後にだけ Pop する。`CanGoBack` は `_sceneHistory.Count > 0`。
- 管理集合に入る時点と出る時点: `AddScene` の経路は、Scene を `_currentScenes` に加えてから（`Loading.cs:430`）`PreLoading` の通知と `ExecutePreLoad` に進む。unload の完了は `_currentScenes.Remove` の直後に `Removed` を出し（`Unloading.cs:293〜296`）、取消の後始末は `Remove` の直後に `CancelCleanedUp` を出す（`Unloading.cs:400〜403`）。**写しの「管理集合にある」は PreLoad の前から Remove までで、`Added` の event（`Stable` に達した時。`Loading.cs:593〜596`）より早く始まる。**
- 変更は全て main thread の UniTask の async method の中で起きる（thread pool への切替え・`lock` は無い）。frame の間に main thread で読むと thread の競合は無いが、操作の途中の状態が見える。
- `SceneState` は 14 値（`None`=0 … `LoadCanceled`=7, `Stable`=8 … `AfterUnloading`=13）。

**`OnSceneEvent`（ライフサイクルの変化の既存の通知）:**

- `SceneEvent`（readonly struct）は Type（`StateChanged` / `Added` / `Removed` / `CancelCleanedUp`）、SceneIdentify、State、ElapsedMs（直前の段の所要時間）、TimestampUtcTicks を持つ。R3 の `Subject`。05-scene.md §5.12 は購読者が SceneDirector の挙動に影響してはならないと定める。
- 状態の遷移のほとんどは `TransitionSceneState`（`SceneDirector.cs:326〜334`）が `Lifecycle.TransitionTo` と `StateChanged` を対で出す。`SceneBase` が自分で遷移する `PreLoading` / `PreLoaded` / `PreUnloading` / `PreUnloaded` は SceneDirector が前後で手で通知する（`Loading.cs:437〜441`、`Unloading.cs:234〜244`）。`PreLoading` と `PreUnloading` の通知は実際の遷移の直前に出る。`AfterUnloading` は単独の `StateChanged` を持たず、`Removed`（`Unloading.cs:294`）に載る。再開の経路と失敗の後始末の経路（`FailedLoadCleanup.cs`）で全ての遷移が通知されるかは未確認。
- event に通し番号は無い（Hierarchy の revision に当たる物が無い）。
- 別に telemetry の span（`SceneLoad`・`SceneUnload`・`SceneTransition`）が DebugStudio へ流れる（接続中だけ、問い合わせ不可）。

**コマンドの経路:**

- `AbstractApplicationInitializer.CreateDebugSocketService(AppConfig)`（`Runtime/Bootstrap/AbstractApplicationInitializer.cs:1428〜1437`、protected virtual、1486 行の file）は enabled が false なら null、true なら `new DebugSocketService(options, CreateDebugCommandDispatcher())`。`CreateDebugCommandDispatcher()`（:1443、protected virtual）の既定は `NullDebugCommandDispatcher.Instance`。**Framework が自分のコマンドを足す口は無い。** SampleGame の `AppInitializer`（`AppInitializer.cs:230〜240`）は override して自分の catalog に Editor のときだけ `debug.set-active` を登録する。
- `DebugSocketService` は受けた dispatcher を `MainThreadDebugCommandDispatcher` で包んで readonly の `_dispatcher` に持つ（`DebugSocketService.cs:129`、:151）。`Dispose()`（:340）は `_dispatcher` を手放さず、`StopAsync().Forget()`（:351）で停止を非同期に始める。**Dispose が返った時点で停止が完了しているとは限らず、旧 service から dispatcher への参照は残りうる。**
- `CatalogDebugCommandDispatcher`（`DebugSocketServices/`、public sealed、64 行）: command が null なら固定の失敗、事前に cancel 済みなら固定の失敗、それ以外は `catalog.TryExecute` の結果を envelope に写す（RequestId・Success・Message・PayloadJson）。handler の例外は伝播し、router が失敗の envelope にする。
- `DebugCommandCatalog.TryExecute` は名前が解決できたかを bool で返す（未登録なら false と固定の失敗の結果）。名前は Ordinal の完全一致。
- 未登録の文言: `NullDebugCommandDispatcher` は `Debug command '<名前>' is not registered.`、catalog は `Debug command is not registered.`。
- 組込みの `ping` / `debugsocket.ping` / `runtime-diagnostics` / `debugsocket.runtime-diagnostics` は router が大小文字を区別せずに先に処理する（`DebugSocketBuiltInCommandHandler.cs`）。
- CLI の `send` の出力は `CommandResult request=… success=… message=…` の行と payload の行で、`hierarchy` / `inspect` の `status/reason/data` の JSON とは形が違う。

**生成と寿命:**

- DI container は無い（手動 DI。[03-di.md](../../unity/Assets/Docs/Architecture/03-di.md)）。SampleGame は `static readonly AppInitializer s_instance` を 1 つ持ち、Play をまたいで同じ initializer を使う。
- BeforeSceneLoad で DebugSocket（と dispatcher）を作り、AfterSceneLoad で `SceneDirector` を作る（:439）。initializer には `protected SceneDirector? SceneDirector`（:128）がある。`ReleaseAll` は services を Dispose してから（:1097）、`_sceneDirector` を Dispose して null にする（:1108〜1109）。
- Enter Play Mode Options が有効で Domain Reload が無効。Play ごとに service と SceneDirector を作り直す。
- 全て `OneStarMaker.Runtime.asmdef` の中で、asmdef の参照の追加は要らない。`Runtime/DebugCommands/` は .NET の offline テスト（`tools/DebugCommandOfflineTests`）が Unity なしで compile する System だけの folder（本書は触らない）。

### 1.4 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef の参照と package を追加しない。
- `SceneState` の 14 値を減らさず並べ替えない。状態の変更は `SceneLifecycleManager` が所有する。出力に整数値を出さず名前で出す。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側の `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- Editor のコードを Runtime の asmdef に置かない。
- path は `tools/path-budget.json` の予算（repo 相対 150 文字）に従う。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 / S3 の契約（`--listen` の待受け、`send` / `hierarchy` / `inspect` の出力と exit、`debug.set-active` の挙動、Player では書き込みのコマンドを登録しないこと）を壊さない。
- 05-scene.md §5.12: `OnSceneEvent` の購読者は SceneDirector の挙動に影響してはならない。本スライスは購読もしない。
- program の停止規則: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。本スライスは `AbstractApplicationInitializer.cs` の DebugSocket の生成の箇所（:1428〜1437）を触るので当たる。
- 人間が開いた Editor を強制終了しない。共有の Editor を触らない。並行の S4-V・S4-3a の Unity の実行と時間を分ける。

### 1.5 原資料と外部の意見の採否

- S3 の HANDOFF（commit `d0e4c03c` の親の `docs/handoff/DEBUGSTUDIO_S3.md`）: §2.1 の配線、§2.4 の後続へ送った項目。
- 外部の意見は 3 件で、どれも owner が別途依頼して要旨を本 session へ渡した。A2 に数えない。
  - 意見 X（2026-11-25、分割版 `1d1ad645` が対象）: 宣言・一覧は今は要らない、1 スライスに戻す、Player の未登録の文言が変わる不整合、escape の規則の食い違い。revision 2 で反映済み。
  - 意見 Y（2026-11-25、revision 2 `b3f06b7f` が対象。owner の補足とは別の設計レビュー）: P1-1〜P1-7、P2-1・P2-2。
  - 意見 Z（2026-11-25、revision 3 `7948b6a7` と構想が対象。owner が「最後の提言」として渡し、本書は owner の確認として扱う）: (1) U1 は現行案、U2 は Framework の名前を優先して予約名を文書化、risk は normal、共通の口・Player・宣言・一覧は作らない、A2 へ進めてよい。(2) DebugStudio を唯一の観測手段にしない。DontDestroyOnLoad のデバッグ表示用の GameObject と MonoBehaviour から Unity の Hierarchy・Inspector で Framework の状態を見る方法も有用で、両方あってよい。正本は各機能が持ち、表示は観測するだけで、機能と寿命に介入せず、消してもゲームに影響せず、不要な時にコストを生まず、Inspector と DebugStudio で状態の取得を重複させない。「表示のための GameObject や MonoBehaviour を作らない」は今回の範囲であって原則にしない。構想に Unity の Hierarchy・Inspector の価値と位置づけを足す。(3) A3 までに: Scene の存在の意味を写しと event で分ける、旧 service の handler が新しい Play を読むことを許容した制約として扱う、cancel 済みの Framework のコマンドが Game へ渡る設計を見直す、oracle・比較・切り替えの手順・再 Play の識別を A3 で固定する。(4) 観測の仕組みを機能の側に置き、用途に応じて Inspector・DebugStudio・CLI から見る。共通化は観測したい物が増えて必要が見えてから。
  - 意見 Z は全て採用した（反映先は §0 の改訂の経緯の revision 4 の行）。
- 意見 Y の採否（owner の補足「今回の目的に直接関係する指摘と、将来の基盤整備に関する指摘を分けて」に従う）:

| 指摘 | 分類 | 採否 | 反映先 |
|---|---|---|---|
| P1-6 出力の意味（履歴は戻り先の管理用の stack、途中・失敗でも entry がある、`canGoBack` は成功の保証ではない、`scenes` は管理集合） | 目的に直接 | 採用。履歴の Push の時点は code で確かめた | §1.3、§2.1 の 5 |
| P1-7 独立した正本での照合（集合の完全一致、安定と途中の分離、履歴の順と内容、空と未生成の区別、機械判定） | 目的に直接 | 採用 | §2.3 E1・E2、§5 |
| P1-4 寿命の主張の正確さ（Dispose は dispatcher を手放さない、`StopAsync().Forget()`、`() => SceneDirector` は initializer を捕まえる） | 目的に直接（読み取りの値が今の Play の物であること） | 採用。Dispose の挙動は code で確かめた。読み取りでは「旧 Play の値を出さない」を契約にし、旧 handler が書き込みを行う問題は構想へ送る | §1.3、§2.3 I2、§5 V5、構想 |
| P1-5 登録の失敗で起動が止まること | 共通の口をやめたので該当しない | 不要（名前の衝突は振り分けで Framework が優先し、例外を投げない） | §2.1 の 1 |
| P1-1 登録の機構と公開の方針の分離 | 将来の基盤 | 構想へ。本書では「Editor でだけ Framework の catalog を作る」を composition root の方針として書く | §2.1 の 2、構想 |
| P1-2 コマンドを足す開発者の体験 | 将来の基盤 | 構想へ。本書の配置（`SceneSystem/Diagnostics/`）を最初の例として書く | §3、構想 |
| P1-3 名前空間と組込みの大小文字 | 将来の基盤 | 構想へ。本書では README に組込みの名前が先に処理されることを書く | 構想 |
| P2-1 機械可読な入出力 | 一部が目的に直接 | `unavailable:` の接頭辞を本コマンドの暫定の出力とし、全体の契約にしない。基本契約は構想へ | §2.1 の 5、構想 |
| P2-2 発見のしやすさ | 将来の基盤 | 構想へ。本書では README に載せる | 構想 |
| 総評「将来の Framework と Game の開発者がコマンドを自然に足せる設計かを A2 の独立観点に」 | 将来の基盤 | 不採用。owner の補足「デバッグコマンド基盤の整備自体が目的ではない」を優先する | — |

### 1.6 観測の構図（Hierarchy との比較と将来の形）

| | Hierarchy（既存） | Scene の観測（本スライスと将来） |
|---|---|---|
| 正本 | Unity の Scene と Transform（Framework の外） | SceneDirector の private な field（列挙の口が無い） |
| 今の値 | 写し（接続時に全体） | 写し（本スライスは要求時に pull。`CaptureDebugSnapshot`） |
| 変化 | Unity の callback → revision 付きの差分を push | `OnSceneEvent`（既存。所要時間と時刻を持つ。通し番号は無い）。本スライスは使わない |
| 保持と読み | WPF が保持、CLI は保持分を読む | 本スライスは保持しない（毎回 Runtime へ問い合わせる） |
| 時間軸 | 無い | ある（14 状態の列と各段の所要時間）。寿命の可視化の本体 |

- 写しは「今どう在るか」（管理集合、各状態、unload の待ち、履歴）、event は「どう変わったか・どれだけかかったか」を担う。寿命の可視化の主役は event の時系列で、写しは途中から見始めた時の初期値になる。将来の形は Hierarchy と同じ「接続時の写し + 以後の event」。
- 本スライスが将来の形と矛盾しないための条件:
  - **C1** 写しのモデル（`SceneDirectorDebugSnapshot`）は SceneSystem に置き、コマンドと JSON から独立させる。将来 push の message にする時は同じモデルを使う。
  - **C2** 写しの語彙を `SceneEvent` と揃える（Scene の id は `SceneIdentify` と同じ文字列、状態は `SceneState` の名前）。event を写しに当てて更新できる。
  - **C3** 写しと event の前後を決める通し番号は今は作らず、後から写しの項目と `SceneEvent` の項目として足せる形にする（出力は項目を足すだけ。§2.1 の 5）。
  - **C4** 写しは要求された時だけ作る（pull）。観測していない時に継続して写しを作るコストを生まない。
- **存在の意味の違い:** 写しの `scenes` に載るのは SceneDirector の管理集合にある Scene で、PreLoad の前（`_currentScenes` に加えた時）から `Remove` まで。event の `Added` は `Stable` に達した時に出るので、写しには `Added` の前の Scene（`PreLoading`〜`Initializing`）も載る。`Removed` / `CancelCleanedUp` は `Remove` の直後に出るので、出る側の境界は写しと event で揃う。将来 event を写しに当てて寿命を表示する時は、入る側の境界が `StateChanged`（`PreLoading`）であって `Added` ではないことに注意する。今回 event は変えない。
- **別の観測手段との関係:** 写しのモデルは CLI 専用ではない。将来 Unity の Hierarchy・Inspector 向けのデバッグ表示（DontDestroyOnLoad の GameObject と MonoBehaviour）を作る時も同じ `CaptureDebugSnapshot` を使い、状態の取得を重複させない。表示は正本を観測するだけで、消してもゲームに影響しない形にする（構想の §2 の観測手段の節）。
- 本スライスで分かった将来の課題（構想へ送る）: `PreLoading` / `PreUnloading` の通知が実際の遷移より前に出ること、`AfterUnloading` が `Removed` に載ること、取消の後始末の `CancelCleanedUp` が `AfterUnloading` まで進んだ後に `LoadCanceled` の状態を載せて出ること（`Unloading.cs:396〜403`）、`Added` が管理集合に入った時ではなく `Stable` の時に出ること、再開と失敗の後始末の経路の通知が未確認であること。event だけで寿命を再構成する前に確かめる必要がある。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 Scene の状態を読む | コマンド名 `osm.scenes`（README の固定値）、payload `{}` | 出力の中身（§2.1 の 5）、読んだ時点の frame の番号 | 実行時の操作 | 「Scene の一覧と各 Scene の `SceneState`、遷移の履歴を…読める」 |
| UC2 読めないとき | 新しい入力は無い | `unavailable: …` の区別 | 実行時の操作 | 「その時点の値として」。読めない時に古い値や空を正常と誤読させない |

A1 自己点検: 準備の操作も開発時の宣言も無い。SampleGame の code・Scene・設定 asset・config は変えない。Game の開発者の作業は増えない。

### 1.8 スライスの分割の検討（A1 の終わり）

- 契機: 「Framework がコマンドを持つための内部の振り分け」と「Scene の写しとコマンド」は独立して変わる理由を持つ。
- 判断: 分けない。振り分けは `osm.scenes` を届けるためだけの内部の部品で、単独では観測できる成立条件が無い。どちらも同じ Editor の Play の 1 回で判定でき、独立して失敗しうる未知は無い。

### 1.9 未決事項

なし。revision 3 の U1・U2 は owner が 2026-11-25 に確定した（§1.5 の意見 Z）。

- U1（確定）: `osm.scenes` の出力の項目は id・`SceneState`・unload の待ち・履歴・`canGoBack`・`capturedFrame`。静的な木、進行中の操作、取消の窓は後から足す。
- U2（確定）: Framework の名前を優先する。Editor では Framework の名前が先に引かれ、Game が同じ名前を登録しても Editor では届かない。Framework が登録した名前（Ordinal の完全一致。今は `osm.scenes` だけ）と組込みの名前を README と §10.5 に書く。`osm.` を Framework の名前に使うのは規約で、`osm.` で始まる未登録の名前は Game へ渡る（接頭辞の強制はしない）。

A3 で固定した物（revision 5）: §5 の oracle の導出の規則、比較の規則、V3 の Scene の切り替えの操作、V5 の再 Play の識別の条件。oracle の eval の C# の本文は、[判定 C の照合 script](../../.agents/skills/osm-workflow/references/review-evidence.md#判定-c-の照合-script) の置き場所の規則（照合に使う script の本体を git に入れない）に合わせ、本文ではなく導出の規則を固定し、本文は判定 C の担当が規則どおりに書く。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SampleGame の通常の Editor の Play 1 回の中で、本スライスでは GameObject・MonoBehaviour・event の購読を足さずに、CLI の `send --command osm.scenes` で、SceneDirector が管理する Scene の集合・各 `SceneState`・遷移の履歴を、その時点の値として正本と完全に一致する形で読め、読む前後で状態が変わらず、それ以外のコマンドと Player の挙動が今と同じか。
- 次スライスへ進む最低条件: §2.3 の不変条件 I1〜I4 と成立条件 E1〜E4 を同じ GO 候補 head で満たす。

### 2.1 統合設計（A3 統合案 revision 5）

全ての新規 file の namespace は、置き場所の folder に関わらず既存と同じにする（`OneStarMaker.Runtime.DebugSocketServices`、`OneStarMaker.Runtime.Bootstrap`、`OneStarMaker.Runtime.SceneSystem`）。`SceneSystem/Diagnostics/` に別の namespace を与えると、`SceneDirector`（`SceneSystem`）が写しのモデルを、コマンドが `SceneDirector` を参照して namespace の間に循環ができるため（A2 の F2）。folder の分け方の前例は `DebugSocketServices/Hierarchy/`（親と同じ namespace）。

1. **振り分けの dispatcher（新規 `Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs`、internal sealed、仮称）**
   - constructor で Game の `IDebugCommandDispatcher` を受け、内部に Framework 用の `DebugCommandCatalog` と、それを包む `CatalogDebugCommandDispatcher` と、名前の集合（`HashSet<string>`、`StringComparer.Ordinal`）を持つ。
   - internal の `Register(name, handler)`: 内部の catalog に登録し、成功したら名前の集合に加える。登録は初期化の中だけ。`DebugCommandCatalog` は変えない（catalog に `Contains` を足す案は、状態の二重化は消えるが catalog を変えるので採らない。A2 の F3）。
   - `DispatchAsync`:
     - 振り分けは登録した名前の Ordinal の完全一致だけで行う（接頭辞では振り分けない）。未登録の `osm.foo` や大小文字の違う `OSM.SCENES` は Game へ渡る。
     - command が null、または `command.CommandType` が名前の集合に無い: Game の dispatcher へ同じ command と token をそのまま渡し、その結果・例外をそのまま返す。cancel 済みの token もそのまま渡す（Game のコマンドの今の挙動を変えない）。
     - 名前の集合にある: 内部の `CatalogDebugCommandDispatcher` へ委譲する。事前に cancel 済みなら既存どおり `Debug command was canceled.` の固定の失敗になり、handler は呼ばれず、Game にも渡らない（Game へ渡すと Game が Null なら「未登録」と、Game の catalog なら別の文言が返り、Framework のコマンドの結果として誤読されるため）。それ以外は catalog の結果を既存の写し方で envelope にする。
   - handler の例外は伝播させる（router が今どおり失敗の envelope にする）。Scene の語彙を持たない。
2. **構成の関数（新規 `Runtime/Bootstrap/FrameworkDebugCommands.cs`、internal static、仮称）**
   - `Compose(bool isEditor, IDebugCommandDispatcher game, Func<SceneDirector?> getSceneDirector)`: `isEditor` が false なら `game` をそのまま返す（同じ参照）。true なら振り分けの dispatcher を作り、`osm.scenes` を `ScenesDebugCommand.CreateHandler(getSceneDirector)` で登録して返す。新しい Framework のコマンドはここに足す（initializer は触らない。A2 の F1）。
   - 失敗しない設計にする: 登録は固定の名前の 1 回だけで、引数の null は呼出し側（initializer）が渡さない。これで「Framework の登録が Editor でだけ例外を投げて BeforeSceneLoad を止める」経路を作らない（A2 の I4 の 2）。
   - 「Editor でだけ Framework のコマンドを持つ」は composition root の公開の方針であり、catalog の意味ではない。
3. **配線（`AbstractApplicationInitializer.CreateDebugSocketService`）**
   - enabled が false なら今どおり null。true なら `new DebugSocketService(options, FrameworkDebugCommands.Compose(Application.isEditor, CreateDebugCommandDispatcher(), () => SceneDirector))`。変更はこの 1 行の置き換えだけ。`CreateDebugCommandDispatcher` の signature・既定値・呼ばれる回数（1 回）と時点は変えない。
   - `CreateDebugSocketService` を override した Game では Framework のコマンドは無い（§10.5 と README に書く）。
4. **写し（`Runtime/SceneSystem/`）**
   - 新規 partial `SceneDirector.DebugSnapshot.cs` に `internal SceneDirectorDebugSnapshot CaptureDebugSnapshot()`。main thread で、await を挟まず一度に `_currentScenes` の key と各 `ScenePair.SceneBase.Lifecycle.State`、`_pendingUnloads`、`_sceneHistory`（列挙だけ）、`CanGoBack` を読んで不変の data に写す。field に書かない、event を出さない、`SceneBase` の method と `GetLoadedScene` を呼ばない、`SceneResource` を読まない。`SceneDirector.cs` の冒頭の partial の一覧のコメントに足す（A2 の F5）。
   - 写しのモデル `SceneDirectorDebugSnapshot`（新規 `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs`）: Scene ごとの id と `SceneState`（enum のまま）、unload の待ちの id の全集合（管理集合に無い id も含めてそのまま）、履歴の `{from, to}` の列（新しい順）、`CanGoBack`。JSON とコマンドを知らない（§1.6 の C1）。
   - Dispose 済みの SceneDirector の読み取りは扱わない。`ReleaseAll` が `_sceneDirector` を null にすることに依存し、それを AC9 で固定する（A2 の I2 の提案）。SceneDirector に disposed の flag は足さない（挙動の変更になる）。
5. **`osm.scenes`（新規 `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`、internal static）**
   - `CreateHandler(Func<SceneDirector?> getSceneDirector)`: handler は実行の時に `getSceneDirector()?.CaptureDebugSnapshot()` と `Time.frameCount` を取り、`Format` に渡すだけ。payload は読まない（README では `{}` を示す）。
   - `Format(SceneDirectorDebugSnapshot? snapshot, int capturedFrame)`（internal static の純関数。A2 の F4）: snapshot が null なら `DebugCommandResult.Fail("unavailable: scene-director-not-created")`。それ以外は `DebugCommandResult.Ok("captured", <JSON>)`。
   - 出力の JSON（`JsonUtility.ToJson` の DTO。DTO は `[Serializable]` の class で、field は public かつ readonly にしない。property を使わない。`SceneState` は enum のままだと整数になるので DTO の field は `string state` にして名前を入れる。field の順は宣言の順で、下の順に宣言する。A2 の F9）:
     - `capturedFrame`（int）。
     - `scenes`: `{id, state, pendingUnload}` の列。SceneDirector の管理集合（Unity のロード済みの全 Scene ではなく、`SceneState` は Unity のロードの状態とは別の物）。PreLoad の前に管理集合へ入った時から unload の完了で外れるまでの Scene が載り、`Added` の event（`Stable` に達した時）より前の Scene も含む。破棄済みの Scene は載らない。順は id の Ordinal 順。空なら `[]`。`pendingUnload` は、その行の Scene が unload の待ちの集合に入っているか（管理集合と交差した射影）。unload の待ちの集合は常に管理集合の部分集合とは限らない（`Loading.cs:278〜293` の取消の後始末は、先に `CleanupCanceledScene` で管理集合から外し、後でまとめて待ちを外す）ので、管理集合に無い待ちの id は出力に出ない。写しのモデルは全集合を持つ。
     - `history`: 新しい順の `{from, to}` の列。SceneDirector の戻り先の管理用の stack で、遷移の途中や遷移の失敗の後にも entry がありうる。`SwitchScene` だけが積み（`from` が null の遷移は積まない）、`GoBack` の成功で減り、`ClearHistory` で空になる。最大 8 件で、超えると最古の entry が黙って捨てられる。空なら `[]`。
     - `canGoBack`: SceneDirector の既存の property の値（履歴があるか）。`GoBack` が成功することの保証ではない。
   - `unavailable:` の接頭辞は本コマンドの暫定の出力で、デバッグコマンド全体の契約ではない。
   - 出力の規則（このコマンドの物）: 項目は足すだけで、名前を変えない・消さない・意味を変えない。列挙値は名前で出す。将来の項目（静的な木、進行中の操作、取消の窓、event との通し番号、履歴の切り捨ての有無）はこの規則で足す。
   - README に書く読み方: 上の各項目の意味。`capturedFrame` の時点の値で、続けて読んだ時の値と同じとは限らない。操作の途中の状態（`LoadCanceled` など）がそのまま出ることがある。「読めない」（`unavailable:`、exit 1）と「空」（`scenes: []`、exit 0）と「届かない」（Play していない・WPF が無い。`CommandResult` の行が無い終わり方）の見分け方。組込みの名前は router が大小文字を区別せずに先に処理する。Editor では Framework が登録した名前（Ordinal の完全一致。今は `osm.scenes`）を Framework が先に引き、それ以外（未登録の `osm.*`、大小文字の違う名前を含む）は Game へ渡る。`osm.` は Framework の名前の規約で、強制はしない。結果不明の読み方は S3 と同じで、読み取りなので送り直してよい。

### 2.2 寿命の整理（意見 Y の P1-4、意見 Z の (3) への答え）

| 寿命 | 本スライスでの扱い |
|---|---|
| Framework のコマンドの登録 | `Compose` が振り分けの dispatcher を作って登録し、DebugSocketService を通してだけ参照される。initializer の field に持たない |
| 接続の session | 既存のまま。CLI の要求は今の service にだけ届く |
| App・Play | Play ごとに service と SceneDirector を作り直す（Domain Reload は無効、initializer は static の 1 つ） |
| handler が対象を解決する時点 | 実行の時に `() => SceneDirector` で今の initializer の SceneDirector を読む。登録の時には読まない |
| Dispose の後 | 旧 service の dispatcher への参照は残りうる（`Dispose` が手放さない）。旧 service で handler が動いた場合、handler は initializer を通して今の Play の SceneDirector（または null）を読む |

**これは寿命の問題を解決したのではなく、読み取り専用のコマンドなので本スライスでは許容した制約である。** 旧 service の handler が新しい Play の SceneDirector を参照しうるのは、handler が Play ではなく initializer（Play をまたいで同じ static の 1 つ）を通して対象を解決するからで、Play の世代を区別する仕組みは無い。読み取りでは出力に旧 Play の Scene が出ない（読むのは常に今の SceneDirector か null。`ReleaseAll` が null にする）ので、本スライスの契約は I2「出力に旧 Play の Scene が出ない」に留める。GC の到達不能性、旧 service の handler が一切動かないこと、旧 Play の要求が新しい Play で実行されないことは保証しない。Play の世代の管理は本スライスで作らず、書き込みのコマンドを増やす前の課題として構想（原則 4、段 F）へ送る。

### 2.3 不変条件と成立条件

**不変条件（起きてはいけないこと）**

- **I1 読み取りは状態を変えない。** `osm.scenes` を何回送っても、SceneDirector の管理集合と各 `SceneState`・unload の待ち・進行中の操作・履歴、Unity の読み込み済みの Scene の集合、hierarchy、AssetManagement の保持数が、送らなかった場合と同じ。`OnSceneEvent` に event が出ない。
  - 主張を崩しうる既存の経路: (a) 読む getter が副作用を持つ（`SceneBase.Lifecycle` と `SceneLifecycleManager.State` は field を返すだけと確かめた。他の getter と `GetLoadedScene` は読まない）。(b) `_sceneHistory` を変える method を呼ぶ（列挙だけにする）。(c) `JsonUtility` が DTO の getter を呼ぶ（DTO は field だけにする）。
  - 探索した境界: `SceneDirector.cs` の field の宣言と `_currentScenes` を書き換える全箇所（`Loading.cs:430`、`Unloading.cs:293`・`:400`、`Release` の `Clear`）、`SceneBase.cs:221`、`SceneLifecycleManager.cs:20`、`SceneDirector.Transitions.cs` の履歴の操作、`MainThreadDebugCommandDispatcher` の同期の呼び出し。A2 は handler の再入と await の混入を含めて破る順序を見つけなかった。
  - 主張と証拠の分担（外部計画レビュー 1 巡目の P1-3）: 単体（AC3）は管理集合・各 `SceneState`・unload の待ちの集合・履歴の内容と順・`OnSceneEvent` の受信数を見る。実機（V2）は管理集合・各状態・unload の待ち・進行中の操作の key・Unity の読み込み済みの Scene の集合・AssetManagement の台帳の件数を、oracle で前後に読んで比べる。hierarchy の不変と event が出ないことは実機では判定しない: 写しが `NotifyHierarchyChanged` を呼ばず GameObject を作らず event を出さないことを構造（発見 C の code の確認）で示し、event は AC3 で見る。hierarchy は retained snapshot で、関係の無い変化や遅れを含みうるので、V2 では revision を参考として記録するだけにする。
- **I2 出力に旧 Play の Scene が出ない。** Play を止めて次の Play を始めた後の読み取りの出力は、次の Play の SceneDirector の管理集合だけを含む。
  - 崩しうる経路: (a) 登録の時に SceneDirector を closure に捕まえる（禁止）。(b) 写しを cache する（持たない）。(c) static の状態を持つ（持たない）。(d) 旧 service の dispatcher が残る（§2.2。許容した制約であり解決ではない）。(e) `ReleaseAll` が `_sceneDirector` を null にしない経路ができ、Dispose 済みの SceneDirector が空の成功を返す（AC9 で固定）。
- **I3 一度の読み取りの値は一つの時点の値である。** 1 回の応答の値は main thread の同じ時点（await を挟まない一続きの読み取り）から取る。
  - 崩しうる経路: 写しの途中で await する。catalog は handler を同期で呼ぶので、handler の中に await が無ければ保たれる。
- **I4 `osm.scenes` 以外の挙動を変えない。** (a) Player: Game の dispatcher をそのまま（同じ参照で）渡し、全てのコマンドの結果と文言が今と同じ。(b) Editor: `osm.scenes` 以外の名前（null の command と cancel 済みを含む）は Game の dispatcher に今と同じ command と token で 1 回だけ渡り、結果・文言・例外が今と同じ（Game が override していない Editor の未登録の文言も Null の名前入りのまま）。(c) `debug.set-active`、組込みの `ping` / `runtime-diagnostics`（router が dispatcher の前に処理し、router は変えない。判定必須の `DebugSocketBuiltInCommandHandlerTests` と V4 の疎通で見る）、enabled=false の扱い。(d) `CreateDebugCommandDispatcher` が呼ばれる回数（1 回）と時点。
  - 崩しうる経路: 振り分けが Game の名前の null の command や cancel 済みの command を自分で処理して文言が変わる、Framework の名前でない command に内部の catalog の「未登録」を返してしまう、Player の判定を誤る、`Compose` が Editor でだけ例外を投げる（固定の名前の 1 回の登録にして作らない）。
  - 受容した差: Editor で Game が `osm.scenes` を自分で登録していると、Editor では Framework が先に引くので届かない（U2。今そういう Game のコマンドは無い）。

**成立条件（起きなければならないこと）**

- **E1 安定した状態での完全一致。** 通常の SampleGame の Editor の Play（Title から起動）で、`hasSnapshot: true` になり、oracle が SceneDirector を返し、管理集合に `Title` があり、全ての管理下の Scene の `Stable` を返した後、Scene の操作をしていない間に、`send --command osm.scenes --payload '{}'` が exit 0 で、出力が §5 の比較の規則で oracle と完全に一致する（余計な Scene も欠けた Scene も無い）。
- **E2 遷移の後の履歴。** §5 V3 の操作（Title → OutGameSession の `SwitchScene`）が例外なく完了したことを §5 の観測で確かめ、oracle が `OutGameSession` の `Stable`、`Title` の不在、全ての管理下の Scene の `Stable` を返してから読むと、出力が oracle と完全に一致し、`history` の先頭が `{from: "Title", to: "OutGameSession"}`、`canGoBack` が true。
- **E3 再 Play。** E2 を済ませた Play を止め、同じ Editor で再び Play し、何も操作せずに E1 と同じ条件（SceneDirector があり、`Title` があり、全て `Stable`）で読むと、出力が 2 回目の Play の oracle と完全に一致し、`history` が `[]`、`canGoBack` が false（1 回目の Play の履歴が出ない）。`[]` の根拠: 起動は全ての経路で `AddScene` を使い、`from` の無い遷移は履歴を積まない（`AbstractApplicationInitializer.cs:547`・`:562`・`:640`・`:697`・`:1020`）。V1 でも 1 回目の Play の初期の `history` が `[]` であることを記録し、`[]` でなければ想定外の事実として未判定で止める（期待を観測に合わせて変えない）。`capturedFrame` は識別に使わない（新しい Play で小さくなる保証が無い）。
- **E4 既存のコマンド。** 同じ Play で S3 の手順（`hierarchy` → `debug.set-active` false → `inspect` → true → `inspect`）が S3 と同じ出力・exit で動く。

### 2.4 受け入れ条件（A3 統合案）

| AC | 検証する条件 | 内容 | 方法 |
|---|---|---|---|
| AC1 | E1・I3 | fake の factory で親子の Scene を AddScene した SceneDirector の `CaptureDebugSnapshot` が、管理集合と各 `SceneState`・unload の待ちの集合を完全に写す。管理集合が空の SceneDirector では空 | EditMode 単体（既存 `SceneDirectorTestBase` の上） |
| AC2 | E2 | `SwitchScene` を 2 回した後の写しの履歴の順と内容、`CanGoBack`。PoNR の後で遷移が失敗した場合も entry が残ること（今の挙動の記録） | EditMode 単体 |
| AC3 | I1 | 写しの前後で管理集合・各 `SceneState`・unload の待ちの集合・履歴の内容と順（件数だけでなく各 `{from, to}`）・`OnSceneEvent` の受信数が同じ。写しを 100 回続けても同じ | EditMode 単体 |
| AC4 | I3・I1（失敗経路） | AddScene の await の途中（`TestableSceneDirector.UnitySceneLoadGate` で止めた途中の状態と、既存の `SceneDirectorCancellationTests` の手順で作る `LoadCanceled`）で写しても例外にならず、途中の状態がそのまま写り、写しの後も操作が完了する | EditMode 単体 |
| AC4b | E1（unload の待ち） | 既存の `SceneDirectorCancellationTests.UnloadScene_AfterPNR_RegistersPendingUnload` と同じ手順（`UnitySceneLoadGate` で止める → `AddScene` → `UnloadScene` → `HasPendingUnload` が true）の時点で写しを取り、`CreateHandler` の handler を通した出力で、対象の id の行だけが `pendingUnload: true`、`state` がその時点の `GetSceneState` と同じ。gate を解放して完了した後の出力では、対象の行が無く、写しの unload の待ちの集合も空 | EditMode 単体 |
| AC5 | E1（出力の形）・I2（失敗経路） | `Format` の純関数: 直接組み立てた写しから、`state` が名前の文字列、`scenes` が id の Ordinal 順、空の列が `[]`、field の順が `capturedFrame`・`scenes`・`history`・`canGoBack`、id の中の `"` と `\` が escape される。`pendingUnload` は待ちの集合にある行だけ `true`、他は `false` で、待ちの集合にあって管理集合に無い id は行として出ない。null を渡すと `unavailable: scene-director-not-created` の失敗 | EditMode 単体（SceneDirector なし） |
| AC6 | I2 | `CreateHandler` の delegate が null を返すと unavailable、別の SceneDirector を返すとその SceneDirector の値だけが出る | EditMode 単体 |
| AC7 | I4 | 振り分けの dispatcher: (1) Framework の名前は Game を呼ばない。(2) Framework の名前で事前に cancel 済みなら `Debug command was canceled.` で、handler も Game も呼ばない。(3) Framework の名前でない command（cancel 済みを含む）と null の command は Game を同じ command と token で 1 回だけ呼び、結果と例外をそのまま返す。(4) Game 側に実の `CatalogDebugCommandDispatcher`（`debug.set-active` 相当を登録した catalog）を置き、null の command・未登録の名前・事前に cancel 済み・登録済みの名前の 4 つで、振り分けを通した envelope と Game を直接呼んだ envelope の RequestId・Success・Message・PayloadJson が同一（A2 の H1）。(5) Game が `osm.scenes` と同じ名前を登録していても Framework の結果が返る（同名の衝突）。未登録の `osm.foo` と大小文字の違う `OSM.SCENES` は Game へ渡る | EditMode 単体（fake と実の Game の dispatcher） |
| AC8 | I4（失敗経路） | `Compose(false, game, …)` が `game` と同じ参照を返す。`Compose(true, game, …)` が `osm.scenes` を解決し、他の名前を `game` へ渡す。initializer: enabled=true で `CreateDebugCommandDispatcher` が 1 回だけ呼ばれ、enabled=false で null を返して呼ばれない（enabled=true のテストは実の `DebugSocketService` を作るので、テストの中で Dispose を明示する。`Dispose` は `StopAsync().Forget()` を呼ぶ） | EditMode |
| AC9 | I2（失敗経路） | `ReleaseAll` の後に initializer の `SceneDirector` が null（既存の Bootstrap の test にあればそれを参照し、無ければ足す。`ReleaseAll` が test から呼べなければ既存の `InternalsVisibleTo` の範囲で呼び、公開 API は足さない。足さないと書けなければ Phase A へ戻す） | EditMode |
| AC10 | I4 | 既存の `SetActiveDebugCommandTests` を変更なしで通す（回帰の確認。dispatcher の鎖は AC7 の (4) が見る） | EditMode |
| AC11 | E1〜E4・I1・I2・I4 | 実 Editor の Play と Release の WPF・CLI で §5 の V1〜V6 | 実機（Phase C） |

### 2.5 判定定義

- GO: I1〜I4 と E1〜E4 を同じ GO 候補 head で満たし、判定必須テスト（§5）が全て通る。
- NO-GO: 上のどれかが満たせない。
- 未判定: §5 の「保留」が 3 回続いた段、oracle が確立できない段、疎通 (a)・(b) の不成立、V3 の完了の印が 60 秒で決まらない、V2 で自然な進行と切り分けられない変化、のどれかがある。GO の証拠にも NO-GO にもせず、原因を整理して再実行する（判定の条件を弱めない）。

### 2.6 risk の理由

`normal`（owner が 2026-11-25 に確定。§1.5 の意見 Z）。公開 API・asmdef・`SceneState`・Addressables・寿命の契約を変えない。SampleGame の code を変えない。SceneDirector には internal の読み取り method を 1 つ足すだけで、挙動は変えない。新しい部品（振り分けの dispatcher と構成の関数）は internal で、Editor の配線にだけ入る。`AbstractApplicationInitializer.cs` を 1 行触るので、program の停止規則（§1.4）により `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に B を走らせない。S4-V（`tools/` だけ）・S4-3a（主に `tools/` と文書）とは触る file が重ならない見込みで、Unity の実行の時間だけ分ける。

- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 ここでは答えない問いと所有する後続

- ライフサイクルの変化の観測（event の配信、通し番号、Hierarchy と同じ push と WPF の保持、時系列の表示）、`OnSceneEvent` の通知と実際の遷移のずれの確認: [DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md) の段 A。所有するスライスは未起票。
- Unity の Hierarchy・Inspector からの観測: 同じ構想の段 G。未起票。
- デバッグコマンドの共通の口、宣言・一覧、名前空間、Player への公開、Scene 寿命のコマンド、書き込みのコマンドの Play の世代、read の出力の統一、失敗の方針、`Compose` に Framework のコマンドが増えた時の登録の形: 同じ構想。未起票。
- `osm.scenes` の追加の項目（履歴の切り捨ての有無、履歴の通し番号を含む）と、Scene 以外の状態のコマンド: 同じ構想。未起票。要求時解決の記録（`ContentRecord`）は program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の S5。
- S3 から移送された項目（DontDestroyOnLoad の表示、Play をまたぐ NodeId の識別、inspect の自己応答保証）: そのまま未起票。

## 3. 責務マップ（A3 統合案）

| file | namespace | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs`（新規） | `…DebugSocketServices` | Framework のコマンドの登録を受け、名前で Framework と Game に振り分ける | 振り分けの規則が変わった時 | DebugSocketService（Play 1 回） | DebugCommands、`CatalogDebugCommandDispatcher`、wire DTO、UniTask | internal | EditMode 単体（fake と実の dispatcher） | 0 → 約 60 |
| `Runtime/Bootstrap/FrameworkDebugCommands.cs`（新規） | `…Bootstrap` | Editor / Player の判定で、Game の dispatcher をそのまま返すか、Framework のコマンドを登録した振り分けを返すかを決める | Framework のコマンドを足す時、公開の方針が変わった時 | 値を返すだけ（状態を持たない） | 振り分けの dispatcher、`ScenesDebugCommand`、SceneSystem | internal | EditMode 単体（`isEditor` を引数で渡す） | 0 → 約 40 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 既存 | `Compose` を呼ぶ 1 行 | 配線だけ | 既存 | 既存 | 変えない | EditMode | 1486 → 約 1487 |
| `Runtime/SceneSystem/SceneDirector.DebugSnapshot.cs`（新規 partial） | `…SceneSystem` | SceneDirector の private な状態を一つの時点の不変の data に写す | Scene の状態の持ち方が変わった時 | SceneDirector（Play 1 回） | SceneSystem の内部だけ | internal | EditMode 単体 | 0 → 約 60 |
| `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs`（新規） | `…SceneSystem` | 写しのモデル（将来の push・Inspector と共有する語彙。§1.6 C1・C2） | 観測する項目が変わった時 | 値 | なし | internal | 上と同じ | 0 → 約 50 |
| `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`（新規） | `…SceneSystem` | `osm.scenes` の handler と、写しを出力にする純関数（DTO と JSON、読めない時の文言） | コマンドの出力が変わった時 | Framework の catalog（Play 1 回） | SceneSystem、DebugCommands、`JsonUtility` | internal | EditMode 単体（`Format` は SceneDirector なし） | 0 → 約 100 |
| `Runtime/SceneSystem/SceneDirector.cs` | 既存 | 冒頭の partial の一覧のコメントに 1 行足す | — | 既存 | 既存 | 変えない | — | 336 → 337 |
| test（新規の Scene・dispatcher・Bootstrap の test） | — | AC1〜AC9 | — | — | — | — | EditMode | 新規 |
| 文書（§5.12、§10.5、CLI README） | — | harvest（Phase B が書く） | — | — | — | — | — | 追記 |

- 行数警報: `AbstractApplicationInitializer.cs` は既に 500 行を超えるが、変更は 1 行の置き換えだけで、判断は新規 file に置く。initializer の分割は program の `RUNTIME_REFACTOR_*` 系の所有。
- 配置の理由: 写しは SceneDirector の private field を読むので partial に置く。写しのモデルとコマンドは「Scene の診断」という別の変更理由を持つので `SceneSystem/Diagnostics/` の folder に分ける（namespace は分けない。§2.1 の冒頭）。構成の関数は composition root の一部なので `Bootstrap/` に置き、Scene の語彙はここで初めて振り分けとつながる（振り分けの dispatcher は Scene を知らない）。機能が自分の診断を持ち、コマンドは薄い adapter、登録は composition root、transport は機能を知らない、という置き方の最初の例（構想の原則 2）。`Runtime/DebugCommands/` は System だけの決まりがあり、`JsonUtility` と SceneSystem を使うこのコマンドは置けない。
- 依存の向き: `SceneSystem` → `DebugCommands`（`DebugCommandResult` / `DebugCommandHandler`）は、機能がデバッグの語彙に依存する初めての edge。`DebugCommands` は System だけの末端で同じ asmdef なので許容する（逆向きの `DebugSocketServices` → `SceneSystem` は作らない）。
- 新規 file の path は予算（repo 相対 150 文字）の範囲。最長は `Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs.meta`（repo 相対で 106 文字）。

## 4. 実装計画（A3 統合案）

- 順序: (1) 写しのモデルと `CaptureDebugSnapshot` と AC1〜AC4。(2) `ScenesDebugCommand` と AC5・AC6。(3) 振り分けの dispatcher と AC7。(4) 構成の関数と initializer の 1 行と AC8・AC9・AC10。(5) §5.12、§10.5、CLI README。
- Phase B から Phase A へ差し戻す条件: I1 を守るには SceneDirector の挙動を変える必要がある。`DebugCommandCatalog`・router・SampleGame を変える必要がある。公開 API が要る。asmdef の参照が要る。`Compose` が失敗しうる形でないと書けない。
- 対象外を維持する方法: `DebugCommandCatalog`・router・protocol の YAML・`tools/DebugStudio/src` の code・SampleGame を変えない（README だけ）。`osm.scenes` の出力を §2.1 の 5 の項目に留める。`OnSceneEvent` を購読しない。

## 5. テストとレビュー計画（A3 統合案）

- 単体テスト: AC1〜AC10（EditMode）。新しい test の class は `unity/Assets/OneStarMaker/Tests/Scene/`（写しとコマンド）、DebugSocket の test の置き場（振り分け）、Bootstrap の test の folder（構成の関数と配線）。
- 差し戻し中の起点 `-Filter`: `SceneDirector|ScenesDebugCommand|FrameworkFirstDebugCommandDispatcher|FrameworkDebugCommands|SetActiveDebugCommand|BeforeSceneLoadServiceStaging|FrameworkServiceSet|DebugCommand`（C が根拠付きで変えてよい）。
- 判定必須テスト: 上の新規 test、既存の `SceneDirector*Tests`（`SceneDirectorCancellationTests` を含む）、`DebugCommandCatalogTests`、`SetActiveDebugCommandTests`、`DebugSocket*Tests`（組込みのコマンドの `DebugSocketBuiltInCommandHandlerTests` を含む）、最終の全 EditMode 回帰。
- 全 EditMode 回帰の適用除外: なし。
- `tools/DebugStudio` と `tools/DebugCommandOfflineTests`: code を変えないので判定必須にしない。
- **oracle の導出の規則（A3 で固定。eval の C# の本文は判定 C の担当がこの規則どおりに書き、git に入れない）:**
  - 入口: `SampleGame.DependOnAll.AppInitializer` の static の private field `s_instance`（`BindingFlags.Static | BindingFlags.NonPublic`。前例 `SampleGame/Tests/Rendering/Vfx/VfxScenePlayTests.cs`）。
  - SceneDirector: 基底型 `OneStarMaker.Runtime.Bootstrap.AbstractApplicationInitializer` に対して `GetField("_sceneDirector", BindingFlags.Instance | BindingFlags.NonPublic)`（派生型の `GetType().GetField` では見つからない）。null なら `{"sceneDirector": null}` を返す。
  - 管理集合: `SceneDirector` の `_currentScenes` を reflection で読み、各 value（private の入れ子型 `ScenePair`）の `SceneBase`（get だけの自動 property。getter に副作用は無いので、property の getter か backing field のどちらで読んでもよい）を reflection で読み、`SceneBase` の `_lifecycle` field、その `_state` field を直接読む（`CaptureDebugSnapshot` と `State` の getter を通らない）。状態は enum の名前の文字列にする。
  - unload の待ち: `_pendingUnloads` の集合をそのまま返す（Scene ごとの flag にしない。A2 の G1）。
  - 履歴: `_sceneHistory` を列挙し（stack の列挙は新しい順）、各 entry（private の struct `SceneHistoryEntry`）の `FromSceneId` / `ToSceneId` field を読む。`CanGoBack` は `_sceneHistory.Count > 0` を oracle 側でも計算する。
  - V2 の不変の観測用に足す物（外部計画レビュー 1 巡目の P1-3）: 進行中の操作の 4 つの辞書（`_inFlightAddScenes`・`_inFlightSceneBaseLoads`・`_inFlightUnitySceneLoads`・`_inFlightUnloads`）の key の集合。Unity の読み込み済みの Scene の名前と `isLoaded` の列（`SceneManager.sceneCount` と `GetSceneAt`）。AssetManagement の台帳の件数（基底型の `_assetManagement` field → `AssetManagement` の `_registry` field → `AssetRegistry` の `_assets` と `_scenes` の `Count`。method を呼ばない）。
  - 出力: `{"sceneDirector":true,"scenes":[{"id","state"}...],"pendingUnloads":[...],"history":[{"from","to"}...],"canGoBack":bool,"inFlight":{"add":[...],"sceneBase":[...],"unityScene":[...],"unload":[...]},"unityLoadedScenes":[{"name","isLoaded"}...],"assetCount":int,"assetSceneCount":int}`。何も書かない、`Count` と列挙と field の読み取り以外の method を呼ばない（`SceneManager.GetSceneAt` は読み取りの API として例外）。
- **比較の規則（判定 C の offline 実行。A3 で固定）:**
  - 有効な比較の条件: 同じ Play の中で oracle → `osm.scenes` → oracle の順に読み、(1) 前後の oracle が `sceneDirector: true` を返し、(2) 前後の oracle が完全に一致し、(3) oracle の全ての `state` が `Stable` で、(4) 段ごとの存在の条件（V1・V5 は `Title` があること、V3 は `OutGameSession` があり `Title` が無いこと）を満たすこと。空の管理集合は (4) を満たさないので、空の oracle と空の出力の一致を採らない。
  - 保留: 有効な比較の条件を満たさない回は「保留」で、GO の証拠ではない。読み直しは 1 段につき 3 回まで。3 回とも保留なら、その段を未判定のまま止め、原因（自然な進行、oracle の不備、実装）を整理して記録し、判定の担当が再実行を判断する。結果を見て判定の条件を弱めない。
  - oracle が確立できない（reflection の経路が無い、eval が失敗する、`sceneDirector` が取れない）: 未判定とし、検証の基盤の不備として記録する。`osm.scenes` の出力だけで合否を決めない。
  - 一致の定義（全て満たす）:
    - 出力の形: payload が JSON の object で、`capturedFrame`（整数）・`scenes`（列）・`history`（列）・`canGoBack`（真偽）が全てある。`scenes` の各行に `id`（文字列）・`state`（文字列）・`pendingUnload`（真偽）、`history` の各要素に `from`・`to`（文字列）がある。
    - `scenes`: 行の数が oracle の `scenes` の数と同じ。`id` が一意で、Ordinal 順に並ぶ。`id` の集合が oracle と同じ。各行の `state` が oracle の同じ id の `state` と同じ。`{id | pendingUnload = true}` が oracle の `pendingUnloads` と oracle の `scenes` の id の集合の交差と同じ。
    - `history`: 要素の数と各要素の `from`・`to` が、順まで oracle と同じ。
    - `canGoBack`: oracle と同じ。
    - `capturedFrame`: 整数として存在することだけを見て、値は比べない。
    - 終わり方: exit 0、stdout に `success=true message=captured`。
  - 実行担当は結果を見て期待値を決めない。期待値は毎回 oracle から得るので、機械向けの期待値 file は作らない。
  - 新しい経路の疎通（判定とは分けて記録する。外部計画レビュー 1 巡目の P1-2、2 巡目の P1）: (a) oracle の eval が規則どおりの JSON を返すこと。Phase C の冒頭、V1 の前に確かめる。(b) eval で `SwitchScene` を駆動し、その完了と例外を検証用の印で観測できること。(b) は Title → OutGameSession の遷移を伴うので冒頭では行わず、V3 の開始時に兼ねる（V1 の初期状態を崩さないため）。どちらも不成立なら、その段から先を未判定で止め、検証の基盤の不備として記録する。E2 を狭めて進めない。
- 操作・実行時の条件の検証経路（AC11。実行担当は判定 C 用に開いた session。S3 の §5.4 と同じ環境の作り方）:
  - V0: clean な検証用 checkout を GO 候補 head に detach し、その `unity/` を自分で開いた Editor 6000.6.9f1。DebugStudio の App・CLI を Release で build し、App を `--listen` で起動して PID を記録する。
  - V1（E1）: V0 の Editor で `Assets/SampleGame/OutGame/Title/Title.unity` を開き（開き替えた後に dirty でないこと、保存確認の dialog が出ないことを記録）、Editor の Play を始める（公開 command の名前を `tools/unity-editor.cmd command` で確かめ、無ければ eval で `UnityEditor.EditorApplication.isPlaying = true;`）。Play の入口の dialog、空の Scene への差し替え、起動の失敗が出たら、そのまま記録して未判定とし、場面の不備として判定の担当へ返す（判定の担当が場面を決め直す場合は A3 の新しい revision）。疎通 (a) の後、`hierarchy` が `hasSnapshot: true` になるまで読み直す（上限 60 秒）。oracle が `sceneDirector: true`、`Title` あり、全ての `state` が `Stable` になるまで oracle を読み直し（上限 60 秒）、比較の規則で照合する。
  - V2（I1）: oracle → `osm.scenes` → oracle を 5 回続け、各回の前後の oracle の全ての項目（管理集合・状態・unload の待ち・履歴・進行中の操作の key・Unity の読み込み済みの Scene・AssetManagement の台帳の件数）が変わらないことを確かめる。V2 では前後の変化は保留ではなく副作用の候補として扱う（外部計画レビュー 2 巡目の P2）: 全ての試行の oracle と出力を保存し、変化のあった回を捨てて一致した回だけを集めない。変化があったら、同じ間隔で `osm.scenes` を送らずに oracle を 2 回読み、同じ変化が起きるか（自然な進行か）を確かめて記録する。`osm.scenes` に起因する変化（送らない間には起きない）は I1 の NO-GO、自然な進行と切り分けられない変化は未判定とする。`hierarchy` の revision は参考として記録するだけで判定に使わない。
  - V3（E2）: oracle と同じ入口で取った SceneDirector に対し、eval で `SwitchScene("Title", "OutGameSession", CancellationToken.None)` を呼ぶ（reflection で呼ぶと戻り値は box された `UniTask` なので、`Cysharp.Threading.Tasks.UniTaskExtensions.AsTask` で `Task` にする）。この段が疎通 (b) を兼ねる。
    - 切り替え先を `OutGameSession` にした理由（A3 の自己点検で見つけた事実）: SampleGame の Title の開始の操作は `Title → HomeScene` だが（`TitleScene.cs:80`）、`HomeScene` は `Stable` に達すると `OnStabledImpl` で `SceneFlow.EnterInGame`（`OutGameScene → InGameSession` の `SwitchScene`）を呼ぶ（`HomeScene.cs:36〜42`）。そのため `HomeScene` を待って照合する状態が来ず、履歴の先頭も `{OutGameScene, InGameSession}` に変わり、InGame の重いロードに入る。`OutGameSession` は `HomeScene` の親で、自分の遷移を持たず（`OutGameSession.cs` に override が無い）、子の `HomeScene` は `OnDemand`（`SceneMap/HomeScene.asset` の `_loadType: 0`）なので一緒に読み込まれない。Title の通常の遷移でも祖先として読み込まれる node なので、`SwitchScene` の対象として通る経路にある。`recordHistory: true` の同じ `SwitchScene` で、履歴が 1 件積まれた安定した状態を作れる。
    - 完了の観測（検証用の一時的な印。製品の code には何も足さない）: 要求ごとに固有の key（例: `osm-s4-v3-` と UTC の ticks を連ねた文字列）を決め、呼ぶ前に `AppDomain.CurrentDomain.SetData(key, "pending")` を書く。同じ eval で戻り値を `Task` にし、継続で成功なら `"success"`、例外なら `"failure:"` と例外の型と文言を同じ key に書く。後の eval でその key だけを読み、`"pending"` の間は読み直す（上限 60 秒）。読み終えたら、または打ち切ったら `SetData(key, null)` で消す。Domain Reload が無効なので、前の実行の印を拾わないよう key を使い回さない。
    - 裁定: `"success"` を読めたら完了とする。`"failure:…"` なら E2 の NO-GO。60 秒たっても `"pending"`、または印を書けない・読めない（疎通 (b) の不成立）なら未判定で止め、基盤の不備として記録する。Console に例外が無いことを完了の代わりにしない。E2 を観測の境界まで狭めて進めることはしない（狭めるなら A3 の凍結の前に E2 と規則を揃えるか、凍結の後なら owner の裁定と revision の更新を先に行う。判定 C の担当は結果を見て E2 を弱めない）。
    - 完了を確かめた後、oracle が `OutGameSession` あり・`Title` 無し・`HomeScene` 無し・全て `Stable` になるまで読み直し（上限 60 秒）、比較の規則で照合する。`history` の先頭が `{from: "Title", to: "OutGameSession"}`、`canGoBack` が true であることも記録する。60 秒以内に `HomeScene` や InGame 系の Scene が管理集合に現れた場合は、想定外の事実として未判定で止める。
  - V4（E4・I4）: V3 の後の Play で `hierarchy` を読み直し、今の hierarchy で名前が一意の root を次の順で選んで（`Directional Light` があればそれ（S3 の前例）。無ければ、名前が `[` で始まらず（`[UICommon]`・`[UpdaterHost]` などの Framework の host を避ける）、`Camera`・`EventSystem` を含まない root。選んだ物と理由を記録する。Framework や Game の機能の host を非アクティブにして後の段を壊さないため）、その現在の NodeId で S3 の固定版 §5.4 と同じ手順（`debug.set-active` を false → true、各回の `inspect` の rawValue と、独立した eval による同じ GameObject の `activeSelf` の照合）を行う。前の段で読んだ NodeId を使い回さない。あわせて `send --command debugsocket.ping` を 1 回送り、組込みのコマンドが今どおり応答することを記録する。
  - V5（E3・I2）: V3 を済ませた Play を止め、同じ Editor で再び Play し、何も操作せずに V1 と同じ条件で照合する。`history` が `[]`、`canGoBack` が false であることを記録する。
  - V6（終了処理）: Play を止め、WPF に通常の終了を要求し（`CloseMainWindow`）、実際の終了と終了コードを記録する（S3 の固定版 §5.4 と同じ）。開いている Scene が clean か、tracked の変更が無いかを記録し、自分で開いた Editor を通常終了する。V1〜V5 のどこかで未判定や失敗で止めた場合も、実行担当が起動した Play・WPF・Editor について、この終了処理を行ってから記録を返す。
  - 遷移の途中の状態と unavailable の出力は実機で見ず、AC4・AC5・AC6 で閉じる。
  - 保存: 各段のコマンド、開始・終了の UTC、stdout / stderr / exit、oracle の戻り値、比較の結果。Git に入れず、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って保存する。
- 未知の操作経路の疎通結果: `send` の経路は S3 で成立済み。oracle の reflection と、eval による `SwitchScene` の駆動と完了の観測は未確認で、初回確認は Phase C の疎通 (a)（V1 の前）と (b)（V3 の開始時）。不成立なら基盤の不備か設計の不備かを分けて記録し、判定の担当が A3 の新しい revision の要否を判断する。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: 判定 C の offline 実行（上の比較の規則）。A3 の時点に存在する機械向けの期待値・合成 fixture・script は無いので、Phase A snapshot は不要。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当: Cloud の計画 session。
- C' 用に予約した担当: Anthropic と OpenAI 以外のベンダーの系列（A0〜A3 と A2 は Anthropic、外部の意見 Y・Z は OpenAI が出した）。

### 5.1 A2 の独立レビューと A3 の採否

- A2 の担当と入力版（Cloud の計画 session の中の独立した subagent。互いの指摘を見せていない。どちらも A0/A1 の主担当とは別のモデル）:
  - 目的レビュー: Anthropic の別のモデル。入力は §1.1 の目的と利用者の原文だけ（sha256 `61ff31504ef5fa12117e03da4f799d45965a780c81e073f837b1c0f74f9f1dab`）。ファイルを読まずに回答した。
  - アーキテクチャゲート: Anthropic の別のモデル（目的レビューとも別）。base `07cc2c975b63cf5b668130a1c45490dd3e77c5f0` の clean な detached checkout と、revision 4 の HANDOFF の固定 copy（sha256 `60412cf3e8901057f3ecd5e2ba82c0aa49debd022c210291ac9aca8ec406ec3f`、commit `c79bc8f9` の版）。照合は担当が自分で行い一致した。構想の文書は base に無いので読んでいない。
- A3 の統合: A0/A1 の主担当。owner が revision 8 で凍結した（§0 の A3 凍結記録）。
- 目的レビューの採否（初稿との突き合わせ）:

| 指摘 | 採否 | 理由・反映先 |
|---|---|---|
| 一覧の過不足が無いこと | 既にある | E1、§5 の比較の規則 |
| 状態は `SceneState` の正本の値そのもの | 既にある | §2.1 の 5（名前で出す） |
| 一覧・状態・履歴が同じ時点、時点の情報を出す | 既にある | I3、`capturedFrame` |
| 履歴は CLI の前の遷移も含み、順を保つ | 既にある | `_sceneHistory` は Play の開始から SceneDirector が持つ |
| 履歴の上限で切り捨てたことを出力で示す | 一部採用 | 上限（8 件）と黙って捨てることを README と出力の説明に書く（§2.1 の 5）。切り捨ての有無の項目は、知るには SceneDirector に新しい状態が要り、出力の項目も確定済み（U1）なので足さない。後から足せる項目として §2.7 へ |
| 観測がゲームを変えない | 既にある | I1 |
| 通常の Play でそのまま使える | 既にある | §1.7（準備の操作なし） |
| 読めないと空の区別 | 採用（README の記述を足した） | §2.1 の 5 の「読めない・空・届かない」 |
| 出力が安定して一意に読める | 既にある | 出力の規則 |
| 履歴の項目に単調に増える番号 | 保留 | §1.6 の C3（後から足す）。§2.7 |
| `SceneState` と Unity のロードの状態が別物と分かる | 採用（記述を足した） | §2.1 の 5 の `scenes` |
| 作らない物（購読・待機・操作・WPF・Hierarchy の再実装・利用者の入力・新しい状態の語彙・永続化・Play 以外・所要時間・自動の再試行） | 一致 | 初稿はどれも作らない。所要時間（`ElapsedMs`）も出さない |
| 原文で決まらない点（履歴の範囲、破棄済みの Scene、「矛盾しない」の判定） | 採用 | 履歴の意味と破棄済みの Scene の扱いを §2.1 の 5 に書いた。「矛盾しない」は §1.6 の C1〜C4 を設計の条件とし、受け入れ条件にはしない（将来の手段は本スライスで観測できない） |

- アーキテクチャゲートの採否:

| 指摘 | 重さ | 採否 | 反映先 |
|---|---|---|---|
| F1 構成を独立した関数にし、Editor / Player の判定を引数で差し替える | A | 採用 | §2.1 の 2・3、§3、AC8 |
| F2 `Diagnostics/` の namespace と循環 | A | 採用（namespace は分けない） | §2.1 の冒頭、§3 |
| F3 名前の集合と catalog の二重管理、写し方の重複 | B | 採用（内部の `CatalogDebugCommandDispatcher` へ委譲。`Contains` は足さない） | §2.1 の 1 |
| F4 整形を純関数にする | B | 採用 | §2.1 の 5、AC5 |
| F5 partial の一覧のコメント | C | 採用 | §2.1 の 4、§3 |
| G1 oracle は unload の待ちを集合で返す | B | 採用 | §5 の oracle と比較の規則 |
| G2 履歴は `SwitchScene` だけが積む。V3 の経路を根拠で選ぶ | C | 採用（`TitleScene.cs:80` と同じ `SwitchScene`） | §2.1 の 5、§5 V3 |
| I2 Dispose 済みの SceneDirector の方針と、`ReleaseAll` の null 化の固定 | B | 採用 | §2.1 の 4、I2 (e)、AC9 |
| I4 の 2 `Compose` が例外を投げない設計 | C | 採用 | §2.1 の 2 |
| H1 dispatcher の鎖を通した同一性のテスト | A | 採用 | AC7 (4)、AC10 |
| H2 「安定」の定義と 3 回の不一致の扱い | A | 採用（全 `Stable` を前提、3 回で保留） | E1、§5 の比較の規則 |
| H3 再 Play の識別の前提 | B | 採用（V3 の後、`capturedFrame` を使わない） | E3、§5 V5 |
| H4 `ReleaseAll` の後の null の AC | B | 採用 | AC9 |
| H5 I1 の hierarchy・保持数を単体で見ない理由 | B | 採用（記述を足した） | I1 |
| H6 AC2 の失敗の後の entry の記録 | C | 確認のみ | AC2 |
| F6〜F8・F10 実現可能性 | — | 採用（oracle の reflection の経路を規則に入れた） | §5 |
| F9 `JsonUtility` の DTO の制約 | B | 採用 | §2.1 の 5 |
| D1〜D3 後続の入力 | D | 構想にある | §2.7、構想 |

- A2 の観点の再レビューの要否: 目的・利用者の文言は A2 の後に変えていないので、目的レビューはやり直さない。設計の変更（構成の関数、namespace、委譲）はアーキテクチャゲートの指摘そのものの反映で、新しい依存の edge は `Bootstrap` → `SceneSystem`（composition root、既存の initializer と同じ）だけなので、再レビューは外部計画レビューに含めて受ける。

- 外部計画レビュー 1 巡目（PR #216 のコメント、2026-11-25、owner のアカウント。対象 revision 5 `748d4c8e`。結論は「方向性は賛成。凍結の前に検証計画の穴を閉じる。実装の範囲は増やさない」）の採否:

| 指摘 | 採否 | 反映先 |
|---|---|---|
| P1-1 `pendingUnload: true` が出力まで届くことを必須の単体テストにする（既存の `UnloadScene_AfterPNR_RegistersPendingUnload` の手順） | 採用 | AC4b、AC5 |
| P1-1 unload の待ちを常に管理集合の部分集合と仮定しない。出力は管理集合と交差した射影とし、契約と比較を揃える | 採用（U1 の範囲。全集合の外部公開は求めない） | §2.1 の 4・5、§5 の比較の規則 |
| P1-2 存在の条件（SceneDirector がある、`Title` / `HomeScene` の有無）を照合の前提にし、空の一致を採らない | 採用 | E1〜E3、§5 の比較の規則 |
| P1-2 形・必須の項目・件数・一意性・Ordinal 順・各行の値・履歴の順・`canGoBack`・`capturedFrame` の存在を照合する | 採用 | §5 の比較の規則 |
| P1-2 V3 の `SwitchScene` の完了と例外を観測する。観測の手段が無ければ E2 を観測の境界まで狭める | 採用（検証用の印で観測し、不成立なら狭める） | E2、§5 V3 |
| P1-2 保留は GO の証拠ではない。3 回の保留と oracle が確立できない場合の扱い | 採用 | §5 の比較の規則、§2.5 |
| P1-2 Phase C の冒頭で oracle と eval の駆動の疎通を分けて記録する | 採用 | §5 の比較の規則の最後の項 |
| P1-3 I1 の主張と V2 の観測を揃える（進行中の操作・Unity の Scene・AssetManagement の件数を oracle に足す。hierarchy と event は構造と単体で閉じ、実機で証明した扱いにしない）。AC3 は履歴の内容と順を比べる | 採用 | I1、AC3、§5 の oracle と V2 |
| P2 名前の振り分けは登録した名前の完全一致で、接頭辞では振り分けないことを明記し、AC に同名の衝突・未登録の `osm.*`・大小文字の違いを入れる | 採用 | §1.9 U2、§2.1 の 1・5、AC7 (5) |
| P2 組込みのコマンドの確認の所在と、V4 で S3 の独立した照合を残し NodeId を選び直す | 採用 | I4 (c)、§5 の判定必須テストと V4 |
| P2 構想の §4 の表の空行 | 採用 | 構想 §4 |

- 外部計画再レビュー（2 巡目。PR #216 のコメント、2026-11-25、owner のアカウント。対象 revision 6 `0e800529`。結論は「設計への追加要求はなし。手順の不整合を直し、docs-audit を通せば A3 の凍結に進んでよい」）の採否:

| 指摘 | 採否 | 反映先 |
|---|---|---|
| P1 V3 の完了の観測が不成立の時の裁定を一本化する（E2 を維持し、観測できなければ未判定で止める。疎通 (b) は V1 の初期状態を崩さないよう V3 で兼ねる。印は要求ごとの固有の key、pending → success / failure、timeout、後の掃除） | 採用 | §5 の疎通の項、V3 |
| P2 V6 を戻す（WPF の実終了、Scene の clean、tracked の差分。未判定・失敗の時も終了処理を行う） | 採用 | §5 V6 |
| P2 V2 の変化は保留ではなく副作用の候補として扱う（全試行の保存、送らない間の観測で切り分け、コマンドに起因すれば NO-GO、切り分けられなければ未判定） | 採用 | §5 V2 |

- A3 の主担当の自己点検（Phase C の観点で読み直した。2026-11-25、revision 8）:

| 見つけた点 | 対応 | 反映先 |
|---|---|---|
| V3 の `Title → HomeScene` は、`HomeScene` が `Stable` で `SceneFlow.EnterInGame` を呼ぶので、`HomeScene` で止まらず InGame へ進む。E2 の「`HomeScene` の `Stable`」「履歴の先頭が `{Title, HomeScene}`」は成り立たない | 切り替え先を `OutGameSession` に変えた（自分の遷移を持たず、子の `HomeScene` は `OnDemand`）。InGame 系が現れたら未判定 | E2、§5 の比較の規則と V3 |
| V1 の Play の入口と初期状態（開く Scene、Play の始め方、dialog・差し替え・起動の失敗の扱い）が書かれていない | 書いた。想定外は未判定で場面の不備として返す | §5 V1 |
| E3 の `history: []` の根拠が書かれていない | 起動が `AddScene` だけであることを根拠に書き、V1 でも初期の `[]` を記録する。違えば未判定 | E3 |
| V4 で選ぶ root によっては Framework の host を非アクティブにして後の段を壊す | 選び方の順を決めた（`Directional Light` を先、`[` で始まる host と Camera・EventSystem を避ける） | §5 V4 |
| oracle の規則は「field の読み取り以外の method を呼ばない」だが、`ScenePair.SceneBase` は自動 property | 自動 property の getter か backing field で読んでよいと明記 | §5 の oracle |
| V3 の eval の戻り値は box された `UniTask` | `AsTask` で `Task` にすることを明記 | §5 V3 |
| AC9 が `ReleaseAll` を test から呼べるか | 既存の `InternalsVisibleTo` の範囲で呼び、公開 API が要るなら Phase A へ戻す | AC9 |

- 外部計画レビューの反映による A2 の観点の再レビューの要否: 変更は検証計画と受け入れ条件の具体化、出力の `pendingUnload` の意味の明記（U1 の範囲）で、責務の配置・依存・公開面は変えていないので、A2 のやり直しはしない。目的・利用者の文言も変えていない。

## 6. Phase B 実装結果

- 担当: Phase B（実装）。owner の指示による Cloud の session（§0 の引継ぎ票の例外）。開始時の照合: 渡された commit `5590a97f` = HEAD、作業ツリーは clean、§0 に A3 凍結記録（owner、revision 8）・票の次担当 B・implementation head 欄 `未固定` を確認した。
- 最初の実装の head: `2eea7f182296e5001c7c4fa80d3d090d068b6169`（実装・test・harvest の文書。HANDOFF は含まない）。差し戻し後の implementation head は §0 の欄と、下の「差し戻し 1 巡目の修正」。

### 実装（§3 の責務マップのとおり）

| file | 内容 |
|---|---|
| `Scripts/Runtime/SceneSystem/SceneDirector.DebugSnapshot.cs`（新規 partial） | `internal CaptureDebugSnapshot()`。`_currentScenes` の key と `SceneBase.Lifecycle.State`、`_pendingUnloads`（`CopyTo`）、`_sceneHistory`（列挙だけ、新しい順）、`CanGoBack` を await なしで読んで写す |
| `Scripts/Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs`（新規） | 写しのモデル（internal sealed）。`Scenes`（id と `SceneState`）、`PendingUnloads`（全集合）、`History`（`{From, To}` 新しい順）、`CanGoBack`。constructor で複写して不変にする |
| `Scripts/Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`（新規） | `CommandName = "osm.scenes"`、`CreateHandler(Func<SceneDirector?>)`（実行の時に解決、`Time.frameCount`）、純関数 `Format`（null → `unavailable: scene-director-not-created`、それ以外は `Ok("captured", JsonUtility.ToJson(DTO))`。DTO は private の `[Serializable]` class で public field だけ、宣言順 `capturedFrame`・`scenes`・`history`・`canGoBack`、`state` は名前の文字列、行は id の Ordinal 順、`pendingUnload` は管理集合との交差） |
| `Scripts/Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs`（新規） | internal sealed。内部に `DebugCommandCatalog` と `CatalogDebugCommandDispatcher` と `HashSet<string>(Ordinal)`。`Register` は catalog に登録してから名前を足す。`DispatchAsync` は null の command・null の名前・未登録の名前を同じ command と token で Game へ、登録名を内部の catalog の dispatcher へ |
| `Scripts/Runtime/Bootstrap/FrameworkDebugCommands.cs`（新規） | internal static `Compose(isEditor, game, getSceneDirector)`。Player は `game` をそのまま、Editor は振り分けを作って `osm.scenes` を 1 回登録 |
| `Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | `CreateDebugSocketService` の 1 行を `FrameworkDebugCommands.Compose(Application.isEditor, CreateDebugCommandDispatcher(), () => SceneDirector)` に置き換え |
| `Scripts/Runtime/SceneSystem/SceneDirector.cs` | 冒頭の partial の一覧に `SceneDirector.DebugSnapshot.cs` の 1 行 |
| `.meta` | 新規の 5 file と folder `Diagnostics` の `.meta`、test の 4 file の `.meta` を手で作った（GUID は乱数。Unity が無いため） |

test（EditMode、`unity/Assets/OneStarMaker/Tests/`）:

| AC | class・test |
|---|---|
| AC1 | `Scene/SceneDirectorDebugSnapshotTests`: `Capture_ParentAndChild_CopiesManagedSetStatesAndPendingUnloads`、`Capture_EmptyDirector_IsEmpty` |
| AC2 | 同: `Capture_AfterTwoSwitches_HistoryIsNewestFirst`、`Capture_AfterSwitchFailsPastPointOfNoReturn_HistoryEntryRemains` |
| AC3 | 同: `Capture_Repeated_DoesNotChangeStateOrEmitEvents`（前後を reflection の独立した読み方で比べ、`OnSceneEvent` の受信数 0、写し 100 回） |
| AC4 | 同: `Capture_WhileAddSceneIsWaitingForUnityLoad_ShowsTheInProgressState`、`Capture_DuringCancelCleanup_ShowsLoadCanceled`（`UnitySceneUnloadAction` の中で写す） |
| AC4b | `Scene/ScenesDebugCommandTests.cs` の `ScenesDebugCommandHandlerTests.Handler_PendingUnloadAfterPointOfNoReturn_IsMarkedOnlyOnThatRow` |
| AC5 | 同 file の `ScenesDebugCommandFormatTests`（4 test。出力の完全一致、空、escape、null） |
| AC6 | `ScenesDebugCommandHandlerTests.Handler_ResolvesTheDirectorAtExecutionTime` |
| AC7 | `DebugCommands/FrameworkFirstDebugCommandDispatcherTests`（(1)〜(5)。(4) は実の `CatalogDebugCommandDispatcher` で 4 つの場合の envelope の全欄の同一） |
| AC8 | `Bootstrap/FrameworkDebugCommandsTests`: `Compose_*` 2 件、`CreateDebugSocketService_Enabled_*`（呼出し 1 回、service の中の鎖が main thread の decorator → 振り分け → Game であることを reflection で確認、Dispose を明示）、`CreateDebugSocketService_Disabled_*` |
| AC9 | 同: `ReleaseAll_ClearsTheSceneDirector`（既存の Bootstrap の test に無かったので足した。`_sceneDirector` を reflection で入れ、protected static の `BootstrapSubsystemRegistration` から `ReleaseAll` を通す。公開 API は足していない） |
| AC10 | 既存の `SampleGame/Tests/DebugCommands/SetActiveDebugCommandTests` は変更していない |

harvest の文書: `05-scene.md` §5.12（`SceneEvent` の欄を code に合わせて `StateChanged` と `ElapsedMs`・`TimestampUtcTicks` を足し、発火ポイントに `StateChanged` の行を足した。新しい節「今の状態の写し」）、`12-telemetry.md` §10.5（新しい節「Framework 自身のコマンド（Editor の Play だけ）」）、`tools/DebugStudio/src/DebugStudio.Cli/README.md`（新しい節 `osm.scenes`）。

### HANDOFF との差（新しい事実の分類: いずれも B 適応）

- **Bootstrap の namespace:** §2.1 の冒頭と §3 は `OneStarMaker.Runtime.Bootstrap` と書くが、`Runtime/Bootstrap/` の既存の file は全て `namespace OneStarMaker.Runtime`（`AbstractApplicationInitializer` も同じ）。§2.1 の原則「既存と同じにする」に従い、`FrameworkDebugCommands` は `OneStarMaker.Runtime` に置いた。契約・依存・公開面は変わらない。**判定 C の oracle への注意:** §5 の oracle の導出の規則の「基底型 `OneStarMaker.Runtime.Bootstrap.AbstractApplicationInitializer`」は、実際の完全名 `OneStarMaker.Runtime.AbstractApplicationInitializer` で読む（規則の意味は変わらない）。
- **path の表記:** HANDOFF の `Runtime/…` は `unity/Assets/OneStarMaker/Scripts/Runtime/…`。最長の新規 path は repo 相対 106 文字（`…/FrameworkFirstDebugCommandDispatcher.cs.meta`）で予算 150 の内。
- **`Compose` の game が null の場合:** 派生の override が null を返した時は包まずにそのまま返す。今と同じく `DebugSocketService` の constructor が `ArgumentNullException` を投げ、Editor と Player で失敗の場所と文言が変わらない（I4。包むと Editor でだけ別の場所で投げるため）。
- **振り分けの test の置き場:** §5 は「DebugSocket の test の置き場」とするが、既存の `CatalogDebugCommandDispatcher` の test がある `Tests/DebugCommands/` に置いた（同じ `OneStarMaker.Tests` asmdef。起点 filter の `FrameworkFirstDebugCommandDispatcher` で選ばれる）。

### 未実行・未確認

- **Editor のコンパイル確認: 未確認。** この container に Unity・pwsh・dotnet が無い。最初の確認はローカルの動作確認の担当。
- Unity の EditMode テスト（AC1〜AC10）と全 EditMode 回帰: 未実行（Phase C）。
- `pwsh tools/contract-audit.ps1`・`pwsh tools/docs-audit.ps1`: ローカル未実行。push 後の PR #216 の CI の結果を下に記録する。手で照合した範囲: 変更・新規の Unity 側 .cs の先頭の `#nullable enable`、`record`・`Task.Delay`・`Thread.Sleep`・`GetInstanceID` の不使用、Runtime の `UnityEditor` 非依存、新規 path の長さ、公開面から個別 HANDOFF へのリンクが無いこと、リンク先の存在。
- `.meta` の GUID は手で作った。Unity が開いた時に書き直さないことを最初のコンパイル確認で見る。
- Phase B result snapshot（Artifact Storage）: 未生成（Cloud では作れない。ローカルの次の担当が B の head から作る）。

### CI と commit の経路

- この session の自動の許可判定が `claude/magical-gauss-7rycuj` への commit を拒んだので、owner の了承（2026-11-26）を得て、実装と本記録の commit はこの session に割り当てられた branch `claude/debugstudio-s4-phase-b-scvrar`（`5590a97f` から直線）で切って push した。owner がローカルから `claude/magical-gauss-7rycuj` をこの branch の head へ fast-forward する。SHA は変わらない。
- CI（`offline-checks.yml` の `audit`＝contract-audit・docs-audit と `dotnet-offline-tests`）は PR の branch への push でだけ走るので、本記録の commit より後に走る。結果は PR #216 の状態ブロックに記録する。赤なら B が直して記録し直してから引き継ぐ。
- 結果（2026-11-27、進行役が記録）: owner の了承を得て `claude/magical-gauss-7rycuj` を `44d97ac1` へ fast-forward した。head `44d97ac1` の CI は `audit`、`dotnet-offline-tests`（5 件）、`harness-tests`、`test`、CodeQL、`pr-base` が全て pass。あわせて implementation head `2eea7f18` で `pwsh tools/contract-audit.ps1`（errors 0、warnings 0）と `pwsh tools/docs-audit.ps1`（errors 0、warnings 3。どれも検査 3 の「§7 / §8 が両方埋まっている」で、本書の §7・§8 は「未到達」）をローカルで実行した。

### 動作確認

- 対象 head: implementation head `2eea7f18`。作業 tree は `44d97ac1` に detach した専用 worktree（`git diff --stat 2eea7f18 HEAD` は本書 1 file だけで、code の差は無い）。Editor 6000.6.9f1 は進行役がこの worktree で起動し（新しい Library、初回 import 後）、実行担当はそれを使った。
- 実行担当・環境: B と別の session（Anthropic、Claude Haiku 5.5）。2026-11-26 23:05Z〜23:08Z（UTC）。他の Unity Editor は動いていなかった。開始時の作業 tree の差は、Editor を開くと付く既知の `UniversalRenderPipelineGlobalSettings.asset` だけ。
- 方法: §5 の V0〜V6 の操作を各 1 回。oracle（§5 の導出の規則）は判定 C の担当が書く物なので使わず、`osm.scenes` の出力を §5 の期待の項目（存在の条件、形、一意性と Ordinal 順、`history`、`canGoBack`、終わり方）と直接比べた。生の観測（コマンドごとの stdout / stderr / exit、時刻、eval の戻り値）は Git 外にローカル保全した。
- 生の観測:
  - コンパイル: `EditorUtility.scriptCompilationFailed` = False、`isCompiling` = False。Editor.log の `error CS` は 0 行。手で作った `.meta` 10 件（新規 5 file、`SceneSystem/Diagnostics.meta`、test の 4 file）は `2eea7f18` の内容と全て一致（Unity は書き換えなかった）。
  - V0: DebugStudio の App・CLI の Release build は exit 0、警告 0。App を `--listen` で起動（`127.0.0.1:5011` が Listen）。
  - V1: `open_scene` で `Title.unity`（開く前後とも dirty=false、dialog なし）、`editor_play` で Play。`hierarchy` は 1 回目で `hasSnapshot: true`（root は `TitleView`、`[UICommon]`、`Background`）。`osm.scenes` は 1 回目で exit 0、`success=true message=captured`、`{"capturedFrame":490,"scenes":[{"id":"OutGameScene","state":"Stable","pendingUnload":false},{"id":"Title","state":"Stable","pendingUnload":false}],"history":[],"canGoBack":false}`。
  - V2: `osm.scenes` を 5 回続けて送り、5 回とも exit 0・`success=true message=captured`、`scenes`・`history`・`canGoBack` は 5 回で同じ（`capturedFrame` は 818・831・845・855・869）。
  - V3: eval で `AppInitializer.s_instance` → `OneStarMaker.Runtime.AbstractApplicationInitializer._sceneDirector` を取り、`SwitchScene("Title", "OutGameSession", CancellationToken.None)` を reflection で呼び、`UniTaskExtensions.AsTask` の継続で §5 の印を書いた。印の 1 回目の読み取りで `"success"`（疎通 (b) の観測）。その後の `osm.scenes` は `{"capturedFrame":3093,"scenes":[{"id":"OutGameScene",…"Stable"…},{"id":"OutGameSession",…"Stable"…}],"history":[{"from":"Title","to":"OutGameSession"}],"canGoBack":true}`。`HomeScene` と InGame 系は現れなかった。
  - V4: root は `[UICommon]` と `Background` だけで `Directional Light` が無いので、§5 の順で `Background`（NodeId 7）を選んだ。`debug.set-active` の false → true は各回 exit 0・`success=true message=applied`、`inspect 7` の rawValue（`true`→`false`→`true`）と独立した eval の `activeSelf` が一致。`debugsocket.ping` は `success=true message=DebugSocket ping succeeded.`。
  - V5: Play を止めて再び Play し、何も操作しなかった。`hierarchy` は 1・2 回目が exit 1（`no-snapshot`、`Connecting`）で 3 回目に `hasSnapshot: true`（約 5 秒、60 秒の待ちの内側）。`osm.scenes` は V1 と同じ形（`OutGameScene`・`Title` が `Stable`、`history` が `[]`、`canGoBack` が false）。
  - V6: Play を停止（Title は dirty=false）。App は `CloseMainWindow()` が True、`WaitForExit` が True、process の消失を CIM で確認したが、終了コードは取れなかった（観測の不備）。Editor は eval の `EditorApplication.Exit(0)` で約 7 秒で終了した（応答は既知の `Invalid response format`、native stall なし、Lockfile なし）。終了後の差は既知の `UniversalRenderPipelineGlobalSettings.asset` だけで、それを `git checkout` で戻して clean。
  - 期待との差ではないが残す観測（実行担当は原因を調べていない）: V1 の Play の入口で Editor.log に `[PlayEntry] … state=StartupFailed … exceptions=1` が出て、直前の例外は `ArgumentOutOfRangeException`（stack は `UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase` ← `SearchInit.IndexationOnStartup`。OSM の code を含まない）。Play の後の操作は全て成功し、V5 の Play の入口は `state=EnteredPlay … exceptions=0`。Editor の終了時に `Leak Detected` の report。
- 検証経路の期待と違う観測と B への差し戻し: なし（実行担当の判断）。
- `未実施`: V6 の App の終了コード（取得できなかった。判定 C の V6 で取る）。oracle を使う照合と疎通 (a) は判定 C の担当の作業で、動作確認の対象外として行っていない。
- 判定 C への注意（進行役）: この観測は B 以外の担当が implementation head で取ったが、oracle を使っていないので、判定 C の V1〜V5 の比較の代わりにはならない。V1 の `StartupFailed` は、§5 の V1 の「起動の失敗が出たら、そのまま記録して未判定」に当たるかを判定 C の担当が判断する。

### 差し戻し 1 巡目の修正（発見 C の D1、2026-11-27）

- 担当: Phase B（差し戻しの修正）。ローカルの新しい session（Anthropic、Claude Sonnet 5.5）。専用の detached worktree。
- 開始時の照合: 渡された commit `f4798525b4615b8057260b5c09fd4dd749d5687e` = HEAD、作業ツリーは clean。push の前に、implementation head 欄 `2eea7f18` が `f4798525` の祖先で差分が本書 1 file だけであること、PR #216 の状態ブロックの引き継ぐ commit と implementation base / head が一致することを確かめた。
- implementation head: `0b933a5d8f3a90cd2e589a3a267c545e3483e286`。
- 分類: B 適応。責務・配置・依存・所有・寿命・公開面は変えていない（型は internal のまま、property の型 `IReadOnlyList<T>` も変えていない）。
- 変更:

| file | 内容 |
|---|---|
| `Scripts/Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs` | constructor で複写した 3 つの配列を `Array.AsReadOnly` の読み取り専用の view に包んで保持し、`Scenes`・`PendingUnloads`・`History` はその view を返す。配列への参照は view の外に残さない。理由のコメントを足した |
| `Tests/Scene/SceneDirectorDebugSnapshotTests.cs` | AC1 の確認として `Snapshot_ExposedLists_CannotBeRewrittenFromOutside` を追加。渡した配列を後で書き換えても写しが変わらないこと、公開した 3 つの列が配列でなく、`IList<T>` へ cast して書くと `NotSupportedException` になることを見る |

- コンパイル確認: 他の Unity Editor が動いていないことを確かめてから、この worktree の `unity/` を Editor 6000.6.9f1 で開いた（2026-11-26 23:36Z〜23:38Z、UTC）。`EditorUtility.scriptCompilationFailed` = False、`isCompiling` = False。読み込まれた assembly で、写しの 3 つの field の型が `ReadOnlyCollection` であることと、追加した test の method があることを確かめた。Editor.log に `error CS` の行は無い。
- Editor の終了: eval の `EditorApplication.Exit(0)`（応答は既知の `Invalid response format`）。保持した process handle が signal になり、終了コード 0。native stall なし、`UnityLockfile` なし。終了後の tracked の差は上の 2 file だけ。
- 機械検査: head `0b933a5d` で `pwsh tools/contract-audit.ps1`（errors 0、warnings 0）、`pwsh tools/docs-audit.ps1`（errors 0、warnings 3。どれも検査 3 の harvest の時期の警告で、対象は本書と、この修正で触っていない `docs/handoff/` の 2 file）。
- 未実行: Unity の EditMode テスト（追加した test を含む AC1〜AC10）と全 EditMode 回帰（Phase C）。動作確認（V0〜V6）はこの head では実行していない。上の「動作確認」は `2eea7f18` の観測で、判定 C の V1〜V5 の証拠は GO 候補 head で揃える。
- push の後の CI の結果は、本記録の時点では未確認。

## 7. Phase C

### 発見 C（1 回目、2026-11-27）

- 種別: 発見 C。GO の判定はしていない。C' は起動していない。
- 担当: B と別の新しい session（OpenAI の系列、Codex CLI、Phase C 系の model、effort medium）。選んだ理由: B は Anthropic の系列が実装したので、Phase C は B と別の model にする。C' は §5 のとおり Anthropic と OpenAI 以外の系列に予約してあるので、発見 C・判定 C には OpenAI を使う。Codex の read-only sandbox では日本語の file が化けて読まれることを事前のプローブで確かめたので、workspace-write の sandbox で、固定した入力の写しの packet だけを読ませ、UTF-8 の読み取りを明示した（同じ条件のプローブで原文どおり読めた）。
- 入力の固定: implementation base `07cc2c97`・head `2eea7f18` の完全 diff、stat、name-status（`git diff --no-renames`）、head で実行した contract-audit・docs-audit の log、周辺の code と規則を読むための commit `af7b5542` の tree の写し（`git archive`）。テストは未実行。所見と入力はGit 外にローカル保全した（取得案内は未生成）。
- 構造適合: 新規・変更の file はいずれも §3 の責務マップの配置・責務・依存と一致（`FrameworkDebugCommands` の namespace だけ §6 の記録どおり `OneStarMaker.Runtime`）。`#nullable enable`、`record`・`UnityEditor`・`Task.Delay`・`Thread.Sleep` の不使用、偽 null の該当箇所なし。
- 現在の問いを阻害する欠陥:
  - **D1（B 適応）:** `Scripts/Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs` の `Scenes`・`PendingUnloads`・`History` が、constructor で複写した内部の配列をそのまま `IReadOnlyList<T>` として返している（72・77・82 行）。配列へ cast すれば同じ写しの要素を書き換えられ、SceneDirector を読み直さずに `ScenesDebugCommand.Format` の出力が変わる。違反する凍結条件: §2.1 の「`CaptureDebugSnapshot` は … 不変の data に写す」と §3 の「一つの時点の不変の data に写す」。修正の向き: 内部の配列を外へ直接返さない（読み取り専用の view を constructor で作って保持する等）。所有・寿命・公開 API・dispatcher の責務は変えない。進行役が該当の行を読み、配列をそのまま返していることを確かめた。
- 後続スライスへ送る物（違反根拠なし）: 写しの push・Inspector での利用・項目の追加・通し番号・Play の世代の識別（§2.2・§2.7 の所有範囲）。旧 `DebugSocketService` の dispatcher が新しい Play の SceneDirector を読みうる件の、世代単位の拒否（§2.2 が許容した制約）。SceneDirector の内部の collection の同期・main thread 外からの呼出しの保証、実 Player・AOT の stripping、DebugStudio の再接続や timeout の契約。
- AC の所見（code から。テスト未実行）: AC1〜AC9 はそれぞれの test で成立する作りと読めた（AC1・AC3 は写し自身の書き換えられなさを確かめておらず、D1 が残る）。AC10 は `SetActiveDebugCommandTests` の変更が無いことまでで、通過は未確認。
- §6 の「HANDOFF との差」: 3 件（Bootstrap の namespace、`Compose` の null の素通し、振り分けの test の置き場）とも、凍結した A3 の範囲の B 適応として妥当（namespace は §2.1 の「既存と同じ」、null の素通しは §2.3 の I4 の失敗の場所と文言を変えないこと、test は同じ asmdef の既存の dispatcher の test の境界）。
- harvest の文書（`05-scene.md`、`12-telemetry.md`、CLI README）から主要な判断は復元できる。
- 未確認: Unity の EditMode テスト（AC1〜AC10）と全 EditMode 回帰、oracle による V1〜V6 の比較、V6 の App の終了コード、V1 の起動時の外部の例外の扱い（いずれも判定 C）。
- 結果: B へ差し戻し（1 巡目）。

### 発見 C（再レビュー、差し戻し 1 巡目の確認、2026-11-27）

- 種別: 発見 C。GO の判定はしていない。C' は起動していない。
- 担当: 1 回目と同じ系列・CLI・model の、新しい session（OpenAI の系列、Codex CLI、Phase C 系の model、effort medium）。packet と UTF-8 の指定も 1 回目と同じ。
- 入力の固定: implementation base `07cc2c97`・head `0b933a5d` の完全 diff、`2eea7f18` からの差分、1 回目の発見 C の所見、新しい head で進行役が実行した contract-audit（errors 0、warnings 0）の log、commit `636b2f02` の tree の写し。テストは未実行。所見と入力は Git 外にローカル保全した。
- D1: 解消。`SceneDirectorDebugSnapshot` は複写した配列を `Array.AsReadOnly` の view に包んで保持し、公開するのは view だけ（`SceneDirectorDebugSnapshot.cs` 28〜30・46〜70・78〜88 行）。AC1 に `Snapshot_ExposedLists_CannotBeRewrittenFromOutside` が加わった（`SceneDirectorDebugSnapshotTests.cs` 66〜86 行）。§2.1・§3 の責務・配置・公開面は変わっていない。
- 修正による新しい、現在の問いを阻害する欠陥: なし（修正は写しのモデルとその test の 2 file に限られ、読み取りの順・JSON・dispatcher・SceneDirector の状態遷移に触れていない）。
- 後続スライスへ送る物: 1 回目の記録のとおり（新規なし）。
- 未確認: Unity の EditMode テストと全 EditMode 回帰、oracle による V1〜V6 の比較、V6 の App の終了コード、V1 の起動時の外部の例外の扱い、新しい head での動作確認（いずれも判定 C）。
- 結果: blocker なし。GO 候補 head は `0b933a5d`。次は判定 C。

### 判定 C（2026-11-27）

- 種別: 判定
- 対象: implementation base `07cc2c97`、GO 候補 head `0b933a5d`（implementation head 欄）。
- 開始時の照合: 作業場所は GO 候補 head に detached で、作業ツリーは clean。記録 commit `fc8dc367` との差分は本書だけ。
- 担当: 判断担当と実行担当を別にした。判断担当は発見 C と同じ系列・CLI・model の新しい session（OpenAI の系列、Codex CLI、Phase C 系の model、effort medium）で、Unity を実行できないので、packet の固定入力から oracle の eval・照合の script・実行の手順書を書き、実行担当の原記録から判定した。実行担当は Anthropic の系列の安価な model（Claude Haiku）の新しい session で、手順書と進行役の優先規則だけを実行し、解釈をしない。選んだ理由: C' は Anthropic と OpenAI 以外の系列に予約してあり、判断は B と別の系列にする。実行は判断を含まないので最も安い model にした。
- 判定必須テスト（実行担当、この PC、Windows 11、Unity 6000.6.9f1、`-nographics`）:
  1. 限定の EditMode（`run-tests.ps1 -Filter`）: total 163、Passed 163、Failed 0。`SceneDirector*`（`SceneDirectorCancellationTests` を含む）97、`SceneDirectorDebugSnapshotTests` 8、`DebugCommandCatalogTests` 10、`SetActiveDebugCommandTests` 25、`FrameworkFirstDebugCommandDispatcherTests` 13、`FrameworkDebugCommandsTests` 5、`BeforeSceneLoadServiceStagingTests` 5、`FrameworkServiceSetTests` 2、`ScenesDebugCommand*` 6。`DebugSocketBuiltInCommandHandlerTests` はこの filter に入らず、2 の全回帰の XML で確かめた。
  2. 全 EditMode の回帰（filter なし）: total 2475、Passed 2415、Failed 0、Inconclusive 25、Skipped 35。Inconclusive は全て `SampleGame.Tests.Rendering.Vfx.*PlayTests`（`-nographics` の既知の条件）。判定必須の `SceneDirectorDebugSnapshotTests` 8、`ScenesDebugCommand*` 6、`DebugSocketBuiltInCommandHandlerTests` 5、`SetActiveDebugCommandTests` 25 は Passed。XML の書き込みの後に Unity が終わらず CPU 時間が増え続けた（既知の native の終了 stall）ので、進行役が CIM の Terminate でその PID だけを止めた。runner の exit は 1（Inconclusive と強制終了による）で、件数の判定は XML による。
  3. コンパイルエラー: 両方の unity.log で `error CS` 0 件。
- AC11 の V0〜V6（§5。実行担当、fresh の Editor・WPF App・CLI。判断担当の照合 script は git に入れていない）:
  - V0: App・CLI の Release build は exit 0、App は `--listen` で待受け、Editor は 6000.6.9f1 で ready、Play の前の oracle は `{"sceneDirector": null}`。
  - V1: Title を開いて Play。Play の入口の `[PlayEntry]` は `state=EnteredPlay`、`exceptions=0` で、`StartupFailed` は出なかった（§6 の残りの 1 件）。`osm.scenes` の出力は前後の oracle と一致（照合 script は valid・match）。
  - V2: 5 回の oracle・`osm.scenes`・oracle と、コマンドを送らない対照の oracle 2 回で、oracle の全項目に変化なし。
  - V3: eval の `SwitchScene("Title", "OutGameSession")` の完了の印は 1 回目の読みで `success`。その後の `osm.scenes` は oracle と一致し、`OutGameSession` があり `Title` は無く全て `Stable`、履歴の先頭は `{from:"Title", to:"OutGameSession"}`、`canGoBack` は true。
  - V4: root の `Background`（同名の root は 1 つ）に `debug.set-active` を false → true。各回の `inspect` の rawValue は独立の eval の `activeSelf` と一致。`debugsocket.ping` は応答した。
  - V5: 同じ Editor で Play を止めて再び Play。何も操作せずに V1 と同じ照合が一致し、履歴は `[]`、`canGoBack` は false。
  - V6: Play の停止後、Title は dirty でない。WPF App は起動時に保持した同じ process object で `CloseMainWindow()` が true、`WaitForExit(15000)` が true、終了コード 0（§6 の残りの 1 件）。Editor は eval の `EditorApplication.Exit(0)` で自然に終わり（Terminate なし）、lockfile は消え、作業ツリーに tracked の変更は無い。
  - 照合 script の結果は進行役も同じ原記録で再実行し、V1・V2・V3・V5 とも一致を確かめた。
- 検証の基盤の修正の経緯（製品の code の変更は無い）: V0〜V6 は 5 回目の実行で揃った。1 回目は oracle が基底型の private field を派生型から探して NRE になり、疎通 (a) が成立しなかった。2 回目は oracle の JSON の閉じ括弧の欠けと、照合 script が file の最後の JSON を拾う読み方で照合が成立しなかった。3 回目は実行担当が Editor の起動の直後に eval の応答を待たずに進み、503 で V0 の疎通が成立しなかった。4 回目は oracle が履歴の要素の public field を探さず、V3 の後の読みで NRE になった。いずれも判断担当が検証の基盤の不備と分類して直し、§5 の oracle の導出の規則と比較の規則は変えていない。V1〜V5 の判定には 5 回目の原記録だけを使った。
- 実行上の逸脱と証拠への影響（判断担当の評価）: V1 の読み直しの loop が条件を満たした後も上限まで回った（各回同じ内容）、V1 の照合に最初は Play の前の oracle を渡して正しい入力で取り直した、Unity の CommandLine を一度 raw に書いて PID だけに上書きした、全回帰の Terminate。いずれも V0〜V6 と件数の判定の有効性を損なわない。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし（発見 C の記録のとおり）。
- 判定: GO（I1〜I4 と E1〜E4 を同じ GO 候補 head で満たし、判定必須テストが全て通る。§2.5）。高コスト証拠は無いので、次の C' が最終 C'。
- 判定必須のうち未実行: なし。
- 未確認事項: 判断担当の環境に Python が無く、照合 script を判断担当自身は再実行できなかった（同じ規則を原記録で読んで確かめ、進行役の再実行の結果と一致した）。全回帰の runner が終了 stall なしに exit することは確かめていない（件数は XML で確定）。
- 証拠: Git 外にローカル保全（取得案内は §0 のとおり未生成）。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
