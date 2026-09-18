# BS3 Phase B 実装結果 revision 11

- frozen Phase A: `artifacts/bs3-phase-a/phase-a-r3.md`
- implementation base: `25a5024d310347ef9ce67128636a06e0e0fb5172`
- implementation head: `f8fc698965ee3a2595754a02d66397edd67133af`
- 実装担当: 主担当 GPT-6 Astra

Runtime directory の登録、manifest 索引、型付き Object / Prefab / Scene ロード、AssetOwner と resident cache の解放、明示 close と再試行を実装した。既定の Addressables 経路は維持し、明示設定した directory mode を既存のアプリ起動順序から選択する。

既存 BS2b build root の target `StandaloneWindows64-Player` と Runtime の期待値を一致させた。登録・削除 gate・session は canonical absolute path を共有し、Windows の末尾 separator と大小文字 alias を同一 path として扱う。旧 manifest entry の nullable representation は Full として索引化する。

process 内の削除 gate は、登録済み revision と identity または実 path が重なる delete lease のみ拒否する。別 revision の削除を止めず、同じ実 path を別 identity として削除する抜け道は防ぐ。単一 active directory session の制約は維持する。

BeforeSceneLoad で明示 directory 設定の検証が失敗した場合は、部分初期化を回収し、その起動回の AfterSceneLoad を止める。null の設定値だけを根拠に既定 Addressables へ進ませない。不正 mode のテストは、Director と AssetManagement が生成後に残らず、AfterSceneLoad の factory へ入らないことを確認する。

統合テストは実 directory の Build・移設・型付きロードに加え、既定 Addressables と明示 directory mode の実アプリ Editor Play 起動を検証する。二度の content build と二度の PlayMode を伴うため、Unity Editor の再コンパイル時に既定 180 秒で中断されないよう、このテストの上限を 360 秒に指定した。無期限待機はしない。

Unity バッチテスト、契約監査、文書監査の結果は Phase C の固定証拠に記録する。別 process の排他と配布時の物理削除は後続 slice の責務である。
