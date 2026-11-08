# S0-a GENERIC_CONTENT_REQUEST_CONTRACT の決定記録

A3 凍結後の人間の裁定のうち、凍結条件の意味・合否を変えないものを記録する。Phase A snapshot には追記しない。本スライスの Phase D で HANDOFF と一緒に harvest・削除する。C' へは C' 自身の前巡指摘への裁定だけを渡すので、本書を丸ごと渡さない。

## 版 1（2026-11-08、implementation head `7aa249d` に適用）

### DR-1 発見 C の D-1 を S4 へ移送する

- 裁定者: owner
- 対象: `ContentResolver` が `IContentProvider.Id` / `SourceKind` を `try` の外で読むので、getter が例外を投げる adapter では probe の記録なしで例外が抜ける。
- 結論: 現スライスでは修正せず、後続の `LOCAL_LAN_PROVIDER`（S4、adapter の契約）へ移送する。
- 読み方の確定: I5 の「provider または規則が例外を投げたら」は `FindAsync` と `Accepts` を指し、宣言値の getter は含まない。どの凍結条件の合否も変えない。
- 理由: I5 の検証例である T20 の対象は `FindAsync` と `Accepts` で、getter の安定性は adapter が守る契約として扱える。実在の adapter は S4 で作る。

## 版 2（2026-11-08、implementation head `7aa249d` に適用）

### DR-2 AC6 の「head が合格」を XML で読む（C' 1 巡目 F-01 への裁定）

- 裁定者: owner
- 対象: C' 1 巡目の F-01。全 EditMode 回帰の `run-tests.ps1` の `step.json` は base と head で `status=failed`、`exitCode=1` で、理由は `Inconclusive のテストがあります`。base と head の Unity は結果 XML の完成後に終了せず、実行担当が強制終了した。
- 結論: AC6 の「head が合格」と S5 の「全 EditMode 回帰が合格」は、結果 XML の failed 数と root result（`failed=0`、`result=Passed`）で読む。Inconclusive の 25 件と、XML 完成後の強制終了は、事実として記録して受け入れる。
- 理由: AC6 が base の基準として定めるのは「1 件以上実行かつ failed 0」であり、runner の status で読むと base が比較の基準にならない。Inconclusive の 25 件は `SampleGame.Tests.Rendering.Vfx.*PlayTests` で、`-nographics` により VFX Graph が使えないときの `Assume` による結果で、base と head で同じケースである。実装変更の有無に関わらず同じ結果になる。ASSET_CYCLE も同じ読み方で判定した。XML 完成後の終了 stall は `tools/Harness/README.md` の既知事象で、XML の内容を変えない。
