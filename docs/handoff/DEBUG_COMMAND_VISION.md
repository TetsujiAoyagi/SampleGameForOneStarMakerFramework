# デバッグコマンドと観測の基盤 — 構想

## 0. メタデータ

- type: `research`（構想。**未承認。実装キューではない。** ここに書いた物を作ることを決めた文書ではない）
- status: 2026-11-25 の初稿。owner の確認前
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2027-02-28、または構想を置き換える revision。期限時は owner が更新・廃止を判断する
- harvest to: 各段階のスライスが実装・実証した契約だけを `unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（コマンドの口・公開範囲・出力）、`05-scene.md` §5.12（Scene の観測）、`tools/DebugStudio/src/DebugStudio.Cli/README.md` へ移す。構想そのものは公開面へ移さない
- 起点: [DEBUGSTUDIO_S4_FRAMEWORK_STATE.md](DEBUGSTUDIO_S4_FRAMEWORK_STATE.md) の計画の過程（revision 1〜3）で出た、S4 では作らない基盤の論点を集めた。owner の依頼「debugcommand 基盤整備についてビジョンがあれば折角なのでまとめて欲しい」（2026-11-25）
- 実測: base `07cc2c97` の code を読んだだけ。Unity も .NET も起動していない

## 1. 何のための基盤か

OSM は、複雑なゲームを載せても Scene・寿命・UI・Service・ロードが一緒に複雑化しないための境界・寿命・依存の契約を公開する（[GOALS_AND_STRENGTHS.md](../GOALS_AND_STRENGTHS.md)）。デバッグコマンドと DebugStudio は、その**契約が実際に守られている様子を、動いているゲームの外から見る窓**である。

- 人が見る: 今どの Scene がどの状態か、どの asset を誰が持っているか、どの UpdateSystem が動いているか。GameObject の Hierarchy では見えない、Framework の中の構造と寿命。
- AI が使う: 検証の driver と判定の担当が、log の文字列を拾う代わりに、構造化された状態を問い合わせて照合する（S4-V の検証の基盤が観測の口として使える）。
- Editor で触る: 開発中の確認のために状態を変える（今は `debug.set-active` だけ）。

## 2. 原則

1. **観測が主、操作は従。** まず読めるようにする。書き込みは例外で、既定は Editor だけ。読み取りは何度送っても状態を変えない。
2. **機能が自分の診断を持つ。** 各機能（SceneSystem、AssetManagement、UpdateSystem など）が自分の状態の写しを作る口を持ち、コマンドの handler はその写しを入出力へつなぐ薄い adapter にする。登録は composition root（initializer）が組み立て、transport（`DebugSocketServices/`）は個々の機能を知らない。S4 の `SceneSystem/Diagnostics/` が最初の例。
3. **写しと変化を分ける。** 写しは「今どう在るか」、event は「どう変わったか・どれだけかかったか」。状態を持つ機能の観測の完成形は、Hierarchy と同じく「接続時の写し + 以後の event」を通し番号で突き合わせる形。最初は要求時の写し（pull）から始め、続けて見たくなったら push と WPF の保持へ進む。写しのモデルはコマンドや JSON から独立させ、pull と push で同じ物を使う。
4. **寿命を区別する。** コマンドの登録・接続の session・App と Play・handler が対象を解決する時点・Dispose の後に新しい実行を止める境界は、別々の物として扱う。handler は Scene 寿命・Play 寿命の物を登録の時に捕まえず、実行の時に解決する。書き込みのコマンドは、旧 Play の要求が新しい Play に当たらない仕組み（Play の世代）を持ってから増やす。
5. **公開範囲は配線の方針であって、登録の機構の意味ではない。** catalog は登録・名前の解決・実行だけを持つ中立の部品に保つ。Editor・Development Player・Release のどこにどのコマンドを出すかは composition root が決める。既定は閉じていて、Player へ出す物は明示して足す。
6. **機械が読む物と人が読む物を分ける。** transport の成否、名前の解決の成否、コマンドの処理の成否、取れない理由（機械向けの語）、人向けの説明、data を区別する。人向けの message の接頭辞を機械判定の恒久の契約にしない。
7. **決めすぎない。** 後から変えると、既存の Game の登録・利用者の script・README の手順・出力を読む側のどれかを書き直させる物だけを先に決める。足しても既存が今と同じに動く物は、契機が来てから足す。決めずに置くと後で破壊的にしか足せなくなる物は、先に決める側に入れる。
8. **デバッグの機構の失敗でゲームを止めない（原則）。** 登録の誤りは開発者に確実に知らせるが、ゲーム本体の起動を止めるかは環境ごとに意図して決める（Editor の開発時の fail-fast は選びうる）。

## 3. 今の姿（S4 の完了を仮定）

- 口: Game は `CreateDebugCommandDispatcher()` の override で自分の dispatcher（実際は `DebugCommandCatalog` + `CatalogDebugCommandDispatcher`）を作る。Framework は Editor でだけ内部の catalog を持ち、振り分けの dispatcher が Framework の名前を先に引く（S4）。
- コマンド: 組込みの `ping` / `runtime-diagnostics`（transport の層、main thread を待たない、大小文字を区別しない）。Framework の `osm.scenes`（読み取り、Editor だけ、S4）。SampleGame の `debug.set-active`（書き込み、Editor だけ、S3）。
- 観測: GameObject の Hierarchy は写しと差分の push、WPF の保持、CLI の `hierarchy`。Scene は要求時の写しだけ（`osm.scenes`）。Scene の変化は `OnSceneEvent` があるが外へ出していない（telemetry の span は流れている）。
- 出力: `send` の text の行と payload（コマンド）と、`status/reason/data` の JSON（`hierarchy` / `inspect`）の 2 系統。
- 発見: README だけ。

## 4. 段階（契機で進む。予定表ではない）

| 段 | 内容 | 進む契機の例 | その段で決めること |
|---|---|---|---|
| A. Scene の寿命の可視化 | `osm.scenes` の項目の追加（静的な木、進行中の操作、取消の窓）。`SceneEvent` と写しへの通し番号。Scene の写しと event の push、WPF の保持、CLI の `scenes` の read、時系列の表示（各段の所要時間を含む） | Scene の遷移の不具合を時系列で追いたくなった時 | 前提として、`OnSceneEvent` の通知と実際の遷移のずれ（`PreLoading` / `PreUnloading` が遷移の前に出る、`AfterUnloading` が `Removed` に載る、再開と失敗の後始末の経路）を確かめ、event だけで寿命を再構成できるかを決める。telemetry の span との役割の分担 |
| B. 他の機能の観測 | `osm.assets`（所有者ごとの保持。材料は `IAssetDiagnostics`・`AssetRegistry.GetAllLoadedAssets`）、`osm.update`（layer と登録。`UpdateCoordinator` に列挙の口を足す）、Hierarchy の DontDestroyOnLoad | 個別に必要になった時 | 各機能の写しの口の形（原則 2） |
| C. read の出力の統一 | CLI に read 用の出力（`status/reason/data`）を足し、コマンドの写しを `data` に入れる。取れない理由を機械向けの語で返す | read のコマンドが 2 つ以上になった時、driver が使い始めた時 | 結果の構造（原則 6）。`send` の出力は変えない |
| D. 共通の登録の口 | Framework と Game が同じ catalog に登録する hook（例: `RegisterDebugCommands(DebugCommandCatalog)`）。古い `CreateDebugCommandDispatcher` からの移行 | Game のコマンドが増えた時、Framework と Game の名前の衝突が実際に起きた時 | 名前空間（下の §5）、登録の順と衝突の扱い、登録の失敗の方針（原則 8）。hook の引数は catalog の object そのものにする（宣言付きの登録などを overload で足しても hook の signature が変わらない） |
| E. 自己説明 | 宣言（名前・説明・読み取りか書き込みか・使える場・入力と出力の例）を catalog の overload で足し、`osm.commands`（一覧・help）を足す。宣言なしの登録は名前だけ出す | コマンドが README で追えなくなった時、AI が動的にコマンドを選ぶ・自動検証が一覧を要る時 | 宣言の項目。README との役割の分担 |
| F. 寿命と公開範囲 | Scene 寿命のコマンド（登録の解除の handle。登録も実行も main thread なので「初期化の時だけ」を緩められる）。書き込みのコマンドの Play の世代。Development Player への公開（明示して足す登録） | Scene ごとのデバッグ操作が要る時、Player でデバッグしたい時（program の S4 系の後） | 公開範囲の方針（原則 5）。既存の Editor 用の登録が Player へ漏れない足し方 |

順は固定しない。A と B は観測を増やし、C〜F は口を整える。口の整備は、観測を足す作業の中で具体的に困った時に始める。

## 5. 名前空間（現状と方向）

- 組込み（transport の層）: `ping`・`debugsocket.ping`・`runtime-diagnostics`・`debugsocket.runtime-diagnostics`。router が大小文字を区別せずに catalog より先に処理する。catalog に同じ名前（大小文字違いを含む）を登録しても socket からは届かない。
- Framework: `osm.` で始める（S4 の `osm.scenes` から）。機能ごとに `osm.<機能>` を基本とし、操作が増えたら `osm.<機能>.<動詞>` を検討する。
- Game: `osm.` と `debugsocket.` で始めない。SampleGame は `debug.` を使っている。
- catalog の名前は Ordinal の完全一致。組込みとの大小文字の扱いの違いは、D で口を整える時に揃えるか、規約として明記するかを決める。
- 「登録できるが呼び出せない名前」（組込みと重なる名前）をどう扱うかは D で決める。今は規約と README だけ。

## 6. 先に決めておくと後が楽な物（S4 の時点で守っている物）

- 写しのモデルを機能の側に置き、コマンドと JSON から独立させる（pull と push で共有）。
- 出力は JSON の object。列挙値は名前。項目は足すだけで、名前を変えない・消さない・意味を変えない。列の順を書く。
- 入力は JSON の object。将来の引数は省略できる field として足し、省略した時は今と同じ結果にする。
- handler は Scene 寿命・Play 寿命の物を実行の時に解決し、static の状態を持たない。
- Framework の名前は `osm.`。

## 7. 外部の意見から受けた入力（2026-11-25、S4 の計画のレビュー。どれも S4 では作らない）

- 登録の機構と公開の方針の分離（原則 5、段 F）。
- コマンドを足す開発者の体験: 機能ごとに、どの file を足し・どこを編集するか。例として `osm.assets` と Game 固有のコマンドを足す時の手順を、段 B か D の計画で書く（原則 2）。
- 名前空間と組込みの大小文字、登録できるが呼び出せない名前（§5、段 D）。
- 寿命の区別と、旧 Play の handler が新しい Play に当たる問題（原則 4、段 F）。
- 登録の失敗でゲームの起動を止めるかの方針（原則 8、段 D）。
- 機械可読な入出力の基本契約（原則 6、段 C）。
- 発見のしやすさ: 当面は README を台帳とし、コマンドを足す時に README も書くことを作業に含める。一覧・help の導入の契機は段 E。
- 宣言 5 項目と一覧を先に作らないこと、後から overload で足せること（段 E）。

## 8. owner に確かめたい問い

- 段の優先: Scene の寿命の可視化（A）を、他の機能の観測（B）より先に進めるか。
- 書き込みのコマンドをどこまで増やしたいか（増やすなら段 F の Play の世代を先にする）。
- Player でのデバッグの需要と時期（段 F）。
- WPF で見ることをどの程度重視するか（段 A の push と保持の要否を左右する）。
