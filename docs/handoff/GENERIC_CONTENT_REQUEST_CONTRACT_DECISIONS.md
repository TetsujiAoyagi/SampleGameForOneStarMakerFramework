# S0-a GENERIC_CONTENT_REQUEST_CONTRACT の決定記録

A3 凍結後の人間の裁定のうち、凍結条件の意味・合否を変えないものを記録する。Phase A snapshot には追記しない。本スライスの Phase D で HANDOFF と一緒に harvest・削除する。C' へは C' 自身の前巡指摘への裁定だけを渡すので、本書を丸ごと渡さない。

## 版 1（2026-11-08、implementation head `7aa249d` に適用）

### DR-1 発見 C の D-1 を S4 へ移送する

- 裁定者: owner
- 対象: `ContentResolver` が `IContentProvider.Id` / `SourceKind` を `try` の外で読むので、getter が例外を投げる adapter では probe の記録なしで例外が抜ける。
- 結論: 現スライスでは修正せず、後続の `LOCAL_LAN_PROVIDER`（S4、adapter の契約）へ移送する。
- 読み方の確定: I5 の「provider または規則が例外を投げたら」は `FindAsync` と `Accepts` を指し、宣言値の getter は含まない。どの凍結条件の合否も変えない。
- 理由: I5 の検証例である T20 の対象は `FindAsync` と `Accepts` で、getter の安定性は adapter が守る契約として扱える。実在の adapter は S4 で作る。
