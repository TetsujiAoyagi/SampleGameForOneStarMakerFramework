# 次実装前の文書整合確認

> type: research
> status: 文書修正済み・Draft review 待ち。実装のA3承認、Phase C/C'の合格ではない。
> branch: `docs/pre-implementation-drift-20260927`
> base: `e225b5a36226cefd36c82b372fc646bb3949b918` (`develop`)
> owner: 文書整合修正担当 / レビュー担当
> created: 2026-09-27
> expires: この文書修正PRのPhase D完了時。マージ前に本書とdocs/README.mdの一時案内を削除する。
> harvest to: 修正した既存README / Architecture / Streaming仕様 / 進行中program。調査と検証の記録は本PR本文へ残す。

## 対象と境界

AGENTS、osm-workflowと関連参照、osm-unity-editor、docs/READMEを入口とし、進行中program、Artifact Storage、Content Directory / DIST、通常Play / Player、公開README / Architecture / Streamingを照合した。関連する既存実装、asmdef、テスト入口、スクリプト、PR #77 / #78の差分・レビューを証拠にした。

変更は文書のみ。実装、Unityアセット、設定、CI、監査スクリプトは変更しない。新しい設計を選ばず、到着契約を未実装という理由で消さない。日付・対象版がある過去の測定値と採否記録はその対象範囲のまま保つ。

## 修正した正本

- 通常経路と旧Addressables互換経路: Architecture §4 / §18、S-4 program、Streaming現状仕様。§20、SampleGameContentBuild、VariantFilteringBuildScript、AbstractApplicationInitializerを照合した。
- World制作の後続前提: S-4a〜c完了、代表Lighting追加済み、一時生成器撤去、現在のEditor操作境界をS-4 / 世界制作programへ反映。S-4dの実装は追加しない。
- 公開入口: 実際の既定develop、採用パッケージ、既存InGame / テスト分離 / 最小RenderEnvironment、古い測定値の対象版を整理した。
- Artifact Storage: ローカル段1=スライス0、次のローカル段2=スライス1をREADMEで明確化。既存CLI / ダミー鍵テストはR2接続・Cloud成立の証拠ではない。

## 未決・保留

1. 世界制作W-4: 編集保護は必要なまま。一時生成器撤去後の判定方法は未決で、旧再実行手順では合格にできない。S-8aのPhase Aで、現行制作操作の保護実証か、別途必要な生成スライスかを判断する。免除・達成扱いにはしない。
2. Editor directory起動: 旧SceneVariant resolverの先行検証と、mode判定前のAddressables JSON読取が残る。仕様上許容する依存か実装の切断漏れかを判断する。静的な不一致候補であり、Unity再現・実装修正はしていない。旧profileを通常起動の必須条件にしない。
3. S-4dのVFX Graph版: 現在のURPは17.6.0、旧計画は17.5.0。導入方針を取り消さず、対応版はS-4dのPhase Aで確定する。全CellのLighting標準化、S-9の共有contentの容量帰属も未決のまま。
4. Artifact Storage: #77 / #78はマージ済みだが、r2 / r3の記録にない再レビュー完了・A3承認・R2/Cloud検証は補わない。programの方向性と個別実装HANDOFFの凍結は別である。

`tools/contract-audit.ps1`の説明コメントにも旧「Phase BはUnityを起動しない」が残る。現行規約はAGENTS / Skillであり、このコメントを作業指示にしない。監査スクリプト変更は本PRの対象外として残す。既存コードの古い説明コメントも、この文書PRでは変更しない。

## 検証境界

- GitHub connectorによる固定baseの読取と書込が利用可能。ローカルGitは利用可能だが、GitHubへの直接接続はDNS解決失敗で完全cloneできなかった。
- 変更対象の原文をGitHubのblob SHAと照合し、変更文書だけのローカル比較用Git snapshotを作成した。`git diff --check`はexit 0。変更したリンク・パス・名称・見出し構造の確認結果もPR本文へ記録する。完全clone上の監査とは区別する。
- `pwsh tools/docs-audit.ps1` / `pwsh tools/contract-audit.ps1`はPowerShell未導入のため未実行。手動確認をこれらの成功とは扱わない。
- Unity起動、テスト、Build、R2接続、Cloud、独立C'監査は未実行。文書変更のための新たな全回帰・Buildは要求しない。
