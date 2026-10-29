# Artifact Storage の人間向け利用性: ユースケース整理

## 0. メタデータ

- type: `research`（スライス HANDOFF ではない。Phase 欄を持たない）
- status: ユースケースの洗い出し。A0 の入力候補で、実装キューではない
- branch: `claude/artifacts-human-usability`
- owner: root
- created: 2026-10-29
- expires: 2026-12-31 または、この内容を取り込んだ A0 / program revision による置換
- harvest to: 着手時の A0（新しいスライス HANDOFF）と `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の保留list。恒久的な決定は `tools/Artifacts/README.md` へ移す
- 引継ぎ票: 次の作業は owner による再開判断（§4）。モデル名、reference・packageSha256 の実値、ローカルの path は書かない

## 1. 目的と前提

- 目的: Artifact Storage（Evidence・Build・Content と清掃 job）を、人間も正式な利用者として使えるようにする改修の前に、ユースケースを固定する。
- owner の決定（2026-10-29）: **人間を正式な利用者に含める。** 現在の CLI は主にエージェントの利用を想定しており、使いにくさの一部はセキュリティ対策から来ている。
- 対象外: このファイルでは設計・実装をしない。どのユースケースから着手するかと、§3 の論点の採否は A0 / A3 で決める。

## 2. ユースケース

記号: 🔒 は安全契約に由来するため残す前提の制約。🔧 は偶発的な使いにくさで、改修候補。保留list の欄は `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §6 の既存項目との対応を示す。

### 2.1 Build / Content（人間の比重が大きい）

| # | ユースケース | 今の詰まり | 保留list との対応 |
|---|---|---|---|
| H1 | 最新の Build を取得して遊ぶ | 🔧 `build fetch` は reference と sha256 を必須とし、「系列・target の最新」で指定できない。🔒 期待 hash を独立した経路から渡すのは改ざん検知のため | Content確定記録（「最新」の解決） |
| H2 | テスターや別PCへ Build を渡す | 別PCは R2 を直接読めず、取得した PC から手で運ぶ（2台の往復は未検証）。Player の置き方・起動条件が未記載 | Cloud・別PCからの取得 |
| H3 | Player と Content の組み合わせを選ぶ | 🔧 どの Content revision がどの Player に合うかを一覧で見る手段がない | Contentの分離・取得・取り込み |
| H4 | Content だけ差し替えて試す | 取得はできるが、Player へ当てる手順が人間向けに無い | Contentの分離・取得・取り込み |
| H5 | 残したい Build を守る（QA、リリース候補） | 🔧 `build use` は最長90日で、consumer・until・reason を手で指定する。保持件数 N=10 はコード定数 | 件数清掃の自動起動（N の系列ごとの設定） |
| H6 | 保存されている物と使用量を見る | 🔧 系列ごとの一覧・使用量を人間向けに出す機能が無い | Contentの容量上限と孤児回収（一部） |
| H7 | 取得物のローカル掃除 | 🔧 `content fetch` の取得先は自動清掃されず残る | ローカルの残置の清掃 |
| H8 | ビルドしたら自動で保存する | 🔧 `build collect` / `content collect` は手動 | BuildSystem / Harness接続 |
| H9 | 不具合報告と Build・Evidence を紐付ける | どの Build で起きたかを Evidence から辿る導線がない | 新規 |

### 2.2 Evidence と清掃 job

| # | ユースケース | 今の詰まり | 保留list との対応 |
|---|---|---|---|
| A1 | 日次清掃が止まったことに気づく | 🔧 失敗が通知されない。2026-10-29 の日次 job は pwsh の Store 更新（7.6.8 → 7.6.9）で旧 path を起動できず `0x80070002` で失敗し、レビュー時の `schedule status`（`scheduler-identity-mismatch`）で初めて判明した | 新規 |
| A2 | 清掃 task の切替（pwsh 更新・新しい head・config 変更） | 🔧 worktree 作成 → 3 project の Release build → task 名を調べて削除 → install → status と手順が多く、config の path と task 名を人間が探す。🔒 CLI が task を削除・上書きしないのは意図された安全策 | 診断の改善（schedule 系） |
| A3 | Evidence を目で見る（ログ・スクショ） | 🔧 reference と sha256 を手で渡す必要がある。「この task の最新」で引けない | 新規 |
| A4 | 資格情報の更新・ローテーション | 🔒 DPAPI・同一 Windows ユーザー限定は維持する | — |

### 2.3 エージェントの利用（人間向け改修の副次効果）

| # | ユースケース | 今の詰まり | 保留list との対応 |
|---|---|---|---|
| B1 | Phase 間の `stage` / `publish` / `fetch` | 🔧 `--profile` `--config` `--reference` `--sha256` が毎回必要で、config は deployment に1つしか無いのに path を渡す | 新規 |
| B2 | 新しい worktree で初めて使う | 🔧 Release build が無いと `unconfirmed` だけが返り、build 不足と分からない | 診断の改善 |
| B3 | 失敗の原因を突き止める | 🔒 stderr は `Artifact operation failed.` だけ（endpoint・path・例外本文を出さない）。🔧 秘密を含まない原因の分類はもっと出せる | 診断の改善 |
| B4 | 仮想化された host から使う | 🔒 非仮想化の pwsh 以外は拒否する（ストアの分裂を防ぐため） | — |

## 3. 横断的な論点（A0 で採否を決める）

1. **名前で引く。** 同じ PC の receipt から「系列・target・revision・最新」で引き、期待 hash も receipt から補う案。H1・H3・A3・B1 をまとめて解決する。ただし「期待 hash は独立した経路から渡す」という前提をどこまで緩めるかはセキュリティ判断で、owner が決める。
2. **既定値の解決。** v2-config・build-config・content-config・task 名を deployment から解決し、指定を省略可能にする。安全契約には触れない。
3. **状態の一画面化。** 清掃 job の失敗（A1）、保護の期限（H5）、使用量（H6）は、どれも「止まっていても気づかない」問題。1つの `status` で見せる。
4. **pwsh の版を identity に含め続けるか。** 今は版入りの実体 path に固定しているため、Store の自動更新のたびに task が壊れる。版の入らない path に固定すれば壊れないが、実行バイナリの固定は弱まる。A1・A2 の根本原因。
5. **危険な操作の1コマンド化。** 例: `schedule repair`（診断 → 旧 task の削除 → install → status）。CLI が削除を持たないという現在の契約を、毎回の確認付きで変えるかを決める。
6. **入口。** 人間向けの層を CLI に置くか、Unity Editor のメニューや DebugStudio に置くか。保留list の「Storage単独の利用をUnity GUIやEditorに依存させず」との整合が要る。

方向の案: 中核の契約（削除しない・秘密を出さない・hash 照合）は残し、その上に人間向けの層（名前解決・既定値・一覧と状態・確認付きの1コマンド操作）を別に置く。BuildSystem が主目的なので、着手順は H1・H3・H6 → A1・A2 を候補とする。

## 4. 再開条件との関係

program §6 の再開条件（RC1〜RC3）は、エンジン側の停止・同じ障害の反復・別 host の稼働を求めており、「人間を正式な利用者にする」という目的の変更を想定していない。owner の 2026-10-29 の決定は再開条件より上位なので、再開時は次のどちらかを program に追記する。

- 再開条件に「RC4: owner が利用者の範囲を変えた（人間を正式な利用者に含める、2026-10-29）」を加える。
- または、この research を新しい program として切り出し、§6 の該当項目（§2 の表の対応欄）をそこへ移す。

参考の観測事実: 清掃 job が pwsh の Store 更新で壊れた事例は 2026-10-16（7.6.6 → 7.6.7、README に記載）と 2026-10-29（7.6.8 → 7.6.9）の少なくとも2回。どちらも README の切替手順で回復できたため、RC2 の「回復できなかった」は満たしていない。
