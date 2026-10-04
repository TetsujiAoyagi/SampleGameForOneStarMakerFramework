# INPUT_SYSTEM_MANAGER

- type: slice
- status: B
- branch: cursor/input-system-manager-159b
- implementation base commit: 2c29c99806788406551affba6cc795e67e614748
- implementation head commit: 未記入
- risk: high
- owner: 未割当（このクラウドセッションが A1 と実装を担当。A3 の人間はいない）
- created: 2026-10-04
- expires: 2026-11-04
- harvest to: unity/Assets/Docs/Architecture/07-09-services.md の §8 と、unity/Assets/Docs/Architecture/32-accessibility-input-dof.md の「InputManager 待ち」の現状文。マージ時まで公開面は変えない
- Phase A snapshot path / id: docs/handoff/input-system-manager/phase-a-snapshot.md
- Phase A snapshot generated at: 2026-10-04T01:39:23Z
- Phase A snapshot hash: 03ffdabf2100511bf4ee486b792d69a473d561ff110fbf6a5d8bc85142467c3a
- Phase B result snapshot path / id: 未記入
- Phase B result snapshot generated at: 未記入
- Phase B result snapshot hash: 未記入
- evidence bundle path / id: 未記入
- evidence bundle generated at: 未記入
- evidence bundle hash: 未記入
- C' blind bundle path / id: 未記入
- C' blind bundle generated at: 未記入
- C' blind bundle hash: 未記入

## 1. 目的と対象外

- 目的: OneStarMaker の入力マネージャが、既存の Unity Input System アセットからアクション単位の現在値を読み、SceneState が Stable のあいだだけ公開し、それ以外では中立値を公開する。Player と UI はアクションマップの切替として持ち、プロファイル ID の枠だけを残す。
- 対象外:
  - SampleGame の FlyController の置換、および SampleGame や AppInitializer からこのマネージャへの接続
  - Game.Common の NewStgCommonInput と、ゲーム固有アクション enum
  - 二つ目の InputActionAsset
  - UI のフォーカス、ヒント、ラベル、タブ順
  - リマップ UI、触覚、ワールド巡回、片手プロファイルの中身
  - AI による同じ値形の生成、疑似デバイス入力、キャラクターコントローラ
  - ボタンのエッジ（押したフレームだけ真）の公開
  - Vector3 / Quaternion の公開
  - SceneDirector の購読と、どのシーンを操作対象にするかの決定
  - アセットを IAssetManagement でロードすること
  - 公開アーキテクチャ文書の更新（harvest は Phase D）
  - オープンな PR #86（ScriptSystem）と PR #87（DebugStudio）のファイル
- 現況:
  - develop の HEAD は 2c29c99806788406551affba6cc795e67e614748。InputManager の実装は無い。
  - [07-09-services.md](../../unity/Assets/Docs/Architecture/07-09-services.md) §8 は InputManager と InputObserver、その先の NewStgCommonInput を図にしている。コードにはどちらも無い。図は未着手の目標であり、このスライスはマネージャで止める。InputObserver という独立型は作らない。マップ切替と、マップが変わったときの R3 通知はマネージャが持つ。
  - [32-accessibility-input-dof.md](../../unity/Assets/Docs/Architecture/32-accessibility-input-dof.md) はプロファイル本体を InputManager に置くが、「今やらない」にプロファイル / リマップ UI、ワールド巡回、UI の初期フォーカス、触覚を挙げている。片手は同時自由度を落とすことで、このスライスの仕事ではない。
  - [SceneState.cs](../../unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneState.cs) の Stable = 8 のコメントは「ユーザー操作を受け付けられる唯一の状態」である。ユーザー制約と一致する。14 値の順序は変えない。
  - Update の層順は UpdateLayerIds に Camera = 50、Streaming = 60 がある。ゲームプレイ用の共有定数は無い。UpdateBehaviourAdapter の既定 LayerOrder は 0。入力をそれらの前に置く定数は無かった。
  - UpdateSystemHost は、不安定なシーンが残っているあいだ新規登録の active 化を止める。すでに active な Element の Update は止まらない。入力の公開をこの active 化ゲートだけに頼ると、遷移中に最後のサンプルが残る。公開の中立化は Element が Update を受け続けることが前提である。
  - イベントの既存形は R3 である。SceneDirector は Subject でシーンイベントを流し、UI は ReactiveProperty を使う。毎フレームの値ストリームは無い。
  - com.unity.inputsystem 1.20.0。ProjectSettings の activeInputHandler は 1（新しい Input System）。既存アセットは unity/Assets/InputSystem_Actions.inputactions。マップは Player と UI だけ。アクションの expectedControlType は Button、Vector2、それに UI の TrackedDevicePosition（Vector3）と TrackedDeviceOrientation（Quaternion）である。
  - OneStarMaker.Runtime は既に Unity.InputSystem を参照している。asmdef の新規追加はしない。
  - FlyController は Input System を直接読んでおり、このマネージャの消費者ではない。ファイルは変更しない。
  - 上記のコードと文書は、合意した製品制約を否定していない。否定ではなく、文書の図より狭い実装で止める。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: ゲーム固有 enum を Runtime に置かず、既存アセットのアクション現在値を、Stable のあいだだけ割り当てなしで公開し、Player / UI の切替とプロファイル枠をマネージャに持たせられるか。
- 進める最低条件:
  1. 公開値はアクションの席であり、物理キーではない。Runtime にゲーム固有アクション enum と NewStgCommonInput が無い。
  2. 渡された SceneState が Stable のとき、有効マップのサンプルが公開される。それ以外では全部の席が中立（X = 0、Y = 0）で、直前の Stable の量を残さない。
  3. Player と UI の切替はマネージャのアクションマップ切替である。UI のフォーカス、ヒント、ラベル、タブ順は無い。
  4. プロファイル ID の枠があり、受理されるのは既定 ID "default" だけである。未知の ID は拒否され、バインドは変わらない。片手プロファイルの中身、リマップ UI、触覚、ワールド巡回は無い。
  5. サンプルは UpdateSystem の Update で、Layer 名 Input、LayerOrder -100 に登録できる。これは UpdateBehaviourAdapter の既定 0、Camera 50、Streaming 60 より前である。
  6. サンプルと公開の定常経路で GC 割り当てが 0 である。
  7. SampleGame と FlyController と AppInitializer は、このマネージャへ接続されていない。
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  1. 公開形 InputActionValue は Index、Map、Kind、X、Y を持つ。Kind は Button と Axis2 だけである。名前解決は文字列とマップの組で、毎フレームの公開読み取りはインデックスである。
  2. SceneState の 14 値のうち IsAccepting が真なのは Stable だけである。未定義の整数も偽である。非 Stable の Sample はデバイスリーダーを呼ばず、公開バッファを席の識別子付きの零で書き直す。Stable に戻ったあとの値は、その時点のサンプルであり、遷移前の量ではない。
  3. 有効マップ以外の席は、リーダーが非零を書いても公開時には零である。TrySetMap は Player と UI だけを受理し、変わったときだけ R3 の MapChanged を 1 回流す。同じマップの再設定はイベントを出さず、デバイスの Enable もやり直さない。
  4. TrySelectProfile は InputProfileId.Default（値 "default"）だけを真にする。それ以外は偽で、ActiveProfile は Default のままである。
  5. TryRegister は UpdateLayerIds.Input と InputLayerOrder で 1 回だけ成功する。LayerOrder 0 の Element より前の Update でサンプルが走る。
  6. Release 構成のオフライン測定で、安定と非安定を含む 2000 回の Sample 経路の割り当てバイト数が 0 である。EditMode の同種測定は判定 C の対象である。
  7. 差分に SampleGame、FlyController、AppInitializer の変更が無い。Runtime の InputSystem ソースに SampleGame、FlyController、NewStg、UnityEditor が無い。
  8. 既存 inputactions を二つ目のアセットにせず、Player / UI の Button と Vector2 を席にする。Vector3 と Quaternion は席にせず、UnsupportedActionCount に数える。
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
  - ゲーム固有 enum と SampleGame への接続: 後続スライス INPUT_GAME_ACTIONS
  - どのシーンの SceneState を渡すか、SceneDirector 購読、起動時の TryRegister と active 化: 後続スライス INPUT_SCENE_BINDING
  - アセットのロードと AssetOwner: 後続スライス INPUT_SCENE_BINDING
  - 片手プロファイルの中身（巡回と決定 1 つ）: 後続スライス INPUT_ONE_HANDED_PROFILE
  - リマップ UI と触覚: 後続スライス INPUT_BINDING_UI
  - Vector3 / Quaternion を公開形に入れるか: 後続スライス INPUT_POSE_ACTIONS
  - 同じ InputActionValue を出す AI: 後続スライス INPUT_AI_PRODUCER
  - エッジ（押下フレーム）の公開: 後続スライス INPUT_ACTION_EDGES
  - 他システムが InputLayerOrder 未満を選べないようにする強制: 後続スライス INPUT_LAYER_GUARD
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: 進める最低条件の証拠が揃い、その条件と常時契約への致命的な反証が無いとき GO。証拠が欠ける、または反証があるとき NO-GO。CONDITIONAL ACCEPT は使わない。このクラウドセッションは GO を記録しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし
- 本文へ転記した実装制約:
  - 依存は Game から Framework の一方向。このスライスは Runtime と、テスト用に OneStarMaker.Tests へ Unity.InputSystem を足すこと以外、asmdef を増やさない。Runtime は元から Unity.InputSystem を参照している。テストアセンブリの参照は推移しないため、公開コンストラクタが InputActionAsset を取る以上、テスト側の参照が要る。
  - アセットは呼び出し側が所有する。マネージャは Destroy しない。Dispose では自分が有効にした Player と UI を両方無効にする。
  - SceneState の既存 14 値は減らさず並べ替えない。状態変更の所有者は SceneLifecycleManager のままである。マネージャは渡された値を読むだけである。
  - 公開 API に ZLogger 型を出さない。定常経路ではログしない。
  - Update の登録先は UpdateSystem である。1 フレームの中では、入力 Element は Update でサンプルし、LateUpdate では公開値を変えない。
  - 1 つの UpdateSystem の例外で他の Tick を止めない、は既存の UpdateCoordinator の契約である。サンプル定常経路は例外を投げない。
  - Editor コードを Runtime に置かない。
  - Unity 側の C# で record を使わない。
  - 新規と編集した Unity 側 .cs の先頭は #nullable enable。
  - 破棄されうる UnityEngine.Object には == null を使う。InputActionAsset はこれに該当する。InputActionMap と InputAction は UnityEngine.Object ではない。
  - テストで Task.Delay と Thread.Sleep を使わない。
  - 参照 0 を削除理由にしない。既存の FlyController は残す。
  - PR の base は develop。main と develop へは push しない。
- 未決事項: なし

このスライスの凍結は、A2 の複数モデルレビューと A3 の採否を経ていない。ユーザーが、その未実施を記録したうえでこの本文を実装境界にするよう指示した。これは A3 の採否記録ではない。凍結後に設計判断が増える場合は実装を止め、本文の対象外へ送る。

## 3. 責務マップ

develop 時点の行数と、この凍結で置く行数。500 行、3 責務、既存ファイルの 50% 以上の増加は UpdateLayerIds だけが該当する。

- UpdateLayerIds.cs（24 行 → 37 行、約 54% 増）
  - 責務: Runtime が共有する Layer 名と順序の定数。
  - 変更理由: 入力をゲームプレイ既定順と Camera より前に置く数を、呼び出し側のばらばらなリテラルにしない。
  - 所有者・寿命: 定数。寿命はアプリのコンパイル単位。
  - 依存: 無し。UnityEngine を参照しない。
  - 公開面: Input、InputLayerOrder。既存の Camera と Streaming の値は変えない。
  - テスト境界: オフラインで定数の大小を見る。
  - 配置理由: 既存の共有表である。
  - 非分割: 増分は定数 2 つと説明である。絶対行数は 37 で、表をファイル分割すると順序の所在が再び分かれる。分割しない。

- InputControlMap.cs（新規 18 行）
  - 責務: Player と UI の二値。
  - 所有者・寿命: 値型。永続化しない。
  - 依存: 無し。
  - 公開面: enum。
  - テスト境界: 未定義整数は拒否される。
  - 配置: Runtime/InputSystem。ゲーム enum ではない。

- InputActionMapNames.cs（新規 15 行）
  - 責務: 既存アセットが既に持つマップ名 "Player" と "UI"。
  - 公開面: 定数。アクション名は持たない。

- InputActionValueKind.cs（新規 17 行）と InputActionValue.cs（新規 58 行）
  - 責務: 公開するレベル。Button は X、Axis2 は X と Y。中立は両方 0。
  - 所有者・寿命: 値型。バッファの要素。呼び出し側がコピーすれば Sample をまたいで残る。
  - 依存: InputControlMap のみ。
  - 公開面: 構造体。物理キーやデバイス ID は持たない。
  - テスト境界: 等値と比較はオフラインで足りる。
  - 配置理由: 後続の AI も同じ形を出す。デバイス型にしない。

- InputProfileId.cs（新規 45 行）
  - 責務: プロファイル枠。受理文字列は "default" だけ。
  - 公開面: 構造体と Default。バインド適用メソッドは無い。
  - テスト境界: 既定は真、それ以外は偽。

- InputActionSlot.cs（新規 33 行）、InputActionNameKey.cs（新規 37 行）、InputActionKindMap.cs（新規 35 行）
  - 責務: 束ね時の席、名前とマップの検索キー、expectedControlType から Kind への写像。
  - 公開面: internal。
  - 依存: 文字列比較のみ。Unity 型は使わない。
  - テスト境界: Button と Vector2 だけが真。Vector3、Quaternion、大文字小文字の違いは偽。

- InputAcceptance.cs（新規 18 行）
  - 責務: SceneState が Stable のときだけ操作を公開する。
  - 依存: SceneState。SceneLifecycleManager は呼ばない。
  - テスト境界: 14 値と未定義整数。

- InputPublication.cs（新規 58 行）
  - 責務: 公開バッファを割り当てなしで零化、有効マップ以外を零化、同長のときだけコピーする。
  - テスト境界: オフライン。長さ不一致はコピーしない。

- IInputDeviceReader.cs（新規 15 行）、IInputMapControl.cs（新規 15 行）
  - 責務: デバイス読み取りと、マップの有効化。AI の注入口にはしない。
  - 公開面: internal。
  - テスト境界: 偽リーダーでフレームを駆動する。

- InputFrame.cs（新規 165 行）
  - 責務: 公開スナップショット。有効マップ、プロファイル ID、渡された SceneState、サンプルバッファと公開バッファ。
  - 変更理由: これらは同じ「今フレーム何を公開するか」を決める。
  - 所有者・寿命: マネージャが生成し、マネージャと同時に捨てる。アセットは持たない。
  - 依存: 上記の純粋型と SceneState。UnityEngine と R3 は参照しない。
  - 公開面: internal。
  - テスト境界: Unity 無しのオフライン実行。
  - 配置理由: Policy を Unity I/O から離す。
  - 非分割: プロファイル、マップ、受理、バッファは同じテスト方法（純粋関数）と同じ寿命である。プロファイルのバインド適用はここに置かない。行数は 500 未満で、50% 増加の対象になる既存ファイルでもない。分割しない。

- InputActionAssetReader.cs（新規 135 行）
  - 責務: InputActionAsset の Player / UI を束ね、Button と Vector2 だけを席にし、片方のマップだけを Enable する。
  - 所有者・寿命: マネージャが所有する読み取り口。アセット自体の所有者は呼び出し側。Dispose 相当の Disable はマップを無効にするだけで、アセットは Destroy しない。
  - 依存: UnityEngine.InputSystem。束ね時の List は許す。Read のループは割り当てない。
  - 公開面: internal。
  - テスト境界: Unity コンパイルが要る。この環境では未コンパイル。Kind の写像と、既存 JSON の形はオフラインで固定する。
  - 配置理由: Unity I/O を InputFrame に混ぜない。

- InputManager.cs（新規 243 行）
  - 責務: フレーム、リーダー、R3 の MapChanged、Update 登録の接続。
  - 所有者・寿命: 呼び出し側が new し Dispose する。DontDestroyOnLoad しない。シーンには属さない。
  - 依存: InputFrame、リーダー、R3、UpdateCoordinator、UpdateLayerIds。
  - 公開面: コンストラクタ（InputActionAsset）、ActiveMap、ActiveProfile、InteractionState、MapChanged、Published、ActionCount、UnsupportedActionCount、TryGetActionIndex、TryRead、SetInteractionState、TrySelectProfile、TrySetMap、TryRegister、Sample、Dispose。
  - テスト境界: EditMode は偽リーダーと UpdateCoordinator で登録順、通知、中立化、割り当てを見る。内部コンストラクタは OneStarMaker.Tests にだけ見える。
  - 配置理由: §8 のマネージャの場所。orchestration を Policy と分ける。
  - 非分割: 入れ子の Update Element とテスト用の空マップ制御は、マネージャの寿命と登録のためだけの private 型である。外へ出すと公開面が増える。243 行で閾値未満。分割しない。

- OneStarMaker.Tests.asmdef
  - 変更理由: 公開コンストラクタの InputActionAsset をテストアセンブリが解決する。
  - 依存: 参照配列へ Unity.InputSystem を 1 件追加する。Runtime の参照は変えない。
  - テスト境界: テストアセンブリだけ。

- OneStarMaker.Tests/InputSystem/InputManagerTests.cs（新規 240 行）
  - 責務: マネージャの EditMode 検証。
  - 依存: NUnit、R3、Runtime、Foundation。実時間待ちはしない。

- tools/InputSystemOfflineTests
  - 責務: Unity Editor 無しで純粋側を実行する。製品アセンブリではない。
  - 依存: net8.0。連結コンパイルは SceneState、UpdateLayerIds、Unity を参照しない InputSystem のソースだけ。
  - テスト境界: 割り当て、SceneState、既存 inputactions の JSON、ソースの禁則語。

## 4. 実装計画

- 変更対象: 上記のファイルだけ。既存 inputactions、SampleGame、AppInitializer、FlyController、アーキテクチャ文書、PR #86 と #87 のパスは変えない。
- 順序: 純粋型、フレーム、アセットリーダー、マネージャ、EditMode テスト、オフライン実行、共有 Layer 定数。
- Phase B から Phase A へ差し戻す条件: 凍結に無い状態、asmdef 参照、所有者、寿命、公開 API が必要になったとき。計画した配置で中核の公開規則をオフラインテストできなくなったとき。ゲーム固有 enum、二つ目のアセット、SampleGame 接続、プロファイル本体が必要になったときは実装せず対象外へ送る。
- 対象外を維持する方法: マネージャはシーンを購読しない。アセットをロードしない。アクション名の enum を宣言しない。SampleGame のファイルを差分に入れない。Kind の写像は Button と Vector2 だけを真にする。

## 5. テストとレビュー計画

- 単体テスト: InputFrame と公開規則は tools/InputSystemOfflineTests を `dotnet run -c Release --project tools/InputSystemOfflineTests` で実行する。マネージャの登録と R3 は OneStarMaker.Tests.InputSystem。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: OneStarMaker.Tests.InputSystem
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: `pwsh tools/run-tests.ps1` をフィルタ無しで実行し、結果 XML の失敗 0 かつ実行 1 件以上。オフライン実行は判定必須の代替にしない。
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし
- 統合・Unity テスト: 上記の全 EditMode。PlayMode と Player は凍結条件に無い。
- 操作・実行時・目視条件の検証経路（条件ごとの担当、環境・初期状態、操作、観測と合否、対象版・保存証拠・C / C' への受け渡し。既存テストは参照で可）: プレイヤー操作は条件に無い。API 条件はテスト名で閉じる。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点（未知経路が無い場合だけ `なし`。Phase C へ委譲する場合は「未確認。初回確認は Phase C」と理由・担当・確認地点、不成立時の対応）: なし
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方（観察記録の受理 / 画像の独立再評価。無ければ `なし`）: なし
- 機械検査: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: このクラウドセッション。Grok 4.7。xAI。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施。複数モデルへの独立レビューは、このクラウドセッションでは行っていない。
- A3 統合担当・モデル・採否: 未実施。人間が指摘を採用、不採用、保留に分類していない。ユーザー指示により、その未実施を記録した本文を実装境界にした。
- C' 用に予約した担当・モデル・ベンダー: Grok 以外の系列。新規セッション。このセッションは A と B に Grok 4.7 を使った。判定 C の Unity XML が同じ implementation head に付くまで C' は開始しない。
- 独立性の強化条件を満たせない場合の理由: A2 を行っていないため、Phase A がモデル系列を使い切ってはいない。C' の予約は残っている。このセッションの自己確認を独立した Phase C または C' と呼ばない。

## 6. Phase B 実装結果

- 実装: 未実施
- HANDOFF との差: 未実施
- 未実行: 未実施
- implementation head commit: 未実施
- Phase B 担当・モデル・ベンダー: 未実施

## 7. Phase C

- 種別: 未実施
- evidence bundle id / hash: 未実施
- 構造適合: 未実施
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: 未実施
- 後続スライスへ移送する findings: 未実施
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: 未実施
- テスト結果（XML 上の実行テスト名と件数）: 未実施
- 判定必須のうち未実行: 未実施
- 重い検証を発見段階で限定実行した場合の理由と範囲: 未実施
- 未確認事項: 未実施
- 担当・モデル: 未実施

## 8. Phase C'

- 担当方式: 未実施
- blind audit bundle id / hash: 未実施
- 確認範囲・方法（全件機械検査 / 代表箇所の目視・操作等）: 未実施
- 判定（人間担当は本人の明示回答まで未実施）: 未実施
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: 未実施
- 後続スライスへ移送する findings: 未実施
- 残存リスク: 未実施
- 監査できなかった範囲: 未実施
- 独立性: 未実施
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: 未実施
- 担当・モデル: 未実施

## 9. Phase D

- C / C' の突合: 未実施
- マージ判断: 未実施
- harvest: 未実施
- 削除確認: 未実施
