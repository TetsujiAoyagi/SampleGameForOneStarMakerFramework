# BS2c — SampleGame の選択 policy と Content Directory 入力

- type: slice
- status: A2（独立レビュー済み。A3 凍結前）
- branch: `codex/bs2c-samplegame-selection`
- implementation base commit: `16650d7`
- implementation head commit: 未作成
- risk: high（本番 Scene graph、選択 cardinality、Editor build 入力）
- owner: BS2c 担当
- created: 2026-09-17
- expires: BS2c Phase D。遅くとも 2026-10-17 に前提を再確認
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`、`20-variant-checkout-workflow.md`
- Phase A snapshot path / id: 本文 §1–5 の A3 凍結版
- Phase A snapshot generated at / hash: A3 時に記録
- Phase B result snapshot path / id / generated at / hash: 未作成
- evidence bundle path / id / generated at / hash: 未作成
- C' blind bundle path / id / generated at / hash: 未作成

## 1. 目的、現況、対象外

本スライスの問いは、本番 `SceneResourceMap` の親子関係から SampleGame 固有の Season と Representation を導出し、意図した論理 content だけを BS2b の既存入口へ渡せるか、である。BS2b までは代表 fixture の Content Directory build が完了した。現行 materializer は payload が空の構造ノードを含む全 SceneResource に `ExactlyOne` を置き、payload の空 Variant を `Full` とする。季節を絞ると他季節の requirement が残り、Full と Whitebox を同時選択すると同一 group の `ExactlyOne` に反する。現行 `BuildTagSelector` は候補のタグが request に含まれない場合に除外し、タグのない dimension は制約しない。

対象外は成果物 schema、Runtime 起動時選択と Editor Play 接続（BS3）、Player build（BS4）、配信と cache（DIST）、非 Scene Description 全種の本番 source、Scene graph の再生成、実行中切替 UI。Framework へ Season 名を入れない。旧 Addressables 経路は RET まで保持する。

入力正本は `BUILD_SYSTEM_REBUILD_PROGRAM.md` §3 の BS2c、`AGENTS.md`、既存 BS1/BS2a/BS2b 実装と公開 Architecture §18。未追跡の PRE 文書は編集も追跡追加もしない。

## 2. 意思決定と受け入れ境界（A1 案）

### 進める最低条件

1. 全季節 Full、Spring Full、Spring Whitebox の候補、必須 logical group、除外理由を、実際の SceneResourceMap から決定的に説明できる。
2. 三つの選択の成功 plan を、同じ materialization snapshot と対にして BS2b の実 build へ渡し、成功した Content Directory と preflight/outcome を確認する。
3. Full+Whitebox の同時同梱を開発用途として選択でき、両表現を持つ季節 group の両方が plan に残る。BS3 に起動時選択を渡せる。
4. 本番 graph 全件に対する欠落・重複・親子不整合を機械検査し、代表箇所の動作確認を行う。標本を全件実行済みとは書かない。

### 観測可能な受け入れ条件

- Season schema は `Spring`、`Summer`、`Autumn`、`Winter`。`SceneResource.Parent` の祖先にある季節 node から membership を決める。季節名や Identity の接頭辞では子孫を推測しない。季節外の bootstrap/共通 Scene は Season タグを持たず、各 build へ含める。重複 Identity、親参照の循環、map 外参照、親子の相互不一致、孤立 node、複数季節 membership は選択前に失敗として報告する。
- Representation は既存 materialization の `Full`（空 Variant）と `Whitebox` を使う。未知 Variant は error。個別 Scene へのタグ複製は行わない。例外 metadata は project policy の明示的な入力として設け、初期値は空にする。例外なしで現 graph を説明できない場合は A3 前に具体例と所有者を決める。
- `BuildRequest` の `Representation` は materializer のタグ検証に使う。共通 Full を Spring Whitebox に含めつつ季節 Full を除くため、SampleGame 固有の `SeasonalMode` タグを季節候補だけに付ける。Spring Whitebox の request は `Representation={Full,Whitebox}`、`SeasonalMode={Whitebox}`、`Season={Spring}`。Spring Full は `Representation={Full}`、`SeasonalMode={Full}`、`Season={Spring}`。全季節 Full は Season の4値。Full+Whitebox は両表現と両 mode を要求する。この二重タグの意味を SampleGame の選択診断とテストに明記する。BS2b の preflight は request と requirements を出力しないため、その report だけを選択根拠としない。
- requirement は SceneResource の payload 宣言を根拠に、選択した季節の group と季節外の group に限定して新たに作る。payload が空の構造ノード（例: `Season_Spring`）には requirement を置かない。payload が宣言されているのに materialization 候補が欠ければ失敗とし、黙って optional にしない。単一表現は `ExactlyOne`、二表現同梱の季節 group は `OneOrMore` とし、Full と Whitebox の双方が存在する group について双方が選ばれることを project policy が検証する。materializer snapshot の `Requirements` と `Candidates`、依存閉包は変更せず、snapshot の `Requirements` を selector に丸ごと渡さない。
- 選択や materialization が失敗した場合は build を開始しない。成功 plan の全候補は選択または除外として残し、BS2b projection の整合検査を通す。除外には既存の tag/value 理由を残す。
- 三つの代表 build と Full+Whitebox の plan 検査、逆順入力の決定性、共通 content、季節外 requirement、欠損 Variant、重複、誤った親子、例外 metadata の単体テストを持つ。

GO は上記最低条件をすべて満たし現在の問いへの反証がない場合。NO-GO はいずれか未達。停止規則は GO 証拠が揃った時点で終了し、BS3 の Runtime 機能を追加しないこと。A3 後の例外承認は現在なし。

後続へ送る問い: Runtime load identity、local directory 登録・寿命、起動時の Full/Whitebox 選択、Editor Play 配線は BS3。Player bootstrap と stripping は BS4。source 欠損で取得済み成果物からの起動は DIST。汎用サブアセット locator と本番非 Scene source は program §3 の実需要後続 slice。

### 常時契約と停止条件

Game → Framework の一方向を維持し、asmdef edge 追加はこの Phase A で固定する。Unity Editor 依存を Editor asmdef に置く。新規/編集 Unity `.cs` は `#nullable enable`。Unity fake null は `== null` / `!= null`。SceneState と AssetOwner と `IAssetManagement` は変更しない。`record`、テスト内の時間 sleep は使わない。BS2b の plan/snapshot schema の変更が必要、または次節の配置で pure policy を単体テストできない場合は Phase A を再開する。

## 3. 責務マップ（A1 案）

| 配置 | 責務と変更理由 | 依存、所有者・寿命、テスト境界 | 規模 |
| --- | --- | --- | --- |
| `SampleGame/DependOnAll/Editor/Build/SeasonSceneSelectionPolicy.cs`（新規） | 不変の graph record を検証し、候補への Season/SeasonalMode タグ、request ごとの requirement と整合 issue を生成する project policy | 入力は Identity/Parent/Children/payload 有無の複製 record と BS1/BS2a の immutable candidate。状態は一回の build 呼び出し内。Unity Object と AssetDatabase と build I/O を持たず、合成 graph で単体テスト | 約250行。graph 検証と選択が独立して増えるなら分割 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs`（新規） | Unity graph を不変 record に複製し、materialize → policy → selector → coordinator を順に呼ぶ薄い Editor 入口。選択診断を出力 | AssetDatabase と Unity Editor I/O。一回の build 呼び出しが所有。成果物の publish は BS2b に委譲。代表 build の統合テスト | 約150行 |
| `SampleGame/DependOnAll/Editor/SampleGame.DependOnAll.Editor.asmdef`（既存） | Selection/Materialization/Content の3 asmdef 参照を明示 | Game Editor → Framework Editor のみ。新しい逆依存なし | +3行 |
| `SampleGame/Tests/Editor/Build/SeasonSceneSelectionPolicyTests.cs` と `SampleGame.Tests.Editor.asmdef`（新規） | pure policy と入口の検証 | テスト asmdef は `SampleGame.DependOnAll.Editor` と BS1/BS2a、Runtime のみ参照。実 build は Phase C の限定実行 | 約200行 |

上の新規 policy は project 語彙だけを所有し、生成 root や Scene lifecycle を所有しない。`500行`、`3責務`、`50%増` の警報は Phase C で実測し、越えた場合は配置と独立した変更理由を説明する。

## 4. 実装順序と検証計画

1. graph の季節 node と共通/bootstrap node を Editor 読み取りで確認し、例外 metadata の要否を確定する。
2. 純粋な membership と selection policy を実装し、単一表現と二表現のテストを追加する。
3. Editor 入口と asmdef 参照を追加し、BS2b coordinator へ既存 plan/snapshot を渡す。
4. `contract-audit.ps1` と Editor compile を Phase B で確認する。Phase C は Editor を閉じてから `run-tests.ps1`、代表 Content Directory build、全 graph 機械検査、構造レビューを実施する。

A0/A1 主担当: Codex / GPT-6 Astra。A2: 独立 architecture gate は別セッションの Codex agent、追加レビューは別モデルで行う。A3 はレビュー指摘を採否分類し、この本文の責務と acceptance を凍結する。Phase B/C/C' の担当はそれぞれ別モデルと新規セッションを使う。C' 用モデル系列を予約する。

## 5. Phase A レビュー記録

- A2 architecture gate: 親子 membership と project policy の分離を推奨。全 map の `ExactlyOne` を request 範囲に絞り、二表現では cardinality を変える。materialization snapshot は変更せず BS2b に渡す。共通 Full と Whitebox-only の選択は selector の AND/OR semantics に注意。採用: `SeasonalMode` を使い、Framework の public selector API 変更を避ける。
- A2 独立レビュー: BS2b report だけでは request/requirement を説明できない、空 payload node の扱いが曖昧、pure policy の Unity Object 依存とテスト配置が曖昧、graph 相互整合検証が不足、と指摘。すべて採用し、上記受け入れ条件と責務マップを更新した。
- A3 統合・採否・凍結: 未実施。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未実施。

## 8. Phase C'

未実施。

## 9. Phase D

未実施。
