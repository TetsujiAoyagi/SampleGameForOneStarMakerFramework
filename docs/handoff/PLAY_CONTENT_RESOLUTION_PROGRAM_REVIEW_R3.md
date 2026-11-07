# PLAY_CONTENT_RESOLUTION_PROGRAM 独立計画レビュー R3

Claude への入力。revision 3.1 の本文はまだ変えていない。採否は program の §11 に残し、採用した文は revision 3.2 の本文へ入れる。このファイルは要約を §11 へ移した時点で削除する。

- 種別: 計画レビュー（program の付属。実装スライスでも program でもない）
- 対象: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) revision 3.1
- 対象 commit: `7385e7e19e791755680d4ab6bc1426edc3a1e1bf`
- 対象ファイル sha256: `a24c61f595079128426c022eb8271bd706b4e31dbce009e56c5108e65442b2f5`（299 行、UTF-8）
- レビュー日: 2026-11-08（Asia/Tokyo）
- 担当: Cursor 上の Grok 4.7。R2 の原文は持たず、program §11 の要約だけを見た。目的レビューではない（§1 だけでなく全文を読んだ）
- owner: root
- expires: 2026-12-31、または本レビューの採否が program §11 に入りこのファイルを削除した時点
- harvest to: 恒久文書へは移さない。採用分の要約先は program §11。不採用も理由ごと §11 に残す
- コード確認: base の作業ツリーで、下の「確認したコード」だけを読んだ。Unity は起動していない。行数表の再計測はしていない

## 判定

A0 の問題設定、要求時解決、案 A、Traceable までを必須にすること、S0-a / S0-b / S0-c の分離を支持する。全体の再計画は不要。R2 の F-01〜F-04 は再開しない。

S0 の A1 草稿は、下の F-01〜F-04 を本文に入れた版から始める。3.1 のまま S0-a を A3 まで進めると、単一 directory の規則と I2 を、S0-b の結果より先に凍結できる。スライスの増減は不要。

## 確認したコード

base `e022934` と同じ作業ツリー（HEAD は文書 commit `7385e7e1`。該当 `.cs` はこの commit でも同じ）で確認した。

- `AssetManagement.InstallContentDirectory` は `_contentDirectory != null` のとき `InvalidOperationException`（`AssetManagement.ContentDirectory.cs` 20〜22 行）。セッションは 1 つ。
- directory と決まった直後に `SealAddressables`（`AbstractApplicationInitializer.cs` 238〜245 行）。`addressables` のときは `ReadContentDirectoryConfiguration` が null を返し、封じない（853 行）。
- 未指定で installed pair も無いときは失敗する（856〜862 行）。合法な mode は `addressables` と `directory` だけ（864〜865 行）。
- `RegisterAlreadyLoadedScenes` はロード済み Scene を名前で `SceneResourceMap` に引き、未登録はスキップする（640〜656 行）。由来は持たない。
- `ContentTransportManifest.declaration` は任意で、コメントどおり validation は見ない（`ContentTransportManifest.cs` 21〜22 行。`ContentManifestValidation.cs` に declaration の参照は無い）。
- Player の protected 照合は `content:runtimeMode` / `buildIdentity` / `contentSet` / `target` / `representation` / `firstScene` / `relativeDirectory` / `probeToken`（`PlayerContentConfiguration.cs` 24 行付近）。
- [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の連携部分は `ContentDirectorySession` と `UnityContentDirectoryBackend` と起動 facade。resolver をそこに置く、という program §0 の境界はこの表と矛盾しない。

## 上位の問い（program §1）への評価

上位の問いは正しい。「宣言 1 つで source overlay」「Play 前に全 Scene の供給元を確定」を問いから降ろした判断も正しい。Play は解決の利用者であり、解決のモデルそのものではない。利用者 (d) は Play の対象ではなく、resolver を必須依存にしないための制約として置けている。

| 問い | 評価 | A1 で固定してよい範囲 |
|---|---|---|
| Q1 要求 | 正しい。logical identity、kind、表現、互換制約、cancellation、診断相関で足りる | Cell、UI 画面、Season、VCS、Scene 状態は要求に入れない。表現は resolver の enum にしない、でよい |
| Q2 供給 | 正しい。優先順位のある provider を要求時に探す | 「今どこに cache されているか」は供給 tier と別、という §3.1 の 8 を保つ |
| Q3 信頼 | 正しい。所有者は S1。S0-b の行に Q3 が付いているのは対応の誤り（F-06） | 使用可能と互換は、候補が見つかったこととは別の判定 |
| Q4 所有 | 正しい。この program で一番硬い | 現行は directory 1 つ、封じ 1 回、既ロード Scene は名前だけ。同一 session で snapshot と directory を所有できるかは S0-b の実測が先 |
| Q5 観測 | 正しい。必須を Traceable までに切ったのは過剰な再現要求を避けている | record は policy と別型。hash は出所の手掛かり |
| Q6 統合の任意性 | 正しい。案 B/C/D を汎用の正本から外す根拠になっている | resolver 無しの AssetManagement と、SceneDirector 無しの resolver 利用は、S0-a が別々に示す（本文どおり） |

保証として A1 以降も残すもの: 受信 package の検証前使用をしない。明示設定を壊さない。同一 logical identity を snapshot と directory で二重に実行しない。SampleGame の語彙を core の契約に入れない。

作らないもの: Play 前の全 Scene 解決、sha 差分の source overlay、load API への provider chain の必須組み込み、known-good directory pair の自動適用、snapshot hash による再現保証、Cloud tier の実装。

## 未決事項（program §10）への評価

番号は現行 §10 のまま書く。本文では F-05 のとおり U1〜U10 に改名する。ここでの「答え」はレビューの推奨で、owner 決定の置き換えではない。D1〜D11 は動かさない。

### U1（現行 Q1）新しい interface の範囲

問いとして必要で、1 つの問いに二つの決定が混ざっている。分けて凍結する。

1. resolver の表面。推奨: 小さな任意の表面が要る。`IAssetManagement` は logical key と representation で、既に入っている 1 つの directory から読む。provider の探索も record も返さない。探索を全 load API に入れる案 B は Q6 に反する。S0-a の合格を「新しい型が 0」に置かない。
2. directory の基数。`InstallContentDirectory` の 2 件目拒否は、resolver の型とは別の既存契約。§5.2 (ii) が勝ったときだけ製品コードで変える。S0-a は「AssetManagement は 1 directory のまま」を凍結しない。実験コードは S0-b の外に残さない、という本文も維持する。製品変更のスライスは S0-b の結論の直後に切り、S3 の Phase B より前に置き場を決める。

### U2（現行 Q2）snapshot の機構 (i) / (ii)

開いたままが正しい。勝者は選ばない。S0-b の比較に、次の脱落条件を足す。

- (i) が directory mode の `SealAddressables` より後に AssetDatabase / Addressables で複製を読むなら、その読み方は脱落。封じを外す比較はしない。
- (i) が封じの前に既ロード Scene として渡るなら、由来の無い `RegisterAlreadyLoadedScenes` に乗る。その場合の証明項目は本文の実験 (1)(3)(4) のまま。
- (ii) は複数 directory 登録が前提で、U1 の 2 と結び付く。publisher の `Full` 限定も比較のコストに入れる（本文どおり）。

### U3（現行 Q3）snapshot を Runtime へ渡す形

推奨: Editor 専用 bridge。`content:` の予約 key には入れない。protected 照合が `content:runtimeMode` から `probeToken` までを Player の baked 値と突き合わせる。snapshot の path をこの名前空間に置くと、I10 の保護キーと同じ形になる。Player にはこの bridge が無い。

### U4（現行 Q4）known-good と、adapter が pair を書く行為

この program で残っている決定の中で、A1 が一番誤解しやすい境界。S1 の詳細ではなく、S0-c の入口が書く値の境界。

推奨: 二つの行為に分ける。

- key がどの層にも無いとき、Editor の transaction が `content:runtimeMode=addressables` をセッションに書くことは、verified pair の自動適用ではない。`addressables` は既存の mode で、directory を選ばない。Console に 1 行残す。これは D1（`app-config.json` に `addressables` を置いて直押しを通す案）とは別で、D1 は維持する。コミットされた既定値にはしない。
- `content:installedRevisionPath` と `content:manifestSha256` を adapter が埋めることは、§4.7 が拒んだ known-good の自動適用そのもの。書かない。

「Runtime の文言は変えず、policy と record を出す」は、上の分離とセットなら採用してよい。pair を書く実装の言い訳には使わない。

### U5（現行 Q5）互換 fingerprint

問いの焦点が一段低い。graph version を最小集合に入れるかより先に、汎用の一致と host の述語を分ける。

推奨する汎用の一致: contentSet、target、revision の identity（名前の一致だけでは不可、本文 F-04 どおり）、file digest、Unity version、root schema。

`declaration` は任意で validation が見ない。空の name 同士は一致してしまう。汎用の最小集合に入れると、旧 manifest が空一致で通る。`declaration.name`、表現、graph は host が登録したときだけ比較する述語にする。表現は要求元の data なので、汎用側は中身を解釈しない。

### U6（現行 Q6）Stage II の索引の置き場

答えるのは早い。制約だけ先に書くなら: 索引は BuildSystem が正規 directory と一緒に出す bytes にする。Runtime が成果物を走査して作らない。Editor にしか無い sidecar にしない。Local と LAN が同じ bytes を見る。

### U7（現行 Q7）checkout hook の形

S4 まで開いてよい。推奨の方向: VCS adapter は resolver の内部を参照しない。`tools/` の script か host が、prefetch の公開入口を呼ぶ。手動 checkout も同じ入口。SVN の型は OSM に入れない（D8 どおり）。

### U8（現行 Q8）menu root と観測 UI

契約ではない。S5 の整理でよい。観測の置き場は Editor adapter。二重 root の統合を S0 の gate にしない。

### U9（現行 Q9）CI publish

本文 §8 がすでに答えている。供給の CI publish は program の外。供給が無い間の経路は source-only Play と S5 の明示回復。この受け入れで閉じる。CI の所有者をこの program の未決に残すと、別の答えがもう一度付く。

### U10（現行 Q10）対象外

対象外の切り方は正しい。1 文だけ足す。§4.2 の RunPurpose 相当の prefetch は program 内の待ち時間対策。U10 の「先読みの最適化」は streaming の保温で、prefetch の言い換えではない。

## 本文へ入れる指摘

優先度は「A1 の凍結が誤る」順。いずれもスライス追加ではない。

### F-01 高。§4.1 の「同じ revision」は package の合成規則

§3 の標準連鎖は、Editor snapshot の Scene と、別 tier の directory の Scene を同一 session で使う。§4.1 は「複数供給元の同じ revision から取るのが標準。異なる revision の寄せ集めは拒否」。snapshot に revision は無い。この文の主語が広いと、S1 は D4 と S0-b の成功条件を非互換として拒否する。

入れる場所: §4.1 の当該段落の先頭。

入れる文:

> この段落の revision 規則は、検証済み package 同士の合成に限る。異なる logical key について Editor snapshot と directory を併用することは、revision の寄せ集めではなく、§3 の標準連鎖であり S0-b が実機で見る。同一 logical key は snapshot を 1 つだけ実行し、directory 側は実行しない（G21）。

### F-02 高。単一 directory の規則は S0-a が凍結しない

根拠は「確認したコード」の `InstallContentDirectory`。§5.2 (ii) は複数登録が前提。製品変更の所有スライスが無い。S0-b は実験コードを残さない。S3 を暗黙の所有者にすると、S1 が基数を既定にして進む。

入れる場所: §8 S0-a の gate と、§10 の現行 Q1。

入れる文:

> S0-a が凍結するのは、要求・候補・record・所有の契約と、resolver を AssetManagement の必須依存にしないことまで。`InstallContentDirectory` が 2 件目を拒否する規則は、S0-b が §5.2 の (i) / (ii) を選ぶまで凍結しない。(ii) を採るときの製品変更は実験コードの残置ではなく、S0-b の結論の直後に切るスライスとし、S3 の Phase B より前に置き場を決める。

### F-03 高。I2 に Editor snapshot の例外が無い

I2 は検証前の bytes を使用対象にしない。D4 は digest の無い Dirty な複製を最優先で採用する。S0-a が I2 を無例外で符号化すると、snapshot 候補は契約違反になる。

入れる場所: §6 I2。

入れる文:

> I2: 供給元から受け取った bytes は、manifest / digest / 互換の検証を通るまで Unity の使用対象にしない。Editor snapshot tier の受け入れ理由は digest ではなく、利用者が開いている Scene の複製であること。record に dirty と、digest 検証を受け入れ理由にしていないことを残す。これは別 revision への fallback ではない。

### F-04 高。D6 と §4.3 が gate の所有者を逆に書いている

D6 は「UX 要求を受け入れ条件にするかは S0-c が決める」。§4.3 は R2 M-07 を受け、Play ボタン、明示設定の非破壊、失敗と由来の診断の 3 つを program の必須かつ S0-c の gate にし、裁量は表示の配置と通知の細則だけにしている。S0-c の A1 は D6 を読んで 3 条件を落とせる。

入れる場所: §7 D6 の決定欄。

入れる文:

> 通知は必要な警告だけ。Play のたびの popup は出さない。§4.3 の 3 条件（Play ボタンから起動できる、明示設定を壊さない、失敗と由来を診断できる）は program の必須で S0-c の gate。表示の配置と通知の細則は S0-c の裁量。

経緯欄に「R3 で、受け入れ条件にするかを S0-c に任せていた文を撤回」と残す。

### F-05 中。Q 番号が二系列ある

§1 の Q1〜Q6 と §10 の Q1〜Q10 は別の問い。§8 の「上位の問い」列は §1 を指す。§8 の「§10 Q9」と §10 の Q1 は別物。HANDOFF が「Q1 に答える」と書くと、要求の形と、interface の切り分けが入れ替わる。

入れる作業: §10 の見出しと各項を U1〜U10 に改める。本文中の「§10 Q9」は「§10 U9」にする。§1 の Q1〜Q6 と §8 の上位の問い列は変えない。

### F-06 中。S0-b の行が Q3 を受け持っている

§8 S0-b の上位の問いが「Q3、Q4、D4」。Q3 は互換の判定で、所有者は S1。D4 は決定であって問いではない。このセルのままだと、spike が互換 envelope を実装既定にする。R2 が「方式選択を実装既定に凍結するのは尚早」とした範囲を超える。

入れる作業: そのセルを「Q4、I4、I11」にする。

### F-07 中。I11 の「内容」と G22 の「検出または明示」

I11 と S0-b の実験 (4) は、元 Scene の内容が無傷であることを証明に含む。G22 は、参照 prefab が変わっていれば hash が一致しても実行内容が違い得る、と認める。M-05 は snapshot を Scene 限定にするかを S0-b の scope へ送ったが、I11 はすでに不変条件なので、scope の未決より強い。G22 の「検出または明示」は、検出機能を合格にして Traceable の上に再現を戻す。

入れる場所: §6 I11 と §9 G22。

I11 に足す文:

> 対象は、開いている Scene 資産のシリアライズ内容と Dirty。参照先の prefab / material / ScriptableObject は複製せず、I11 の対象外。

G22 の合格条件を置き換える文:

> record が、snapshot の hash は当該 Scene 資産の出所追跡であり、参照先 asset の一致を保証しない、と説明できる。参照先の漂移を検出する機能は合格条件にしない。

### F-08 中。S0-c の gate に S1 の事例が混ざる

S0-c の問いは、Runtime を変えずに Play ボタンから source-only / directory-only を始め、明示設定を壊さず、由来を診断できるか。gate の「別 revision」は S1 の互換判定。初見の失敗は、mode 不在かつ verified pair 不在であり、resolver が無くても現行の config がすでにその形をしている。

入れる場所: §8 S0-c の gate。

残すもの: §4.3 の 3 条件、G18（手動 ledger、明示 runtimeMode、環境変数、Variant profile、directory-only への既ロード Scene の無言混入を許さない）、候補が無いときの振る舞い（U4 の mode 書き込みか、ブロックか）。

S1 へ移すもの: 別 revision、互換でない digest / target / schema。

### F-09 低。U9 を未決の一覧から閉じる

§10 の現行 Q9 を「§8 で受け入れ済み。CI publish の所有者は program の外」に書き換え、未決の作業にしない。

### F-10 低。LoadRecord は由来を持つ

`RegisterAlreadyLoadedScenes` は名前だけで登録する。S0-a の fake provider が由来無しの成功を許すと、G12 と G21 を後から契約に足せなくなる。

入れる場所: §8 S0-a の gate の末尾。

入れる文:

> LoadRecord は供給元の種別（snapshot / directory / addressables）を持つ。fake provider のテストでも、由来の無い成功は合格にしない。

### F-11 低。§5.2 (i) に封じの時点を書く

S0-b が封じの解除を比較案として発明しないための 1 文。入れる場所: §5.2 (i)。

入れる文:

> directory mode では config の直後に Addressables を封じる。封じの後に AssetDatabase / Addressables で複製を読む案は比較に入れない。封じの前に既ロード Scene として渡す案は、由来を持たない既存の既ロード登録に乗るので、実験 (1)(3)(4) で見る。

## 採否欄

Claude は各行を採用 / 不採用 / 保留にし、理由を program §11 の R3 項へ転記する。保留は A1 の未決へ送る先（スライス名）を書く。

| ID | 推奨 | 採否 | 理由 |
|---|---|---|---|
| F-01 | 採用。§4.1 に 1 段落 | | |
| F-02 | 採用。§8 S0-a と §10 U1 | | |
| F-03 | 採用。§6 I2 | | |
| F-04 | 採用。§7 D6 | | |
| F-05 | 採用。§10 を U1〜U10 に改名 | | |
| F-06 | 採用。§8 S0-b のセル | | |
| F-07 | 採用。§6 I11 と §9 G22 | | |
| F-08 | 採用。§8 S0-c の gate | | |
| F-09 | 採用。§10 U9 を閉じる | | |
| F-10 | 採用。§8 S0-a | | |
| F-11 | 採用。§5.2 (i) | | |

## 入れないこと

- R2 の F-01〜F-04 の再開。3.1 の反映で足りている。
- 案 A の差し替え。案 B は単独利用に Editor と LAN を混ぜる。
- D1〜D11 の撤回。D6 の文面だけを §4.3 に合わせる。
- S0 の並行そのものの取りやめ。F-02 と F-08 を入れれば、三つの A1 は並行のままでよい。製品統合を S0-a の契約凍結後にする、という M-01 も維持する。
- snapshot 機構の (i) / (ii) の選択。
- Stage II の単位、索引形式、Cloud、Player での要求時解決。
- 行数表の改訂。今回は契約に効く箇所だけを読んだ。
