# S-4c 判定 C のレビュー記録

- 担当: Codex / GPT-6（OpenAI）。Phase B の Cursor Grok 4.7 / xAI とは異なる。C' は起動しない。
- base: `553b7b150e13245b369d75dc4baa12d86e9559aa`
- head: `c93cab004724fe6ed4f9b011edfbd852b5e879d9`
- 開始時: 対象ブランチ、tracked / untracked とも clean。リモートに対し ahead 1。
- 既存再確認 head `1241c8e` との差は、記録 bundle・HANDOFF 更新と Runtime Rendering のコメント3か所。実行文の変更はない。ただし今回の完全 diff と検査結果は新 head に固定し、34件の旧 XML は判定証拠へ転載しない。
- 旧 bundle の完全 diff hash は manifest と一致。A3 hash も一致。A1 の実測 hash は `B7CE692F0F0EA04C4111C08BA429D443F3C92321D687420CAF047AFC4A28FA38` で旧 bundle の manifest と一致するが、HANDOFF メタデータの `38be...` とは異なる。A1 は A2 入力で、今回の受け入れ境界は A3 決定と固定 head の本文 §1〜5。snapshot の版差を隠さない。
- 旧 B snapshot の「床境界目視未記録」は現在の §6 により更新済み。人間の境界目視と Cell Lighting 閉鎖後の維持確認を未確認へ戻さない。

## 構造（機能検査に先行）

公開6型と internal Validator、Game の preset / Scene adapter、Composition Root の注入という配置を確認。asmdef、SceneState、LoadType に差分なし。新規 Host、URP 依存、Service Locator はない。Fake sink が policy と Unity I/O を分離し、Light が必要な試験だけ一時 GameObject を作る。

AppInitializer は 383→422行（+39）。App サービスの配線と回収を同じ Root に置く凍結判断どおり非分割妥当。SeasonLightingScene は42→118行で50%警報に該当するが、探索・取得・解放に限った orchestration であり、preset と lease policy は別型に分離済み。既存 Factory は95→99行。WorldCompanionCreationPlan の親パス修正は既存 creator の配置修正で、トランザクション再設計ではない。

最低条件2: Game RenderSettings grep 0件。条件3: 四季の Factory 注入、Acquire 前の sun / identity 検査、Acquire 後 catch での解放、PreUnload 解放をコード確認。条件4: companion の構造分岐、各 Point 1・Directional 0 の既存 EditMode 試験を確認。条件5: 指定 GUID の LightingSettings / LightingData 参照は代表7 Scene。lightmap 4組が固定 head に追跡されている。四季 sun authored の変更と代表7 Sceneの bake を区別し、他の Cell / Whitebox の bake 差分はない。条件6: §6 の人間確認を採用。条件1は全回帰 XML、条件7は runtime 観測が必要。

## 現在の問いを阻害する欠陥

### C2-1: 破棄済み Light を挟むと二度目の BindSun を受け入れる

- severity: P2 / category: semantic / unique / accepted（静的所見、実行再現は未実施）
- 発見: 今回の Phase C、Codex / GPT-6。
- 根拠: `RenderEnvironment.cs:78` の `_boundSun != null` は Unity 偽 null 判定。Bind(A) → A の GameObject を破棄 → 同一 lease.BindSun(B) では false となり、89行で B を受け入れる。lease は解放されていない。
- 違反: 凍結 §2.4「BindSun は lease 生存中に1回。二度目は失敗」。Light の生存と bind 済みの履歴は異なる。既存 `BindSun_Twice_Throws` は Light 生存時だけを試験している。
- 修正先: B 適応。公開面・所有者・寿命を変えず、bind履歴の保持と破棄済み入力の拒否を区別する。実装修正は行っていない。

### C2-2: 固定 head の docs-audit が失敗

- severity: P2 / category: machine / unique / accepted
- 発見: 今回の Phase C、Codex / GPT-6。
- 根拠: 既存 `artifacts/s-4c-phase-c-rediscovery-1241c8e/phase-b-result.md:7` の個別 HANDOFF パス参照。`docs-audit` 検査2、exit 1、1件。
- 違反: `docs/README.md` の層の契約と HANDOFF §5 の必須機械検査。旧 bundle の exit 0 は現 head の合格証拠にならない。
- 修正先: evidence / 記録の整理。旧固定 bundle を無断で書き換えず、修正対象と新しい hash を明記する。今回は変更しない。

## 後続スライスの入力と B 残件

- Workspace が SceneGraph Editor / Layout を更新しない件は既に凍結済みの後続入力。S-4c を差し戻す根拠にしない。
- Volume.weight 実行時所有、Camera skybox 季節差し替え、S-5 の受け渡しと baseline 間隙、S-9 の Probe メモリは凍結された後続のまま。新規検証を要求しない。
- A1 hash と旧 B snapshot の目視記載の古さは版管理の注記であり、目視の再要求や受け入れ条件追加にはしない。
- `content:runtimeMode` 未指定の fail-closed は既存契約。実装変更や既定値追加はしない。既存コードは明示 `addressables` と `directory` を受け入れる。現在の app-config には runtimeMode がない。

## Editor / Play の制約

開始時の Editor は対象project / 6000.6.0f1 / PID 31640 / ready。command 一覧取得は成功するが `list_open_scenes` は CLI と Pipeline 0.4.0-exp.1 の不整合で exit 1（0.6.0-exp.1 以降への更新要求）。既存Editorを強制終了せず、ユーザーに保存確認と終了を依頼。その後 status で接続なしを確認し、標準ランナーのロック検査を通過した。

Package更新は固定headの検証対象を変えるため実施しない。この接続経路では Play 操作・観測を完了できない。runtime の両 Cell unload 中の春 fog/sun維持と戻った baked床は未確認。§6 の Editor確認済み事項とは区別する。新しい欠陥として起動契約を緩和しない。

## 全回帰と最終状態

`pwsh tools/run-tests.ps1` を Editor 接続なし・project lockなし確認後、最初からsandbox外で実行。Filter空、EditMode、既定nographics、Unity 6000.6.0f1。Unity / runner exit 0、所要13.1分。908 passed / 0 failed / 0 skipped。XML の全908 test-caseを確認し、4アセンブリ（644 / 249 / 14 / 1件）とS-4c関連34件の収録を確認した。旧XMLの流用ではない。

PC_RPAssetにテストが生成した再シリアライズ差分は保存後、元のHEADへ復元した。実装変更を残していない。

判定は **保留（検証証拠不足）**。Play Mode必須観測未完了のためGO / NO-GOを確定しない。C2-1とC2-2を修正側へ返し、最低条件7のruntime観測を残す。C'は未起動で、最終判定用blind bundleも未生成。今回の記録は未コミット。
