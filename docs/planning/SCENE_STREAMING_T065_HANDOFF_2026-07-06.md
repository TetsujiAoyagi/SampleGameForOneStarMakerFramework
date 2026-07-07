# SceneStreaming T-06.5 引き継ぎプロンプト (2026-07-06)

> 新セッションの最初のメッセージとして、以下の `---` 以降をそのまま貼り付ける。

---

## 最初に読むもの（この順で）

1. `docs/planning/SCENE_STREAMING_TDD_PLAN_2026-07-06.md`
   — TDD 施行表。全チケットのテスト仕様・絶対制約（G-1〜G-6）・
   SceneDirector.Loading.cs の不変条件（I-1〜I-5）・テスト実行コマンドが書いてある。
   作業はすべてこの計画書に従うこと
2. `unity/Assets/Docs/Architecture/21-scene-streaming.md`
   — 設計の正典。§10 の完了記録で現在地を確認する

## 現在地 (2026-07-06 時点)

- T-01〜T-06 完了。直近のコミット:
  - `81a38e2` T-05: World Cell Generator（ComputePlan/ApplyPlan 純関数分離、冪等生成）
  - `dc47283` T-06: WorldStreamingController + ISceneStreamingBackend（ポリシー層、G-6 再照合）
- テストは OneStarMaker.Tests 全体 **211 / 211 グリーン**（EditMode）。未コミットの作業なし
  （テスト XML・ログ等の管理外ファイルは無視してよい）
- T-05 / T-06 の設計上の割り切り（in-flight Add の解放タイミング、Remove 中の再 Add 抑止、
  boxedValue 不使用、既存 .asset 取り込み等）は正典 §10 の完了記録に明記済み

## 次の作業

施行表の推奨順序に従い **T-06.5（Controller × 本物 SceneDirector 統合テスト）** から。
ここが全チケットの合流点で最重要の品質ゲート。

- 新規: `Runtime/Streaming/SceneDirectorStreamingBackend.cs`（ISceneStreamingBackend の本実装）、
  `Tests/Streaming/StreamingIntegrationTests.cs`（施行表のテスト一覧 5 本が仕様）
- 構成: `TestableSceneDirector` + World 親シーン + `Cell_x_y` の SceneResource 群
  （`SceneDirectorTestBase` のセットアップヘルパーを拡張）
- 注意: このチケットの失敗は SceneDirector 側の欠陥を示している可能性がある。
  その場合は G-1/G-5 に従い、修正方針を報告してから着手する

各チケットは施行表 §1.1 の TDD サイクルで進める:

1. レッドテスト作成（施行表のチケット別テスト一覧が仕様。安価モデルへの委任可）
2. テストレビュー後、Unity バッチ実行で「コンパイル成功 + 意図したレッド + 既存グリーン」を確認
3. 実装（テストの変更・弱体化は禁止。G-2）
4. 全体グリーン確認 → 正典 §10 へ完了記録を追記 → チケット単位でコミット

## 運用メモ

- テスト実行は施行表 §1.2 のコマンド（`-nographics` 必須、PowerShell は `&&` 不可で `;` を使う）。
  Unity.exe は即座に制御を返すため `Get-Process Unity | ForEach-Object { $_.WaitForExit() }` で完了を待つこと
- Unity バッチは同一プロジェクトで並行実行不可。サブエージェントへ委任する場合は
  コード作成のみ任せ、Unity 実行はオーケストレータに集約する
- 並列作業時のワークフロー実績: 実装 = 安価モデル（Composer）、レビュー = GPT-5.5、
  最終チェック + Unity バッチ = オーケストレータ。レビューと最終チェックの指摘がゼロになるまでループ
- コミットメッセージは日本語・複数行のため、一時ファイルに書いて `git commit -F` で渡す
  （PowerShell のヒアドキュメント制約回避）
- 着手前に施行表 §5「既知の落とし穴」を一読すること（特に UniTask の二重 await 禁止と
  未観測例外の濡れ衣問題）
