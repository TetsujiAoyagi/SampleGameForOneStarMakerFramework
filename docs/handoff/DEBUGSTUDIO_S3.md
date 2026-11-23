# DebugStudio S3 — CLI から検証用 GameObject の activeSelf を切り替える

## 0. メタデータ

- type: slice
- status: Phase D。C・C′ は凍結した機能条件に GO。マージは owner の判断待ち
- branch: codex/debugstudio-s3
- implementation base commit: d176033a06ebcd1f481aa9082ed5d1053a2a03b2
- implementation head commit: cf44020199a2cc687ad7cf7a29b780661283bd25
- A3 凍結記録: owner が 2026-11-23 に「コメント来たので確認して そのあとで凍結してPhaseB入ろう」と承認。対象は revision 3（候補 commit a1178db53fa7290cb391d1760f893f745b44fef4）。
- risk: normal（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-20 / updated: 2026-11-23（動作確認完了）
- expires: 2026-12-31 または置換 revision
- harvest to: `tools/DebugStudio/src/DebugStudio.Cli/README.md`（`debug.set-active` の送り方、検証用 GameObject の名前、結果の読み方と結果不明のときの扱い）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（SampleGame の catalog 配線、`TryGetHierarchyNodeId` の契約、hierarchy が DontDestroyOnLoad を出さないこと）
- Phase A snapshot の取得案内・状態: 凍結版をこの commit で確定。オーケストレーターが task の手元引渡し先に保存・取得案内を用意し、B・C・C′ へ渡す。
- Phase B result と動作確認の Evidence はローカル固定・発見 C で取得確認済み。判定・blind 共通入力の取得案内はオーケストレーターが手元で管理する。
- 基準動作の Evidence: 116 ファイルをローカル固定（未昇格）。別の Claude CLI セッションが取得し、両 Play の hierarchy・inspect・Editor 独立値、WPF 終了と観測票を閲覧した。取得案内は task の担当者と owner 向け手元引渡し先に保管済み。
- 動作確認の Evidence: implementation head で取った非秘密の生記録（各段の command・開始終了 UTC・stdout / stderr / exit、Editor の eval の独立値、WPF の保有 handle の signal と実 exit、実行 script、要約 `operation.json`、stage 対象の一覧）をローカルに保全（未昇格）。Editor の private log は含めない。stage と取得案内の用意は root。
- task: `debugstudio-s3`（開始済み）
- A2 の入力版: 計画開始点 `a629f9c19bdfe55b24fcac2f1756e25824d9e453` の A1 初稿（§5.5）。revision 1・2 は develop `d176033a` で照合した統合候補。revision 3 は owner の縮小方針で目的・設計・受け入れ条件を改めた候補。
- 引継ぎ票: 次は Phase D のオーケストレーター。owner にマージ可否を確認し、承認後に harvest、最終 CI、develop へのマージ、Evidence の引渡し、正式な task 終了と専用作業領域の後始末を行う。
  - 作業: 判定資料・最終所見・削除前の本票を保存し、引渡し案内からの取得を確認する。製品機能に変更なし。
  - 必要な権限: Unity Editor の起動・終了と `tools/unity-editor.cmd`、`pwsh tools/run-tests.ps1`（Editor を閉じて実行）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`dotnet build -c Release`（DebugStudio App / CLI）、WPF の起動と通常終了。
  - 停止規則: 人間が開いた Editor や WPF を強制終了しない。重複して起動しない。専用 worktree の Editor は動作確認の終了時点で root が開いたまま（Play 停止、Scene clean）で、全 EditMode の前に閉じる。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。検証用 GameObject を eval で作らない。
  - 入力の取得案内: 凍結仕様、B 結果、全差分、標準・描画ありの全 EditMode、実機の生記録を同梱した共通 bundle。判定 C の別セッションが取得・閲覧済み。C′ の取得案内は所見と別にオーケストレーターが管理する。
  - 引き渡さないもの: 可変 HANDOFF、PR 会話、発見 C・判定 C の所見、結論、疑念。
- 再開位置: この票の commit を PR に配布し、同一 PR head の CI と最終所見の保存・別セッションでの取得を照合する。両方が揃ってから owner に D の判断を求める。

## 1. 目的と対象外

### 1.1 目的と利用者（revision 3。目的レビューのやり直しへ渡す原文）

- 目的: Editor の通常の Play 中に、DebugStudio CLI だけで、SampleGame が用意した検証用 GameObject を hierarchy で見つけ、その activeSelf を指定した値に切り替え、inspect で読み返せるようにする。検証用でない GameObject は変更しない。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント。

### 1.2 目的の変更記録

- 変更前の原文（revision 1・2。初回の目的レビューが見た文言）:
  - 目的: Editor の Play 中に、DebugStudio CLI の hierarchy で SampleGame の検証用 GameObject を 1 つ特定し、名前付きコマンドでその activeSelf を切り替え、inspect で結果を読み返せるようにする。Play をまたいで古い指定を使っても別の対象を変更しないこと、切断やタイムアウトで実行されたか分からないときに利用者がそれを成功や失敗と取り違えないことを含む。
  - 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント。
- 変えたもの: 目的だけ。利用者は変えていない。
- なぜ: 旧文言の 2 つの保証（Play をまたぐ古い指定の拒否、結果不明の確定的な区別）が、snapshot と control への SessionId の追加、target の生成連鎖、専用 CLI と新しい outcome・exit の体系を必要にしていた。DebugStudio は開発用のデバッグツールで、このスライスで示したいのは既存の hierarchy / send / inspect が「読むだけ」から「1 件書き込める」へつながることである。外部計画レビュー（PR #204 の 2 件目）がこの縮小を推奨し、旧文言のまま受け入れ条件だけを削るのは不整合と指摘した。
  - 古い指定: 新しい目的では、検証用でない GameObject を変更しないことだけを保証する。前の Play の NodeId が次の Play の検証用 GameObject に偶然一致した場合、その 1 個への操作が通ることを許容し、Play をまたぐ識別は後続へ送る。
  - 結果不明: 目的から外し、README で読み方を説明する（§2.1 の 4）。timeout を未実行と断定せず、自動で再送しない。
- owner の承認（承認者: owner（root）、2026-11-23）:
  - 方針の発言: 「DebugStudioはただのDebug用途のアプリなんで厳密性があってもほぼ意味はない」
  - 確認「前の Play の NodeId が、次の Play の検証用 GameObject にも割り当てられた場合、その1個に限って操作が通ることを許容しますか？どちらでも通常のゲームオブジェクトは拒否」への明示裁定: 「許容、後続へ送る」
  - これは新しい目的の承認であり、凍結の承認ではない。利用者の入力を増やす変更ではない（§1.7）。

### 1.3 対象外

- activeSelf 以外の field・component・メソッドへの書き込み（汎用編集）。activeSelf 以外の操作コマンド。
- 検証用 GameObject 以外への書き込み。複数の検証対象。
- Play をまたぐ指定の識別（旧 NodeId と新しい検証用 GameObject の一致の拒否）。
- 結果不明を確定させる仕組み（要求の相関、送信前後の区別、新しい outcome・exit）。自動再送。操作履歴。
- 専用の CLI サブコマンド、protocol・wire の変更、WPF の変更（GUI を含む）。
- Player（Build した実行ファイル）と遠隔接続。
- SampleGame の Scene・Prefab・設定 asset の変更。シーン遷移を跨ぐ実機テスト。
- S2 HANDOFF（`docs/handoff/DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

### 1.4 現況（develop `d176033a` のコードの事実）

- CLI の `send --command <name> --payload <json>` は WPF の control 経由で `DebugCommandEnvelopeV1` を Runtime へ送り、同じ requestId の `CommandResult` を待つ。`CommandResult` を受け取れば `CommandResult request=… success=… message=…` の行と、payload があればその JSON を出し、success に応じて exit 0 / 1。受け取れなければ detail を stderr に出し、期限切れは exit 2、それ以外の失敗（control へ届かない、送った後の切断を区別しない）は exit 1。
- Runtime: router は組込みの `ping` / `runtime-diagnostics` 以外を App の dispatcher へ渡す。dispatcher は `MainThreadDebugCommandDispatcher` で包まれ、main thread で中の dispatcher を呼ぶ。handler の例外は router が catch して payload の無い Success=false にする。
- 登録経路: `AbstractApplicationInitializer.CreateDebugCommandDispatcher()`（protected virtual、既定は `NullDebugCommandDispatcher`）。`DebugCommandCatalog`（`Register(name, handler)`、Ordinal 完全一致、同期、未登録は Success=false）と `CatalogDebugCommandDispatcher`。`DebugCommandResult.Ok(message, payloadJson)` / `Fail(message)`。SampleGame は配線していない。dispatcher は DebugSocketService より先に作られる。
- NodeId: internal な `DebugSocketRuntimeNodeRegistry` が service ごとに 1 から採番し、EntityId→NodeId と NodeId→GameObject の対応を持つ（`DebugSocketService._gate` で排他、main thread）。capture で見えなくなった NodeId は prune、WPF の接続が替わると `Reset`（採番値は戻さない）。NodeId から GameObject を引く処理は private で、GameObject から NodeId を引く公開手段は無い。
- hierarchy の capture は `SceneManager.sceneCount` / `GetSceneAt` の scene だけを走査し、DontDestroyOnLoad を出さない。非アクティブな root と子も出す。capture は hierarchy を購読するセッションがある時だけ走る。`DebugSocketService.NotifyHierarchyChanged()` は public（main thread の文脈が無ければ warning で skip）。
- UICommon は `BootstrapUiCommonLoader` が AfterSceneLoad で読む App 常駐の scene で、SceneDirector の管理外、Play の終わりまで残る。`_services.StartAsync`（DebugSocket の起動）の後に `OnServicesInitializing`（protected virtual）、後段の失敗で `OnAfterSceneLoadInitializationFailed` が呼ばれる。SampleGame は後者だけを override。`AbstractApplicationInitializer` に DebugSocketService の accessor は無い（`LoggerFactory => _services?.LoggerFactory` 等の同形の accessor はある）。
- Inspector は `GameObject.ActiveSelf` を property として返し、CLI の inspect 出力の `rawValue` は `"true"` / `"false"`。
- `SampleGame.DependOnAll` は `SampleGame.Tests` へ、`OneStarMaker.Runtime` は `OneStarMaker.Tests` と `SampleGame.Tests` へ internals を公開している。Unity 側で JSON の解析に使えるのは `JsonUtility`。

### 1.5 現行 base の基準動作（2026-11-22 観測済み。再実行しない）

別担当が `d176033a` の専用 worktree で観測した（Editor 6000.6.9f1、Release の WPF と CLI）。新機能は未実装の状態の観測である。

- Release build は exit 0。WPF を `--listen` で直接起動し、Play 前の `hierarchy` は `no-snapshot`、Play 後に `hasSnapshot: true`。
- 通常の Editor Play の hierarchy は、開いていた scene の root（`Main Camera`、`Directional Light`）と UICommon の root（`[UICommon]` と子 4 つ）だった。DontDestroyOnLoad に残る CameraSystemHost などは出なかった。検証用の GameObject も書き込みのコマンドも無い。
- 検証支援の eval が作った fixture（root と子 `Target`）は Play1 と Play2 の両方で NodeId 8・9 になり、Play2 で Play1 の NodeId 9 を `inspect` すると Play2 の `Target` を返した（NodeId の Play 間の再利用）。
- 各 Play で Editor の eval が読んだ `Target` の activeSelf は、その Play の inspect と一致した。
- `runtime-diagnostics` の sessionId は Play ごとに異なった。
- `send --command debugsocket.set-active --payload '{}'` は `success=false`、`... is not registered.`、exit 1。
- WPF に通常の終了を要求し、process handle の signal と exit 0。Editor は Play 停止・Scene clean・tracked 変更なしで残った。

### 1.6 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef 参照と package を追加しない。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側 `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `ZLogger*` 型を公開面へ出さない。Editor コードを Runtime asmdef に置かない。
- path は `tools/path-budget.json` の予算（repo 相対 150 文字）に従う。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 の不変条件（`--listen` なしの起動と既存 send / compare の契約、待受け loop は 1 本、inspect は GUI 選択を変えない、CLI / control が期限なく固着しない）を壊さない。
- 人間が開いた Editor を強制終了しない。重複した Editor や WPF を起動しない。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC0 検証対象を用意する | 無し | SampleGame が Editor Play の起動時に `[DebugStudioVerificationTarget]` を 1 つ作る | 自動 | 「SampleGame が用意した」「CLI だけで」 |
| UC1 見つける（`hierarchy`） | 無し | NodeId・名前・flags（既存） | 自動導出 | 「hierarchy で見つけ」。名前は README に固定 |
| UC2 切り替える（`send`） | コマンド名 `debug.set-active`（README の固定値）、payload の `nodeId`（UC1 の行から写す）と明示した `active`（`true` / `false`。省略不可） | requestId（既存） | 実行時の操作 | NodeId: 見つけた 1 つを指す。値: 「指定した値に」。反転にしないので、結果不明の後に送り直しても逆に戻らない。コマンド名は既存の send の必須引数 |
| UC3 読み返す（`inspect`） | NodeId（既存） | 既存どおり | 実行時の操作 | 「inspect で読み返す」 |
| UC4 結果が分からないとき | 新しい入力は無い（UC3 で現在値を読み、必要なら UC2 を人が送り直す） | CLI は自動で再送しない（既存） | 実行時の操作 | README で説明（§2.1 の 4） |

A3 自己点検: 新しい入力は send の payload の 2 値だけで、準備の操作は無い。目的に機構の語（NodeId の照合、session）は無い。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 通常の SampleGame の Editor Play 1 回の中で、既存の hierarchy・send・inspect だけを使って、SampleGame の検証用 GameObject の activeSelf を false にしてから true に戻して読み返せ、検証用でない GameObject への送信は拒否されて変更されないか。
- 次スライスへ進む最低条件: §2.2 の I1〜I3 と E1〜E2 を同じ GO 候補 head で満たす。

### 2.1 統合設計

書き込みの意味と対象は SampleGame がコマンド登録経路で持つ。Framework は「この GameObject の現在の NodeId」を返す読み取り口だけを足す。DebugStudio（WPF・CLI・protocol）は変えない。

1. **検証用 GameObject（SampleGame）:**
   - 新規 `DebugVerificationTargetHost`（internal plain class、`SampleGame.DependOnAll` の `DebugCommands/`）が、名前 `[DebugStudioVerificationTarget]` の GameObject を 1 つ作り、所有し、破棄する。component は Transform だけ。
   - 置き場所は UICommon の scene の別の root。`AppInitializer` が `OnServicesInitializing` を override し、`Application.isEditor` のときだけ、`FindAnyObjectByType<UICommon>()` の `gameObject.scene` を host に渡して作らせる（`SceneManager.MoveGameObjectToScene`）。作った後に `DebugSocket?.NotifyHierarchyChanged()`。理由: hierarchy は DontDestroyOnLoad を出さず、ゲームの scene は SceneDirector が入れ替える。UICommon は App 常駐で管理外なので Play の間ずっと hierarchy に出る。UICommon の root の子にはしない（Framework の GameObject の所有を混ぜない）。UICommon が見つからなければ作らず warning。
   - 重複させない: host は生きた GameObject を持っていれば作らない。`AppInitializer` は既存の Camera 等と同じく `SubsystemRegistration` と `OnAfterSceneLoadInitializationFailed` で host を解放する。Play の終わりに Unity が破棄した GameObject は `== null` で判定する。
2. **読み取り口（Framework。公開面はこの 2 つ）:**
   - `DebugSocketService.TryGetHierarchyNodeId(GameObject gameObject, out long nodeId)`（public、main thread で呼ぶ）。`_gate` の中で registry の EntityId→NodeId を引き、その NodeId の GameObject が同じ参照（`==`）のときだけ true。未 capture・prune 済み・`Reset` 後・破棄済みは false。registry に `TryGetNodeIdUnsafe` を足す。
   - `AbstractApplicationInitializer` に `protected DebugSocketService? DebugSocket => _services?.DebugSocket;`（既存の accessor と同じ形）。
   - 逆引きにした理由: 呼び手が既に持つ GameObject の番号を返すだけで、任意の NodeId から GameObject を渡す口を Framework に作らない。これが最も狭い。
3. **コマンド（SampleGame）:**
   - `AppInitializer` が `CreateDebugCommandDispatcher()` を override し、`Application.isEditor` のときだけ `DebugCommandCatalog` に `debug.set-active` を登録して `CatalogDebugCommandDispatcher` を返す（Editor 以外は既定の dispatcher）。handler は `() => DebugSocket` と `() => host.Target` で実行時の値を読む。新規 `SetActiveDebugCommand`（internal static）に置き、解決を delegate で差し替えられるようにする。
   - payload: `{"nodeId":<正の整数>,"active":true|false}` の 2 つだけ。どちらも省略不可。
   - 解析: `JsonUtility.FromJsonOverwrite` で、`active` の初期値を `false` にした DTO と `true` にした DTO の 2 つへ同じ payload を読む。`FromJsonOverwrite` は JSON に無い field を初期値のまま残すので、2 つの `active` が食い違えば `active` が無い（または bool として読めない）と分かる。厳密な JSON パーサや schema の規約は足さない。nodeId は採番が 1 から始まるので、無い場合の 0 を 1 未満で弾く。
   - 順序: (1) 解析で例外、`active` が無い、または nodeId が 1 未満 → 拒否 `invalid-payload`。(2) 検証用 GameObject が無い・DebugSocket が無い・`TryGetHierarchyNodeId` が false → 拒否 `target-unavailable`。(3) 返った NodeId が payload の nodeId と違う → 拒否 `not-verification-target`。(4) `SetActive(active)`、`NotifyHierarchyChanged()`、`DebugCommandResult.Ok("applied", "{\"nodeId\":<N>,\"activeSelf\":<後の activeSelf>}")`。拒否は (4) より前に `DebugCommandResult.Fail("rejected: <reason>")` で返す。
   - 名前・印・パスは照合に使わない。照合は「検証用 GameObject の現在の NodeId と payload の一致」だけなので、どの NodeId を送っても書き換わりうるのは検証用 GameObject だけである。
4. **結果の読み方（README に書く。CLI の動作は変えない）:**
   - `success=true` と `{"nodeId":…,"activeSelf":…}`（exit 0）: その NodeId の検証用 GameObject に適用した。
   - `success=false` で message が `rejected: ` で始まる（exit 1）: SetActive の前に拒否し、変更していない。
   - それ以外の `success=false`（handler の例外、未登録など）、および `CommandResult` の行が無い終わり方（期限切れ exit 2、それ以外の失敗 exit 1）: 結果不明。適用されたかどうかは分からず、後から確定しない。timeout は未実行を意味しない。CLI は自動で再送しない。
   - 結果不明の後の `inspect` は読んだ時点の現在値で、その要求の実行の証拠ではない。望む値にしたいときは `hierarchy` で NodeId を読み直し、値を明示して送り直す（値を明示するので逆には戻らない）。
   - 既知の制約: 前の Play の NodeId が次の Play の検証用 GameObject の NodeId と一致すると、その送信は検証用 GameObject に適用される。検証用でない GameObject は Play によらず拒否する。

**比べた代替と採らない理由:**

- revision 2 の構成（snapshot と control に SessionId、`<sessionId>:<NodeId>` の target、専用 `set-active` CLI、`not-sent` / `unknown` と exit 3）: 新しい目的に要らない。owner が Play をまたぐ識別を後続へ送った（§1.2）。
- Framework が NodeId から GameObject を返す（revision 1・2 の `TryResolveHierarchyNode`）: 任意の GameObject を Game 側へ渡す口になる。逆引きで足りる。
- 印（marker）で対象を表す、Scene asset に置く、DontDestroyOnLoad に置く、ゲームの scene に置く、DebugSocket の組込みコマンドにする、名前で探す: revision 2 と同じ理由で採らない（§5.5）。

### 2.2 進める最低条件

#### 不変条件

- **I1: `debug.set-active` は検証用 GameObject 以外の activeSelf を変えない。** ゲームの GameObject、Framework の GameObject、同じ名前を付けた別の GameObject を含む。
  - 主張を崩しうる既存の経路: (a) NodeId は Play ごとに 1 から振られ、別の GameObject に同じ番号が付く（基準観測で確認）。(b) WPF が切断後も古い hierarchy を残す。(c) WPF の再接続で registry が `Reset` され、同じ GameObject に新しい番号が付く。(d) GUI のコマンド画面や `send` から任意の payload を送れる。(e) 名前は誰でも付けられる。(f) 検証用 GameObject が破棄済み、または未 capture。どれも handler が「検証用 GameObject の現在の NodeId」とだけ比べるので、書き換わりうるのは検証用 GameObject だけになる。(a) で旧番号が新しい検証用 GameObject に一致する場合は owner が許容した（§1.2）。
  - 探索した境界: Runtime の DebugSocketServices（router、dispatcher 群、registry、publisher、Inspector の lookup）、`DebugCommandCatalog`、Bootstrap の SubsystemRegistration・BeforeSceneLoad・AfterSceneLoad。未確認: なし。
- **I2: 拒否と報告した要求で activeSelf を変えず、結果を確定できない終わり方を確定した結果として案内しない。** handler は拒否を SetActive の前に決める。README は §2.1 の 4 のとおり説明し、timeout を未実行と書かない。CLI は自動で再送しない（既存のまま）。
  - 主張を崩しうる既存の経路: router の catch は SetActive の後の例外も Success=false にする（`rejected: ` で始まらないので結果不明と読む）。catalog は未登録を Success=false で返す。Failed は送る前と後を区別しない。期限切れの後に届いた結果は CLI へ返らない。
  - 探索した境界: CLI の `Program.WriteResult` と client、Runtime の router・dispatcher 群・catalog。
- **I3: 既存の契約を変えない。** DebugStudio の code・protocol・GUI、`send` / `compare` / `hierarchy` / `inspect` の出力と exit、S2 の不変条件。Framework の変更は §2.1 の 2 の追加だけ。
  - 既知の観測できる変化: (1) Editor の SampleGame で未登録の名前を送ったときの message が catalog の文言に変わる（Success=false と exit は同じ）。(2) Editor Play の hierarchy に `[DebugStudioVerificationTarget]` の root が 1 つ増える。
  - Editor 以外では検証用 GameObject を作らず、コマンドも登録しない（Player の挙動を変えない）。DebugSocket を有効にした Player でも `CreateDebugCommandDispatcher()` と `OnServicesInitializing` は呼ばれるので、どちらも `Application.isEditor` で分ける。

#### 成立条件（現行 base では未実装）

- **E1:** 人手や eval の準備をしない通常の Editor Play で、hierarchy に `[DebugStudioVerificationTarget]` がちょうど 1 つ出て、その GameObject は UICommon と同じ scene にある。Play を止めて再開した次の Play でも hierarchy に 1 つだけ出る（新しい GameObject の解放と再生成の確認。書き込みは再演しない。同じ WPF が Play をまたいで hierarchy を読めることは §1.5 の基準観測で成立済み）。
- **E2:** 同じ Play の中で、Release の WPF を `--listen` で起動し、その NodeId で `send --command debug.set-active` を `false`・`true` の順に送ると、各回で `success=true` と結果の `nodeId`・`activeSelf`、Editor で独立に読んだ activeSelf、`inspect` の `GameObject.ActiveSelf` の rawValue が一致する。false の間も hierarchy に同じ NodeId で 1 つだけ出る。

### 2.3 受け入れ条件

- **AC1（I1・I2、失敗経路）:** Unity EditMode:
  - registry: capture した GameObject の `TryGetNodeIdUnsafe` は capture の NodeId を返す。未 capture、破棄済み、`Reset` の後は false。
  - handler（解決を差し替える）: payload の nodeId が検証用 GameObject の現在の NodeId と違う → `rejected: not-verification-target` で、検証用 GameObject と、その nodeId に対応する別の GameObject（同じ名前を付けたものを含む）の activeSelf が変わらない。解決が false・検証用 GameObject が破棄済み・DebugSocket が無い → `rejected: target-unavailable`。解析できない payload、nodeId が無い・0、`active` が無い（`{"nodeId":<一致する N>}` を含む）→ `rejected: invalid-payload` で activeSelf が変わらない。一致 → `active` の値（true・false の両方）になり、結果の payload の `nodeId`・`activeSelf` と一致する。
  - 実 Editor（§5.3 V5）: 検証用でない node の NodeId で送ると `rejected: not-verification-target`（exit 1）で、その GameObject の activeSelf が変わらない。
- **AC2（E1）:** Unity EditMode: host は渡された scene に GameObject を 1 つ作り、生きている間に 2 回目を求めても作らない。解放で破棄し、破棄済みでも解放は失敗しない。component は Transform だけ。実 Editor: eval で何も作らずに、Play1 で数が 1 かつ scene が UICommon の scene と同じ（§5.3 V1・V2）、Play2 の hierarchy で数が 1（V6）。
- **AC3（E2）:** 実 Editor（§5.3 V3・V4、Play1 の中）で false にしてから true に戻し、各回で CLI の `success=true` と結果の `nodeId`・`activeSelf`、Editor の独立値、`inspect` の rawValue が一致し、false の間も hierarchy に同じ NodeId で 1 つある。GUI のボタンを押さない。不合格例: eval で対象を作った、途中で WPF を再起動した、対象が 2 つ出た。
- **AC4（I3）:** Unity EditMode: 登録と生成に Editor でないことを渡すと catalog に `debug.set-active` が無く、検証用 GameObject も作らない。`git diff --stat <base> <head>` で `tools/DebugStudio/` の変更が `DebugStudio.Cli/README.md` だけ、`protocol/` に変更が無い。
- **AC5（I2、文書）:** CLI README に §2.1 の 4 の読み方（適用・拒否・結果不明の区別、timeout は未実行ではない、自動再送しない、inspect はその要求の証拠ではない、送り直しの手順、Play をまたぐ既知の制約）、payload の形（`nodeId` と `active` の 2 つ、どちらも省略不可）と送信例、結果の payload の形、検証用 GameObject の名前があり、§2.1 の 3 と §5.3 のコマンドと一致する。

### 2.4 ここでは答えない問いと所有する後続スライス

- 汎用編集、別の操作コマンド、GUI からの書き込み、複数対象、ゲームの GameObject への書き込み: DebugStudio S4 以降（未起票）
- Play をまたぐ指定の識別（NodeId の世代、snapshot の SessionId）: DebugStudio S4 以降（未起票）
- 結果不明の確定（要求の相関、送る前の失敗の区別）と専用 CLI: DebugStudio S4 以降（未起票）
- Player と遠隔での書き込み: DebugStudio S4 以降（未起票）
- inspect の自己応答保証、hierarchy に DontDestroyOnLoad を出すこと: DebugStudio S4 以降（未起票）
- S2 から移送された client / server の timeout 起点と client 離脱後の占有: DebugStudio S4 以降（未起票）
- S2 HANDOFF の Phase D: S2 の担当

### 2.5 スライス分割の判断

- 契機の確認: 変更理由は「検証用 GameObject を置く」と「1 件書き込む」の 2 つで、どちらかだけでは問いに答えられない。未知は配置（V1〜V2）と書き込み経路（V3 以降）で、同じ実機手順の前半と後半で別に判定できる。別の問いの受け入れ条件は混じっていない。
- 判断: 分割しない。

### 2.6 risk と判定

- risk: normal。理由: Framework の変更は読み取りだけの public method 1 つと protected accessor 1 つの追加で、既存の動作・wire・protocol を変えない。並行性の主張は無い（main thread の既存の dispatcher で動く）。新しい GameObject の所有と解放は `AppInitializer` の既存の型に従い、Editor の Play に限る。revision 2 を high とした理由（wire field、Play をまたぐ照合、接続の差し替えとの競合）は revision 3 で無くなった。
- 判定定義: GO は I1〜I3 と E1〜E2 を同じ GO 候補 head で満たすこと。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- §1.6 の常時契約と既知の制約。
- Framework の変更は §2.1 の 2 だけ。router、組込みコマンド、`MainThreadDebugCommandDispatcher`、`DebugCommandCatalog`、hierarchy の走査範囲、UICommon の loader を変えない。
- `tools/DebugStudio/` は CLI README だけを変える。protocol と codegen を変えない。
- SampleGame の Scene・Prefab・設定 asset を変えない。検証用 GameObject は SampleGame のコードが Editor でだけ作る。1 つ、component は Transform だけ。
- handler の照合は検証用 GameObject の現在の NodeId と payload の一致だけ。拒否は SetActive より前に決める。

### 2.8 人間が凍結前に判断すること

- 凍結の承認だけ。目的の変更と Play をまたぐ識別の移送は承認済み（§1.2）。

## 3. 責務マップ

行数は `d176033a` の現在値。増分は見込み。変更は Unity 側 6 file（新規 2）とテスト、文書 2。

| file | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 現在 / 増分 |
|---|---|---|---|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/Hierarchy/DebugSocketRuntimeNodeRegistry.cs` | `TryGetNodeIdUnsafe`（逆引き） | service（Play） | internal | `DebugSocketRuntimeNodeRegistryTests`（`unity/Assets/OneStarMaker/Tests/Foundation/`、128 行）に追加 | 141 / +10〜15 |
| `unity/.../DebugSocketServices/DebugSocketService.Hierarchy.cs` | `TryGetHierarchyNodeId`（`_gate` で包む） | service | public method | registry のテストで足りる | 87 / +10〜15 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | protected `DebugSocket` accessor | initializer（App） | protected property | — | 1305 / +2〜4 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/DebugVerificationTargetHost.cs`（新規） | 検証用 GameObject の生成（1 つ・scene へ移す）・所有・解放 | `AppInitializer` が所有。GameObject は Play の寿命 | internal | SampleGame.Tests の EditMode | 新規 40〜70 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs`（新規） | 登録（Editor のときだけ）、payload の解析、NodeId の照合、SetActive、結果 | catalog は Play ごと | internal static | 解決を delegate で差し替えて EditMode | 新規 60〜100 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | `CreateDebugCommandDispatcher()` と `OnServicesInitializing` の override、host の解放 | initializer | — | — | 431 / +20〜35 |
| `tools/DebugStudio/src/DebugStudio.Cli/README.md`、`unity/Assets/Docs/Architecture/12-telemetry.md` | 使い方と結果の読み方。§10.5 の「SampleGame に catalog が配線されているという意味ではない」を現状に直す | 公開文書 | — | docs-audit、AC5 | 91 / +20〜30、515 / +5〜15 |

配置の理由: 検証用 GameObject とコマンドはゲームの内容ではなく SampleGame の debug 配線なので composition root の `DependOnAll` に置く。所有者は `AppInitializer`（既存の Camera・RenderEnvironment と同じ解放の型）、寿命は Play。入れ物の UICommon scene は Framework が所有し、寿命は検証用 GameObject 以上に長い。

規模の警報: `AbstractApplicationInitializer` は既に 500 行を超えるが、既存の accessor と同じ 1 行で責務を増やさないので分割しない。`AppInitializer` は約 465 行で、生成と所有は host へ、照合は `SetActiveDebugCommand` へ出す。

新しい中核ロジックの単体テスト: 逆引きは registry の EditMode、host と handler は SampleGame.Tests の EditMode。

## 4. 実装計画

- 順序: (1) Framework: registry の逆引き、`TryGetHierarchyNodeId`、protected accessor、registry のテスト。(2) SampleGame: host、`SetActiveDebugCommand`、`AppInitializer` の override と解放、EditMode テスト。Editor で compile を確かめる。(3) CLI README と §10.5。
- Phase B から Phase A へ差し戻す条件: asmdef 参照・package が要る。§2.1 の 2 以外の Framework の公開 API が要る。DebugStudio の code・protocol を変える必要がある。SampleGame の Scene・設定を変える必要がある。UICommon の scene に置けない事実が見つかる。
- 対象外を維持する方法: 汎用の書き込みや別のコマンドの土台を作らない。検証対象を増やす仕組み（印、一覧、登録 API）を作らない。

## 5. テストとレビュー計画

### 5.1 テスト

- 単体テスト: §3 の各テスト境界。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可）: `DebugSocketRuntimeNodeRegistryTests`、SampleGame の `DebugVerificationTargetHostTests`・`SetActiveDebugCommandTests`。
- 判定必須テスト（GO 候補 head）: 最終の全 EditMode 回帰（標準の `pwsh tools/run-tests.ps1`）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC4 の diff 確認、§5.3 の実機手順。
- 全 EditMode 回帰の扱い: 既存 Harness（`tools/Harness/README.md`）の規則に従う。実 exit と保持した process handle の signal を記録し、Unity 終了時の native stall は同 README の停止規則に従う（強制終了した取得を成功証拠にしない）。本書は、失敗・Inconclusive・非 0 の exit を合格と読み替える独自の規則を置かない。結果は原記録のまま判定 C に記録する。
- .NET のテスト: 判定必須にしない。理由: DebugStudio の code・protocol を変えない（AC4 の diff で確かめる）。Release build は §5.3 V0 で行う。
- 全 EditMode 回帰の適用除外: なし。PlayMode は要しない。
- 人間の判断が必要な条件: なし。高コスト証拠: なし。

### 5.2 未知の操作経路

- 既存の読取り経路: §1.5 で成立を観測した。
- 検証用 GameObject の配置（V1〜V2）と書き込み（V3 以降）: 未確認。初回確認は Phase C。理由: 未実装。担当: 判定 C の実行担当。確認地点: AC1〜AC3 の実機部分。不成立時は、待受け・接続、生成と配置、登録、照合のどこが原因かを分けて B へ戻し、受け入れ条件を黙って減らさない。B の出口の動作確認で同じ手順を 1 回軽く実行してよい（B の観測は判定の入力にしない）。

### 5.3 実機検証の手順（Phase C。V0〜V7）

GO 候補 head で判定 C の実行担当が行う。主要な確認（V3〜V5）は Play1 の 1 回の中で行い、Play2（V6）は検証用 GameObject が 1 つだけ再生成されることだけを見る。全 EditMode 回帰は Editor を閉じて別に行う。`tools/unity-editor.cmd` の呼出しは 1 回ずつ別の command にし、eval の C# は UTF-8 の Base64 で渡す。Editor が開いていればその所有者を確かめ、重複して起動しない。検証用 GameObject を eval で作らない。eval は独立値の読取りだけに使う。CLI は Release 出力の `debugstudio-cli.exe` を pwsh 7 から使い、payload は単引用符で渡す。

- V0: HEAD・clean・Editor の版を記録する。`dotnet build tools/DebugStudio/src/DebugStudio.App/DebugStudio.App.csproj -c Release` と `dotnet build tools/DebugStudio/src/DebugStudio.Cli/DebugStudio.Cli.csproj -c Release`。App の Release 出力の exe を `--listen` で直接起動し、PID を記録する。
- V1: Play1 を開始する（`tools/unity-editor.cmd command` で公開名を確かめ、無ければ eval で `UnityEditor.EditorApplication.isPlaying = true;`）。`hierarchy` を `hasSnapshot: true` かつ `[DebugStudioVerificationTarget]` が現れるまで読み直す（総計 15 秒以内）。その node の数（1 を期待）と NodeId（N1）を記録する。
- V2: 置き場所と独立値を eval で読む。読み込み済みの scene の root を走査し、検証用の名前の root の数、その scene が UICommon の scene と同じか、activeSelf を返す。

  ```csharp
  var ui = UnityEngine.Object.FindAnyObjectByType<OneStarMaker.Runtime.UISystem.UICommon>();
  UnityEngine.GameObject t = null; var n = 0;
  for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
      foreach (var go in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects())
          if (go.name == "[DebugStudioVerificationTarget]") { t = go; n++; }
  return $"count={n} sameSceneAsUICommon={(t != null && ui != null && t.scene == ui.gameObject.scene)} activeSelf={(t != null && t.activeSelf)}";
  ```

  検証用でない node として、開いていた scene の root で hierarchy に 1 つだけある名前（基準動作では `Directional Light`）を選び、その NodeId（X）を記録する。同じ走査でその名前の root が 1 つだけであることを確かめ、その `UnityEngine.EntityId.ToULong(go.GetEntityId())`（EX）と activeSelf を記録する。以後この GameObject は EX で特定する（名前で引き直さない）。
- V3: `send --command debug.set-active --payload '{"nodeId":<N1>,"active":false}'` → `success=true`、`{"nodeId":<N1>,"activeSelf":false}`、exit 0 を期待する。V2 の eval で独立値、`inspect <N1>` の `GameObject.ActiveSelf` の rawValue、読み直した `hierarchy` の検証用 node の数と NodeId を記録する。
- V4: `"active":true` で V3 と同じことを記録する。
- V5: `send --command debug.set-active --payload '{"nodeId":<X>,"active":false}'` → `success=false message=rejected: not-verification-target`、exit 1 を期待する。読み込み済みの scene の root から EntityId が EX の GameObject を探し、activeSelf が V2 と同じか記録する。
- V6: Play を停止し、Play2 を開始する（WPF は同じ、GUI のボタンを押さない、eval で何も作らない）。V1 と同じく `hierarchy` を読み直し、`[DebugStudioVerificationTarget]` の node の数（1 を期待）を記録する。送信は行わない。
- V7: Play を停止する。WPF に通常の終了を要求し（`CloseMainWindow`）、process handle の signal と実終了コードを記録する。強制終了した場合はそのとおり記録し、正常終了とは扱わない。Editor の Scene が clean か、tracked 変更が無いかを記録する。
- 保存するもの: 各段のコマンド、stdout / stderr / exit、HEAD、Editor の版、WPF の PID と終了コード、各 Play の hierarchy、eval の戻り値。結果不明の実機再現は求めない（読み方は AC5 の README で判定する）。

### 5.4 判定 C の照合方式

- 照合 script を使わず、判定 C が原記録を直接照合する。理由: 照合する値は十数個（Play1 の各 send の success・message・nodeId・activeSelf・exit、inspect の rawValue、eval の値、hierarchy の node 数）で、どれも等値の比較で閉じる。判定 C は条件ごとに原記録の該当箇所と期待（§2.1 の 3・4、eval の独立値）を対にした表を判定記録に残す。期待は実装からではなく eval の独立値と §2.1 から導く。
- Phase A snapshot はこの方式と関係なく、凍結後に固定する（§0）。
- 機械検査: contract-audit、docs-audit、AC4 の diff 確認。
- A0/A1 主担当: Claude Opus 5.5 High（Anthropic）。revision 1〜3 の A3 統合も同じ担当。
- C' 用に予約した担当: grok-4.7-xhigh（xAI、新規セッション）。Phase A の構造レビューが Grok 4.6 High なので同じ Grok 系列であり、`独立性制約あり` と記録する。risk normal なので強化条件は適用しない。

### 5.5 A2・再レビュー・外部計画レビューと採否

**入力版と担当（実績）:**

| 観点 | 入力版 | 担当・モデル・ベンダー | 備考 |
|---|---|---|---|
| 目的レビュー | revision 1・2 の §1.1 の 2 欄の原文（sha256 `1699e1dd34d533f41442a7fd971e488f48665f0af7e0aeab49b195e6079f0196`） | GPT-6.2-sol High（OpenAI、新規セッション） | 初稿・A0 を読む前に回答 |
| 高リスク代替レビュー | A0 固定 copy（SHA-256 `67647af6420f3fe123f8c280549b8f7a130aaaa2e236b6bf8f3bbee91ec435ab`） | GPT-6.2-sol High（目的レビューの回答保存後に同じ担当へ A0 だけを渡した） | 初稿・コードは読んでいないと報告 |
| アーキテクチャゲート（構造） | 固定 base `a629f9c1` と A1 copy（SHA-256 `474ad1e9a9c2e8614654a48b3d4be72a19a52736335aa0fe6861d6099fa1cfc1`） | Grok 4.6 High（xAI、cursor-agent の ask モード） | clean と copy の hash は送信側の計測記録を読んだだけ |
| 構造の限定再レビュー（revision 1） | 固定 base `d176033a` と revision 1 の固定 copy | Grok 4.6 High（新規セッション） | 阻害指摘なし |
| 外部計画レビュー（revision 1） | 公開 Draft PR #204 head `4f17e54b` | 記録は PR とオーケストレーターの側 | 所見 1 件 |
| 構造の限定再確認（revision 2） | 固定 base `d176033a` と revision 2 の固定 copy | Grok 4.6 High（新規セッション） | 阻害指摘なし |
| 外部計画レビュー（revision 2） | 公開 Draft PR #204 head `6a60c059` | 記録は PR とオーケストレーターの側 | コメント 2 件（2 件目が 1 件目の主要所見を撤回） |
| 目的レビューのやり直し（revision 3） | §1.1 の 2 欄の原文（sha256 `8d00d0c18e092c554adb53f6c2e3f2ce49669cb0bf9dc282721d0123ffc9aa09`） | GPT-6.2-sol High（OpenAI、新規セッション） | 目的・利用者の原文だけを読んだ。やり直しは 1 回目で、これで使い切った |
| 構造の限定再確認（revision 3） | 固定 base `d176033a` と revision 3 の固定 copy（SHA-256 `68e7f97a6a547cc78727dff3fbf0d3b92e9eb88505020ba41a42f15e2b0ad5e3`） | Grok 4.6 High（xAI、cursor-agent の ask モード、新規セッション） | 着手時 detached HEAD・dirty なし。ask モードのため copy の SHA-256 は担当が再計算していない。阻害する矛盾なし |

**revision 1・2 の採否の revision 3 での扱い:** 目的レビュー・代替レビュー・構造レビュー・外部計画レビュー（revision 1）の採否は revision 2（commit `6a60c059`）の §5.5 のとおりで、revision 3 では次を変えた。

| 前の採用 | revision 3 |
|---|---|
| 変更時点で Play と対象の同一性を確かめ、再利用で別対象に解決しない（目的レビュー）、Runtime で識別を確定したコピー可能な target（代替レビュー）、F1 の 1 値 target（構造） | 撤回。owner の裁定で Play をまたぐ識別を後続へ。検証用でない GameObject の拒否（I1）は維持 |
| 確定・拒否・不明の区別（目的レビュー）、CLI→WPF の未送信と不明の区別（代替レビュー）、F2 の接続確立前後の分類（構造）、専用 CLI 名（代替レビュー） | 撤回。既存 send のまま、README で結果の読み方を説明（§2.1 の 4）。不明後の inspect を実行の証拠にしない点と自動再送しない点は維持 |
| Framework は照合と解決を持つ（代替レビュー） | 縮小。Framework は逆引きの読み取り口だけ。照合は SampleGame |
| Play ID 等を手入力させない、反転にしない、名前・パスを推測しない、汎用編集を作らない、Player では登録しない、F3（SampleGame の登録経路）、検証対象の自動生成（外部所見 revision 1） | 維持 |

**外部計画レビュー（revision 2、PR #204 head `6a60c059`）の採否:**

| 所見 | 採否 | 反映 |
|---|---|---|
| 1-1 旧 delta と新 snapshot の混在で I1 が破れうる。排除の設計か反例テストを凍結前に | 不採用（同じレビュー担当が 2 件目で撤回） | SessionId と target を廃止したので対象の経路自体が無い |
| 1-2 置き場所を `target.scene == UICommon.gameObject.scene` で記録し、必要ならシーン遷移後も確かめる | 置き場所の観測は採用、シーン遷移の確認は不採用 | V2 の eval で同じ scene かを記録。シーン遷移の実機テストは owner の方針で増やさない。UICommon が SceneDirector の管理外で Play の終わりまで残るのはコードの事実（§1.4） |
| 1-3 非対象の独立値を `GameObject.Find` で読むと特定できない | 採用 | V2 で名前が 1 つだけと確かめて EntityId を固定し、V5 は EntityId で読む |
| 2 目的・受け入れ条件を縮め、既存 send・最小の読み取り口・参照の照合・main thread・同一 Play の false/true と非対象の拒否を残す。目的を変えずに AC だけ削るのは不整合 | 採用（owner が目的の変更を承認） | §1.1〜§1.2、§2 全体、§3〜§5 |

**目的レビューのやり直し（revision 3）の採否:**

| 指摘 | 採否 | 反映 |
|---|---|---|
| 通常 Play に CLI だけで対象を見つけ、指定値を書いて inspect で確かめる | 採用（既存） | E1・E2、UC0〜UC3 |
| 非アクティブでも同じ対象を見つける | 採用（既存） | E2（false の間も同じ NodeId で 1 つ） |
| 変更対象は SampleGame の検証用 GameObject に限る。同名・曖昧な指定で変更しない | 採用（既存） | I1。照合に名前を使わない |
| 値の指定は繰り返しても同じ状態になる | 採用 | 反転にしない（UC2）。`active` を省略不可にし、欠落を false と読まない（§2.1 の 3） |
| 前の Play の参照で変更しない | 不採用（owner の裁定） | owner が 2026-11-23 に明示的に後続へ送った（§1.2、§2.4） |
| CLI の出力と終了コードから対象・成否・値が分かる | 一部採用 | 既存 send の分かる範囲で足す: 結果の payload に `nodeId` と `activeSelf`、拒否の reason。exit は既存のまま |
| WPF の追加操作、手動登録、別途の内部 ID、確認画面、汎用編集、一括変更、永続化を作らない | 採用（既存） | §1.3、§2.7 |

**構造の限定再確認（revision 3）の採否:** 指摘なし。既存 send が `--payload` を不透明な文字列のまま catalog の handler へ渡し、WPF・CLI・wire の追加が要らないこと、registry が EntityId→NodeId と NodeId→GameObject を既に持ち公開の逆引きを `_gate` の中で足せること、handler が main thread の dispatcher の上で所有する GameObject の現在の NodeId と payload を比べ、不一致を SetActive の前に拒否し、他の GameObject を解決しないことを、担当が現行ソースで確かめた。

**凍結候補への実務上の調整（root の指示、revision 3 の中）:**

| 調整 | 採否 | 反映 |
|---|---|---|
| Play2 で V3・V4 を再演する条件を薄くする | 採用 | E1・AC2・V6 は Play2 で hierarchy の数 1 だけ。同じ WPF が Play をまたいで読めることは §1.5 の基準観測を使う |
| 全 EditMode は既存 Harness の実 exit と停止規則に従い、独自の PASS 扱いを置かない | 採用 | §5.1 |
| `JsonUtility` が `active` の欠落を false にする点 | 採用 | 初期値の違う DTO 2 つへの `FromJsonOverwrite` で欠落を区別（§2.1 の 3）、AC1 に欠落の拒否 |

**最終の外部計画レビュー。** [revision 3 へのコメント](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/204#issuecomment-7084506095) は凍結を支持した。文字列や null を active に渡した場合の Unity の挙動は、B が AC1 の handler fixture に代表入力を含め、C がその結果を見る。追加の GO 条件は設けない。Play 世代識別は後続のままとする。

### 5.6 再レビューと凍結までの順序

- 目的レビュー: やり直し済み（§5.5）。§1.1 の 2 欄はその原文のまま固定する。
- 構造（アーキテクチャゲート）: 限定再確認済み（§5.5）。その後の調整（payload の欠落の区別、結果の payload への `nodeId`、Play2 の縮小、全 EditMode の扱い）は handler の中の読み方と手順・判定の記述だけで、file・所有者・寿命・依存・公開面を変えないので、再確認しない。
- 代替レビュー: やり直さない。revision 3 は既存の経路の縮小で、新しい構成要素は逆引き 1 つだけであり、代替レビューの「Game 側の狭い宣言」「名前・パスに依存しない」は維持している。risk も normal になった。
- 順序: (1) 目的レビュー（済）、(2) 構造の再確認（済）、(3) 採否を添えて Draft PR #204 へ 1 commit、(4) owner の凍結判断・対象 revision・次担当 B を §0 に記録して凍結版を commit。implementation base は凍結時の base に固定し、凍結後に Phase A snapshot を固定する。

## 6. Phase B 実装結果

- 実装（§3 の 6 file・テスト・文書 2。§2.1 の 1〜4 のとおり）:
  - Framework: `DebugSocketRuntimeNodeRegistry.TryGetNodeIdUnsafe`（EntityId→NodeId を引き、その NodeId の GameObject が `==` で同じ時だけ true。破棄済みは `GetEntityId` の前に `== null` で false。採番しない）、`DebugSocketService.TryGetHierarchyNodeId`（`_gate` で包む）、`AbstractApplicationInitializer.DebugSocket`（protected accessor）。
  - SampleGame（`DependOnAll/DebugCommands/`）: `DebugVerificationTargetHost`（`TryCreate(isEditor, scene)` は Editor・有効な scene・生きた GameObject が無い時だけ 1 つ作って scene へ移す。`Release()` は冪等で、Play 中は `Destroy`、それ以外は `DestroyImmediate`）。`SetActiveDebugCommand`（`Register(catalog, isEditor, getDebugSocket, getTarget)` は Editor の時だけ登録。`Execute(payload, target, resolver, notify)` が §2.1 の 3 の順で判定し、拒否は SetActive より前。解析は `active` の初期値が違う DTO 2 つへの `FromJsonOverwrite`）。
  - `AppInitializer`: `CreateDebugCommandDispatcher()`（Editor の時だけ catalog + `CatalogDebugCommandDispatcher`、それ以外は base）、`OnServicesInitializing`（Editor の時だけ `FindAnyObjectByType<UICommon>()` の scene に作り、作れたら `DebugSocket?.NotifyHierarchyChanged()`。UICommon が無ければ warning）、`SubsystemRegistration` と `OnAfterSceneLoadInitializationFailed` で host を解放。
  - 文書: CLI README に「Setting the SampleGame verification object's activeSelf」（名前、payload、送信例、結果の payload、適用・拒否・結果不明の読み方、timeout、自動再送なし、inspect、送り直し、Play をまたぐ既知の制約）。12-telemetry §10.5 の「SampleGame に catalog が配線されているという意味ではない」を現状（Editor だけの配線、置き場所の理由、`TryGetHierarchyNodeId` の契約）に置き換えた。
- 新規テスト（EditMode。§5.1 の起点 filter と同じ 3 fixture）:
  - `DebugSocketRuntimeNodeRegistryTests`（OneStarMaker.Tests）に追加: `TryGetNodeIdUnsafe_CapturedGameObject_ReturnsCapturedNodeId`、`TryGetNodeIdUnsafe_NotCaptured_ReturnsFalse`、`TryGetNodeIdUnsafe_DestroyedGameObject_ReturnsFalse`、`TryGetNodeIdUnsafe_AfterReset_ReturnsFalse`。
  - `SampleGame.Tests.DebugCommands.DebugVerificationTargetHostTests`: `TryCreate_InEditor_CreatesOneTransformOnlyRootInGivenScene`、`TryCreate_WhileTargetAlive_DoesNotCreateSecond`、`Release_DestroysTarget_AndNextCreateMakesNewOne`、`Release_AfterUnityDestroyedTarget_DoesNotThrow`、`TryCreate_NotEditor_CreatesNothing`（AC4）、`TryCreate_InvalidScene_CreatesNothing`。
  - `SampleGame.Tests.DebugCommands.SetActiveDebugCommandTests`: `Execute_NodeIdIsNotVerificationTarget_RejectsWithoutChange`（同名の別 GameObject の NodeId と未知の NodeId）、`Execute_ResolverReturnsFalse_RejectsAsTargetUnavailable`、`Execute_TargetDestroyed_RejectsAsTargetUnavailable`、`Execute_TargetMissing_RejectsAsTargetUnavailable`、`Execute_DebugSocketMissing_RejectsAsTargetUnavailable`、`Execute_InvalidPayload_RejectsWithoutChange`（解析不能、空、`{}`、nodeId 無し・0・負、`{"nodeId":7}` を初期値 true / false で）、`Execute_MatchingNodeId_AppliesFalseThenTrue`、`Execute_SameValueTwice_StaysAtRequestedValue`、`Execute_ActiveIsStringOrNull_RejectsOrReportsActualValue`、`Register_NotEditor_DoesNotRegister`（AC4）、`Register_InEditor_RegistersAndReadsDebugSocketAtExecution`。
  - `active` に文字列 `"false"` / `"true"` / `null` を渡す代表入力（§5.5 の最終の外部計画レビューの確認事項）は `Execute_ActiveIsStringOrNull_RejectsOrReportsActualValue` に置いた。JsonUtility の挙動は未観測なので、どちらの挙動でも成り立つ契約（拒否なら `rejected: invalid-payload` で activeSelf 不変、適用なら結果の activeSelf が実際の値と一致）だけを assert し、観測した結果を `TestContext.WriteLine`（テスト結果 XML の output）に残す。C がその output を読む。
- HANDOFF との差:
  - 新規の 2 フォルダと 4 file の `.meta` を手で書いた（Unity 6.6 の既存の形式に合わせた 2 行の `.cs.meta` と folder の `DefaultImporter`。GUID は repo 内に無いことを検索で確かめた）。Editor を開けず import で生成できなかったため。B 適応（責務配置・契約は変えない）。
  - `DebugVerificationTargetHost.TryCreate` は無効・未ロードの scene では作らない（`MoveGameObjectToScene` の失敗で active scene に取り残さないため）。`AppInitializer` は Player で UICommon の検索と warning をしないよう先に `Application.isEditor` で抜け、生成の可否自体は host の `isEditor` 引数でも決める（AC4 のテスト境界）。
  - それ以外の差は無い。Phase A へ戻す条件（§4）には当たらなかった。
- 確認した事実:
  - `pwsh tools/contract-audit.ps1`: 実装後の作業ツリーで 2 回、`AUDIT_RESULT kind=contract files=835 errors=0 warnings=0 checks=1,2,3,4,5,6,7,8,9 applicable8=False`。出力が短いので原出力の保全は不要とした。
  - `git diff --stat d176033a cf440201`: `tools/DebugStudio/` の変更は `DebugStudio.Cli/README.md` だけ、`protocol/` の変更なし（AC4 の diff 部分）。
  - 変更・新規の Unity 側 `.cs` は全て先頭が `#nullable enable`。`record` なし。asmdef・package の変更なし。
- 未実行:
  - **Editor コンパイル確認: 未確認。** この session では Unity CLI（`unity editors` / `open`）が承認されず Editor を起動できなかった（`tools/unity-editor.cmd status` は exit 6、Editor なし）。代替の `dotnet build`（生成済み csproj を import する一時 project。作った file は削除済み）も承認されず実行していない。型・名前解決・明確代入・nullable は手で読んで確かめたが、compile の証拠ではない。動作確認の最初の手順で確かめる（§0 の引継ぎ票）。
  - `pwsh tools/docs-audit.ps1`: 承認されず未実行（判定必須テストとして C でも実行する）。
  - Unity テスト（EditMode の新規・全回帰）と Player Build: Phase C の担当なので未実行。
  - B の出口の動作確認（§5.2 で B が軽く実行してよいとした V0〜V7）: Editor を起動できないので未実行。
- implementation head commit: cf44020199a2cc687ad7cf7a29b780661283bd25
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic、新規セッション）

### 動作確認

- 対象 head: implementation head `cf440201`（作業 worktree の HEAD は B の記録 commit `4f999815`。`cf440201` は HEAD の祖先で、間の差分は本 HANDOFF だけと照合済み。作業ツリー clean）。
- 実行担当・モデル・ベンダー: Claude Opus 5.5（Anthropic、新規セッション。B の session とは別）。合否と GO / NO-GO は書かない。
- 環境: 専用 worktree の Editor 6000.6.9f1（root が通常経路で開いた instance。wrapper の `status` で ready・対象 project が専用 worktree の `unity/` であることを確認）。WPF / CLI は V0 の Release build の出力（App・CLI とも build exit 0）。V0〜V7 は 1 つの実行 script で 1 回だけ流し、wrapper は 1 回に 1 invocation ずつ順に呼んだ。実行 2026-11-22T19:56:09Z〜19:57:11Z（UTC）。
- Editor コンパイル: Editor の log に `error CS` の行なし（script compilation が完了し、warning だけ）。V0 前の eval で `playing=False;compiling=False;updating=False;compileFailed=False;sceneCount=1;scene0=:dirty=False`。
- 生の観測（期待は §5.3）:

| 段 | 観測 |
|---|---|
| V0 | HEAD `4f999815`、`git status --porcelain` 空。App / CLI の Release build exit 0。WPF を `--listen` で直接起動（PID 27228）。起動前に他の DebugStudio の process なし |
| V1 | `editor_play` exit 0。`hierarchy` を 8 回読み、8 回目（約 9.0 秒）で `hasSnapshot:true`・`Connected`・`Negotiated`、`[DebugStudioVerificationTarget]` の node 1 つ、N1=`8`（flags `ActiveSelf, ActiveInHierarchy, SceneRoot`）。1〜7 回目は exit 1 の `no-snapshot`（`Connecting` / `Negotiating`） |
| V2 | 凍結 snippet の eval: `count=1 sameSceneAsUICommon=True activeSelf=True`。非対象 root `Directional Light`: hierarchy の root に 1 つ（X=`2`）、eval で `count=1`、EntityId EX=`3298534918567`、`activeSelf=True` |
| V3 | `send … '{"nodeId":8,"active":false}'` → stdout `CommandResult … success=true message=applied` と `{"nodeId":8,"activeSelf":false}`、exit 0。eval `count=1 sameSceneAsUICommon=True activeSelf=False`。`inspect 8` の `GameObject.ActiveSelf` rawValue `"false"`（targetName `[DebugStudioVerificationTarget]`）。読み直した hierarchy で対象 node 1 つ、NodeId `8`（flags `SceneRoot`） |
| V4 | `'{"nodeId":8,"active":true}'` → `success=true message=applied`、`{"nodeId":8,"activeSelf":true}`、exit 0。eval `activeSelf=True`（count=1、同じ scene）。inspect rawValue `"true"`。hierarchy で対象 node 1 つ、NodeId `8`（flags `ActiveSelf, ActiveInHierarchy, SceneRoot`） |
| V5 | `'{"nodeId":2,"active":false}'` → stderr `CommandResult … success=false message=rejected: not-verification-target`、stdout 空、exit 1。EntityId EX の root を eval で探して `count=1 name=Directional Light activeSelf=True`（V2 と同じ） |
| V6 | `editor_stop` exit 0、停止後の eval `playing=False`、Scene dirty なし。`editor_play` exit 0。Play2 開始後に capture された snapshot を 4 回目（約 3.8 秒）で取得し、対象 node 1 つ（NodeId `8`）。1〜3 回目は exit 1 の `no-snapshot`。送信なし、GUI 操作なし、eval で何も作っていない |
| V7 | `editor_stop` exit 0、eval `playing=False;compiling=False;updating=False;compileFailed=False;sceneCount=1;scene0=:dirty=False`。WPF に `CloseMainWindow`（true を返した）、保有 handle が 30 秒以内に signal（約 0.13 秒）、実 exit 0、強制終了なし。`git status --porcelain --untracked-files=no` 空（tracked の変更なし） |

- 期待との差: なし。B へ戻すものなし。
- 未実施: なし。
- 補足（期待との差ではない）: 実行 script が手順外で足した待受け port の確認（`Get-NetTCPConnection` で 5011 / 5012）は、WPF の起動中も空を返したので、port の観測としては使えない。他の WPF の有無は process の列挙（起動前に 0 件）で確かめた。CLI はこの WPF と通信し、Editor と同じ回の hierarchy を返した。
- 終了時の Editor: root が開いた instance（PID 41016）を開いたまま、ready・Play 停止・Scene clean。閉じていない。

## 7. Phase C

### 発見 C と判定 C の実行記録

- 引き継いだ commit は f68a6e7936f9945c6ec49271325ecb020d16b5be。implementation は cf44020199a2cc687ad7cf7a29b780661283bd25。間の変更はこの HANDOFF のみ。ローカル evidence tag で実装を固定。
- 新規の Grok 4.6 High（xAI）が、取得先で凍結仕様・完全な製品差分・B 記録・実操作の原記録を読んだ。発見 C の差し戻しなし。判定 C も同じモデルの別セッション。第1巡の C′ は下記 §8。
- 独立した Claude Opus 5.5 実行セッションが contract-audit、docs-audit（各 exit 0）、AC4 の diff、同一 head・空 filter の全 EditMode を実行した。対象 3 fixture の 37 件は全て Passed。文字列 "false" と null はどちらも false として適用され、結果と実値が一致した。
- 標準全回帰は 2202 件中 Passed 2175、Inconclusive 25、Skipped 2、Failed 0。25 件はすべて VFX の GraphicsDeviceType.Null。Unity 実 exit 2、Harness exit 1。
- 描画あり全回帰は 2202 件中 Passed 2200、Inconclusive 0、Skipped 2、Failed 0。2 件は名前を指定して別の checkout 条件で実行する既存 R8 テスト。最終 shutdown 出力後にプロセスが残った。45 秒隔てた同一 handle はいずれも未 signal、報告 exit code はいずれも 0、CPU 時間は増え、ログは不変だった。限定調査はこの 1 回で終了。今回の専用 batch process だけを強制終了し、signal を確認。実 exit 99、Harness exit 1。原因・スタックの同一性は未確定。無変更の全件再実行をしない。
- 実機は B と別セッション・同一実装 head の生記録（§6）を使用する。全回帰の非 0 終了は原記録のまま残す。判定 C は I1〜I3、E1〜E2、AC1〜AC5 と失敗経路を照合して GO。標準回帰の Inconclusive 25 は VFX に限られる。いずれの Harness run も成功とは記録しない。V0〜V7 の期待・生観測・出典ごとの照合表は判定記録に保存。別セッションの Grok 4.6 High（xAI）。実装、実機、回帰の checkout 間の差分が HANDOFF のみであることをオーケストレーターがローカル Git で確認した。

## 8. Phase C'

- 第1巡は Grok 4.7 256K Extra High（xAI）の新規セッション。判定 C と同じ固定 bundle を別途取得し、可変 HANDOFF と PR の会話は渡していない。モデルは C と異なるが同一ベンダー・モデル系列なので独立性制約あり。
- 判定は NO-GO。実操作とテストの実行 checkout が implementation head と異なり、製品ファイルが同一であることを、監査担当が bundle の Git 原出力から検証できなかった。機能の観測そのものは凍結時の期待と一致。別の所見として、README が非 bool の拒否を約束しているが実測では文字列と null が変換された。
- 再開位置は同じ実装での第2巡。第1巡の原記録200ファイルの hash を全件照合したうえで、実装・実機・テストの各 commit の親、tree、product diff、ancestor 検査の Git 原出力を追補した共通 bundle を用意した。製品変更・Unity 再実行なし。判定 C と新規 blind C′ の双方に同一の取得経路で引き渡す。README の実測に関する訂正文は B 担当が D の harvest 向けに用意した。

### 第2巡

判定 C は Grok 4.6 High の同じ判定セッションで Git の原出力を読んだ。C′ は予約モデル Grok 4.7 256K Extra High の別の新規セッションで、同じ判定入力を独立に取得・閲覧した。base と implementation head は第1巡と同じ。原記録の閲覧範囲と限界を含む各担当の所見は blind 入力と分けて保存する。

両者が V0〜V7 の製品コードの同一性と I1〜I3・E1〜E2 の成立を確認した。C′ は第1巡の帰属の疑義を解消し GO。全 EditMode の標準実行と描画あり実行が Harness として非 0 であることを、両者とも合格に読み替えていない。C′ は Player 実行を扱わず、Harness 停止規則の原文を bundle 内では照合できなかった。オーケストレーターは停止規則と終了観測原本を別途照合した。

## 9. Phase D

実装の変更は終了。PR の base は develop。owner 承認後の harvest は CLI README の非 bool 入力の説明を実測に合わせ、削除前の HANDOFF と所見の保存・取得を確認して本票を削除する。標準全回帰の VFX 25 件と描画あり終了 stall は原記録のまま引き渡す。Play 世代の識別は owner の方針どおり後続。マージと task 終了は未実施。
