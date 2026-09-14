# CD0 additional independent code review (Grok)

- Date: 2026-09-14
- Reviewer: Cursor Grok 4.6 (xAI), new session after GitHub `@cursoragent code_review`
- Implementation base: `0a11a4be58c7b75356b076f356078d8d001c2e5b`
- Implementation head: `189bc21086aaf96d77f64c017b4ebf7ad4301876`
- Independence: **制約あり**。C / C' の HANDOFF 記録と evidence を読んでから差分を見ている。盲検の C' 代替には使えない。モデル系列は OpenAI の既存 C/C' とは異なる。
- Verdict: **HOLD / inconclusive**（既存 C/C' と一致。native 未実施を成功扱いにしない）

## 構造適合

Phase A の責務分割（ledger / session / runner / authoring / content build / player / inventory / cleanup）は実装に残っている。既存 OSM / SampleGame / Addressables / 既存 asmdef への参照 0。Runtime に `UnityEditor` 無し。`#nullable enable` 18/18。`record` / `Task.Delay` / `Thread.Sleep` 無し。Unity 偽 null は確認した箇所で `== null` / `!= null`。

行数警報（新規のため増加率 50%+）は発火するが、独立した変更理由の混在は計画どおり分離されている。`分割済み`。500 行超えファイルは無い。

`Tests.Editor` が `CD0Spike.Editor` を参照している点だけが、凍結 A3 の「Runtime と Test Framework のみ」から外れている。inventory 試験のためなら妥当だが、B-result の差として未記録。

## Findings ledger

| ID | severity | category | vs C/C' | finding |
|---|---|---|---|---|
| G1 | medium | semantic | C' P3 の具体化 | `Cd0PlayerExperiment` の host 判定が `Contains("/artifacts/cd0/player-host")`。`player-host-evil` / `player-hosting` が通る。同 PR の `IsContained` は `root + "-other"` を拒否するが、ここは使っていない。 |
| G2 | medium | semantic | unique | Player の `outputPath` と `previousBuildReportDirectory` を CD0 root 内へ閉じていない。任意 path へ Player を書け、任意 report を読める。 |
| G3 | low | semantic | unique | `BuildContentDirectory` / `QuarantineFailedOutput` も出力 path の contain 検査が無い。現行 menu の相対 path は `artifacts/cd0/content/` 配下だが、ヘルパー単体は防御していない。 |
| G4 | low | obvious | unique | A3 は test asmdef を Runtime + Test Framework のみとした。実装は Editor 参照あり。B-result に差として無い。 |
| G5 | low | semantic | unique | incremental 再build ガードは `BuildName == "cd0-local-v1"` のみ。path 一致でも名前が違えば登録中上書きを止めない。isolated run 側は登録検査なし（run-id 一意なので衝突は低い）。 |
| G6 | low | semantic | C' P2 duplicate | inventory に role 分類と snapshot/diff が無い。unknown を成功判定前に解消できない。 |
| G7 | medium | semantic | C' P1 duplicate | runner は `local-happy-path` のみ。E5 matrix を駆動できない。 |
| G8 | medium | semantic | C' P2 duplicate | directory / borrowed root は session bool と log のみ。run-level ledger が無い。 |
| G9 | low | semantic | unique residual | `async void Start` + `OnDestroy` は abandon するだけ。Play 中断や domain reload で await が完了しないと native cleanup が走らない。 |
| G10 | trivial | obvious | unique | `CompletedSuccess_IsAcceptedUntilAbandoned` は Abandon を呼んでいない。 |

## 受け入れ条件

AC1–AC5 / AC7 の native 実測は未実施のまま HOLD。AC6 の本番 path 差分 0 と「実験を Framework 公開 API へ昇格させない」は静的に成立。cleanup 追跡は ledger + session で部分的。Player host / output の境界は G1–G3 が残る。

## 機械検査

このセッションは Cloud のため `pwsh` も Unity も起動していない。既存 evidence の contract-audit PASS / EditMode 687/687 を再実行せず、18 ファイルに対する hard gate（nullable / record / Delay / Runtime の UnityEditor / 既存 asmdef の CD0 参照）だけ Python で再確認し 0 件。

## 判定

GO にしない。native E0–E8 が無い以上 CONDITIONAL にもしない。ハーネス欠陥 G1–G3 は E7 前に直す価値があるが、このレビューは実装 head を動かさない。
