# 実機の検証の基盤 — S4-V

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-V。要求の正本は program §9.1）
- status: `A`（A0 の前。この版は行を開くための骨組みで、§1 以降は A0 の担当が書く）
- branch: `claude/scenario-verification-base`（PR の base は `develop`）
- implementation base commit: 未定（A0 の担当がその時点の `develop` の head で書く）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `normal`（program の行の値。A1 で見直す）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: A1 で決める（候補: [tools/Harness/README.md](../../tools/Harness/README.md)、program §9.1 の表の状態）
- Phase A snapshot の取得案内・状態: 未定（A1 で要否を決める）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A（A0〜A3）の主担当。
  - 次の作業: program §8 の S4-V の行と §9.1 を入力に、A0 の planning packet（§1・§2）を書き、A1〜A3 を進める。§9.1 の利用者表の「基盤に要る操作」を 1 つずつ (a)(b)(c) に分けることが A1 の中心である。凍結は owner の判断を待つ。
  - 読む範囲: 本書の全節（骨組み）、program の §8 の S4-V の行と §9.1、次の入力。
  - A0 の入力（2026-11-25 までに決まったこと）:
    - owner の決定: 実機の検証の基盤を、S4-3a の中ではなく独立の行にする。S4-3b〜d が続くことは確定しているので、「今の使い手が 1 つ」を理由に共通化を見送らない。基盤の要求は行の都合ではなく program §9.1 の表から決める（部分最適を避ける）。S4-2 の driver はこの行で基盤に移し、Player の build は取り直さない。
    - S4-3a の外部計画レビュー（[PR #214](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/214) のコメント 3 件、2026-11-25）。特に 3 件目「追加設計提案：S4-2 の検証資産をコードとして再利用する」の、S4-2 の driver の関数の分解、`run-tests.ps1` の process と lock の扱い（`Invoke-UnityTestProcess`、`Test-UnityProjectLock`）、`tools/unity-editor.ps1` をそのまま使うこと、写してはいけない弱点の一覧、offline test の項目。内容は担当が原文を読み、本書に転記して採否を付ける。
    - S4-3a の HANDOFF（PR #214 の branch の `docs/handoff/LOCAL_CONTENT_ORIGIN.md`）の §1 の現況のうち、Play ボタンの入口が batch と Test Runner の Scene を素通りする行（Play を見る検証は `run-tests.ps1` の test では組めず、窓のある Editor を `eval` で操作する）と、§5 の E 系列の driver の約束。S4-3a は基盤の最初の利用者の 1 つで、その case の列は S4-3a が持つ。
  - 必要な権限: Phase A は文書だけ。Unity を起動する確認（窓のある Editor の疎通 1 回）は Phase C。
  - 停止規則: 製品の code を変えない。program §9.1 の表に無い汎用化を足さない（足すなら先に表を直し、owner の確認を取る）。凍結の記録が無いまま Phase B を始めない。
  - 引き渡さないもの: なし。
  - C' の独立性の注記: 基盤の設計の材料は OpenAI の model（web の ChatGPT）の外部レビューが出したので、この行の C' の未関与の系列に OpenAI を数えない。

## 1. 目的と対象外

A0 で書く。

## 2. 意思決定と受け入れ境界

A0〜A1 で書く。

## 3. 責務マップ

A1 で書く。

## 4. 実装計画

A1 で書く。

## 5. テストとレビュー計画

A1 で書く。

### 5.1 A2 の独立レビューと A3 の採否

未着手

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
