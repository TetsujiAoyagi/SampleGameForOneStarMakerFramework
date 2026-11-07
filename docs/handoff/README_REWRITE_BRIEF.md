# README 書き直しの brief（レビュー用）

- type: `brief`（単一スライス。実装は `README.md` の書き直しのみ）
- status: 下書き。Editor で手順を通した確認はまだ行っていない
- owner: root
- created: 2026-11-07
- expires: README の PR マージ時にこの file を削除する（harvest 先は無い。残す内容は README 本文がすべて）

## 1. なぜ書き直すか

owner の目的は「**Unity の開発のつらみを減らしつつ、準オープンワールドのゲームを開発できる土台**」を作ることである。ところがこの一文は README にも `docs/GOALS_AND_STRENGTHS.md` にも書かれておらず、README は「工事中」の一行とライブラリ一覧から始まっていた。読んだ人が「何ができて、どう試すか」を掴めない。

外から見つけてもらう前提として、README を読んだ人が 1 分で次の 3 つを掴める状態にする。

1. 何のために作っているか
2. 今日この repo を開いて何が動くか
3. どの menu を押せば何が見えるか

## 2. 方針

- **目的を先頭に置く。** owner の一文を README の冒頭と「目的」節に書き、`GOALS_AND_STRENGTHS.md` の「侵食防止の契約」はその手段として後ろで参照する。
- **「できること」は今日動くものだけ。** 設計上の意図や予定は書かない。各項目はコードの入口（menu 名・型名・定数）と突き合わせて書いた。
- **「まだできないこと」を同じ重さで書く。** 季節遷移、編集保護、性能の数字、Windows 以外、配信の作り込み、ゲームルール未投入。「工事中」の一言より、無いものを列挙した方が信用される。
- **数字は測った物だけ。** セル数・セル寸法・半径はコード定数から取った。build 時間や反復時間はまだ測っていないので書かない。
- **既存の節は消さずに下へ送る。** ブランチ・環境・DebugStudio・ライブラリ一覧は内容を変えず順序だけ変えた。
- **英語の要約を 2 行だけ置く。** 検索と海外の読者のため。本文の英訳はしない。
- **旧 README の誤りを 1 箇所直した。** 「Editor Play 向け publish 入口は未整備」は PR #156 の `Verify Play`（build → publish → install → Play）で解消済みなので、その記述に置き換えた。

## 3. 変更した file

- `README.md`: 全面的に並べ替え。新規の節は「目的」「できること」「試す」「まだできないこと」。
- この file。

## 4. レビューで見てほしい点

1. **目的の一文が owner の意図と合っているか。** 「準オープンワールドの土台」「Unity のつらみ」の言い方が過不足ないか。
2. **「できること」に過大な主張が無いか。** 特に次の 3 つは実装と照らしてほしい。
   - `Play As` が「同じ表現・同じ companion 集合で Play に入る」
   - `Verify Play` が「途中で失敗したら環境を変えずに止まる」（bridge 以降の失敗は ledger を戻してから止まる、が正確。README ではそこまで書いていない）
   - Streaming の「4 季 × 9 × 6 セル、250 m、LoadRadius 375 m / UnloadRadius 550 m」（`WorldCellCatalog` の定数）
3. **「試す」の手順が実際の menu と一致するか。** menu 名は `PlayAsLauncher.MenuRoot`・`VerifyPlayLauncher.MenuRoot`・`SampleGameContentBuild` の `MenuItem` から写した。Editor で通した確認はまだ無い。
4. **「まだできないこと」に漏れが無いか。** 逆に、既にできているのに「できない」と書いた物が無いか。
5. **読者の視点。** Unity で広い世界を作っている人が README だけ読んで、自分のつらみのどれに効くか分かるか。

## 5. 書かなかったこと（意図的）

- build 時間・反復時間の数字（未計測）
- 工程（Phase A/B/C）や Artifact Storage、Harness の説明（開発者向けで、README の読者には不要。`docs/README.md` に残っている）
- 外部フレームワークとの比較（`docs/README.md` の方針どおり repo に置かない）

## 6. この brief の外にある関連作業

README とは別に、同じ目的で直す候補。この brief の範囲ではない。

- GitHub の repository description に全角の `ｗ`（`Frameｗork`）が入っていて検索に一致しない。半角の英語を含む一文に差し替える。
- `docs/GOALS_AND_STRENGTHS.md` の冒頭に owner の目的の一文を足す。
- `docs/handoff/SEASON_WORLD_DESIGN.md` §4 の「イテレーション」行が `Verify Play` 以前の記述のままになっている。
- 手順を Editor で一度通し、所要時間を測って README の「試す」に足す。
