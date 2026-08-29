---
name: osm-workflow
description: >-
  Use for planning, HANDOFF creation, implementation, code review, independent
  audit, Unity test execution, documentation changes, branches, or pull requests
  in the OneStarMaker / SampleGame repository.
---

# OSM 開発ワークフロー

`AGENTS.md` の常時契約を前提に、作業の Phase と責任を分離する。使用するモデルやサービスは固定しない。

## 作業開始時

1. 現在のブランチ、差分、対象 HANDOFF を確認する。
2. 自分が担当する Phase と成果物を特定する。
3. 専用ブランチで作業する。`main` / `develop` に直接コミットしない。
4. PR の base は `develop` とする。

Phase A、C、C'、HANDOFF の作成・更新では [Phase と HANDOFF](references/phases-and-handoff.md) を全文読む。ドキュメントを変更する場合は [ドキュメント方針](references/docs-policy.md) も全文読む。

## Phase B: 実装

- HANDOFF がある場合は本文を正とし、書かれていない設計文書を読み込んで独自に再設計しない。
- HANDOFF と他の記述が衝突したら実装を止め、判断を返す。
- 設計判断が新たに必要になった場合も実装を止め、Phase A に戻す。
- Unity Editor、Scene、Prefab、Addressables等を扱う場合は `../osm-unity-editor/SKILL.md` を先に読む。
- Unity バッチテストと Addressables ビルドは Phase C の責任とし、Phase B では実行しない。完了時に未実行を明記する。

## Phase C: レビューとテスト

- HANDOFF の受け入れ条件と差分を照合し、機能レビューより先に構造レビューを行う。
- 変更量、責務の混在、単体テスト可能性、Unity の偽 null を確認する。
- Unity Editor が閉じていることを確認して `pwsh tools/run-tests.ps1` を実行する。絞り込みは `-Filter` を使う。
- exit 0 は1件以上実行かつ failed 0。0件は失敗として扱う。
- `0xC0000005` でも結果 XML が完成していれば、ログ末尾と XML を基に判定する。
- 確認結果と未確認事項を HANDOFF に記録する。

## 完了

- マージ済みの HANDOFF は、恒久的に残すべき知見だけを公開ドキュメントへ反映して削除する。
- PRを作る場合は差分と検証結果を要約し、base が `develop` であることを確認する。
