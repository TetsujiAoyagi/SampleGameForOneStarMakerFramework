# Test assembly split

- type: `slice`
- status: `C'`
- branch: `cursor/split-framework-app-tests-a5ed`
- implementation base commit: `bc73119`
- implementation head commit: `3467fad9dfaf2af101d99c0c3a9742724eeedee8`
- risk: `high`（テスト asmdef の依存向き）
- owner: 実装担当（このスライス）
- created: 2026-09-25
- expires: マージ時に harvest して削除
- harvest to: `unity/Assets/README.md` と `unity/Assets/Docs/Architecture/27-folder-structure.md` の依存図。本文の判断はそこへ既に反映済み。残件は Phase D で削除のみ
- Phase A snapshot path / id: このファイルの「## 2」まで
- Phase A snapshot generated at: 2026-09-25T15:40:00Z
- Phase A snapshot hash: 実装 commit 時点のこのファイル hash を Phase B 欄へ書く
- Phase B result snapshot path / id: このファイルの「## 6」
- Phase B result snapshot generated at: 2026-09-25T15:40:00Z
- Phase B result snapshot hash: 同上
- evidence bundle path / id: `artifacts/test-assembly-split-phase-c/`
- evidence bundle generated at: Phase C 開始時
- evidence bundle hash: Phase C で記入
- C' blind bundle path / id: `/tmp/cprime-blind`（phase-a-and-b.md、diff-stat、name-status、contract-audit.txt。実装 diff は `bc73119..3467fad`）
- C' blind bundle generated at: 2026-09-25T15:35:27Z
- C' blind bundle hash: `dccb746f980c0385514eb1effb4e70c5db5a0b62dc767eff5b783fbfe1da1297`（phase-a-and-b.md）

## 1. 目的と対象外

- 目的: Framework のテストアセンブリが App（SampleGame）を参照している状態をやめ、アプリのテストを SampleGame 側のテストアセンブリへ移す。
- 対象外: 本番の Scene / Prefab / Addressables の再生成。テストの意味を変えるリファクタ。`RetiredAddressablesMenuNoOpTests` が retired メニューの副作用として `SampleGame/Config/app-config.json` のバイト列を見ることは、型参照ではないのでこのスライスでは残す。namespace の一括改名はしない。
- 現況: `OneStarMaker.Tests` が `SampleGame.DependOnAll` / `InGame` / `OutGame` を参照し、`OneStarMaker.Tests.Editor` が `SampleGame.DependOnAll.Editor` と `SampleGame.InGame` を参照している。対象のテストは SampleGame の型か、SampleGame の編集用シーンを直接検証している。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Framework のテストアセンブリから SampleGame へのコンパイル依存をゼロにできるか。
- 進める最低条件:
  1. `OneStarMaker.Tests` と `OneStarMaker.Tests.Editor` の asmdef references に SampleGame が無い。
  2. 移したテストの subject は SampleGame の型、または SampleGame 配下の編集用シーンである。
  3. フレームワーク側に残るテストは、SampleGame 型を呼ばずに同じ identity 文字列を作る。
  4. 依存の向きは App テスト → Framework テスト / Framework 実装であり、逆向きの asmdef 参照を足さない。
  5. 判定時に全 EditMode（空 filter）が 1 件以上、failed 0、unexpected skip 0。
- 受け入れ条件:
  - 上記 1〜4 を asmdef と `using SampleGame` の所在で確認できる。
  - `InternalsVisibleTo` は、移したテストが今まで見ていた internal を、移先のテストアセンブリにだけ付け替える。新しい本番 API は増やさない。
  - フレームワークの格子フィクスチャは `Cell_{x}_{y}` をローカルに作り、`CellIdentity` を参照しない。
  - `pwsh tools/contract-audit.ps1` が違反なし。
  - 全 EditMode の生 XML で実行件数 > 0、failed 0、skipped 0。
- ここでは答えない問いと所有する後続スライス:
  - namespace を `SampleGame.Tests.*` へ揃えること。所有: 後続のテスト整理スライス（この HANDOFF の harvest 後に切る）。
  - `RetiredAddressablesMenuNoOpTests` の設定ファイルパスをアプリ側へ移すか。所有: 同上。型参照は無い。
  - フレームワーク internal をアプリテストへ見せる `InternalsVisibleTo` を、テスト専用の公開ポートへ置き換えるか。所有: 同上。このスライスは移設に必要な friend だけを追加する。
- 判定定義: 進める最低条件 1〜5 を満たせば GO。5 が未実行なら GO にしない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。人間は確認待ちをせず Phase C / C' まで進めるよう指示した。設計の凍結範囲は上記の最低条件に限る。
- 本文へ転記した実装制約:
  - 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断。
  - Editor コードを Runtime アセンブリに置かない。
  - Unity 側 C# の先頭は `#nullable enable`。`record` を使わない。
  - 破棄されうる `UnityEngine.Object` は `== null` / `!= null`。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。
  - 参照 0 だけを削除理由にしない。
  - テストは `pwsh tools/run-tests.ps1`。`unity test` / `unity run` は使わない。
  - Cloud で Editor が無い場合、Unity CLI を導入して接続済みにしない。
- 未決事項: なし（namespace 改名と retired menu のパスは後続へ送った）。

## 3. 責務マップ

| 対象 | 責務 | 依存 | テスト境界 |
|---|---|---|---|
| `OneStarMaker.Tests` | フレームワーク EditMode。SampleGame 参照を外す | Foundation, Runtime, Debug, TestSupport | このアセンブリ |
| `OneStarMaker.Tests.Editor` | フレームワーク Editor テスト。SampleGame 参照を外す | Editor, Build.*, Runtime | このアセンブリ |
| `SampleGame.Tests`（新） | アプリの EditMode。旧 `OneStarMaker.Tests` から移した実行時テスト | SampleGame の実行時 asmdef、Foundation、Runtime、`OneStarMaker.Tests`（internal ヘルパー） | このアセンブリ |
| `SampleGame.Tests.Editor`（既存を拡張） | アプリの Editor テスト。World / Build / OutGame 背景 | DependOnAll.Editor、OutGame、Framework の Editor / Build | このアセンブリ |
| `SceneDirectorTestBase` / `StreamingCandidateFixtures` | 格子 identity をローカル文字列にする | SampleGame を外す | `OneStarMaker.Tests` に残す |
| InternalsVisibleTo | friend の付け替え | 本番型の可視性はテストアセンブリだけ | コンパイル境界 |

新しい中核ロジックは無い。500 行超のファイル移動はあるが、責務の分割でありロジックの追加ではない。非分割理由: テストファイルは既存の責務のままアセンブリだけを移す。

`SampleGame.Tests` → `OneStarMaker.Tests` を選んだ理由: `SceneTestHelper` と `FakeAssetBackend` は internal のまま、アプリテストだけが使う。公開 API へ昇格させない。

## 4. 実装計画

- 変更対象: 上記 asmdef、テストファイルの移動、friend 属性、依存図の 2 文書、`tools/run-tests.ps1` の説明。
- 順序: ファイル移動、asmdef、friend、フレームワーク側の `CellIdentity` 除去、文書。
- Phase B から Phase A へ差し戻す条件: 移したテストをコンパイルするために SampleGame 以外の新しい本番型・新しい状態・新しい寿命が要る場合。
- 対象外を維持する方法: 本番アセットを編集しない。namespace を変えない。retired menu テストは残す。

## 5. テストとレビュー計画

- 単体テスト: 既存テストの移動のみ。新規ケースは足さない。
- 差し戻し中の起点 `-Filter`: `SampleGame.Tests|SampleGame.Tests.Editor|OneStarMaker.Tests.Streaming|OneStarMaker.Tests.SceneSystem`
- 判定必須テスト: `pwsh tools/run-tests.ps1`（Filter 空、EditMode 全件）
- 全 EditMode 回帰の適用除外: なし
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当: このセッションの実装担当
- A2 独立レビュー: 別モデルの事前レビューは、確認待ちなしで実装へ進める指示のため、発見 C を別モデルの新規セッションに割り当てる。事前の複数モデル A2 は未実施
- A3 統合: 上記最低条件で凍結。人間の追加確認は待たない
- C' 用に予約した担当: 判定必須テストの生結果が揃ったあと、Phase B と発見 C の両方と異なるモデル
- 独立性の強化条件を満たせない場合の理由: Editor が無い Cloud では判定 C の生結果が作れない。その間 C' は開始しない

## 6. Phase B 実装結果

- 実装: SampleGame を参照するテストを `unity/Assets/SampleGame/Tests` へ移した。`OneStarMaker.Tests` と `OneStarMaker.Tests.Editor` から SampleGame 参照を削除した。格子フィクスチャは `Cell_{x}_{y}` をローカル生成する。friend は移先アセンブリへ付け替えた。Runtime / Build.Selection / Build.Materialization / Build.Content には、移した Editor テストが既に使っていた internal を `SampleGame.Tests.Editor` へ見せる friend を追加した。
- HANDOFF との差: なし。
- 未実行: Unity Editor がこの環境に無い。コンパイル確認と EditMode は未実行。`contract-audit.ps1` は実装ツリーに対して違反なし。
- implementation head commit: `3467fad9dfaf2af101d99c0c3a9742724eeedee8`
- Phase B 担当: このセッションの実装担当（Grok）

## 7. Phase C

- 種別: 発見。判定 C は未実施。GO ではない
- evidence bundle id / hash: 固定 diff `bc73119..3467fad9dfaf2af101d99c0c3a9742724eeedee8`。`contract-audit.ps1` は head で違反なし。生テスト XML は無い
- 構造適合: フレームワークテスト asmdef から SampleGame 参照は 0。移したテストは SampleGame 型か SampleGame のシーンパスを対象にする。残った格子フィクスチャは `Cell_{x}_{y}` をローカル生成する。アプリテストからフレームワークへの参照だけが増えている
- 現在の問いを阻害する findings: なし
- 後続スライスへ移送する findings: namespace が `OneStarMaker.Tests.*` のままであること。`RetiredAddressablesMenuNoOpTests` の設定ファイルパス。どちらも HANDOFF の対象外と一致する
- 実行したテストコマンドと `-Filter`: 未実行。Unity Editor が環境に無い。Cloud へ Unity を入れて接続済みにはしない
- テスト結果: 未実行
- 判定必須のうち未実行: 全 EditMode（空 filter）
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし
- 未確認事項: コンパイル、EditMode。このため判定 C は開始しない
- 担当・モデル: 発見 C は実装と別セッションの Claude Sonnet。GO 判定はしていない

## 8. Phase C'

- 担当方式: AI
- blind audit bundle id / hash: `/tmp/cprime-blind`。phase-a-and-b.md の hash は `dccb746f980c0385514eb1effb4e70c5db5a0b62dc767eff5b783fbfe1da1297`。入力は凍結した Phase A/B と `bc73119..3467fad` の diff と contract-audit 出力
- 確認範囲・方法: asmdef の参照方向、移したテストの subject、friend 属性、編集した C# の機械契約を目視と機械検査で確認
- 判定: 構造の問いは PASS。全 EditMode は未監査のため、スライス全体の GO にはしない
- 現在の問いを阻害する findings: なし
- 後続スライスへ移送する findings: 新規 `.meta` の行末空白。既存 Unity meta と同じ空フィールド表記で、依存契約の違反ではない
- 残存リスク: Unity 上のコンパイルと全 EditMode は未実行
- 監査できなかった範囲: 全 EditMode（実行件数 > 0、failed 0、skipped 0）
- 独立性: Phase B は Grok、発見 C は Claude Sonnet、C' は GPT の別セッション。C' には発見 C の結論を渡していない
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: なし
- 担当・モデル: GPT

## 9. Phase D

- C / C' の突合: 未着手
- マージ判断: 未着手
- harvest: README と 27-folder-structure へ依存図は反映済み。このファイルはマージ時に削除
- 削除確認: 未
