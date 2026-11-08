# 汎用の Content 互換判定と host 述語（S1）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（**A1 初稿 = A2 入力版。** A2 の独立レビューは未実施、A3 凍結は未実施、Phase B は未着手。program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S1。A0 の入力は同書 §3・§4.1・§6（I1・I2・I5・I7・I8・I10）・§8 の S1 行と「S0-a から移送した入力」「S0-b から移送した入力」・§9 G08・§10 U4・U5。program の停止規則「S1 以降は S0-b の Phase D より前に Phase B を始めない」は、S0-b が PR #172 でマージ済みなので満たしている）
- branch: `claude/loving-lamport-7lbluf`（S1 のスライスブランチ。PR の base は `develop`）
- implementation base commit: `7b28eba`（`develop` の head、2026-11-08。本書の実測は全てこの base。Phase B の着手時に head を取り直し、`Runtime/ContentResolution/` に差分があれば §1 現況を読み直す）
- implementation head commit: `未固定`
- risk: `normal`（program §8 のとおり。既存の公開型・asmdef・所有者・寿命・`SceneState`・Addressables・起動順序を変えず、`Runtime/ContentResolution/Core/` に新しい型を足すだけ。新しい公開面を足すことは S0-a の owner 裁定（同スライスの Q1）と同じく「公開 API を変える」高リスクに数えない。A2 のアーキテクチャゲートはこの分類も確認する）
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-11-08
- expires: 2026-12-31、または Phase D で harvest・削除した時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md` の「要求時の Content 解決」節（`IContentCompatibilityRule` の製品実装、互換 envelope、host 述語の合成規則、由来ごとの汎用判定、互換の不採用が `RuleRejected` + 理由 code で残ること、まだ保証しないこと）。program の harvest（§18 §4 等）は program の完了時
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（**所有境界の正本。** 本スライスの型は S0-a と同じく AssetManagement の上の任意の層 `Runtime/ContentResolution/Core/` に置き、BuildSystem 本体を参照しない。境界を本書で定義し直さない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本スライスは起動コードに触れないので、Phase B の同時実行制約の対象外）、S0-c `EDITOR_PLAY_ENTRY_PROOF`（owner のローカルで進行中。本スライスは S0-c の入口・ledger・config に触れない。§2.3）
- Phase A snapshot の取得案内・状態: 未生成（A2 入力版は本書を含む commit。A2 のレビュー入力は commit の本書だけで、snapshot に入れる機械向け期待値・照合 script は無い（§5「判定 C の照合 script の方式」が `なし`））
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー）。owner が担当とモデルを選ぶ。
  - 次の作業: §5「A2 の計画」に従い、(1) 目的レビュー（§1 の「目的」と「利用者」の 2 欄の原文だけを渡す。sha256 の作り方は phases-and-handoff.md「Phase A の内部フロー」）、(2) アーキテクチャゲート（本書全体と base の `Runtime/ContentResolution/`、`tools/ContentResolutionOfflineTests/`）、(3) 反例レビュー（§2.1 の各不変条件を破る順序を、§2.1 が挙げた経路の外も含めて探す）を、互いの指摘を見せずに依頼する。§2.5 の未決事項 U-S1-1〜U-S1-5 は A2 の回答と合わせて A3 で owner が裁定する。
  - 引き継ぐ commit: 本書を含む A2 入力版の commit（SHA は tree の外、起動指示と計画 PR の状態ブロックで渡す）。
  - 必要な権限: なし（文書のみ）。A2 の担当は repository の読み取りだけで足りる。
  - 停止規則: A3 凍結まで Phase B を始めない。A2 の指摘で本書を改訂しても新しい commit を切らず、改訂版は Phase A snapshot で渡す（phases-and-handoff.md「Phase A の commit と snapshot」。本書の次の commit は A3 凍結版）。S0-c の入口・Bootstrap・既存 Runtime の公開型を変える提案が A2 から出たら、本スライスでは採らず、§2.3 の所有先へ送るか owner に返す。
  - 引き渡さないもの: 目的レビューには §1 の 2 欄以外を渡さない。

### PR 状態ブロック

PR を作る前は本 §0 を唯一の正本とする。PR 作成時にテンプレートの形で本文先頭へ写す。

## 1. 目的と対象外

- 目的: 要求時の Content 解決で、供給元が申告した候補を採ってよいかを、特定の host の宣言（宣言名・Scene 名・RunPurpose 等）を知らずに、Play 前に固定した互換の基準（target・engine の version・root schema、必要なら content set と revision）と、要求が与えた expected identity（digest を含む全欄）だけで判定できるようにする。host 固有の比較（宣言の名前・表現・graph の version 等）は、host が登録した時だけ汎用の判定に追加の条件として加わり、汎用の判定を緩めない。判定できない・合わない候補は採用せず、なぜ採らなかったかを供給元ごとに機械で区別できる理由として残す。
- 利用者: OSMFramework の開発者（供給元の adapter、Editor の Play 入口、SceneSystem adapter を後続スライスで書き、本スライスの規則を `ResolutionPolicy` に渡す人）と、OSM を使う host application の開発者（Play 前の互換の基準を宣言し、必要なら自分の述語を登録する人）。本スライスに実行時の利用者の操作は無い。
- 目的・利用者の文言の改訂（A1）: A0（program §1・§8 S1 行・§10 U5）から A1 で書き起こした初出の文言。A2 の目的レビューはこの原文を見る。A2 前の人間の承認は不要（A0 の文言を改めたのではなく、スライスの目的を初めて書いたため）。
- 対象外（所有する後続。§2.3 に一覧）:
  - 既存の公開型（`ContentRequest`、`ContentCandidate`、`ContentIdentity`、`ContentAvailability`、`IContentProvider`、`IContentCompatibilityRule`、`ResolutionPolicy`、`ContentResolver`、`ProbeOutcome`、`ContentResolutionFailureCode` 等）の宣言と、`ContentResolver` の判定順序・終わり方の変更。**依頼者の指示で変えない。** 互換の不採用は既存の `ProbeOutcome.RuleRejected` と `Reason` の文字列で表す（§2.1 I-C6）。
  - S0-c の Editor Play 入口、`ExitingEditMode` の transaction、ledger、`content:` の config の書き込み（program §10 U4）: S0-c。本スライスの規則を S0-c の入口から呼ぶ接続も S0-c の後（§2.3）。
  - `Runtime/Bootstrap/**`、`AbstractApplicationInitializer`、`ContentDirectoryStartup` に当たる起動 code: 触れない。
  - 実在の manifest（`ContentTransportManifest`）から候補の事実を作る adapter、`ContentManifestValidation` の install 時検査との一本化: S3 / S4。
  - 複数 package の合成可否（共同の build plan / dependency graph の出所、shared asset の所有と重複、同一 logical identity の衝突、package 索引の hash 関係）: S2（§2.5 U-S1-1 で owner に確認する）。
  - 要求をまたぐ pin・要求をまたぐ revision の一貫（最初に採った revision に後の要求を揃える）: S4。
  - Editor snapshot の機構 (i) / (ii) の選定: S0-c / S3 の A1（program §10 U2）。本スライスは機構に中立。
  - record の保存・可視化・UI: S0-c / S5。
- 現況（base `7b28eba` の実測。path は `unity/Assets/OneStarMaker/Scripts/` からの相対）:
  - `Runtime/ContentResolution/Core/`（20 file、1,250 行。S0-a、PR #171）: `IContentCompatibilityRule`（19 行）は `bool Accepts(ContentRequest, ContentCandidate, out string reason)` だけを持ち、**既定の実装も製品の実装も無い**。XML doc に「既定を置くと互換判定の製品実装が決めるべき既定値を先に凍結する。製品実装が入るまで host は自前の規則を渡さない限り `ResolutionPolicy` を組めない」「規則が `OperationCanceledException` 以外を投げると resolver は fault として止める」とある。
  - `ContentResolver`（318 行）の各 probe の判定順は、(1) 要求の不変（provider id / 由来の一致、logical key、kind、表現、pin の全欄一致）→ (2) 由来ごとの受け入れ根拠の固定表（`ContentDirectory` → `VerifiedDigest`、`EditorSnapshot` → `OpenSceneSnapshot`、`Addressables` → 採用しない。identity 付きの `Rejected` の後は `VerifiedDigest` だけ）→ (3) 規則。規則が false なら `RuleRejected` を記録して**次の供給元へ進む**。規則が例外なら `Fault` を記録して止め `ProviderFault`。誰も採らなければ `IntegrityRejected` → `ProviderFault` → `NoCompatibleCandidate` → `NoCandidate` の優先で 1 つに決める（`RuleRejected` は `NoCompatibleCandidate` に数える）。
  - `ContentIdentity`（113 行）は `ContentSet` / `Revision` / `Target` / `Digest` の 4 欄で、等値は ordinal の全欄一致だけ。`IsComplete`（4 欄が非空）と `MatchesPin`。**engine の version と root schema の欄は無い。**
  - `ContentCandidate`（117 行）は `Facts`（provider 固有の事実の `IReadOnlyDictionary<string,string>`。constructor で複製し、null の値は空文字にする）を持つ。互換に使う事実の key は定義されていない。
  - `ContentRequest.ExpectedIdentity` は任意の pin（4 欄がそろった値だけ）。resolver は pin と全欄一致しない候補を規則の前に `PinMismatch` で弾く。
  - 既存の install 時の検査 `Runtime/BuildContent/Distribution/ContentManifestValidation.cs`（293 行、internal）`Validate`（70〜85 行目）: transport の `version == 1`・`product == "OneStarMaker"`・`playerConfigSchemaVersion == 1`、`unityVersion == "6000.6.0f1"`（`SupportedUnityVersion` 定数）・`rootSchemaVersion == 2`（`SupportedRootSchemaVersion` 定数）、要求との `contentSet` / `revision`（`IdentityMismatch`）、`target`（`TargetMismatch`）、`unityVersion` / `rootSchemaVersion`（`CompatibilityMismatch`）、manifest の digest（`IntegrityMismatch`）。**`declaration`（name・representation・runtimeDefaults）は検証しない**（`ContentTransportManifest.cs` 22〜38 行目、optional）。これは install する bytes の検査で、resolver の候補の採否とは別の層にある。本スライスは変えない。
  - offline テスト `tools/ContentResolutionOfflineTests/`（`Program.cs` 693 行、28 件）が Core の `.cs` を `<Compile Include>` で link し、CI の `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` matrix（77 行目）で回っている。T15 `CoreSources_DoNotNameUnityOrHosts`（267〜302 行目）は `Core/` 直下の `*.cs`（**非再帰**の `Directory.GetFiles`）を走査し、`#nullable enable`、`UnityEngine`・`SceneDirector`・`SampleGame`・`Season`・`RunPurpose`・`Workset` 等の語、AssetManagement / BuildContent / SceneSystem の `using`、`record` 宣言、`init` accessor を拒む。
  - S0-a から移送された S1 の入力（program §8）: 「pin が未充足のまま `Fault`」→ `IntegrityRejected` の組み合わせを固定するテストが無い。本スライスで足す（§5 C24）。
  - S0-b の結論（program §5.2）: snapshot の機構 (i)・(ii) とも成立し、勝者は選んでいない。どちらの機構でも候補の由来は `EditorSnapshot`、受け入れ根拠は `OpenSceneSnapshot` で、digest の検証を受け入れ理由にしない（program I2）。本スライスの汎用判定は由来と受け入れ根拠で分岐し、機構で分岐しない。
  - 本書の作成環境（Cloud）に Unity・pwsh・dotnet が無く、`tools/docs-audit.ps1`・`tools/contract-audit.ps1`・offline テストは未実行。link 先の存在は `git ls-files` で手で照合した。**Unity 上では何も確認していない。**
- UC ごとの利用者の入力（実行時の操作は無い。開発時の宣言を利用者の作業として数える）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| host が Play 前の互換の基準を作る | target、engine の version、root schema の version（必須の 3 つ） | なし（汎用の既定値を持たない） | host の開発者が開発時に宣言し、Play 前に policy を作る時に渡す。値の出所（Player の baked 値、Editor の build target 等）は host / 後続 adapter が決める | 目的の「Play 前に固定した互換の基準」そのもの。既定値を core に置くと core が特定の engine・target を知ることになり、I7 と S0-a の「既定を先に凍結しない」に反する |
| 同上（任意） | content set、revision（どちらも任意。与えれば完全一致を要求） | 与えなければ比べない | host の開発者が開発時に宣言 | program §4.1「同じ revision から取るのが標準」を host が Play 前に固定したい場合の手段。固定しない host は要求時に任意の互換 revision を受ける（§2.1 I-C1 の読み方） |
| 要求ごとに特定の内容を期待する | expected identity（任意。既存の `ContentRequest.ExpectedIdentity`） | 与えれば digest を含む全欄一致を要求 | 呼び出し側が要求時に | 目的の「要求が与えた expected identity」。新しい入力ではなく S0-a の既存の欄 |
| host 固有の比較を足す | 述語の一覧（任意。既存の `IContentCompatibilityRule` を実装した host の型） | 汎用の判定の後に、登録順に全てが採ると答えた時だけ採る | host の開発者が開発時に宣言 | 目的の「host が登録した時だけ追加の条件として加わる」。宣言名・表現・graph を汎用の最小集合に入れない（program U5） |
| 供給元の adapter が事実を申告する | engine の version と root schema の version を `Facts` の予約 key に書く | key の名前は core が定数で決める | adapter の開発者（S3 / S4）が実装時に | 汎用の判定に必要な事実の入れ物。既存の `ContentCandidate` の宣言を変えずに渡す唯一の経路（§2.5 U-S1-2） |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 特定の host 宣言無しで、expected identity / digest / target / schema の汎用の互換判定ができるか。`declaration` の内容・表現・graph は、host が登録した時だけ比べる述語として、汎用の判定を緩めずに追加できるか（program §8 S1、U5）。
- 問いへの A1 の答え（A2 で検証する仮説）: **できる。既存の公開型を変えずに、`IContentCompatibilityRule` の製品実装 1 つと、それが読む不変の基準（envelope）と、事実の予約 key で。** 汎用の判定は由来で分岐する。`ContentDirectory`（`VerifiedDigest`）の候補には、identity の 4 欄がそろうこと、要求の expected identity と全欄一致すること（与えられた時）、envelope の target・engine の version・root schema の version と一致すること、envelope が content set・revision を固定していればそれとも一致することを要求し、事実が欠けた・形が壊れた候補は採らない。`EditorSnapshot`（`OpenSceneSnapshot`）の候補には digest・revision・target・engine・schema の比較をしない（受け入れ理由が digest ではないため。program I2）。それ以外の由来（`Addressables` と、将来足される値）は採らない。host 述語は既存の `IContentCompatibilityRule` をそのまま使い、汎用の判定が採ると答えた後に登録順に評価して、全てが採ると答えた時だけ採る（AND だけ。汎用の不採用を覆す経路を作らない）。
- どうすればこの問いに誤って YES を出せるか（反例。A2 と C はこれを探す）:
  - (a) 欠けた事実を「比べられないので一致」と扱う（engine の version の無い候補、schema の無い候補が通る）。壊れた manifest を誤採用する（I-C1、C5）。
  - (b) 寛容な解析で「一致」を作る: `" 2"`、`"+2"`、`"02"`、`"2.0"`、全角数字、大文字小文字の違う target を一致とする（I-C1、C6・C10）。
  - (c) `default` の判定結果（struct の 0）が「互換」と読める（I-C6、C18）。
  - (d) host 述語が true を返せば汎用の不採用を覆せる合成（OR、上書き、最後の述語の結果だけを見る）（I-C2、C13）。
  - (e) host 述語の例外を握りつぶして「不採用」や「採用」にする（resolver の fault で止まらない）（I-C2、C14）。
  - (f) `declaration.name` を汎用の判定に入れて、空の name 同士を一致とする。または host 述語を登録していないのに宣言を比べて、宣言の無い正規の候補を落とす（I-C3、C11・C12）。
  - (g) snapshot の候補を digest が無いことで落とす（snapshot tier が死ぬ）、または逆に snapshot の候補に「digest 検証済み」と読める理由を残す（I-C4、C16）。
  - (h) 将来 `ContentSourceKind` に値が足された時に、その由来の候補を汎用の判定が素通りさせる（I-C4、C17）。
  - (i) 同じ content set・revision を名乗る別 digest の候補を、expected identity が与えられているのに採る（同名異内容。I-C1、C8）。expected identity が**無い**時に同名異内容を見分けたと主張する（§2.1 I-C1 の読み方。汎用の判定は期待されていない内容を知り得ない）。
  - (j) 規則の外で互換を判定する（新しい規則を通すために `ContentResolver` の順序や `ProbeOutcome` を変える。依頼者の指示に反する）（I-C5、AC2・AC3）。
  - (k) fake の provider と fake の事実で通っただけで、実在の manifest から作った候補が通ると主張する（§2「成果の範囲」。Unity と実 manifest では未検証）。
  - (l) 新しい file を `Core/` の下の子 folder に置き、T15 の非再帰の走査を逃れて Unity・host の語を持ち込む（I-C3、AC1）。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- このスライスの成果の範囲: GO は「既存の公開型と resolver を変えずに、汎用の互換規則と host 述語の合成が、fake の供給元と fake の事実の上で、program §8 S1 の gate の反例（同名異内容、壊れた manifest、異なる version、別表現、旧 manifest）を採らないこと」だけを意味する。実在の manifest から事実を作る adapter、Unity の Content Directory からの実ロード、Editor Play の入口、複数 package の合成、要求をまたぐ一貫は主張しない。**Unity 実機では何も検証しない**（本スライスの対象は pure な core で、Unity で確かめるのは Phase B の compile と Phase C の全 EditMode 回帰による非退行だけ）。program §8 の「誤った合格を除外する」の列をそのまま引き継ぐ。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-C1 非互換の directory 候補を採らない（program I1・I5、G08）:** `ContentCompatibilityRule` を規則に持つ policy で、由来 `ContentDirectory` の候補が次のどれかに当たれば採用されない。(1) identity の 4 欄のどれかが空（空白だけを含む）。(2) 要求に expected identity があり、候補の identity と全欄一致しない。(3) envelope の target と候補の `Identity.Target` が ordinal で一致しない。(4) 候補の `Facts` に engine の version の予約 key が無い・値が空白だけ・envelope の値と ordinal で一致しない。(5) root schema の version の予約 key が無い・正規の 10 進表記（ASCII の `1`〜`9` で始まり ASCII の数字だけ、9 桁以内）でない・envelope の値と一致しない。(6) envelope が content set または revision を固定していて、候補の値と ordinal で一致しない。
  - 読み方（A2 で読み違いを探す）: **expected identity も envelope の revision も無い要求では、互換の基準を満たす候補はどの revision・digest でも採ってよい**（供給元の順で最初に互換と判定された候補）。この時、上位の供給元が非互換で落ち、下位の供給元の別 revision が採られることは program §3.1 の 10「非互換 revision への暗黙 fallback」に当たらない（採られた候補は互換であり、上位の不採用は理由付きで probe に残る）。合格例: envelope（target T、engine E、schema 2）、Local が schema 1 の候補、LAN が schema 2 の別 revision の候補 → LAN を採り、Local は `RuleRejected`（理由 `compat:SchemaVersionMismatch`）。不合格例: 同じ条件で要求の expected identity が Local の identity → LAN の候補は `PinMismatch`、Local は `RuleRejected`、採用なし。同名異内容（同じ content set・revision で digest が違う）を見分けるのは expected identity が与えられた時だけで、無ければ採った digest が record に残る（Traceable）だけである。
  - 崩しうる経路: (a) 規則が `Facts` を `TryGetValue` で読み、無い時に既定値や「比べない」に倒す。(b) schema を `int.TryParse`（culture・符号・空白・先頭 0 を許す）で読む。(c) 文字列の比較に `StringComparison.OrdinalIgnoreCase` や culture 依存の比較を使う。(d) envelope を可変にして、policy を作った後に値が変わる。(e) resolver の判定順で規則より先に採用される経路: 無い（規則は採用の直前に必ず呼ばれる。`ContentResolver.cs` 116〜142 行目）。(f) `ContentCandidate.Facts` を候補を作った後に変える: constructor が複製するので無い（`ContentCandidate.cs` 96〜116 行目）。(g) 修復（identity 付きの `Rejected`）の後の候補: resolver が pin と根拠を先に見るが、規則は修復中の候補にも同じ判定をする（C27）。
  - 探索した境界: `ContentResolver.ResolveAsync` の 1 経路、`ContentCandidate` の constructor、`ContentIdentity` の等値、`ContentManifestValidation.Validate`（比較の対象と粒度の参考。変えない）。未確認: 実在の provider adapter が manifest の値を `Facts` へ正しく写すか（S3 / S4。本スライスに実在の adapter は無い）。
- **I-C2 host 述語は汎用の判定を緩めない:** host が登録した述語は、汎用の判定が採ると答えた候補にだけ評価され、登録順に全てが採ると答えた時だけ採用される。述語が true を返しても汎用の不採用は覆らない。述語が `OperationCanceledException` 以外の例外を投げたら、規則はそれを握りつぶさずに投げ、resolver は `Fault` を記録して探索を止め `ProviderFault` で失敗する（S0-a の既存の契約）。述語の一覧に null は入れられず、規則を作った後に元の一覧を変えても影響しない。
  - 崩しうる経路: (a) 述語を汎用の判定の前に評価し、その結果で早期に true を返す。(b) 述語の結果を OR で合成する、または最後の述語の結果だけを返す。(c) `try { } catch { return false; }` で述語の例外を不採用に変える（例外の事実が `Fault` として残らず、壊れた host の述語で全候補が静かに落ちる）。(d) 一覧を複製せずに保持する。
  - 探索した境界: 規則の `Evaluate` / `Accepts` の 1 経路と、resolver の規則呼び出しの `try` / `catch`（`ContentResolver.cs` 118〜133 行目）。未確認: なし。
- **I-C3 汎用の判定は host の宣言を必要としない（program I7、U5）:** 本スライスの製品の型（`Runtime/ContentResolution/Core/` の新規 file）は、宣言名・表現の中身・graph の version を読まず、比べず、`declaration` に当たる予約 key を定義しない。host 述語を登録しない policy では、候補の `Facts` に宣言に当たる事実があってもなくても判定は変わらない。新規 file は `Core/` 直下に置き（T15 の走査の対象）、SampleGame の Scene 名・Season・Cell・RunPurpose・宣言名・VCS・SceneResourceMap・UICommon・Workset の語と UnityEngine を含まない。表現は既存の resolver が要求との一致を見るだけで、規則はその中身を解釈しない。
  - 崩しうる経路: (a) 「便利だから」と `declaration.name` の比較を汎用の判定に入れる（空の name 同士が一致する。U5）。(b) host の例に使う宣言名（`Spring` 等）を core のテストや既定値に書く。(c) 新規 file を `Core/Compatibility/` 等の子 folder に置き、T15 が走査しない（反例 (l)）。
  - 探索した境界: 新規 file の全部（T15 と AC6 の grep）。未確認: なし。
- **I-C4 由来ごとの受け入れ理由を混ぜない（program I2、S0-b の結論）:** 由来 `EditorSnapshot`（受け入れ根拠 `OpenSceneSnapshot`）の候補に、汎用の判定は identity の欄・engine・schema・envelope の content set / revision を要求せず、digest が無いことを不採用の理由にしない。逆に、snapshot の候補を採った時の規則の理由に「digest」「verified」に当たる語を残さない。由来が `ContentDirectory` でも `EditorSnapshot` でもない候補（`Addressables`、将来足される値）と、由来と受け入れ根拠の組が resolver の固定表と合わない候補は、規則を直接呼んでも採ると答えない。判定は由来と受け入れ根拠で分岐し、snapshot の機構 (i) / (ii) では分岐しない。
  - 崩しうる経路: (a) 汎用の判定を由来に関係なく全候補に当て、identity の空な snapshot が落ちる。(b) `switch` の `default` を「採る」にして、将来の由来を素通りさせる。(c) 規則が resolver の固定表を信じて根拠の検査を省き、規則を単独で呼ぶ利用者（S0-c の入口が候補を検査する等）に固定表外の組を採らせる。
  - 探索した境界: `ContentSourceKind`（4 値）、`ContentAcceptanceBasis`（3 値）、`ContentResolver.RequiredBasis`（219〜230 行目）。未確認: snapshot の候補に host 述語をどう当てるか（§2.5 U-S1-3）。
- **I-C5 既存の公開契約と既存の挙動を変えない（依頼者の指示、program I8・I10）:** base に在る `Runtime/ContentResolution/**` の file（`Core/` の 20 file、`ContentRequestLoader.cs`、`ContentLoadOutcomes.cs`）と、`Runtime/Bootstrap/**`、`Runtime/AssetManagement/**`、`Runtime/BuildContent/**`（`ContentManifestValidation` を含む）、`Runtime/SceneSystem/**`、`SampleGame/**`、`Editor/**`、`.asmdef`、`ProjectSettings/`、`Packages/` を変えない。既存の offline テスト T1〜T26 の名前・assertion・本文と、既存の EditMode テストを変えない。
  - 崩しうる経路: (a) 理由 code を機械で読むために `ProbeOutcome` に値を足す。(b) engine・schema を渡すために `ContentCandidate` や `ContentIdentity` に欄を足す。(c) 「互換の不採用で次へ進ませない」ために resolver の判定を変える。(d) S0-c が使う入口から規則を呼ぶ配線を `Editor/**` や `SampleGame/**` に足す。(e) Unity Editor が import 時に書き換えた既存 `.meta` を一緒に commit する。
  - 探索した境界: AC2 の変更集合の許可リスト。未確認: なし。
- **I-C6 不採用は理由付きで残り、成功に化けない（program §4.1 fail-closed、Q5）:** 規則が採らない候補は、resolver の probe に `RuleRejected` として残り、`Reason` は `compat:<不一致の種別>` で始まる（種別は §3 の `ContentCompatibilityMismatch` の名前）。全ての候補が規則で落ちた要求は `NoCompatibleCandidate` で失敗し、`NoCandidate`（不在）とも成功とも区別される。規則の判定結果の型は、`default` の値が「互換」と読めない。
  - 崩しうる経路: (a) 判定結果の enum の 0 を「互換」にする。(b) 理由を空文字で返す。(c) 規則が例外で不採用を伝え、resolver が `Fault` で止まる（互換の不採用で探索が止まり、下位の互換な候補へ進めない）。
  - 探索した境界: resolver の `RuleRejected` の記録と `DecideFailureCode`（`ContentResolver.cs` 135〜139 行目、277〜316 行目）。未確認: なし。

**成立条件（起きなければならないこと）**

- **E1 汎用の判定の成立:** host 述語の無い `ContentCompatibilityRule` で、envelope と一致する完全な identity と事実を持つ `ContentDirectory` の候補が採用される。I-C1 の (1)〜(6) の各 1 欄だけを外した候補は、それぞれ別の `ContentCompatibilityMismatch` の値で採られない。
- **E2 host 述語の追加:** host が登録した述語（テストの fixture。宣言名の一致、宣言の表現と要求の表現の一致、graph の version の一致の 3 種）が、汎用の判定を通った候補にだけ評価され、合わなければ採られず、合えば採られる。登録しなければ同じ候補・同じ事実で判定が変わらない。
- **E3 snapshot tier の維持:** host 述語の無い規則で、identity が空の `EditorSnapshot`（`OpenSceneSnapshot`）の候補が採用され、規則の理由に digest の検証を主張する語が無い。
- **E4 program gate の反例:** 同名異内容（expected identity あり）、壊れた manifest（事実の欠落・形の崩れ・identity の欠け）、異なる version（engine）、別表現（resolver の既存の表現一致と、host 述語の宣言の表現）、旧 manifest（root schema が古い）の各候補が採られない。
- **E5 Unity 非依存と非退行:** offline テスト project が Core の file だけを link してビルド・実行でき（exit 0、1 件以上実行、failed 0）、既存の 28 件と §5 の C1〜C27 が全て通る。Unity でのコンパイルが通り、全 EditMode 回帰に base からの退行が無い。`pwsh tools/contract-audit.ps1` が合格する。

### 2.2 受け入れ条件

合否は各 AC の本文で決める。「確認手段」は合否を見る手段で、同じ意味を別の手段で示してもよい。

- **AC1（E5, I-C3）core の純粋さ:** 新規の `.cs` が全て `Runtime/ContentResolution/Core/` の**直下**にあり、`#nullable enable` で始まり、T15 が拒む `using` と語、`record` 宣言、`init` accessor を持たない。`tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が新規の Core の file を `<Compile Include>` で link し、`PackageReference` を持たない。`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が exit 0 で、最後の行が `ContentResolution offline: <n> passed, 0 failed, <n> executed`（n ≥ 55）。
  - 確認手段: `git diff --name-status` で新規 file の置き場所を見る。`dotnet run` の出力。T15 が新規 file を走査した事実は「core files are found」の件数（base の 20 から新規 file 数だけ増える）で見る。
  - 不合格の例: `Core/Compatibility/ContentCompatibilityRule.cs` に置き、T15 の対象外になる。
- **AC2（I-C5）変更集合の許可リスト:** base から head までの変更（全 path）が次だけである。これ以外の追加・変更・削除が 1 件でもあれば不合格。
  1. 新規 `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentCompatibility*.cs` と各 `.meta`（§3 の 5 file）。
  2. `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` への `<Compile Include>` の追加（新規 file の数だけ）。
  3. `tools/ContentResolutionOfflineTests/Program.cs` の変更: `partial` の付加と、`Main` から新しいテストを呼ぶ行と、§5 C24 の 1 件の追加だけ。既存の T1〜T26 の名前・assertion・本文は変えない。
  4. 新規 `tools/ContentResolutionOfflineTests/CompatibilityTests.cs`。
  5. `docs/handoff/` 配下の HANDOFF の記録（本書と program §8 の S1 行）。
  6. harvest 先の公開文書（Phase D）。
  - 確認手段: `git diff -M --name-status <base> <head>` の全行を照合する。`git diff <base> <head> -- tools/ContentResolutionOfflineTests/Program.cs` で既存のテストの本文に変更が無い。
  - 不合格の例: `M .../Core/IContentCompatibilityRule.cs`（XML doc の書き換えも含む。§2.5 U-S1-5）。`M .../Core/ProbeOutcome.cs`。Unity が書き換えた既存の `.meta` が入る。
- **AC3（I-C5）既存の公開型と resolver の不変:** AC2 で base の `Runtime/ContentResolution/**` に変更 0 件なら成立。加えて `git grep -n "ContentCompatibility" -- unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem unity/Assets/OneStarMaker/Scripts/Editor unity/Assets/SampleGame` が 0 件（本スライスの型を誰も配線していない）。
  - 確認手段: 上の grep。
- **AC4（E1, E3, E4, I-C1, I-C2, I-C4, I-C6）offline テストの名前の照合:** AC1 の出力に、§5 の C1〜C27 の各テスト名について `PASS <テスト名>` の行があり、既存の T1〜T26（28 件）の `PASS` 行も全てあり、`FAIL` の行が無い。各 C が検証する条件は §5 の表で紐付ける。
  - 確認手段: 出力の `PASS` 行と §5 の一覧の照合。
  - 不合格の例: C を統合して名前が消えている（統合するなら A3 の決定記録で §5 を直す）。
- **AC5（I-C6）理由 code の形:** C21 が、規則で落ちた probe の `Reason` が `compat:` + `ContentCompatibilityMismatch` の値の名前 + `:` で始まることを、I-C1 の (1)〜(6) と host 述語の不採用の各場合で確かめ、snapshot の採用時の理由に `digest` / `verified`（大文字小文字を無視）が含まれないことを確かめる（C16）。
  - 確認手段: AC4 の該当テスト名の `PASS`。
- **AC6（I-C3）汎用性の文字列検査:** `git grep -n -i -E "SceneDirector|SampleGame|Spring|Summer|Autumn|Winter|Season|Cell|RunPurpose|SceneResourceMap|UICommon|Svn|Workset|declaration" -- 'unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentCompatibility*.cs'` が、`Cancel` を含む行を除いて 0 件。テスト（`CompatibilityTests.cs`）は host の fixture として `declaration` の語を使ってよいが、SampleGame の実名（`Spring` 等の宣言名・Scene 名）を使わない。
  - 確認手段: 上の grep と、`CompatibilityTests.cs` に対する同じ grep から `declaration` を除いた版。
- **AC7（E5, I-C5）Unity のコンパイルと全 EditMode 回帰:** GO 候補 head で Unity Editor のコンパイルが通る（Phase B が確認。できなければ未確認と明記し Phase C の実行で代える）。base と GO 候補 head のそれぞれで、同じ機械・同じ Unity 版・同じコマンド（`pwsh tools/run-tests.ps1`、空 filter、EditMode）で全 EditMode を実行し、head の結果 XML が 1 件以上実行かつ failed 0、base で passed のケース ID（full name）が head で全て passed、Inconclusive のケースの集合が base と同じ（`-nographics` の VFX の 25 件。program §8「全 EditMode 回帰の受け入れ条件の書き方」）。`step.json` の `failed` だけを根拠に不合格にしない。
  - 確認手段: 2 つの XML から full name と結果を抜き出して比べる。総数・passed・skipped・failed・inconclusive を記録する。base が failed 0 にならなければ比較の基準が成立しないので、head を判定せずに人間へ返す。
  - 合格の例: base 2,000 件（passed 1,975、inconclusive 25）、head 2,000 件で同じ集合が passed、同じ 25 件が inconclusive。
  - 不合格の例: head で base の passed の 1 件が skipped になる。head の XML が不完全で件数を数えられない。
- **AC8（E5）contract-audit と CI:** `pwsh tools/contract-audit.ps1` が合格する。CI の `dotnet-offline-tests`（既に `ContentResolutionOfflineTests` を含む）が PR で緑であることは Phase D のマージ条件とし、判定 C の合否にしない。
  - 確認手段: audit の出力。Phase D で PR の check。

### 2.3 ここでは答えない問いと所有スライス

| 問い | 所有 |
|---|---|
| 実在の manifest（`ContentTransportManifest` + 検証済みの digest）から候補の `Identity` と互換の事実を作る adapter。`ContentManifestValidation` の `SupportedUnityVersion` / `SupportedRootSchemaVersion` 定数と、host が envelope に渡す値の出所を一本にすること（二つの真実源が食い違う危険） | S3 `RUNTIME_PROVIDER_INTEGRATION`、S4 `LOCAL_LAN_PROVIDER` |
| 複数 package を 1 つの合成単位として採る可否（共同の build plan / dependency graph の出所、shared asset の所有と重複、同一 logical identity の衝突規則、package 索引の hash 関係。G24）。program §4.1 の「S1 が定める互換 envelope」の後半 | S2 `CONTENT_PACKAGE_DISCOVERY`（§2.5 U-S1-1 で owner に確認） |
| 要求をまたぐ revision の一貫（envelope が revision を固定しない時、最初に採った revision に後の要求を揃える）、要求をまたぐ pin | S4 `LOCAL_LAN_PROVIDER` |
| S0-c の Editor Play 入口が、ledger に書く directory の pair を本スライスの規則で検査すること（program §10 U4 の「S1 が互換判定と接続する」）。明示の directory-only Play で snapshot の provider を policy から外すこと（G18） | S0-c `EDITOR_PLAY_ENTRY_PROOF` の後の接続（S0-c の Phase D 後に、S0-c か S5 の A1 で所有を決める。本スライスは呼べる公開の型を用意するだけで、入口に触れない） |
| transport の protocol version（`version`、`playerConfigSchemaVersion`、`product`）を候補の採否に含めるか | install 時の `ContentManifestValidation` が既に拒否する。resolver 側で要るかは S3 / S4 の adapter が決める |
| engine の version の範囲一致（同じ major の patch を許す等）、互換の緩和 | 本 program の外。必要になれば Phase A の新しい revision（host 述語は AND なので緩和に使えない） |
| `Full` fallback の実効表現を record に写すこと、採用した候補と実ロード元の同一性 | S3 |
| Editor snapshot の機構 (i) / (ii) の選定 | S0-c / S3 の A1（program §10 U2） |
| 理由 code を `ProbeOutcome` の値として持つこと（文字列の前置ではなく） | 既存の公開 enum の変更なので本スライスでは行わない。要るなら S5 の観測 UI の A1 で、S0-a の契約の revision として |

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約と既存の契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない（本スライスは asmdef に触れない。新規 file は `OneStarMaker.Runtime.asmdef` の配下の `Core/` 直下）。
- 新規の Unity 側 `.cs` は先頭に `#nullable enable`。`record` を使わない。core では `init` accessor も使わない（S0-a と同じ理由: `IsExternalInit` polyfill は `OneStarMaker.Runtime` の internal で、core を将来別 asmdef に分けた時に壊れる）。
- core は UnityEngine・UniTask・OSM の他の部分を参照しない。.NET 8 の offline project で core の file だけを link してビルドできることが閉包の証拠になる（他の OSM 型を参照すればビルドが失敗する）。
- net8 の BCL にあって Unity の API 互換レベル（.NET Standard 2.1）に無い API を使わない（`ArgumentNullException.ThrowIfNull`、`int.Parse(ReadOnlySpan<char>, …)` の一部 overload、`string.Contains(char)` 等）。offline では通るので Phase B の Unity コンパイルで捕まえる。schema の解析は §2.1 I-C1 (5) の規則を手書きで実装し、`int.TryParse` を使わない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。本スライスのテストは同期の判定と、完了済みの `ValueTask` を返す fake provider だけで組む。
- 参照 0 を削除理由にしない。`ContentTraceability.Reproducible` 等の先行宣言に触れない。
- path は `tools/path-budget.json` の予算（150 文字）に従う。最長の新 path は `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentCompatibilityEnvelope.cs.meta`（101 文字）。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。offline テスト（`dotnet run`）は Unity でも Build でもないので Phase B が B exit で実行してよい。Phase B を Cloud（Unity の無い環境）で行う場合、Unity のコンパイルは「未確認」と §6 に書き、Phase C の全 EditMode 回帰（コンパイルを含む）で代える。
- 本スライスは `AbstractApplicationInitializer` と S0-c の file に触れないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` との Phase B の同時実行制約と、S0-c との衝突の対象外。

### 2.5 未決事項（A2 の回答と合わせて A3 で owner が裁定する）

- **U-S1-1 program §4.1 の「S1 が定める互換 envelope」の範囲。** program は package の合成可否を「S1 が定める互換 envelope（共同の immutable build plan / dependency graph の出所、target / engine / schema、表現、shared asset の所有と重複の方針、同一 logical identity の衝突規則、package 索引の hash 関係）」で判定するとしている。A1 の推奨: **本スライスは 1 候補の採否の envelope（target / engine / schema、任意の content set / revision）だけを定め、合成の欄（build plan・graph・shared asset・索引の hash）は S2 へ送る。** 理由: resolver は 1 要求に 1 候補を採る構造で、複数候補の関係を比べる場所が無い。合成の欄に値を書く producer（manifest の欄）が今は無く、先に型を置くと検証できない先行宣言になる。G24 の実証は S2 の gate。採るなら program §4.1 の文言を owner が直す（本書は program の文言を変えていない）。
- **U-S1-2 engine / schema の事実の渡し方。** A1 の推奨: `ContentCandidate.Facts` の予約 key（`osm.compat.engineVersion`、`osm.compat.rootSchemaVersion`。core の定数 `ContentCompatibilityFactKeys`）。代替: `ContentCandidate` か `ContentIdentity` に欄を足す（型で強制できるが、依頼者の指示「既存 Runtime の公開契約を変えない」に反するので本スライスでは採らない）。key の取り違えは adapter の責任で、欠けた key は I-C1 で不採用になる（fail-closed）。
- **U-S1-3 snapshot の候補に host 述語を当てるか。** A1 の推奨: **当てる。** 汎用の判定だけが由来で分岐し、host 述語は全ての候補に評価され、述語が自分で `candidate.SourceKind` を見て決める。理由: 汎用側が「snapshot には述語を当てない」と決めると、host が snapshot にも課したい条件（logical key の命名規則等）を課せない。危険: 宣言名を比べる述語を素朴に書くと、宣言の事実を持たない snapshot が全て落ち、snapshot tier が黙って死ぬ。テストの fixture は `ContentDirectory` にだけ宣言を比べる形で書き（C12）、harvest の文書にこの注意を書く。代替: 述語ごとに対象の由来を宣言させる（新しい interface が要る）。
- **U-S1-4 同名異内容の主張の範囲。** program §8 S1 の gate「同名異内容を誤採用しない」を、本書は「expected identity が与えられた時に採らない」と読む（§2.1 I-C1 の読み方）。expected identity の無い要求で同名異内容を見分けるには「どの digest が正しいか」の外部の知識が要り、汎用の判定の外にある。owner がこの読み方を認めるか、要求をまたぐ一貫（S4）まで gate を持ち越すかを決める。
- **U-S1-5 `IContentCompatibilityRule` の XML doc。** base の doc は「製品実装が入るまでは、ホストは自前の規則を渡さない限り `ResolutionPolicy` を組めない」と書いており、本スライスの後は事実でなくなる。A1 の推奨: 本スライスでは file を変えず（AC2・依頼者の指示）、Phase D の harvest で 13-resource-system.md 側を現況に直し、doc の 1 文の修正は owner の承認を得て Phase D の harvest commit に含める（宣言は変えない）。owner が「doc comment も公開契約に含める」と判断するなら、後続のどこかで直すまで doc の古さを残す。

### 2.6 スライスの分割の検討

分割しない。契機の確認: (1) 独立した変更理由の混在 — 汎用の判定と host 述語の合成は「候補を採ってよいかの製品の規則」という 1 つの変更理由で、合成規則（AND・評価順・例外の扱い）は汎用の判定の不変（I-C2）と切り離して判定できない。(2) 高リスクの初回実機の未知 — 無い（pure な core で、実機の検証を含まない）。(3) 別の問いの判定に使う受け入れ条件の混入 — S0-a から移送された「pin 未充足 + `Fault` → `IntegrityRejected`」のテスト（C24）は resolver の既存挙動を固定するテストで本スライスの問いではないが、program §8 が S1 以降に送った入力で、製品 code を変えず 1 件のテストを足すだけなので同居させる。A2 が分離を求めれば C24 を外して後続へ送る（問いの判定には影響しない）。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/` からの相対（offline テストは repository の root から）。型名は A3 で凍結する。行数は予想。全ての製品の型は不変の値で、一覧は constructor で防御的に複製する。namespace は既存の `OneStarMaker.Runtime.ContentResolution`。asmdef は既存の `OneStarMaker.Runtime`（変えない）。

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 単体テスト | 予想行数 |
|---|---|---|---|---|---|---|---:|
| `ContentCompatibilityEnvelope.cs`（新規） | Play 前に固定する 1 候補の互換の基準: `Target`・`EngineVersion`（必須、空白だけを拒む）、`RootSchemaVersion`（必須、1 以上）、`ContentSet`・`Revision`（任意。null は「固定しない」、空白だけは `ArgumentException`）。比較は持たない | 基準の欄が増えるとき | host が作り policy の持ち主が保持する不変の値。寿命は policy と同じ | なし | public sealed class | offline（C19） | 70 |
| `ContentCompatibilityFactKeys.cs`（新規） | 候補の `Facts` に互換の事実を書く予約 key の定数（`EngineVersion = "osm.compat.engineVersion"`、`RootSchemaVersion = "osm.compat.rootSchemaVersion"`）と、予約の接頭辞 `osm.compat.`。宣言に当たる key は定義しない（I-C3） | 予約 key が増えるとき | 定数 | なし | public static class | offline（C5 が使う） | 25 |
| `ContentCompatibilityMismatch.cs`（新規） | 判定の結果の種別: `Unset = 0`（無効。`default` が互換と読めないため）、`Compatible`、`UnsupportedSource`、`IncompleteIdentity`、`ExpectedIdentityMismatch`、`ContentSetMismatch`、`RevisionMismatch`、`TargetMismatch`、`EngineVersionMissing`、`EngineVersionMismatch`、`SchemaVersionMissing`、`SchemaVersionMalformed`、`SchemaVersionMismatch`、`HostPredicateRejected` | 種別が増えるとき | 値型 | なし | public enum | offline | 40 |
| `ContentCompatibilityVerdict.cs`（新規） | 1 候補の判定結果: `Mismatch`、`Reason`（`compat:<Mismatch>:` で始まる文字列）、`IsCompatible`（`Mismatch == Compatible` のときだけ true）。`default` は `Unset` で `IsCompatible == false` | 結果の欄が増えるとき | 値型（readonly struct） | 上の enum | public readonly struct | offline（C18） | 45 |
| `ContentCompatibilityRule.cs`（新規） | `IContentCompatibilityRule` の製品実装: `Evaluate(request, candidate) → ContentCompatibilityVerdict`（由来で分岐する汎用の判定 → 登録順の host 述語の AND）と、それに委譲する `Accepts`。host 述語の一覧は constructor で複製し、null の要素を拒む。述語の例外は握りつぶさない | 汎用の判定の規則、合成規則が変わるとき | host が作り policy が保持する不変の値。状態を持たず、同じ入力に同じ結果 | 上の 4 つ、既存の `ContentRequest` / `ContentCandidate` / `ContentIdentity` / `ContentSourceKind` / `ContentAcceptanceBasis` / `IContentCompatibilityRule` | public sealed class | offline（C1〜C27） | 170 |
| `tools/ContentResolutionOfflineTests/CompatibilityTests.cs`（新規） | §5 の C1〜C23・C25〜C27 と host 述語の fixture（宣言名・宣言の表現・graph の version を `Facts` の host 固有の key で比べる 3 種。空の期待値・空の事実は一致にしない）。`Program` の `partial` として既存の helper（`Req`、`Cand`、`Provider`、`Policy`、`Resolve`、`Run`、assertion）を使う | 互換のテストが増えるとき | テスト | linked core | なし | — | 520 |
| `tools/ContentResolutionOfflineTests/Program.cs`（変更） | `partial` の付加、`Main` から互換のテストを呼ぶ行、C24（S0-a から移送された resolver の既存挙動の固定）の 1 件 | — | テスト | linked core | なし | — | 693 → 740 |
| `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`（変更） | 新規の Core の 5 file の `<Compile Include>` | — | — | — | — | — | 33 → 38 |

- 行数警報: 新規の `ContentCompatibilityRule.cs`（170）は 500 行未満。判断は 1 つ（候補を採ってよいか）で、汎用の判定と合成規則は同じ変更理由（§2.6）。`Program.cs` は 50% 以上の増加を避けるため、互換のテストを `CompatibilityTests.cs` に分ける（変更理由: 互換のテストは S1 の規則と一緒に変わり、T1〜T26 は resolver と一緒に変わる。helper は共有するので `partial` にする）。`CompatibilityTests.cs`（520）は 500 行の警報に掛かりうるが、同じ変更理由・同じ依存のテストの列で、分割すると helper の共有が増えるだけなので非分割（A2 のアーキテクチャゲートが確認）。
- 置き場所の理由: 規則は S0-a の `IContentCompatibilityRule` の製品実装で、同じ pure な core に属する（Unity・AssetManagement・BuildSystem を知らない）。`Core/` 直下に置く理由は、T15 の汎用性・純粋さの検査（非再帰）の対象に入れるため（反例 (l)）。子 folder を作るなら T15 の走査を再帰に変える必要があり、既存テストの本文の変更になる（AC2）。`BuildContent/Distribution/` に置かない理由: install の検査（bytes の検査）と候補の採否は別の層で、Distribution に置くと resolver の core が BuildSystem 本体を参照することになる（ASSET_BUILDSYSTEM_INDEPENDENCE の境界）。
- Policy / orchestration / I/O の区分: 本スライスの製品の型は全て policy（判定の規則と、その入力の値）。orchestration は既存の `ContentResolver`、I/O は後続の adapter。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序:
  1. `ContentCompatibilityMismatch` → `ContentCompatibilityVerdict` → `ContentCompatibilityFactKeys` → `ContentCompatibilityEnvelope` → `ContentCompatibilityRule`。
  2. csproj に link を足し、`CompatibilityTests.cs` の C1〜C27（C24 は `Program.cs`）を書く。
  3. `dotnet run` で全件を通す。
  4. Unity Editor で compile を確認する（できる環境なら）。`.meta` は Unity が生成した新規 file の分だけを commit する。
  5. `pwsh tools/contract-audit.ps1`（できる環境なら）。
- `Evaluate` の判定順（A3 で凍結する。理由 code はこの順で最初に当たった 1 つ）:
  1. `UnsupportedSource`: 由来が `ContentDirectory` で根拠が `VerifiedDigest`、または由来が `EditorSnapshot` で根拠が `OpenSceneSnapshot`、のどちらでもない。
  2. 由来 `ContentDirectory` の時だけ: `IncompleteIdentity` → `ExpectedIdentityMismatch`（要求の expected identity と全欄一致しない。resolver の pin と重なるが、規則を単独で呼ぶ利用者のために持つ）→ `ContentSetMismatch` → `RevisionMismatch` → `TargetMismatch` → `EngineVersionMissing` → `EngineVersionMismatch` → `SchemaVersionMissing` → `SchemaVersionMalformed` → `SchemaVersionMismatch`。
  3. host 述語を登録順に評価し、最初に false を返した述語で `HostPredicateRejected`（理由に述語の 0 起点の番号と述語が返した理由を含める）。
  4. 全て通れば `Compatible`。理由は `compat:Compatible:` + 由来（snapshot なら digest の検証を主張する語を含めない）。
- Phase B から Phase A へ差し戻す条件: 既存の公開型・resolver・`IContentCompatibilityRule` の宣言を変えないと §2.1 を満たせない。新しい interface（述語の対象の由来の宣言等）が要る。`Core/` の外の file（Distribution、Editor、SampleGame）を変える必要が出る。§2.5 の裁定と食い違う実装が要る。
- 対象外を維持する方法: AC2 の許可リストを Phase B の出口で `git diff --name-status` で自分で照合する。S0-c・Bootstrap・Distribution の file を開いても変えない。

## 5. テストとレビュー計画

- 単体テスト（offline、`tools/ContentResolutionOfflineTests/`。名前は `PASS <名前>` で照合する）:

| ID | テスト名 | 検証する条件 |
|---|---|---|
| C1 | `Compat_CompatibleDirectoryCandidate_IsAdopted` | E1 |
| C2 | `Compat_TargetMismatch_IsRejected_AndNextProviderIsTried` | E1、I-C1 (3)、I-C6 |
| C3 | `Compat_EngineVersionMismatch_IsRejected` | E4（異なる version）、I-C1 (4) |
| C4 | `Compat_OlderRootSchema_IsRejected` | E4（旧 manifest）、I-C1 (5) |
| C5 | `Compat_MissingFacts_AreRejected` | E4（壊れた manifest）、I-C1 (4)(5)、反例 (a) |
| C6 | `Compat_MalformedSchema_IsRejected`（`""`、`" 2"`、`"2 "`、`"+2"`、`"-1"`、`"0"`、`"02"`、`"2.0"`、全角の `"２"`、10 桁の値） | I-C1 (5)、反例 (b) |
| C7 | `Compat_IncompleteIdentity_IsRejected`（空の digest、空白だけの revision） | E4（壊れた manifest）、I-C1 (1) |
| C8 | `Compat_SameNameDifferentDigest_WithExpectedIdentity_IsNotAdopted`（resolver 経由で `PinMismatch`、規則の直接呼び出しで `ExpectedIdentityMismatch`） | E4（同名異内容）、I-C1 (2)、反例 (i) |
| C9 | `Compat_EnvelopeFixedContentSetAndRevision_MismatchIsRejected` | I-C1 (6) |
| C10 | `Compat_CaseDifference_IsMismatch`（target・engine・content set） | I-C1、反例 (b) |
| C11 | `Compat_WithoutHostPredicate_DeclarationFactsAreIgnored` | E2、I-C3、反例 (f) |
| C12 | `Compat_HostPredicates_DeclarationNameRepresentationAndGraph`（一致で採用、不一致で `HostPredicateRejected`、空の期待値・空の事実は一致にしない、宣言の表現の不一致は「別表現」） | E2、E4（別表現）、反例 (f) |
| C13 | `Compat_HostPredicate_CannotLoosenGenericRejection` | I-C2、反例 (d) |
| C14 | `Compat_HostPredicateException_IsFaultAndStops` | I-C2、反例 (e) |
| C15 | `Compat_HostPredicates_AllMustAccept_InOrder`（2 番目が拒むと 3 番目は呼ばれず、理由に番号 1） | I-C2 |
| C16 | `Compat_SnapshotWithEmptyIdentity_IsAdopted_WithoutDigestClaim` | E3、I-C4、反例 (g) |
| C17 | `Compat_UnsupportedSourceOrBasis_IsRejectedByRule`（`Addressables`、未定義の enum 値、`ContentDirectory` + `OpenSceneSnapshot`、`EditorSnapshot` + `VerifiedDigest`） | I-C4、反例 (h) |
| C18 | `Compat_DefaultVerdict_IsNotCompatible` | I-C6、反例 (c) |
| C19 | `Compat_Envelope_Validation`（空白の target / engine、schema 0 と負、空白だけの content set / revision） | I-C1 |
| C20 | `Compat_HostPredicateList_Validation_AndDefensiveCopy` | I-C2 |
| C21 | `Compat_RejectionReason_CarriesMismatchCode` | I-C6、AC5 |
| C22 | `Compat_AllIncompatible_IsNoCompatibleCandidate` | I-C6 |
| C23 | `Compat_PinMatchesButSchemaMismatch_IsNoCompatibleCandidate`（既存の resolver の終わり方の読み方を固定する） | I-C1、I-C6 |
| C24 | `UnmetPin_ThenFault_IsIntegrityRejected`（`Program.cs`。S0-a から移送された入力） | resolver の既存挙動の固定（program §8） |
| C25 | `Compat_EvaluateIsDeterministic_AndDoesNotMutateInputs` | I-C1 (d) |
| C26 | `Compat_NoHostPredicate_SameCandidateSameVerdict_AcrossSourceOrder`（供給元の順を入れ替えても、各候補の判定は同じ） | I-C1 の読み方 |
| C27 | `Compat_RepairAfterRejected_RequiresCompatibleCopy`（identity 付き `Rejected` の後、同じ identity でも schema が違えば採らず `IntegrityRejected`、互換なら採る） | I-C1 (g)、program §4.1 |

- 差し戻し中の起点 `-Filter`: Unity テストは不要（offline の `dotnet run` が起点）。Unity の差し戻し確認が要る場合だけ `OneStarMaker.Tests.ContentResolution`（既存の `ContentRequestLoaderTests`。本スライスの型を使わないが同じ assembly の compile を見る）。C は凍結済み条件の確認に必要な範囲を根拠付きで変えてよい。
- 判定必須テスト（GO 候補 head）: (1) offline の全件（AC1・AC4・AC5）。(2) base と head の全 EditMode 回帰（AC7、空 filter、`pwsh tools/run-tests.ps1`）。(3) `pwsh tools/contract-audit.ps1`（AC8）。
- 全 EditMode 回帰の適用除外: なし（新規 file が `OneStarMaker.Runtime` に compile されるため、標準どおり実行する）。
- 統合・Unity テスト: 新しい EditMode テストは書かない。本スライスの製品の型は Unity を使わず、Unity との接続（実 manifest の adapter、実ロード）は S3 / S4 にある。**Unity 実機での動作確認は本スライスの合格条件に無く、行わない。**
- 操作・実行時・目視条件の検証経路: 対象なし（操作・実行時・目視の条件が無い）。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（offline project と全 EditMode 回帰は S0-a で使った既存の経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（offline の出力の `PASS` 行と §5 の表の照合、2 つの XML の比較は、判定 C が凍結済みの手順として手で行う。機械向けの期待値 file は作らない）。
- 機械検査: `dotnet run`（offline）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC2・AC3・AC6 の `git diff` / `git grep`。
- A0/A1 主担当・モデル・ベンダー: A0 は program（revision 3.2）。A1 は Claude Code（Cloud）、Anthropic。
- A2 の計画（担当・モデルは owner が選ぶ。互いの指摘を見せない。入力版は A2 入力版 commit の本書。目的レビューは §1 の 2 欄の原文だけ）:
  - 目的レビュー: 問いは「この目的のために何を保証すべきか」「何を作らないべきか（要らない利用者の入力・操作・機構を含む）」に固定する。
  - アーキテクチャゲート: architecture-gates.md の 7 項目。特に、規則を `Core/` に置くこと、事実を予約 key で渡すこと（U-S1-2）、host 述語に新しい interface を足さないこと、`CompatibilityTests.cs` の非分割。
  - 反例レビュー: §2.1 の各不変条件を破る順序を 1 つ挙げられるか。特に I-C1 の読み方（expected identity の無い要求）と U-S1-4、I-C4 と U-S1-3。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 未実施。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: 未定（A2 で使う系列を owner が決めた後に、A2 に関与しない系列を 1 つ残す）。
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk `normal`）。

## 6. Phase B 実装結果

未着手。

### 動作確認

対象なし（§5 の検証経路に操作・実行時・目視の条件が無い）。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
