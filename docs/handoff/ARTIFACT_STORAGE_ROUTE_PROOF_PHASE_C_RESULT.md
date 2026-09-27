# Artifact Storage Route Proof — Phase C result

この文書はPhase Cの実行結果だけを固定するsnapshotであり、Phase C'のblind audit所見や最終GO宣言を含まない。
- Phase A snapshot: `328843ea521550af9da203315c2704d3d51cc8d0:docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF.md`
- Phase B snapshot: `docs/handoff/ARTIFACT_STORAGE_ROUTE_PROOF_PHASE_B_RESULT.md` (SHA-256 `7D9DCC474D45CCFA18B56C5298F88102DBEACCA03EADEE67CB74990124C734FB`)
- implementation base: `93d2a1c`
- Phase B implementation head: `e122a79`
- Phase C実行時 tip (docs-only後): `59047280c264395153506b0f7dc2ec57b8547f53`
- generated at: `2026-09-27T13:02:37.8398273+00:00`
- 担当: Grok Bot / このsession（実R2はowner Windows Agent上）

## Phase Cで直したprobe不具合

実R2実行を阻んでいた `tools/Artifacts/Probe/RouteProof.ps1` の不具合を同じbranch作業ツリーで修正した（このPhase C結果コミットに含む）。

1. `"OSM-ROUTE-PROOF:$RunId:original"` がPowerShellのスコープ解釈で `OSM-ROUTE-PROOF:` (16 byte) に潰れていた → `${RunId}` に修正
2. 失敗記録・complete記録の `(if ...)` がCommandNotFoundになり、実失敗が `environment-blocked` に化けていた → `$(if ...)` に修正
3. 子process JSON経由の `HttpStatus` / `ByteCount` が Int64 になり `Assert-Observation` が常に失敗していた → `[int]` と `[long]` を許可

## 実行環境（非秘匿）

- profile: `osm`（既存active。再入力・set・replaceなし）
- bucket: `osm-artifacts`（store固定）
- endpoint: account S3 endpoint（Git非記録。形は `^https://[0-9a-f]{32}\.r2\.cloudflarestorage\.com$` に一致）
- LockRuleJson: Prefix `probe/locked/`, Enabled true, Kind Age, RetentionSeconds 86400, RuleCount 1, DateRules 0, IndefiniteRules 0, WriterCanConfigure false, LifecycleCompatible true, BeforeHash=AfterHash `a5e63444a6691cc513a7f45a52c3b4c5087104aaef6422c82202a77cda923159`
- runner: PowerShell 7.6.6（WindowsApps実体パス）。Windows PowerShell 5.1 では `SHA256.HashData` / `Convert.ToHexString` が無く同じスクリプトは動かない

## 実R2 RouteProof結果

- 最終分類: **inconclusive**（exit 4）
- 到達フェーズ:
  - `unlocked-put`: pass（HTTP 200）
  - `unlocked-authenticated-get`: pass（HTTP 200、hash/byte一致、EOF確認）
  - `unsigned-get`: **inconclusive** — HTTP **400**, StatusClass `other`, S3Code `InvalidArgument`, Message `Authorization`。本文hashはobject hashと不一致（中身リークではない）。期待の 401/`Unauthorized` または 403/`AccessDenied` ではないため仕様上 inconclusive
  - locked-* / unlocked overwrite・delete・confirm: **未到達**
- 残骸: 実行後にunlocked probe objectはDELETEで除去確認（204）

## Phase C判定

- **GOしない**。実R2は認証付き書込・読取までは確認できたが、unsigned privacyが許可コード外の `InvalidArgument` で止まり、Bucket Lock拒否の証明に到達していない。
- 内容リーク（unsignedがobject本文を返す）は観測していない。
- `provider-capability-failure` にもしない（privacy失敗条件の「本文hashがobjectと一致」は満たさない）。
- tip `59047280c264395153506b0f7dc2ec57b8547f53` 時点のprobeだけでは上記1–3により実R2が正しく進まない。本結果は修正後スクリプトでの観測である。

## 秘匿の扱い

実鍵、Authorization、HTTP本文、SDK例外本文、endpoint文字列はGit/このsnapshotに保存しない。Lock ruleの非秘匿ledgerと分類・HTTP status / S3 Codeのみ記録する。

## Phase Cで未実施

- unsignedが401/403になる経路の再設計または仕様更新（Phase A戻し候補）
- locked overwrite / delete / confirm
- 固定evidence bundle、Phase C' blind audit
- Unity / Evidence / Build転送
- credentials set / replace
- Phase C result file SHA-256: `9fbbc54203f4798df0b1da49b884873929b6392ce9883c301cd0ab4603d13e96`
