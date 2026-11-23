# DebugStudio S3 — CLI から hierarchy の既存 GameObject の activeSelf を切り替える

## 0. メタデータ

- type: slice
- status: 動作確認（revision 4 の Phase B 完了）
- branch: codex/debugstudio-s3（Draft PR #204、base は develop）
- implementation base commit: `9de34cc2ec2dfe82b782f122b0012b902e98f277`（origin/develop。A1 が読んだ `d926e3b6` からの差分は文書 3 file だけで、DebugSocketServices・Bootstrap・`SampleGame/DependOnAll`・`tools/DebugStudio`・ProjectSettings に変更は無い）
- implementation head commit: この票を含む commit
- A3 凍結記録: owner が 2026-11-23 に「凍結して B へ」と承認。対象は revision 4（外部計画レビューの X1・X2 を反映したこの版）。次担当は Phase B。
- risk: normal（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-20 / updated: 2026-11-23（revision 4 の Phase B 完了）
- expires: 2026-12-31 または置換 revision
- harvest to: `tools/DebugStudio/src/DebugStudio.Cli/README.md`（`debug.set-active` の送り方と結果の読み方）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（SampleGame の catalog 配線と `TryGetHierarchyGameObject` の契約）
- Phase A snapshot の取得案内・状態: 凍結版はこの票を含む commit（SHA は起動指示で渡す）。C / C′ 用の snapshot は B の完了までに主担当が task の手元引渡し先に保存し、取得案内をここに書く。
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: revision 4 は未生成（B は生成していない）。revision 3 の分は §1.5 のとおり保存済みで、revision 4 の判定には使わない。
- task: `debugstudio-s3`（開始済み）
- 引継ぎ票: 次は動作確認（Phase B の後）。
  - 作業: implementation head で §5.4 の V0〜V6 を各 1 回軽く行い、書き込み経路（登録・解決・適用）が実 Editor で成り立つかを確かめる。結果を動作確認の欄に書き、次の担当（発見 C）への票を書く。成り立たなければ待受け・接続、登録、解決、適用のどこかを分けて B へ戻す（§5.3）。
  - 読む範囲: 既定（§0、§5、§6）。期待の導出に §2.1 の 2・3 と §2.2 E1 を読む。
  - 必要な権限: 専用 worktree での自分で開いた Editor の Play と eval（独立値の読取りだけ）、Release の WPF・CLI の build と起動。Unity テスト・Build は行わない。
  - 停止規則: 共有 Editor・人間が開いた Editor を操作も強制終了もしない。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

### 1.1 目的と利用者（revision 4。A2 の目的レビューへ渡す原文）

- 目的: Editor の通常の Play 中に、DebugStudio の既存の hierarchy・send・inspect で、ヒエラルキーにある既存の GameObject を NodeId で 1 つ選び、その activeSelf を指定した値にして読み返せるようにする。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント。

### 1.2 目的の変更と旧判断の撤回

- revision 3 の目的（変更前の原文）: 「Editor の通常の Play 中に、DebugStudio CLI だけで、SampleGame が用意した検証用 GameObject を hierarchy で見つけ、その activeSelf を指定した値に切り替え、inspect で読み返せるようにする。検証用でない GameObject は変更しない。」利用者は変えていない。
- 変えた理由: owner が目的を訂正した。原文「ヒエラルキーにある既存のGameObjectのActiveをDebugStudioから変えられますよー という機能を作ってもらうつもりのスライス」（2026-11-23）。PR #204 の [新着コメント](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/204#issuecomment-7088837289) は、既存 GameObject を NodeId で選び、既存の CLI 経路 hierarchy → send(false) → inspect(false) → send(true) → inspect(true) で確かめる形を示した。引き継いだ主担当が 2026-11-23 に原文を取得して照合し、上の要約と一致した。原文は加えて、操作経路を DebugSocketService 側に main thread / Editor のデバッグ用途に限った狭い経路として置くことを「検討する」よう求め、Play をまたぐ厳密な世代保証は要らないとした（§2.1 の代替、§2.8）。
- owner の承認: 上の訂正そのもの。あわせて、通常の Play に専用の GameObject を作らないこと、Light は一例で対象を特定の 1 個・名前・タグ・印・許可リストに制限しないこと、Editor の Play に限ること、activeSelf の明示セットだけにすること、Player・汎用プロパティ・UI の追加が要らないこと、デバッグでの使いやすさを優先することを指示した。利用者の入力は増えない（§1.8）。
- 撤回したもの: revision 3 の凍結（候補 `a1178db5`）、その実装 `cf440201` に対する判定 C・C′ の GO、Phase D のマージ判断。旧目的の条件に対する判定なので、revision 4 には使わない。実装 head は未固定に戻した。
- 新しい対象範囲へ持ち込まないもの: 当初（revision 1・2）の「前の Play の指定で別の対象へ書かない」要求と、revision 3 の「検証用 1 個への再割当だけを許容、Play 世代の識別は後続」の裁定。どちらも検証用 1 個を前提にしていた。任意の GameObject を対象にしたときの扱いは §2.8 で owner が裁定した（A: 許容）。

### 1.3 対象外

- activeSelf 以外の field・component・メソッドへの書き込み、汎用プロパティの編集、反転（toggle）、一括変更。
- 専用・検証用の GameObject を作ること。対象を名前・タグ・印・許可リストで絞ること。
- hierarchy に出ない GameObject（DontDestroyOnLoad など）。
- 結果不明を確定させる仕組み（要求の相関、送信前後の区別、新しい outcome・exit）と自動再送。操作履歴。
- Play をまたぐ指定の識別（セッション・世代の機構、registry の採番の変更を含む。§2.8 の裁定 A）。
- 専用の CLI サブコマンド、protocol・wire の変更、WPF・GUI の変更。
- Player（Build した実行ファイル）と遠隔接続。
- SampleGame の Scene・Prefab・設定 asset の変更。
- S2 HANDOFF（`docs/handoff/DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

### 1.4 現況（develop `d926e3b6` のコード。A1 担当がこの session で読んだ）

- CLI の `send --command <name> --payload <json>` は WPF の control 経由で Runtime へ送り、`CommandResult` を受け取れば `CommandResult request=… success=… message=…` と payload の JSON を、success=true なら stdout、false なら stderr へ出し、success に応じて exit 0 / 1。受け取れなければ detail を stderr に出し、期限切れは exit 2、それ以外は exit 1。CLI は再送しない。
- Runtime: router は組込みの `ping` / `runtime-diagnostics` 以外を App の dispatcher へ渡し、`MainThreadDebugCommandDispatcher` が main thread で呼ぶ。handler の例外は payload の無い Success=false になる。
- 登録経路: `AbstractApplicationInitializer.CreateDebugCommandDispatcher()`（protected virtual、既定は `NullDebugCommandDispatcher`）、`DebugCommandCatalog`、`CatalogDebugCommandDispatcher`、`DebugCommandResult.Ok` / `Fail`。dispatcher は DebugSocketService より先に作られる。develop の SampleGame は catalog を配線していない。
- NodeId: internal な `DebugSocketRuntimeNodeRegistry` が service ごとに `_nextRuntimeNodeId = 1` から採番する。`Reset`（WPF の接続替え）は対応表を消すが採番値を戻さない。capture で見えなくなった NodeId は prune する。NodeId → GameObject は `TryFindGameObjectByNodeId`（破棄済みは false にして prune）で、DebugSocketService では private な `TryFindGameObjectByNodeId`（`_gate` の中）を inspect だけが使う。Game 側から NodeId で GameObject を引く公開手段は無い。
- hierarchy の capture は `SceneManager` の読み込み済み scene だけを走査し、DontDestroyOnLoad を出さない。非アクティブな GameObject も出す。`NotifyHierarchyChanged()` は public。
- `AbstractApplicationInitializer` に DebugSocketService の accessor は無い（`LoggerFactory => _services?.LoggerFactory` と同形の accessor はある）。
- `EditorSettings` は Enter Play Mode Options が有効で Domain Reload が無効（`m_EnterPlayModeOptions: 1`）。Play ごとに service を作り直すので、NodeId は Play ごとに 1 から振り直される。
- develop は `d176033a` 以後に Play ボタンの配線（`PlayEntryInterceptor` ほか、保存確認や空 scene への差し替えの段を含む）と `AbstractApplicationInitializer` の content overlay を変えた。DebugSocket・hierarchy・inspect のコードは変わっていない。Editor Play の場面は変わっていないことを §1.6 で確かめた。
- この PR の branch には revision 3 の実装（検証用 GameObject の host、逆引き `TryGetNodeIdUnsafe` / `TryGetHierarchyNodeId`、`SetActiveDebugCommand`、`AppInitializer` の配線、テスト、README）が残っていて、develop には無い。

### 1.5 原資料（保存済み。再実行しない。どれも develop の現状ではない）

詳細は commit `d1d11e0ca10203cd77acba9d226d900b3538da93` の本 HANDOFF（§1.5、§6、§7）と、task の公式 Evidence にある。

- 基準観測（`d176033a`、2026-11-22、Editor 6000.6.9f1、Release の WPF・CLI）: Play 前の hierarchy は `no-snapshot`、Play 後に読める。通常の Editor Play の root は開いていた scene の `Main Camera`・`Directional Light` と UICommon。DontDestroyOnLoad は出ない。eval で作った同じ構成の fixture は Play1 と Play2 で同じ NodeId になり、Play2 で Play1 の NodeId を inspect すると Play2 の GameObject を返した。WPF は通常終了で exit 0。
- revision 3 の実装 `cf440201` の動作確認（別担当）: hierarchy の `Directional Light`（NodeId 2）を eval の EntityId で特定でき、inspect・eval の activeSelf が一致した。書き込みは検証用 GameObject に限る設計だったので、`Directional Light` への送信は拒否された。
- revision 3 の全 EditMode（Phase C）: `active` に文字列 `"false"` と `null` を渡すと、どちらも false として適用され、結果の activeSelf と実値が一致した（拒否されなかった）。文字列 `"true"` のケースもテストにあったが、HANDOFF には結果が記録されていない。branch の README（「`active` missing or not a boolean」を拒否と書く）と `TryParse` のコメントはこの実測と食い違う。

### 1.6 A0 確認（develop の Release WPF と実 Editor の読取り。実施済み）

目的: develop の Play の配線の変更後も、通常の Editor Play で既存の GameObject が hierarchy に出て inspect で読めることと、Phase C で使う場面（開いている scene と対象 X）を決める。書き込みは未実装なので確かめない。

- 実行担当: この A1 とは別の新しい session。共有 Editor は使わない。
- 環境・初期状態: origin/develop `9de34cc2ec2dfe82b782f122b0012b902e98f277` の clean な detached worktree（新規に作るか、clean な `D:/repositories/unity/osm-ds3-a2` を detach し直す。どちらも `unity/Library` は無い）と、その worktree の `unity/` を自分で開いた Editor 6000.6.9f1。新しい worktree は import に 10 分前後かかる。起動後、開いている scene を開き替えず、dirty でないことを確かめる。既に Editor が開いていれば所有者を確かめ、自分のものでなければ止める。
- R0: HEAD・clean・Editor の版を記録する。`dotnet build tools/DebugStudio/src/DebugStudio.App/DebugStudio.App.csproj -c Release` と `dotnet build tools/DebugStudio/src/DebugStudio.Cli/DebugStudio.Cli.csproj -c Release`。起動前に他の DebugStudio の process が無いことを確かめ、App の Release 出力を `--listen` で直接起動して PID を記録する。
- R1（実施時の上限は 15 秒。結果を受けて §5.4 は 60 秒に改めた）: `tools/unity-editor.cmd command` で公開名を確かめて Play を始める（無ければ eval で `UnityEditor.EditorApplication.isPlaying = true;`）。dialog や Play の中止が出たらそのまま記録する。Release 出力の `debugstudio-cli.exe hierarchy` を `hasSnapshot: true` まで読み直し（総計 15 秒以内）、全 root の名前・NodeId・flags と、開いていた scene の名前を記録する。
- R2: 開いていた scene の root で名前が 1 つだけのもの（基準では `Directional Light`）を X とし、その NodeId で `inspect` して `GameObject.ActiveSelf` の rawValue を記録する。eval で、読み込み済みの scene の root からその名前の数、`UnityEngine.EntityId.ToULong(go.GetEntityId())`、activeSelf を読む（eval の C# は UTF-8 の Base64 で渡す。作らない、書かない）。
- R3: `send --command debug.set-active --payload '{"nodeId":<X>,"active":false}'` の stdout / stderr / exit を記録する（develop では未登録の Success=false、exit 1 を見込む）。eval で X の activeSelf が R2 と同じか読む。
- R4: Play を止める。WPF に通常の終了を要求し（`CloseMainWindow`）、保有 handle の signal と実終了コードを記録する。Scene が clean か、tracked の変更が無いかを記録し、自分で開いた Editor を通常終了する。
- 保存: 各段のコマンド、開始終了 UTC、stdout / stderr / exit、eval の戻り値。Git に入れずローカルに保全し、要約を主担当へ返す。主担当がこの節に結果を書き、§2 と §5.4 の X と場面を確定する。
- 不成立時: Play が始まらない、hierarchy が読めない、開いていた scene の既存 root が出ない場合は、検証基盤の不備か develop の変化かを分けて記録し、A1 で場面か問いを見直す。原因調査はしない。

**結果（2026-11-23、別 session の実行担当。原記録は Git 外のローカル保全で、主担当が読んだ要約は次のとおり）**

- 環境: `osm-ds3-a2` を `9de34cc2` に detach（clean、`unity/Library` 無し）。Editor 6000.6.9f1 を実行担当が自分で起動し、eval 可まで約 11 分。開いていた scene は Untitled（name・path とも空）、dirty=False、root は `Main Camera`・`Directional Light`（どちらも active）。Release build は App・CLI とも exit 0・警告 0。WPF を `--listen` で起動。
- R1: `editor_play` で Play 開始、dialog・中止なし。**手順の 15 秒以内に `hasSnapshot: true` にならなかった**（10 回とも exit 1 の `no-snapshot`、Connecting / Negotiating）。窓の外の 1 回の読み直しで true、captured は Play 開始から約 21 秒。原因は調べていない。root は 1 `Main Camera`、2 `Directional Light`、3 `[UICommon]`（子 4〜7）。Play 中の scene は Untitled と `UIScene`。空 scene への差し替えは無く、基準観測（`d176033a`）と同じ形。
- R2: X = `Directional Light`（NodeId 2）。`inspect 2` の `GameObject.ActiveSelf` の rawValue `"true"`。eval で同名の root は 1 件、activeSelf=True。
- R3: `send debug.set-active` は exit 1。stdout は空で、`CommandResult … success=false message=Debug command 'debug.set-active' is not registered.` は **stderr** に出た（CLI は success=false のとき CommandResult の行と payload を stderr へ書く。`Program.WriteResult`）。送信後も activeSelf=True。
- R4: Play 停止後 Untitled は dirty=False、`Assets/` の Dirty asset 0。WPF は `CloseMainWindow` で実終了コード 0。Editor は exit 0、native stall なし。tracked の変更は Editor を開くと付く既知の `UniversalRenderPipelineGlobalSettings.asset` の差分 1 件だけで、終了後に戻した。
- 主担当の判断: 読取り経路は develop で成立。Phase C の場面は「開き替えない Untitled の scene の `Directional Light`」に確定する（§2.2 E1、§5.4）。hasSnapshot までの待ちは検証手順の上限であって設計の主張ではないので、§5.4 V1 の上限を 60 秒にする。rejected を含む success=false の出力先は stderr として §2.1 の 3・§5.4 に書く。

### 1.7 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef 参照と package を追加しない。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側 `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `ZLogger*` 型を公開面へ出さない。Editor コードを Runtime asmdef に置かない。
- path は `tools/path-budget.json` の予算に従う。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 の不変条件（`--listen` なしの起動と既存 send / compare の契約、待受け loop は 1 本、inspect は GUI 選択を変えない、CLI / control が期限なく固着しない）を壊さない。
- 人間が開いた Editor を強制終了しない。重複した Editor や WPF を起動しない。共有 Editor を触らない。

### 1.8 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 選ぶ（`hierarchy`） | 対象の行（名前を見て選ぶ） | NodeId・名前・flags（既存） | 実行時の操作 | 「ヒエラルキーにある既存の GameObject を NodeId で 1 つ選び」 |
| UC2 切り替える（`send`） | コマンド名 `debug.set-active`（README の固定値）、payload の `nodeId`（UC1 から写す）と `active`（`true` / `false`。省略不可） | requestId（既存） | 実行時の操作 | 「指定した値にして」。値を明示するので送り直しても逆に戻らない |
| UC3 読み返す（`inspect`） | NodeId（既存） | 既存どおり | 実行時の操作 | 「読み返せる」 |
| UC4 結果が分からないとき | 新しい入力は無い（UC3 で読み、必要なら UC2 を人が送り直す） | CLI は再送しない（既存） | 実行時の操作 | README で説明（§2.1 の 4） |

A1 自己点検: 準備の操作も開発時の宣言も無い。新しい入力は send の payload の 2 値だけ。目的に session・世代などの機構の語は無い。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 通常の SampleGame の Editor Play 1 回の中で、何も生成せずに、既存の hierarchy・send・inspect だけで、hierarchy にある既存の GameObject の activeSelf を false にしてから true に戻して読み返せ、今の hierarchy に無い NodeId への送信は何も変えずに拒否されるか。
- 次スライスへ進む最低条件: §2.2 の I1〜I3 と E1 を同じ GO 候補 head で満たす。

### 2.1 統合設計

書き込みの意味は SampleGame が既存のコマンド登録経路で持つ。Framework は「今の hierarchy の NodeId が指す GameObject」を返す読み取り口だけを公開する。DebugStudio（WPF・CLI・protocol）は変えない。

1. **読み取り口（Framework。公開面はこの 2 つ）:**
   - `DebugSocketService.TryGetHierarchyGameObject(long nodeId, out GameObject? gameObject)`（public、main thread で呼ぶ。XML コメントに main thread で呼ぶこと、Dispose 後は false を返すことを書く）。先に `_disposed` を見て、立っていれば false を返す。`Dispose()` は `_disposed` を立てた後に片付けを非同期で始めるので、registry の対応表が残っている間があり、委譲だけではこの契約を満たさない。立っていなければ、inspect が使う既存の private `TryFindGameObjectByNodeId`（`_gate` の中で registry を引き、破棄済みは false）を呼ぶ。未 capture・prune 済み・`Reset` 後・破棄済み・hierarchy に出ない GameObject は false。
   - `AbstractApplicationInitializer` に `protected DebugSocketService? DebugSocket => _services?.DebugSocket;`（既存の accessor と同じ形）。
2. **コマンド（SampleGame。新規 `DependOnAll/DebugCommands/SetActiveDebugCommand.cs`、internal static）:**
   - `AppInitializer` が `CreateDebugCommandDispatcher()` を override し、`Application.isEditor` のときだけ `DebugCommandCatalog` に `debug.set-active` を登録して `CatalogDebugCommandDispatcher` を返す。それ以外は base。handler は `() => DebugSocket` で実行時に service を読む。解決は delegate（`bool (long nodeId, out GameObject? go)`）で受け、テストで差し替える。
   - payload: `{"nodeId":<正の整数>,"active":true|false}`。どちらも省略不可。解析は branch の現行どおり、`active` の初期値が false と true の DTO 2 つへ `JsonUtility.FromJsonOverwrite` し、食い違えば欠落とみなす。nodeId は 1 未満を弾く。
   - 順序: (1) 解析で例外、`active` が無い、nodeId が 1 未満 → `rejected: invalid-payload`。(2) DebugSocket が無い、または解決が false か GameObject が `== null` → `rejected: node-not-found`。(3) `SetActive(active)`、`NotifyHierarchyChanged()`、`DebugCommandResult.Ok("applied", <payload>)`。結果の payload は `{"nodeId":<N>,"name":"<GameObject.name>","activeSelf":<後の値>}` を `[Serializable]` DTO の `JsonUtility.ToJson` で作る（名前のエスケープを手で書かない）。拒否は (3) より前に `DebugCommandResult.Fail` で返す。
   - 名前・タグ・印・パス・許可リストで絞らない。結果に名前を出すのは、利用者が変えた GameObject を出力だけで確かめられるようにするため。
   - 非 bool の `active`（文字列・null）は拒否を保証しない。revision 3 の実測では `"false"` と `null` が false として適用された（§1.5）。README とコメントはこの実測どおりに書き、拒否するとは書かない。
3. **結果の読み方（README に書く。CLI の動作は変えない）:**
   - `success=true` と結果の payload（exit 0）: その NodeId の GameObject（`name`）に適用し、`activeSelf` は適用後の値。結果は main thread で SetActive の後に返るので、返った時点で適用済みで、続く `inspect` は適用後の値を読む。
   - 変えるのは指定した GameObject の activeSelf だけだが、`SetActive` が呼ぶ `OnEnable` / `OnDisable` などのゲーム側のコードが他の GameObject や対象自身を変えることはあり、それは保証しない。結果の `activeSelf` は `SetActive` の後に読んだ値。
   - `activeSelf` は GameObject 自身の値。親が非アクティブなら `activeSelf` が true でも見えず、`hierarchy` の flags に `ActiveInHierarchy` が付かない。
   - `success=false` で message が `rejected: ` で始まる（exit 1。この行は stderr に出る）: 何も変えていない。
   - それ以外の `success=false`（handler の例外、未登録）と、`CommandResult` の行が無い終わり方（期限切れ exit 2、それ以外 exit 1）: 結果不明。timeout は未実行を意味しない。CLI は自動で再送しない。
   - 結果不明の後の `inspect` は読んだ時点の値で、その要求が実行された証拠ではない。望む値にしたいときは `hierarchy` を読み直し、値を明示して送り直す（逆には戻らない）。
   - `active` は JSON の `true` / `false` で書く。それ以外の値は拒否されないことがあり、実測では `"false"` と `null` が false として適用された。適用された値は結果の `activeSelf` で読む。
   - Play をまたぐ NodeId: Play を始め直したら `hierarchy` を読み直してから送る。前の Play の NodeId は、次の Play で同じ番号を持つ別の GameObject に適用されることがある。変えた GameObject は結果の `name` で確かめる（§2.8 の裁定 A）。
4. **branch に残る revision 3 の実装の扱い:** 置き換えの残骸として B が消す（`DebugVerificationTargetHost` とそのテスト、`TryGetNodeIdUnsafe`・`TryGetHierarchyNodeId` とそのテスト、`AppInitializer` の host の配線）。どれも develop には無く、implementation base（develop）からの差分には現れない。

**比べた代替と採らない理由:**

- DebugSocket の組込みコマンドにする（router が `debug.set-active` を処理）: Framework の router と組込みコマンドを変え、全アプリに書き込みが出る。依頼は既存の登録経路を起点にすることを求めた。
- 書き込みも Framework に置く（`TrySetHierarchyNodeActive`）: 読み取り口 1 つで足り、書き込みの意味を Game に置く現在の役割分担を変える理由が無い。
- revision 3 の検証用 GameObject と逆引き: owner が目的を訂正した。
- 名前・タグ・印・許可リストで対象を絞る: owner の指示で採らない。

### 2.2 進める最低条件

#### 不変条件

- **I1: 拒否と報告した要求では handler は `SetActive` を呼ばない。適用と報告した要求で handler が `SetActive` を直接呼ぶのは、処理した時点の hierarchy でその NodeId が指す GameObject 1 つだけで、1 回だけ。** `SetActive` が呼ぶ `OnEnable` / `OnDisable` などゲーム側のコードによる他の GameObject への波及や対象の再変更は Unity の通常の副作用で、保証しない（README に書く。§5.1 X1）。
  - 主張を崩しうる既存の経路: (a) NodeId が未知・prune 済み・破棄済み・`Reset` 後。(b) DontDestroyOnLoad の GameObject は capture されず NodeId を持たない。(c) GUI のコマンド画面や `send` から任意の payload を送れる。(d) `JsonUtility` は欠落した field を初期値のまま残し、非 bool を変換することがある。(e) SetActive の後の例外を router が Success=false にする（I2 で扱う）。(f) capture・prune・`Reset` と解決の競合（どちらも main thread で、解決は `_gate` の中）。(g) 同じ Play の中のシーン遷移や WPF の再接続の後の古い NodeId（採番値が戻らないので別の GameObject を指さない）。(h) 前の Play の NodeId は次の Play の同じ番号の GameObject を指しうる。I1 の「処理した時点の hierarchy でその NodeId が指す GameObject」はこの場合も成り立ち、利用者の意図との食い違いは §2.8 の裁定 A で既知の制約とし、README（§2.1 の 3）で案内する。
  - 探索した境界: Runtime の DebugSocketServices（router、dispatcher 群、registry、hierarchy publisher、inspect の lookup）、`DebugCommandCatalog`、`AbstractApplicationInitializer` の dispatcher の作成と service の寿命、EditorSettings の Domain Reload。未確認: なし。
- **I2: 結果を確定できない終わり方を確定した結果として案内しない。** README は §2.1 の 3 のとおり書き、timeout を未実行と書かない。CLI は自動で再送しない（既存のまま）。
  - 主張を崩しうる既存の経路: router の catch は SetActive の後の例外も Success=false にする（`rejected: ` で始まらないので結果不明と読む）。catalog は未登録を Success=false で返す。CLI の失敗は送る前と後を区別しない。期限切れの後に届いた結果は CLI に返らない。
  - 探索した境界: CLI の `Program.WriteResult` と client、Runtime の router・dispatcher 群・catalog。
- **I3: 既存の契約を変えない。** DebugStudio の code・protocol・GUI、`send` / `compare` / `hierarchy` / `inspect` の出力と exit、S2 の不変条件、Player の挙動。Framework の変更は §2.1 の 1 だけ。SampleGame は GameObject を作らない。
  - 既知の観測できる変化: Editor の SampleGame で未登録の名前を送ったときの message が catalog の文言になる（Success=false と exit は同じ）。
  - DebugSocket を有効にした Player でも `CreateDebugCommandDispatcher()` は呼ばれるので、`Application.isEditor` で分ける。

#### 成立条件（現行 develop では未実装）

- **E1:** 人手や eval の準備をしない通常の Editor Play で、hierarchy に出ている既存の GameObject X（§1.6 で確定。開き替えない Untitled の scene の `Directional Light`）の NodeId で、Release の WPF を `--listen` で起動したまま `send --command debug.set-active` を `false`・`true` の順に送ると、各回で `success=true`、結果の `nodeId`・`name`・`activeSelf`、Editor で独立に読んだ activeSelf、`inspect` の `GameObject.ActiveSelf` の rawValue が一致する。false の間も hierarchy に同じ NodeId で出る。hierarchy の root の構成は §1.6 の R1 と同じ（SampleGame が何も足していない）。

### 2.3 受け入れ条件

- **AC1（I1、失敗経路）:** Unity EditMode（解決を差し替える）:
  - 解決が false・解決した GameObject が破棄済み・DebugSocket が無い → `rejected: node-not-found` で、fixture の全 GameObject の activeSelf が変わらない。
  - 解析できない payload、空、`{}`、nodeId が無い・0・負、`active` が無い（`{"nodeId":<解決できる N>}` を含む）→ `rejected: invalid-payload` で、解決を呼ばず activeSelf が変わらない。
  - 解決できる NodeId → `active` の値（true・false の両方）になり、同じ値を 2 回送っても同じ値のまま。対象は root、子、親が非アクティブな子の 3 種で、結果の `nodeId`・`name`・`activeSelf` が実際と一致し、他の fixture の activeSelf は変わらない（fixture は `OnEnable` / `OnDisable` で他を変える component を持たない）。名前に `"` を含む GameObject でも結果が JSON として読める。
  - `active` が文字列・null の代表入力: 結果（拒否なら activeSelf 不変、適用なら結果の activeSelf が実値と一致）と観測を `TestContext` に残す。README の記述と食い違えば C が見つけられる。
  - 実 Editor（§5.4 V5）: 今の hierarchy に無い NodeId で送ると `rejected: node-not-found`（exit 1）で、X の activeSelf が変わらない。
- **AC2（E1）:** 実 Editor（§5.4 V1〜V4、1 回の Play の中）で X を false にしてから true に戻し、各回で CLI の `success=true`、結果の値、Editor の独立値、`inspect` の rawValue が一致し、false の間も hierarchy に同じ NodeId で出る。root の構成が §1.6 の R1 と同じ。GUI のボタンを押さない。不合格例: eval で GameObject を作った、途中で WPF を再起動した、Play を開き替えた scene で行った。
- **AC3（I3）:** Unity EditMode: 登録に Editor でないことを渡すと catalog に `debug.set-active` が無い。Editor なら登録し、DebugSocket は実行時に読む。Dispose 後の DebugSocketService の `TryGetHierarchyGameObject` は false を返す。`git diff --stat <base> <head>` で `tools/DebugStudio/` の変更が `DebugStudio.Cli/README.md` だけ、`protocol/` に変更が無い。SampleGame の差分に GameObject の生成（`new GameObject`・`Instantiate`）が無い。
- **AC4（I2、文書）:** CLI README に §2.1 の 3 の全項目、payload の形と送信例、結果の payload の形があり、§2.1 の 2 と §5.4 のコマンドと一致する。非 bool の `active` を拒否するとは書かない。`SetActiveDebugCommand` のコメントも同じ。

### 2.4 ここでは答えない問いと所有する後続スライス

- 汎用編集、別の操作コマンド、GUI からの書き込み、一括変更: DebugStudio S4 以降（未起票）
- Play をまたぐ指定の識別（§2.8 の裁定 A）、結果不明の確定と専用 CLI: DebugStudio S4 以降（未起票）
- hierarchy に DontDestroyOnLoad を出すこと、Player と遠隔での書き込み: DebugStudio S4 以降（未起票）
- inspect の自己応答保証、S2 から移送された client / server の timeout 起点と client 離脱後の占有: DebugStudio S4 以降（未起票）
- S2 HANDOFF の Phase D: S2 の担当

### 2.5 スライス分割の判断

- 契機: なし。変更理由は「既存 GameObject の activeSelf を 1 件書ける」1 つで、未知は書き込み経路だけ。読取り経路の再確認（§1.6）は既存経路の確認で、別の問いではない。
- 判断: 分割しない。

### 2.6 risk と判定

- risk: normal。理由: Framework の変更は、inspect が既に使う lookup を包む public method 1 つと protected accessor 1 つの追加で、既存の動作・wire・protocol を変えない。並行性の新しい主張は無い（main thread の既存の dispatcher、解決は既存の `_gate`）。新しい所有者・寿命は無い（GameObject を作らない）。Editor の Play に限る。
- 判定定義: GO は §2 の最低条件を同じ GO 候補 head で満たすこと。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- §1.7 の常時契約と既知の制約。
- Framework の変更は §2.1 の 1 だけ。router、組込みコマンド、`MainThreadDebugCommandDispatcher`、`DebugCommandCatalog`、hierarchy の走査範囲、registry の採番を変えない。
- `tools/DebugStudio/` は CLI README だけを変える。protocol と codegen を変えない。
- SampleGame の Scene・Prefab・設定 asset を変えない。GameObject を作らない。
- 拒否は SetActive より前に決める。対象を名前・タグ・印・パス・許可リストで絞らない。

### 2.8 A2 の前の人間の裁定（1 件。裁定済み）

**前の Play の NodeId で送ったとき、次の Play でたまたま同じ番号を持つ別の GameObject を変えてよいか。**

- 起きる場面: Play1 の hierarchy で `Directional Light` が NodeId 2 だったとする。Play を止め、scene を開き替えるなどして構成が変わった Play2 で、同じ番号 2 が別の GameObject に振られる。Play2 の hierarchy を読み直さずに Play1 の `{"nodeId":2,"active":false}` を送ると、その別の GameObject が非アクティブになる。同じ構成で Play し直した場合は、基準観測のとおり同じ GameObject が同じ番号になることが多い。同じ Play の中のシーン遷移や WPF の再接続では採番値が戻らないので、古い番号が別の GameObject を指すことは無い（§1.4）。
- A: 許容する。追加のコードは無い。README に「Play を始め直したら `hierarchy` を読み直してから送る」と書き、結果の `name` で変えた GameObject を確かめられるようにする（§2.1 の 2）。I1 の経路 (h) は既知の制約として残し、識別は後続へ送る。
- B: 拒否する。registry の採番値を service ごとではなく Editor の domain の中で続ける（static な開始値。Domain Reload が無効なので Play をまたいで残る）。前の Play の番号は次の Play で解決できず `rejected: node-not-found` になる。費用: registry の変更と EditMode テスト、hierarchy・inspect に出る NodeId が Play を重ねるごとに大きくなる（既存の出力の値が変わる）、script の再 compile（domain reload）の後は 1 に戻るので完全ではない、registry のテストが 1 始まりを前提にしている箇所の見直し。不変条件 I4「前の Play の NodeId で、次の Play の別の GameObject を変えない」と、Play2 で Play1 の番号が拒否される実機手順を足す。
- どちらも利用者の入力は増えず、protocol・wire・session の機構を足さない。
- 推奨の理由: owner の「デバッグで使いやすさ優先」と、revision 3 で示した「厳密性があってもほぼ意味はない」の方針に合い、B は既存の NodeId の見え方を変えるうえ保証が domain reload で切れる。旧裁定は検証用 1 個の前提だったので、任意の GameObject への適用を owner に求めた。
- **裁定: A。** owner が 2026-11-23 に「Aで確定」と回答した。B の採番の変更は行わず、I4 と Play2 の実機手順は足さない。

## 3. 責務マップ

行数は develop `d926e3b6` の現在値。増分は見込み。

| file | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 現在 / 増分 |
|---|---|---|---|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/DebugSocketService.Inspector.cs` | `TryGetHierarchyGameObject`（既存の private lookup を包む） | service（Play） | public method | lookup 本体は既存の `DebugSocketRuntimeNodeRegistryTests` の `TryFindGameObjectByNodeId_*`。包みは lock と委譲だけ | 105 / +10〜15 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | protected `DebugSocket` accessor | initializer（App） | protected property | — | 1326 / +2〜4 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs`（新規。branch の同名 file を書き換える） | 登録（Editor のときだけ）、解析、解決、SetActive、結果。file の中で解析・拒否判定（GameObject に触れない static method）、適用（解決 delegate と SetActive）、結果 DTO の生成を別の method に分ける（§5.1 G1） | catalog は Play ごと | internal static | `SampleGame.Tests` の `SetActiveDebugCommandTests`（解決を差し替え。解析と拒否判定は GameObject なしで試す） | 新規 80〜120 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | `CreateDebugCommandDispatcher()` の override | initializer | — | 登録は上のテスト | 431 / +15〜25 |
| `tools/DebugStudio/src/DebugStudio.Cli/README.md`、`unity/Assets/Docs/Architecture/12-telemetry.md` | 使い方と結果の読み方。§10.5 の「SampleGame に catalog が配線されているという意味ではない」を現状に直す | 公開文書 | — | docs-audit、AC4 | 91 / +20〜30、515 / ±10 |

- 配置の理由: コマンドはゲームの内容ではなく SampleGame の debug 配線なので composition root の `DependOnAll` に置く。NodeId → GameObject の対応は Framework の registry が持つので、読み取り口は inspect と同じ partial に置く。
- 規模の警報: `AbstractApplicationInitializer` は 500 行を超えるが、既存の accessor と同じ 1 行で責務を増やさないので分割しない。
- 新しい中核ロジックの単体テスト: handler（解析・拒否の順・適用・結果）は `SetActiveDebugCommandTests`。

## 4. 実装計画

- 出発点: B は PR の branch に A3 で固定した develop の SHA を取り込み、その上で §2.1 の 4 の残骸を消し、§3 を実装する。差分の判定は implementation base（その develop）から head まで。
- 順序: (1) Framework の 2 つ。(2) `SetActiveDebugCommand` と `AppInitializer`、EditMode テスト。Editor で compile を確かめる。(3) CLI README と §10.5。
- Phase B から Phase A へ差し戻す条件: asmdef 参照・package が要る。§2.1 の 1 以外の Framework の公開 API が要る。DebugStudio の code・protocol を変える必要がある。SampleGame の Scene・設定を変える、または GameObject を作る必要がある。
- 対象外を維持する方法: 汎用の書き込みや別のコマンドの土台を作らない。対象の絞り込み（名前・印・一覧）を作らない。

## 5. テストとレビュー計画

### 5.1 A2 の独立レビューと A3 の採否

**A2 記録（2026-11-23）**

- 目的レビュー: 入力は §1.1 の目的・利用者の原文（sha256 `b03557b8187a95895a52959d463bd0b9f54e742b78ee8d14bd91994ae82220fa`）だけ。担当はこの Phase A に関与していない新しい subagent（Claude Opus 5.5）。ツールを使わず回答した。
- アーキテクチャゲート: 固定 base `9de34cc2ec2dfe82b782f122b0012b902e98f277` の clean な detached checkout（`osm-ds3-a2`）と、A2 入力版（commit `dfd52b30`）の HANDOFF 固定 copy（SHA-256 `ea8ca195deaab2496d126ad9f464d6ed5f6dac6df8d2c99e3ae43ffdd715445e`）。担当は Codex CLI（`gpt-5.6-luna`、effort medium。risk normal なので astra を使わない）。担当が HEAD・clean・hash の一致を報告した。2 件の所見は互いに見せていない。
- 原文は Git 外のローカル保全（主担当の scratchpad の `a2/out/`）。
- C' 用の予約: Grok 系（cursor-agent）を A2 で使わず残した。

**A3 の採否（主担当の案。owner の確認と外部計画レビューの前）**

| # | 出所・指摘 | 採否 | 反映・理由 |
|---|---|---|---|
| G1 | ゲート major: `SetActiveDebugCommand` に登録・解析・解決・Unity の変更・結果生成が集まり、Policy と Unity I/O が混ざる。解析と拒否判定を Unity なしで試せる境界が不明確 | 一部採用 | 採用: 登録は `AppInitializer`（既に計画どおり）、file の中で解析・拒否判定（GameObject に触れない static method）、適用（解決 delegate と SetActive）、結果 DTO の生成を別の method に分け、解析と拒否判定は GameObject なしでテストすると §3 に書いた。不採用: 別 class・別 file への分割。コマンド 1 つ・80〜120 行の段階で層を作らない方針（実装が 1 つの段階で port・層を先取りしない）による。`JsonUtility` は Unity API だが EditMode で GameObject なしに試せる |
| G2 | ゲート minor: 公開する `TryGetHierarchyGameObject` の main thread 契約と Dispose 後の挙動が契約化されていない | 採用 | §2.1 の 1 に、XML コメントで main thread で呼ぶこと、Dispose 後は false を返すことを書き、Dispose 後 false の EditMode テストを 1 件足した（AC3） |
| G- | ゲートの「問題なし」の 1 行（「dispatcher 生成前にサービスを作成する」）は事実と逆 | 記録のみ | 実際は dispatcher を DebugSocketService の constructor 引数として先に作る（`AbstractApplicationInitializer.cs:1276`）。§1.4 の記述どおりで、結論（handler が実行時に DebugSocket を読む）は変わらない |
| P1 | 目的: send の後の inspect が反映済みを返すこと、反映の時点を明示すること | 採用（文書） | 結果は main thread で SetActive の後に返るので、`success=true` が返った時点で適用済み。§2.1 の 3 に README の 1 項目として足した |
| P2 | 目的: activeSelf と activeInHierarchy を区別して返す | 採用（文書） | 既存の inspect は `GameObject.ActiveSelf` を、hierarchy は flags で両方を出す。親が非アクティブなら activeSelf=true でも `ActiveInHierarchy` が付かないことを §2.1 の 3 に足した。AC1 の「親が非アクティブな子」で挙動は試す |
| P3 | 目的: 1 つだけ変わる、非アクティブ後も同じ NodeId で出る、冪等な設定、通常の Play で動く、asset を Dirty にしない | 既に満たす | I1・AC1（他の fixture 不変、2 回送る）、E1・AC2（false の間も同じ NodeId、何も生成しない）、§5.4 V6（Scene clean・tracked 変更なし） |
| P4 | 目的: 存在しない NodeId・破棄済み・Play 中でない・未接続を出力で機械的に区別する | 既に満たす | 前 2 つは `rejected: node-not-found`（AC1）。後 2 つは既存の CLI の接続失敗で、`CommandResult` の行が無い終わり方として I2 の結果不明に入る。新しい区別（outcome・exit）は §1.3 の対象外 |
| P5 | 目的: 作らないもの（新 CLI コマンド・WPF 画面、名前等での選択、一括、汎用書き込み、トグル・Undo・履歴、生成・削除、Player・遠隔、永続化、確認・dry-run、自動追従・push、待ち合わせ API） | 既に満たす | 計画はどれも作らない（§1.3、§2.7）。計画が作るもので目的レビューが要らないとしたものは無い |

**外部計画レビュー（PR #204 head `1e4581ac`、[コメント](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/204#issuecomment-7090475918)）の採否:** 目的の修正と設計方針を支持し、次の 2 点を直せば凍結を支持するとした。

| # | 指摘 | 採否 | 反映・理由 |
|---|---|---|---|
| X1 | I1 の「変わるのはその GameObject 1 つだけ」は任意の既存 GameObject では保証できない。`SetActive` が呼ぶ `OnEnable` / `OnDisable` のゲーム側コードが他を変えうる | 採用 | I1 を「handler が直接 `SetActive` を呼ぶのは解決した 1 つだけ、1 回だけ。拒否では呼ばない」に改め、ゲーム側の波及・再変更を保証外として README の項目に足した（§2.1 の 3）。AC1 の「他の fixture は変わらない」は副作用を持たない fixture での確認と明記した。受け入れ条件を減らす変更ではなく、主張を Unity の意味に合わせた |
| X2 | Dispose 後 false の約束に対し、private lookup は `_disposed` を見ず、`Dispose()` は `_disposed` を立てた後に非同期で片付けるので古い対応表が残りうる | 採用 | 主担当が `DebugSocketService.cs:340-352` と `DebugSocketService.Inspector.cs:97-103` で事実を確かめた。§2.1 の 1 に、public wrapper が先に `_disposed` を見て false を返すことを書いた |

- 再レビューの要否: X1 は I1 の主張の範囲を狭める書き換えで、設計・所有者・公開面を変えない。X2 は G2 で採用した契約の実装方法の明記。どちらも A2 観点の再レビューは要らない。

- 目的・利用者の文言を問う指摘: なし（目的レビューのやり直しは不要）。
- 再レビューの要否: G1・G2 は §2.1 と §3 の書き方の変更で、所有者・依存・公開面を増やさない（G2 は既存の公開面の契約を明記するだけ）。P1・P2 は README の文言の追加。どれも A2 観点の再レビューは要らない。

### 5.2 テスト

- 単体テスト: §3 のテスト境界。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可）: `SetActiveDebugCommandTests`、`DebugSocketRuntimeNodeRegistryTests`。
- 判定必須テスト（GO 候補 head）: 最終の全 EditMode 回帰（標準の `pwsh tools/run-tests.ps1`）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC3 の diff 確認、§5.4 の実機手順。
- 全 EditMode 回帰の扱い: `tools/Harness/README.md` の規則に従う。実 exit と保持した process handle の signal を記録し、native stall は同 README の停止規則に従う。失敗・Inconclusive・非 0 の exit を合格と読み替える独自の規則を置かない。
- .NET のテスト: 判定必須にしない。DebugStudio の code・protocol を変えない（AC3 の diff で確かめる）。Release build は §5.4 V0 で行う。
- 全 EditMode 回帰の適用除外: なし。PlayMode は要しない。
- 人間の判断が必要な条件: なし。高コスト証拠: なし。

### 5.3 検証経路と未知の操作経路

- 読取り経路（hierarchy・inspect・Editor の独立値）: `d176033a` で成立を観測済み（§1.5）。develop での再確認は §1.6（A0 確認）で、A2 の前に行う。
- 書き込み経路（V3 以降）: 未確認。初回確認は動作確認（B の後）と Phase C。理由: 未実装。担当: 動作確認の実行担当と判定 C の実行担当。確認地点: AC1 の実機部分と AC2。不成立時は、待受け・接続、登録、解決、適用のどこかを分けて B へ戻し、受け入れ条件を黙って減らさない。

### 5.4 実機検証の手順（動作確認と Phase C。V0〜V6）

GO 候補 head の専用 worktree で、実行担当が自分で開いた Editor で行う。V1〜V5 は 1 回の Play の中。§1.6 と同じ初期状態（開いている scene を開き替えない、dirty でない）から始める。`tools/unity-editor.cmd` は 1 回ずつ別の command にし、eval の C# は UTF-8 の Base64 で渡し、独立値の読取りだけに使う。CLI は Release 出力の `debugstudio-cli.exe` を pwsh 7 から使い、payload は単引用符で渡す。

- V0: HEAD・clean・Editor の版を記録する。§1.6 R0 と同じ Release build と WPF の `--listen` 起動、PID を記録する。
- V1: Play を始め、`hierarchy` を `hasSnapshot: true` まで読み直す（総計 60 秒以内。§1.6 の A0 では約 21 秒、基準では約 9 秒）。root の構成を §1.6 R1 と比べ、X の NodeId（N）を記録する。
- V2: eval で X を名前で数え（1 を期待）、EntityId（EX）と activeSelf を記録する。以後 X は EX で特定する。`inspect <N>` の rawValue を記録する。
- V3: `send --command debug.set-active --payload '{"nodeId":<N>,"active":false}'` → `success=true message=applied`、`{"nodeId":<N>,"name":"<X の名前>","activeSelf":false}`、exit 0 を期待する。EX の activeSelf（eval）、`inspect <N>` の rawValue、読み直した `hierarchy` の N の有無を記録する。
- V4: `"active":true` で V3 と同じことを記録する。
- V5: V4 の hierarchy の最大の NodeId に 1000 を足した値 M で `send --command debug.set-active --payload '{"nodeId":<M>,"active":false}'` → stderr に `success=false message=rejected: node-not-found`、exit 1 を期待する。EX の activeSelf が V4 と同じか記録する。
- V6: Play を止める。WPF に通常の終了を要求し（`CloseMainWindow`）、保有 handle の signal と実終了コードを記録する。強制終了したらそのとおり記録し、正常終了とは扱わない。Scene が clean か、tracked の変更が無いかを記録する。
- 保存するもの: 各段のコマンド、stdout / stderr / exit、HEAD、Editor の版、WPF の PID と終了コード、hierarchy、eval の戻り値。結果不明の実機再現は求めない（AC4 の README で判定する）。

### 5.5 照合方式・担当・A2

- 判定 C の照合: script を使わず、判定 C が原記録を直接照合する。値は十数個で、どれも等値の比較で閉じる。期待は実装ではなく eval の独立値と §2.1 から導き、条件ごとに原記録の該当箇所と期待を対にした表を判定記録に残す。
- 機械検査: contract-audit、docs-audit、AC3 の diff 確認。
- A0/A1 主担当（実績）: Claude Opus 5.5 High（Anthropic）。
- C' 用の予約: A2 の担当を決めるときに、A2 で使わない系列を 1 つ残す。
- A2 の計画（revision 4）:
  - 目的レビュー: §1.1 の 2 行の原文だけを、この Phase A に関与していない担当へ渡す。owner の指示で A0/A1 を始め直したので、revision 4 の初回の目的レビューとして行う。
  - アーキテクチャゲート: 固定 base（develop の SHA の clean な detached checkout）と、§1.6 と §2.8 を反映した HANDOFF の固定 copy と SHA-256。
  - 高リスク代替レビュー: risk normal なので行わない。
- 順序: (1) §1.6 の A0 確認を反映する（§2.8 は裁定済み）。(2) A2 入力版を commit し、A2 の 2 件を互いに見せずに依頼する。(3) A3 で採否を書き（§5.1。A2 は 2026-11-23 に実施済み）、Draft PR #204 に 1 commit で出して外部計画レビューを受ける。(4) owner の凍結判断・対象 revision・次担当 B を §0 に記録して凍結版を commit し、implementation base を固定する。
- revision 1〜3 の A2・外部計画レビュー・採否と、revision 3 の B・動作確認・C・C′・D の記録は commit `d1d11e0c` の本 HANDOFF にある。revision 4 には持ち込まない。

## 6. Phase B 実装結果

revision 4（2026-11-23）。implementation head は §0 の欄のとおり。

- 取り込み: PR の branch に implementation base `9de34cc2` を merge した（衝突なし）。
- revision 3 の残骸の削除（§2.1 の 4）: `DebugVerificationTargetHost` とそのテスト、registry の `TryGetNodeIdUnsafe` とそのテスト、`DebugSocketService.TryGetHierarchyNodeId`、`AppInitializer` の host の生成・解放と `OnServicesInitializing` の override。`DebugSocketService.Hierarchy.cs`・`DebugSocketRuntimeNodeRegistry.cs`・registry テストの 3 file は base と同じ内容に戻した。
- 実装（§2.1・§3）:
  - `DebugSocketService.Inspector.cs`: public `TryGetHierarchyGameObject`。先に `_disposed` を見て false、立っていなければ既存の private `TryFindGameObjectByNodeId` へ委譲。XML コメントに main thread で呼ぶこと、Dispose 後は false を書いた。
  - `AbstractApplicationInitializer.cs`: protected `DebugSocket` accessor（branch にあったものをそのまま残した）。
  - `SetActiveDebugCommand.cs`: 登録（Editor のときだけ、service は実行時に読む）、`Execute`（解析 → 解決 → 適用の順）、`TryParse`（GameObject に触れない）、`Apply`、`CreateResultPayload`（`[Serializable]` DTO の `JsonUtility.ToJson`）を method で分けた。拒否 message は `rejected: invalid-payload` と `rejected: node-not-found` の 2 つ。187 行で、§3 の見込み 80〜120 を超えた分は主に契約と判断理由のコメント（非 bool の扱い、波及の保証外、Play をまたぐ NodeId）。
  - `AppInitializer.cs`: `CreateDebugCommandDispatcher()` の override だけ（+19 行）。
  - テスト: `SetActiveDebugCommandTests`（AC1・AC3 の登録）を書き直し、`OneStarMaker.Tests` に `DebugSocketServiceHierarchyLookupTests`（AC3 の Dispose 後 false）を足した。後者は transport を起動しない service の registry に reflection で対応を 1 つ入れ、Dispose 後も対応が残ったまま false になることを確かめる（capture にはセッションが要るため）。
  - 文書: CLI README の S3 の節を §2.1 の 3 の全項目で書き直し、`12-telemetry.md` §10.5 を現状に直した。
- AC3 の diff の見込み（判定は C）: `tools/DebugStudio/` の変更は `DebugStudio.Cli/README.md` だけ、`protocol/` の変更なし。SampleGame の差分の `new GameObject` は `SampleGame.Tests` の fixture（AC1 が求める）だけで、SampleGame の Runtime のコードに生成は無い。
- 新しい事実: なし（Phase A へ返す条件に当たらない。asmdef・package・§2.1 の 1 以外の公開 API・Scene・設定 asset の変更なし）。
- 実行した確認:
  - Editor 6000.6.9f1 での compile: この worktree の `unity/` を自分で開いた。初回の起動は compile エラー 2 件（`AppInitializer` の using 不足、`NullDebugCommandDispatcher` の private constructor）で Safe Mode の確認 dialog に止まったので、dialog の Quit で終了し、C# を直して開き直した。2 回目は `EditorUtility.scriptCompilationFailed=False`、新しい型と `TryGetHierarchyGameObject` が読め、`DebugVerificationTargetHost` が無いことを eval で確かめた。Editor を終了した後、tracked の変更は無い。
  - `pwsh tools/contract-audit.ps1`: errors 0、warnings 0（exit 0）。
  - `pwsh tools/docs-audit.ps1`: errors 0（exit 0）。警告 2 件は検査 3 の既知の誤検出（この HANDOFF の §7・§8 は「未着手」の行で埋まって見える）と別の HANDOFF。
- 未実行: Unity テスト（EditMode 全体と `-Filter`）、Release build、実機手順（§5.4）。Phase B の権限の外（revision 4 の B の票と `osm-unity-editor` の制約）で、動作確認と Phase C が行う。

## 7. Phase C

revision 4 では未着手。

## 8. Phase C'

revision 4 では未着手。

## 9. Phase D

revision 4 では未着手。
