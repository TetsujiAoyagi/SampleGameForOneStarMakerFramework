# Phase B: 実装

- HANDOFF がある場合は本文を正とし、書かれていない設計文書を読み込んで独自に再設計しない。
- HANDOFF と他の記述が衝突したら実装を止め、判断を返す。
- 設計判断が新たに必要になった場合も実装を止め、Phase A に戻す。
- 新しい事実は先に [A 再開 / B 適応 / スパイク / 後続スライス](phases-and-handoff.md#新しい事実の分類) に分類する。失敗したこと自体を設計判断にせず、B 適応なら Phase A を再開しない。
- 計画外の状態、依存、所有者、寿命、公開 API が必要になった場合、または計画した配置では中核ロジックを単体テストできない場合も実装を止める。Phase B 内で便宜的な Helper / Manager へ押し込まない。
- コメントには、コードだけでは復元しにくい契約、判断理由、変更時の注意を残す。特に寿命・所有者・選択数・失敗境界や、一見省けそうな例外処理の理由を説明する。処理をそのまま言い換えるコメントや行数を満たすためのコメントは増やさない。読者に通じないスライス略号だけで説明せず、対象と理由を通常の言葉で書く。
- Unity Editor、Scene、Prefab、Addressables等を扱う場合は `../../osm-unity-editor/SKILL.md` を先に読む。
- 対象を限定した Editor 操作とコンパイル確認は Phase B で行ってよい。Unity バッチテストと Addressables ビルドの実行・判定は Phase C の責任とし、H2未適用のPhase Bでは実行しない。H1適用済みのArtifacts/Harness作業は完了引渡し時に関連offline suiteのB exitを通す。中間commitやWIP救援にB exitを要求しない。B限定のUnityテストは、A3で実行経路と対象集合を凍結したtaskに限る。完了済みH2cの実例（承認profile、初回疎通の扱い、固定記録の入口）は [Harness README](../../../../tools/Harness/README.md#h2c-unity-pilot) を正とする。完了時に未実行を明記する。
- 実装を終えたら `pwsh tools/contract-audit.ps1` を実行する。Editor のコンパイル確認を行えなかった場合は、その未確認を明記する。
- B の session は B の出口で commit し、§0 の implementation head 欄と引継ぎ票（取得案内の所在を含む）を書いて終える（欄の書き方と引き継ぐ commit の SHA の渡し方は [引継ぎ票](phases-and-handoff.md#引継ぎ票)。PR が存在する間は状態ブロックも更新する）。push は [公開点](publish-and-history.md) の規則に従う。判定 C の実行や C' の起動を担わない。判定 C の照合 script も書かない。引継ぎ票の次の担当は、動作確認の対象があれば動作確認、無ければ発見 C とする。
