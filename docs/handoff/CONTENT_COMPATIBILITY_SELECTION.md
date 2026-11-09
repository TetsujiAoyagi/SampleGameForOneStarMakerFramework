# 汎用の Content 互換判定と host 述語（S1）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（**A3 統合版（外部レビュー 1 巡と owner の裁定を反映）。まだ凍結していない。** A2 の独立レビュー 3 件と、owner が依頼した外部レビュー（PR #174 のコメントと 2 件の行コメント。§5.1）の採否を本文に反映した。owner は 2026-11-09 に §2.5 の U-S1-1〜U-S1-7 を全て推奨どおりに裁定し、目的の文言を改め（§1）、未関与の担当による目的レビューのやり直しも済んだ（§5.1）。凍結は owner の指示を待つ（A3 凍結版の commit で status を凍結にする）。Phase B は未着手。A2 入力版は commit `50d6ff2e`、A3 統合版の最初の commit は `09ac3df7`。本版を commit にして push したのは、外部レビューに渡すための owner の指示による逸脱で、phases-and-handoff.md「Phase A の commit と snapshot」の「A2 入力版と A3 凍結版の 2 点だけ」の例外である（S0-a の「A3 整理版」と同じ扱い）。program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S1。A0 の入力は同書 §3・§4.1・§6（I1・I2・I5・I7・I8・I10）・§8 の S1 行と「S0-a から移送した入力」「S0-b から移送した入力」・§9 G08・§10 U4・U5。program の停止規則「S1 以降は S0-b の Phase D より前に Phase B を始めない」は、S0-b が PR #172 でマージ済みなので満たしている）
- branch: `claude/loving-lamport-7lbluf`（S1 のスライスブランチ。PR の base は `develop`）
- implementation base commit: `7b28eba`（`develop` の head、2026-11-08。本書の実測は全てこの base。Phase B の着手時に head を取り直し、`Runtime/ContentResolution/` に差分があれば §1 現況を読み直す）
- implementation head commit: `未固定`
- risk: `normal`（program §8 のとおり。既存の公開型・asmdef・所有者・寿命・`SceneState`・Addressables・起動順序を変えず、`Runtime/ContentResolution/Core/` に新しい型を足すだけ。新しい公開面の追加は、S0-a の owner 裁定（同スライスの Q1）と同じく「公開 API を変える」高リスクに数えない。A2 のアーキテクチャゲートはこの分類を妥当とした（§5.1 G-総合）。ただし理由の形式 `compat:<種別>:` と予約 key `osm.compat.*` は S3 / S4 / S5 が依存する事実上の契約になるので、A3 で名前を凍結し、harvest 文書に載せる）
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-11-08
- expires: 2026-12-31、または Phase D で harvest・削除した時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md` の「要求時の Content 解決」節（`IContentCompatibilityRule` の製品実装、互換 envelope、host 述語の合成規則と snapshot の扱い、由来ごとの汎用判定、互換の不採用が `RuleRejected` + 理由 code で残ること、終わり方が既存の優先順に従うこと、予約 key、まだ保証しないこと）。program の harvest（§18 §4 等）は program の完了時
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（**所有境界の正本。** 本スライスの型は S0-a と同じく AssetManagement の上の任意の層 `Runtime/ContentResolution/Core/` に置き、BuildSystem 本体を参照しない。境界を本書で定義し直さない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本スライスは起動コードに触れないので、Phase B の同時実行制約の対象外）、S0-c `EDITOR_PLAY_ENTRY_PROOF`（owner のローカルで進行中。本スライスは S0-c の入口・ledger・config に触れない。§2.3）
- Phase A snapshot の取得案内・状態: 未生成（Cloud に Artifact Storage が無いため、A3 統合版は snapshot ではなく owner の指示で commit と draft PR で渡す。機械向けの期待値・照合 script は無い（§5「判定 C の照合 script の方式」が `なし`））
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3 の凍結（owner の指示で主担当が行う）。
  - 次の作業: owner の凍結の指示を受けたら、§5.2 の自己点検の結果を確認し、owner の環境で `pwsh tools/docs-audit.ps1` を通してから、A3 凍結版を commit する（その commit の §0 は status を凍結に、本票を Phase B 向けに書き換える。implementation base は Phase B の着手時に取り直す）。外部レビューの追加の巡があれば、先に §5.1 と同じ形式で採否に分類する。
  - 引き継ぐ commit: 本書を含む A3 統合版の commit（SHA は tree の外、PR の状態ブロックで渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: A3 凍結まで Phase B を始めない。S0-c の入口・Bootstrap・既存 Runtime の公開型（S0-a の 20 file と resolver を含む）を変える提案は、本スライスでは採らず、§2.3 の所有先へ送るか owner に返す。U-S1-6 を (b)（問いの差し戻し）と裁定された場合は、本書を凍結せず program の A0 へ戻す。S0-c の計画（PR #175）が先にマージされた場合は、本書の program の変更（§8 の S1 行）を S0-c が足した S0-d の行の後に合わせて rebase する（同じ表の隣り合う行を両方が変えるので、後からマージする側に衝突が出る）。
  - 引き渡さないもの: 目的レビューのやり直しでは §1 の 2 欄の原文以外を渡さない（A2 の指摘、本書の他の欄、初稿を渡さない）。

### PR 状態ブロック

PR が存在する間は PR 本文先頭の状態ブロックを更新する。値は本 §0 を正とする。

## 1. 目的と対象外

- 目的: 要求時の Content 解決で、供給元が申告した候補を採ってよいかを、特定の host の宣言（宣言名・Scene 名・RunPurpose 等）を知らずに、Play 前に固定した互換の基準（content set・target・engine の version・root schema と、host が固定を宣言した時だけ revision）と、要求が与えた expected identity（digest を含む全欄）だけで判定できるようにする。host 固有の比較（宣言の名前・表現・graph の version 等）は、host が登録した時だけ汎用の判定に追加の条件として加わり、汎用の判定を緩めない。判定できない・合わない候補は採用せず、なぜ採らなかったかを供給元ごとに機械で区別できる理由として残す。
- 利用者: OSMFramework の開発者（供給元の adapter、Editor の Play 入口、SceneSystem adapter を後続スライスで書き、本スライスの規則を `ResolutionPolicy` に渡す人）と、OSM を使う host application の開発者（Play 前の互換の基準を宣言し、必要なら自分の述語を登録する人）。本スライスに実行時の利用者の操作は無い。
- 目的・利用者の文言の改訂（A1）: A0（program §1・§8 S1 行・§10 U5）から A1 で書き起こした初出の文言。A2 の目的レビューはこの原文を見た（§5.1。原文の sha256 `dbe366046a32fbaaedb8c8df434965fda998e2c9c92ca4d78f7481833fcb9cba`）。
- **目的の文言の改訂（A3、2026-11-09）:** 元の文言は「Play 前に固定した互換の基準（target・engine の version・root schema、必要なら content set と revision）」。目的レビュー P-07（「必要なら」の判断主体が曖昧）と反例レビュー X-04（content set を任意にすると別の content set の候補を採る）を受け、「互換の基準（content set・target・engine の version・root schema と、host が固定を宣言した時だけ revision）」に改めた。人間の承認: owner（root）が 2026-11-09 に U-S1-7 の推奨を採った。利用者の欄は変えていない。改めた 2 欄の原文（sha256 `de6f3a5c2db2699696f62cf449217d9365fc879c640a53639763c795a98b7831`）で、本書・初稿・A2 の指摘を読んでいない担当が目的レビューを 1 回やり直した（§5.1。やり直しは 1 回までで、これ以上 2 欄は変えない）。
- 対象外（所有する後続。§2.3 に一覧）:
  - 既存の公開型（`ContentRequest`、`ContentCandidate`、`ContentIdentity`、`ContentAvailability`、`IContentProvider`、`IContentCompatibilityRule`、`ResolutionPolicy`、`ContentResolver`、`ProbeOutcome`、`ContentResolutionFailureCode` 等）の宣言と、`ContentResolver` の判定順序・終わり方の変更。**依頼者の指示で変えない。** 互換の不採用は既存の `ProbeOutcome.RuleRejected` と `Reason` の文字列で表す（§2.1 I-C6）。要求の終わり方は既存の `DecideFailureCode` の優先順に従う。
  - S0-c の Editor Play 入口、`ExitingEditMode` の transaction、ledger、`content:` の config の書き込み（program §10 U4）: S0-c。本スライスの規則を S0-c の入口から呼ぶ接続も S0-c の後（§2.3）。
  - `Runtime/Bootstrap/**`、`AbstractApplicationInitializer`、`ContentDirectoryStartup` に当たる起動 code: 触れない。
  - 実在の manifest（`ContentTransportManifest`）から候補の事実を作る adapter、`ContentManifestValidation` の install 時検査との一本化: S3 / S4。
  - 供給元の申告（受け入れ根拠、identity、`Facts`）が実体の bytes と一致することの検証: 供給元の adapter（S3 / S4）。本スライスの規則は申告を照合するだけで、申告を真とは証明しない（目的レビュー P-08）。
  - 複数 package の合成可否（共同の build plan / dependency graph の出所、shared asset の所有と重複、同一 logical identity の衝突、package 索引の hash 関係）: S2（§2.5 U-S1-1）。
  - 要求をまたぐ pin・要求をまたぐ revision の一貫（最初に採った revision に後の要求を揃える）: S4。
  - Editor snapshot の機構 (i) / (ii) の選定: S0-d `EDITOR_SNAPSHOT_STARTUP_PROOF`（S0-c の計画 PR #175 が S0-c から切り出した新スライス。#175 のマージ前は program 上 S0-c / S3 の A1、program §10 U2）。本スライスは機構に中立。
  - record の保存・可視化・UI: S0-c / S5。
- 現況（base `7b28eba` の実測。path は `unity/Assets/OneStarMaker/Scripts/` からの相対）:
  - `Runtime/ContentResolution/Core/`（20 file、1,250 行。S0-a、PR #171）: `IContentCompatibilityRule`（19 行）は `bool Accepts(ContentRequest, ContentCandidate, out string reason)` だけを持ち、**既定の実装も製品の実装も無い**。XML doc に「既定を置くと互換判定の製品実装が決めるべき既定値を先に凍結する。製品実装が入るまで host は自前の規則を渡さない限り `ResolutionPolicy` を組めない」「規則が `OperationCanceledException` 以外を投げると resolver は fault として止める」とある。
  - `ContentResolver`（318 行）の各 probe の判定順は、(1) 要求の不変（provider id / 由来の一致、logical key、kind、表現、pin の全欄一致）→ (2) 由来ごとの受け入れ根拠の固定表（`RequiredBasis`、214〜225 行目、private。`ContentDirectory` → `VerifiedDigest`、`EditorSnapshot` → `OpenSceneSnapshot`、`Addressables` → 採用しない。identity 付きの `Rejected` の後は `VerifiedDigest` だけ）→ (3) 規則（119〜142 行目）。規則が false なら `RuleRejected` を記録して**次の供給元へ進む**（135〜139 行目）。規則が例外なら、取消済みの token による `OperationCanceledException` はそのまま投げ、それ以外は `Fault` を記録して止める（121〜133 行目）。誰も採らなければ `DecideFailureCode`（277〜316 行目）が次の優先で 1 つに決める: (1) pin の不一致・識別子付き `Rejected` の後の未採用（`RepairOnly`）・識別子の無い `Rejected`・pin の食い違い → `IntegrityRejected`。(2) `KeyMismatch` / `KindMismatch` / `ProviderMismatch` / `Fault` → `ProviderFault`。(3) `RuleRejected` / `NoAcceptanceBasis` / `RepresentationMismatch` → `NoCompatibleCandidate`。(4) 残り → `NoCandidate`。
  - `ContentIdentity`（113 行）は `ContentSet` / `Revision` / `Target` / `Digest` の 4 欄で、等値は ordinal の全欄一致だけ。`IsComplete`（4 欄が非空）と `MatchesPin`。**engine の version と root schema の欄は無い。**
  - `ContentCandidate`（117 行）は `Facts`（provider 固有の事実の `IReadOnlyDictionary<string,string>`）を持つ。constructor（27〜74 行目）が `CopyFacts`（96〜115 行目）で複製し、null の値は空文字にする。互換に使う事実の key は定義されていない。
  - `ContentRequest.ExpectedIdentity` は任意の pin（4 欄がそろった値だけ）。resolver は pin と全欄一致しない候補を規則の前に `PinMismatch` で弾く。
  - 既存の install 時の検査 `Runtime/BuildContent/Distribution/ContentManifestValidation.cs`（293 行、internal）`Validate`（70〜85 行目）: transport の `version == 1`・`product == "OneStarMaker"`・`playerConfigSchemaVersion == 1`、`unityVersion == "6000.6.0f1"`（`SupportedUnityVersion` 定数）・`rootSchemaVersion == 2`（`SupportedRootSchemaVersion` 定数）、要求との `contentSet` / `revision`（`IdentityMismatch`）、`target`（`TargetMismatch`）、`unityVersion` / `rootSchemaVersion`（`CompatibilityMismatch`）、manifest の digest（`IntegrityMismatch`）。**`declaration`（name・representation・runtimeDefaults）は検証しない**（`ContentTransportManifest.cs` 22〜38 行目、optional）。これは install する bytes の検査で、resolver の候補の採否とは別の層にある。本スライスは変えない。
  - offline テスト `tools/ContentResolutionOfflineTests/`（`Program.cs` 693 行、28 件）が Core の `.cs` を `<Compile Include>` で link し、CI の `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` matrix（77 行目）で回っている。`CoreSources_DoNotNameUnityOrHosts`（file 内の comment は `// T13`、266〜302 行目。T12b / T12c があるので実行順の番号とずれる。**本書はテストを番号ではなく名前で照合する**）は `Core/` 直下の `*.cs`（**非再帰**の `Directory.GetFiles`）を走査し、`#nullable enable`、`UnityEngine`・`SceneDirector`・`SampleGame`・`Season`・`RunPurpose`・`Workset` 等の語、AssetManagement / BuildContent / SceneSystem の `using`、`record` 宣言、`init` accessor を拒む。file 数の assertion（`files.Length >= 20`）は失敗した時しか出力しない。
  - S0-a から移送された S1 の入力（program §8）: 「pin が未充足のまま `Fault`」→ `IntegrityRejected` の組み合わせを固定するテストが無い。本スライスで足す（§5 C24）。
  - S0-b の結論（program §5.2）: snapshot の機構 (i)・(ii) とも成立し、勝者は選んでいない。どちらの機構でも候補の由来は `EditorSnapshot`、受け入れ根拠は `OpenSceneSnapshot` で、digest の検証を受け入れ理由にしない（program I2）。本スライスの汎用判定は由来と受け入れ根拠で分岐し、機構で分岐しない。
  - 本書の作成環境（Cloud）に Unity・pwsh・dotnet が無く、`tools/docs-audit.ps1`・`tools/contract-audit.ps1`・offline テストは未実行。link 先の存在は `git ls-files` で手で照合した。**Unity 上では何も確認していない。**
- UC ごとの利用者の入力（実行時の操作は無い。開発時の宣言を利用者の作業として数える）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| host が Play 前の互換の基準を作る | content set、target、engine の version、root schema の version（必須の 4 つ。content set の必須は U-S1-7 の裁定） | なし（汎用の既定値を持たない） | host の開発者が開発時に宣言し、Play 前に policy を作る時に渡す。値の出所（Player の baked 値、Editor の build target、Distribution の定数等）は host / 後続 adapter が決める | 目的の「Play 前に固定した互換の基準」そのもの。content set は program U5 の汎用の最小集合に入り、install 時の検査も必ず見る（X-04）。既定値を core に置くと core が特定の engine・target を知ることになり、I7 と S0-a の「既定を先に凍結しない」に反する |
| 同上（任意） | revision（任意。与えれば完全一致を要求） | 与えなければ比べない | host の開発者が開発時に宣言 | program §4.1「同じ revision から取るのが標準」を host が Play 前に固定したい場合の手段。固定しない host は要求時に互換な任意の revision を受ける（§2.1 I-C1 の読み方、U-S1-6） |
| 要求ごとに特定の内容を期待する | expected identity（任意。既存の `ContentRequest.ExpectedIdentity`） | 与えれば digest を含む全欄一致を要求。4 欄がそろわない値は既存の constructor が拒む | 呼び出し側が要求時に | 目的の「要求が与えた expected identity」。新しい入力ではなく S0-a の既存の欄 |
| host 固有の比較を足す | 述語の一覧（任意。既存の `IContentCompatibilityRule` を実装した host の型） | 汎用の判定を通った directory の候補にだけ、登録順に全てが採ると答えた時だけ採る。snapshot の候補には当てない（U-S1-3 の裁定） | host の開発者が開発時に宣言 | 目的の「host が登録した時だけ追加の条件として加わる」。宣言名・表現・graph を汎用の最小集合に入れない（program U5） |
| 供給元の adapter が事実を申告する | engine の version と root schema の version を `Facts` の予約 key に書く | key の名前は core が定数で決める | adapter の開発者（S3 / S4）が実装時に | 汎用の判定に必要な事実の入れ物。既存の `ContentCandidate` の宣言を変えずに渡す唯一の経路（§2.5 U-S1-2） |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 特定の host 宣言無しで、expected identity / digest / target / schema の汎用の互換判定ができるか。`declaration` の内容・表現・graph は、host が登録した時だけ比べる述語として、汎用の判定を緩めずに追加できるか（program §8 S1、U5）。
- 問いへの答え（A3 統合版の仮説。owner の裁定を反映、凍結前）: **できる。既存の公開型と resolver を変えずに、`IContentCompatibilityRule` の製品実装 1 つと、それが読む不変の基準（envelope）と、事実の予約 key と、判定結果の種別の enum で。** 規則はまず由来と受け入れ根拠の組を確かめ、次に要求の expected identity を由来に関係なく全欄で比べる。その後は由来で分岐する。`ContentDirectory`（`VerifiedDigest`）の候補には、identity の 4 欄がそろうこと、envelope の content set・target・engine の version・root schema の version と一致すること、envelope が revision を固定していればそれとも一致することを要求し、事実が欠けた・形が壊れた候補は採らない。`EditorSnapshot`（`OpenSceneSnapshot`）の候補には digest・revision・target・engine・schema の比較も host 述語もしない（受け入れ理由が digest ではなく、利用者が開いている Scene を最優先にする owner 決定 A1 を host 述語で崩さないため）。それ以外の由来は採らない。host 述語は既存の `IContentCompatibilityRule` をそのまま使い、directory の候補が汎用の判定を通った後に登録順に評価して、全てが採ると答えた時だけ採る（AND だけ）。
- どうすればこの問いに誤って YES を出せるか（反例。C と C' はこれを探す）:
  - (a) 欠けた事実を「比べられないので一致」と扱う（engine の version の無い候補、schema の無い候補が通る）。壊れた manifest を誤採用する（I-C1、C5）。
  - (b) 寛容な解析で「一致」を作る: `" 2"`、`"+2"`、`"02"`、`"2.0"`、全角数字、大文字小文字の違う target を一致とする（I-C1、C6・C10）。
  - (c) `default` の判定結果（enum の 0）が「互換」と読める（I-C6、C18）。
  - (d) host 述語が true を返せば汎用の不採用を覆せる合成（OR、上書き、最後の述語の結果だけを見る）（I-C2、C13）。
  - (e) host 述語の例外を握りつぶして「不採用」や「採用」にする、または別の例外で包み直す（取消済みの token の `OperationCanceledException` が `Fault` に化ける）（I-C2、C14）。
  - (f) `declaration.name` を汎用の判定に入れて、空の name 同士を一致とする。または host 述語を登録していないのに宣言を比べて、宣言の無い正規の候補を落とす（I-C3、C11・C12）。
  - (g) snapshot の候補を digest が無いことで落とす（snapshot tier が死ぬ）、または逆に snapshot の候補の採用理由に「digest 検証済み」と読める語を残す（host 述語の理由の連結を含む）（I-C4、C16）。
  - (h) 将来 `ContentSourceKind` に値が足された時に、その由来の候補を汎用の判定が素通りさせる（I-C4、C17）。
  - (i) 同じ content set・revision を名乗る別 digest の候補を、expected identity が与えられているのに採る（同名異内容。I-C1、C8）。expected identity が**無い**時に同名異内容を見分けたと主張する（U-S1-4）。
  - (j) 規則の外で互換を判定する（新しい規則を通すために `ContentResolver` の順序や `ProbeOutcome` を変える。依頼者の指示に反する）（I-C5、AC2・AC3）。
  - (k) fake の provider と fake の事実で通っただけで、実在の manifest から作った候補が通ると主張する（§2「成果の範囲」。Unity と実 manifest では未検証）。
  - (l) 新しい file を `Core/` の下の子 folder に置き、`CoreSources_DoNotNameUnityOrHosts` の非再帰の走査を逃れて Unity・host の語を持ち込む（I-C3、AC1、C29）。
  - (m) 宣言名を比べる host 述語を登録した policy で、宣言の事実を持たない snapshot が落ち、下位の directory 版が採られて**成功**する（利用者が編集中の Scene が黙って directory 版に置き換わる。反例レビュー X-03）（I-C4、C28）。
  - (n) content set を比べずに、別の content set（旧い DLC 等）が同じ logical key の候補を返した時に採る（program §3.1 の 5。反例レビュー X-04）（I-C1、C9）。
  - (p) `Evaluate` が `Compatible` を返しただけで、要求の logical key・kind・表現や供給元との一致まで保証されたと読み、S0-c の入口が ledger の pair をそれだけで確定する（`Evaluate` は互換性だけの部分判定で、要求の不変と供給元の一致は resolver が規則の前に見る。PR #174 行コメント P1）（§2.3、§3）。
  - (o) 要求の終わり方を「規則で落ちたら必ず `NoCompatibleCandidate`」と主張する（pin の未充足や他の不一致と混ざると既存の優先順で別の値になる。反例レビュー X-02）（I-C6、C22・C23・C27）。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- このスライスの成果の範囲: GO は「既存の公開型と resolver を変えずに、汎用の互換規則と host 述語の合成が、fake の供給元と fake の事実の上で、program §8 S1 の gate の反例（同名異内容（expected identity がある時）、壊れた manifest、異なる version、別表現（要求が表現を指定した時）、旧 manifest）を採らないこと」だけを意味する。要求が表現を指定しない（空の）時は、上位の候補が規則で落ちた後に下位の別表現の互換な候補が採られうる。表現の中身は汎用側が解釈しない（program U5）ので、これを別表現への切替に数えない（外部レビュー 2。C30 で固定）。host 述語を登録しない policy は、同じ content set・revision・digest で宣言だけが違う候補を採る（program U5 どおり。G08 の `declaration` の比較は host 述語の責任）。供給元の申告が実体と一致することは主張しない（申告を照合するだけ）。実在の manifest から事実を作る adapter、Unity の Content Directory からの実ロード、Editor Play の入口、複数 package の合成、要求をまたぐ一貫は主張しない。**Unity 実機では何も検証しない**（本スライスの対象は pure な core で、Unity で確かめるのは Phase B の compile と Phase C の全 EditMode 回帰による非退行だけ）。program §8 の「誤った合格を除外する」の列をそのまま引き継ぐ。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-C1 非互換の directory 候補を採らない（program I1・I5、G08）:** `ContentCompatibilityRule` を規則に持つ policy で、由来 `ContentDirectory` の候補が次のどれかに当たれば採用されない。(1) identity の 4 欄のどれかが空（空白だけを含む）。(2) 要求に expected identity があり、候補の identity と全欄一致しない（この比較は由来に関係なく行う。I-C4。digest を含む全欄を ordinal で比べ、大文字小文字の同一視・正規化をしない。digest の形式（長さ・16 進）は検査しない。形式の検証は bytes を見る供給元の adapter の責任）。要求が revision だけ・digest だけの部分の期待値を持つことは無い（S0-a の constructor が 4 欄のそろわない expected identity を拒む）。(3) envelope の target と候補の `Identity.Target` が ordinal で一致しない。(4) 候補の `Facts` に engine の version の予約 key が無い・値が空白だけ・envelope の値と ordinal で一致しない。(5) root schema の version の予約 key が無い・値が空白だけ・正規の 10 進表記（ASCII の `1`〜`9` で始まり ASCII の数字だけ、9 桁以内）でない・envelope の値と一致しない。理由 code の対応は engine と schema で揃える: key が無い、または値が空文字・空白だけ → `…Missing`（`CopyFacts` は null の値を空文字にするので、null もここに入る）。値に空白以外の文字があり正規の表記でない → `SchemaVersionMalformed`。表記が正しく値が違う → `…Mismatch`（外部レビュー 7）。(6) envelope の content set と候補の `Identity.ContentSet` が ordinal で一致しない、または envelope が revision を固定していて候補の値と ordinal で一致しない。
  - 読み方（U-S1-6 の裁定で確定、owner 2026-11-09。program §3.1 の 10・§4.1・U5 の文言もこの読み方に直した）: **expected identity も envelope の revision も無い要求では、互換の基準を満たす候補はどの revision・digest でも採ってよい**（供給元の順で最初に互換と判定された候補）。この時、上位の供給元が非互換で落ち、下位の供給元の別 revision が採られることを、本書は program §3.1 の 10「非互換 revision への暗黙 fallback」にも §4.1 第 3 項「version 不整合は同じ expected digest の正規コピーで修復できる時だけ下位 tier へ進む」にも当たらないと読む。§4.1 第 3 項の「hash 不正・version 不整合・権限拒否」は、供給元が自分の申告と bytes の食い違いを検出した**整合性の失敗**で、既存の契約では供給元が `Rejected` で返し、resolver が pin と修復だけの状態で守る。規則の不採用は、正しく作られた候補が今回の基準に合わないという**互換の不一致**で、上位の不採用は理由付きで probe に残る。この読み方は、program U5 の汎用の一致の列（revision と file digest を含む）を「content set・target・engine・schema は常に、revision は host が envelope で固定した時だけ、digest は要求の expected identity がある時だけ」と読み替えることでもある（外部レビュー 1。U-S1-6 の裁定の範囲に含める）。要求が表現を指定しない時に下位の別表現の候補が採られることも、同じ読み方の下で切替に数えない（外部レビュー 2）。反例レビュー X-01 はこの読み方が §4.1 第 3 項の文言と食い違うと指摘した。既存の resolver は `RuleRejected` の後に必ず次の供給元へ進むので、文言どおりに読むと resolver を変えずには満たせない。合格例（この読み方の下で）: envelope（content set S、target T、engine E、schema 2）、Local が schema 1 の候補、LAN が schema 2 の別 revision の候補 → LAN を採り、Local は `RuleRejected`（理由 `compat:SchemaVersionMismatch:`）。不合格例: 同じ条件で要求の expected identity が Local の identity → LAN の候補は `PinMismatch`、Local は `RuleRejected`、採用なし（終わり方は `IntegrityRejected`。I-C6）。同名異内容（同じ content set・revision で digest が違う）を見分けるのは expected identity が与えられた時だけで、無ければ採った digest が record に残る（Traceable）だけである（U-S1-4）。
  - 崩しうる経路: (a) 規則が `Facts` を `TryGetValue` で読み、無い時に既定値や「比べない」に倒す。(b) schema を `int.TryParse`（culture・符号・空白・先頭 0 を許す）で読む。(c) 文字列の比較に `StringComparison.OrdinalIgnoreCase` や culture 依存の比較を使う。(d) envelope を可変にして、policy を作った後に値が変わる。(e) resolver の判定順で規則より先に採用される経路: 無い（規則は採用の直前に必ず呼ばれる。`ContentResolver.cs` 119〜142 行目）。(f) `ContentCandidate.Facts` を候補を作った後に変える: constructor が複製するので無い（`ContentCandidate.cs` 27〜74 行目、`CopyFacts` は 96〜115 行目）。(g) 修復（identity 付きの `Rejected`）の後の候補: resolver が pin と根拠を先に見るが、規則は修復中の候補にも同じ判定をする（C27）。(h) envelope の schema と事実の schema を別々の規則で解析し、両者の表記の範囲が食い違う（アーキテクチャゲート G-01。envelope は 1〜999,999,999 に制限し、事実は同じ 1 つの解析関数で int にしてから比べる）。
  - 探索した境界: `ContentResolver.ResolveAsync` の 1 経路、`ContentCandidate` の constructor、`ContentIdentity` の等値、`ContentManifestValidation.Validate`（比較の対象と粒度の参考。変えない）、program §3.1・§4.1。未確認: 実在の provider adapter が manifest の値を `Facts` へ正しく写すか（S3 / S4。本スライスに実在の adapter は無い）。
- **I-C2 host 述語は汎用の判定を緩めない:** host が登録した述語は、汎用の判定が採ると答えた `ContentDirectory` の候補にだけ評価され、登録順に全てが採ると答えた時だけ採用される。最初に false を返した述語の後の述語は呼ばない。述語が true を返しても汎用の不採用は覆らない。述語が投げた例外は、規則が捕まえず、包み直さず、そのまま伝える。resolver は、取消済みの token による `OperationCanceledException` なら取消として投げ直し、それ以外は `Fault` を記録して探索を止める（S0-a の既存の契約）。その要求の終わり方は既存の `DecideFailureCode` の優先順に従う（pin が未充足なら `IntegrityRejected`、そうでなければ `ProviderFault`）。述語の一覧に null は入れられず、規則を作った後に元の一覧を変えても影響しない。
  - 崩しうる経路: (a) 述語を汎用の判定の前に評価し、その結果で早期に true を返す。(b) 述語の結果を OR で合成する、または最後の述語の結果だけを返す。(c) `try { } catch { return false; }` で述語の例外を不採用に変える（例外の事実が `Fault` として残らず、壊れた host の述語で全候補が静かに落ちる）。(d) 例外を `InvalidOperationException` 等で包み直す（取消が `Fault` に化ける。反例レビュー X-07）。(e) 一覧を複製せずに保持する。
  - 探索した境界: 規則の `Evaluate` / `Accepts` の 1 経路と、resolver の規則呼び出しの `try` / `catch`（`ContentResolver.cs` 121〜133 行目）と `DecideFailureCode`（277〜316 行目）。未確認: なし。
- **I-C3 汎用の判定は host の宣言を必要としない（program I7、U5）:** 本スライスの製品の型（`Runtime/ContentResolution/Core/` の新規 file）は、宣言名・表現の中身・graph の version を読まず、比べず、`declaration` に当たる予約 key を定義しない。host 述語を登録しない policy では、候補の `Facts` に宣言に当たる事実があってもなくても判定は変わらない。予約の接頭辞 `osm.compat.` の下の未知の key は判定に使わず、無視する。新規 file は `Core/` 直下に置き（`CoreSources_DoNotNameUnityOrHosts` の走査の対象）、SampleGame の Scene 名・Season・Cell・RunPurpose・宣言名・VCS・SceneResourceMap・UICommon・Workset の語と UnityEngine を含まない。表現は既存の resolver が要求との一致を見るだけで、規則はその中身を解釈しない。
  - 崩しうる経路: (a) 「便利だから」と `declaration.name` の比較を汎用の判定に入れる（空の name 同士が一致する。U5）。(b) host の例に使う宣言名（`Spring` 等）を core のテストや既定値に書く。(c) 新規 file を `Core/Compatibility/` 等の子 folder に置き、走査の対象から外れる（反例 (l)）。
  - 探索した境界: 新規 file の全部（`CoreSources_DoNotNameUnityOrHosts`、C29、AC6 の grep）。未確認: なし。
- **I-C4 由来ごとの受け入れ理由を混ぜず、snapshot の優先を host 述語で崩さない（program I2、owner 決定 A1、S0-b の結論）:** 由来 `EditorSnapshot`（受け入れ根拠 `OpenSceneSnapshot`）の候補に、規則は identity の欄・engine・schema・envelope の content set / revision を要求せず、digest が無いことを不採用の理由にせず、host 述語を当てない（U-S1-3 の裁定）。要求に expected identity があれば由来に関係なく全欄で比べる（resolver の pin と同じ答えになる。反例レビュー X-05）。snapshot の候補を採った時の規則の理由は `compat:Compatible:EditorSnapshot` で、host 述語の理由を連結せず、「digest」「verified」に当たる語を含まない。由来が `ContentDirectory` でも `EditorSnapshot` でもない候補（`Addressables`、将来足される値）と、由来と受け入れ根拠の組が resolver の固定表と合わない候補は、規則を直接呼んでも採ると答えない。判定は由来と受け入れ根拠で分岐し、snapshot の機構 (i) / (ii) では分岐しない。
  - 崩しうる経路: (a0) 要求の expected identity に directory の identity を載せた要求では、identity の空な snapshot は pin と一致せず（resolver 経由なら規則の前に `PinMismatch`）、pin に合う下位の directory が成功する。反例 (m) の述語ではなく pin からの再入で、規則では防げない（外部レビュー 3。§2.3 の S0-c の行で、snapshot を優先する policy は要求に directory の expected identity を載せないことを引継ぎ条件にする）。(a) 汎用の判定を由来に関係なく全候補に当て、identity の空な snapshot が落ちる。(b) `switch` の `default` を「採る」にして、将来の由来を素通りさせる。(c) 規則が resolver の固定表を信じて根拠の検査を省き、規則を単独で呼ぶ利用者（S0-c の入口が候補を検査する等）に固定表外の組を採らせる。(d) host 述語を snapshot にも当て、宣言の事実の無い snapshot が落ちて下位の directory 版が成功する（反例 (m)）。(e) 採用理由に host 述語の理由を連結する（反例レビュー X-06）。(f) 規則の受け入れ根拠の表が resolver の private な表（`ContentResolver.cs` 214〜225 行目）と将来食い違う（アーキテクチャゲート G-02。C17 の横断 assertion で検出し、表の共有は §2.3 の負債）。
  - 探索した境界: `ContentSourceKind`（4 値）、`ContentAcceptanceBasis`（3 値）、`ContentResolver.RequiredBasis`。未確認: なし。
- **I-C5 既存の公開契約と既存の挙動を変えない（依頼者の指示、program I8・I10）:** base に在る `Runtime/ContentResolution/**` の file（`Core/` の 20 file、`ContentRequestLoader.cs`、`ContentLoadOutcomes.cs`）と、`Runtime/Bootstrap/**`、`Runtime/AssetManagement/**`、`Runtime/BuildContent/**`（`ContentManifestValidation` を含む）、`Runtime/SceneSystem/**`、`SampleGame/**`、`Editor/**`、`.asmdef`、`ProjectSettings/`、`Packages/` を変えない。既存の offline テスト（28 件）の名前・assertion・本文と、既存の EditMode テストを変えない。
  - 崩しうる経路: (a) 理由 code を機械で読むために `ProbeOutcome` に値を足す。(b) engine・schema を渡すために `ContentCandidate` や `ContentIdentity` に欄を足す。(c) 「互換の不採用で次へ進ませない」ために resolver の判定を変える（U-S1-6 を文言どおりに満たそうとすると起きる）。(d) S0-c が使う入口から規則を呼ぶ配線を `Editor/**` や `SampleGame/**` に足す。(e) Unity Editor が import 時に書き換えた既存 `.meta` を一緒に commit する。(f) 受け入れ根拠の表を共有するために `RequiredBasis` を internal / public にする（G-02。§2.3 の負債）。
  - 探索した境界: AC2 の変更集合の許可リスト。未確認: なし。
- **I-C6 不採用は理由付きで残り、成功に化けない（program §4.1 fail-closed、Q5）:** 規則が採らない候補は、resolver の probe に `RuleRejected` として残り、`Reason` は `compat:<種別>:` で始まる（種別は §3 の `ContentCompatibilityMismatch` の値の名前）。誰も採らなかった要求の終わり方は既存の `DecideFailureCode` の優先順に従い、**全ての probe が `RuleRejected` / `NotFound` / `Unavailable` のどれかで、pin が未充足でない**（要求の expected identity と一致しない候補が無く、識別子付きの `Rejected` も無い）時に `NoCompatibleCandidate` になる。規則の不採用は成功にも `NoCandidate`（不在）にもならない。既存の挙動の読み方として次の非対称を固定する: 要求の expected identity と identity が一致する候補が規則で落ちた時は `NoCompatibleCandidate`（`PinMismatch` が無いため）、識別子付きの `Rejected` の後に同じ identity の候補が規則で落ちた時は `IntegrityRejected`（`RepairOnly` のため）。規則の判定結果の種別は、`default` の値が「互換」と読めない。
  - 崩しうる経路: (a) 判定結果の enum の 0 を「互換」にする。(b) 理由を空文字で返す。(c) 規則が例外で不採用を伝え、resolver が `Fault` で止まる（互換の不採用で探索が止まり、下位の互換な候補へ進めない）。(d) 本書や harvest 文書が「規則で落ちれば `NoCompatibleCandidate`」と単純化し、利用者が終わり方を読み違える（反例 (o)）。
  - 探索した境界: resolver の `RuleRejected` の記録と `DecideFailureCode`（`ContentResolver.cs` 135〜139 行目、277〜316 行目）、既存の T25 `FailureCode_Precedence`。未確認: なし。

**成立条件（起きなければならないこと）**

- **E1 汎用の判定の成立:** host 述語の無い `ContentCompatibilityRule` で、envelope と一致する完全な identity と事実を持つ `ContentDirectory` の候補が採用される。I-C1 の (1)〜(6) の各 1 欄だけを外した候補は、それぞれ別の `ContentCompatibilityMismatch` の値で採られない。
- **E2 host 述語の追加:** host が登録した述語（テストの fixture。宣言名の一致、宣言の表現と要求の表現の一致、graph の version の一致の 3 種）が、汎用の判定を通った directory の候補にだけ評価され、合わなければ採られず、合えば採られる。登録しなければ同じ候補・同じ事実で判定が変わらない。
- **E3 snapshot tier の維持:** identity が空の `EditorSnapshot`（`OpenSceneSnapshot`）の候補が、host 述語の有無にかかわらず採用され、規則の理由に digest の検証を主張する語が無い。
- **E4 program gate の反例:** 同名異内容（expected identity あり）、壊れた manifest（事実の欠落・形の崩れ・identity の欠け）、異なる version（engine）、別表現（要求が表現を指定した時の resolver の既存の表現一致と、host 述語の宣言の表現。要求が表現を指定しない時は別表現の候補を採りうるので数えない）、旧 manifest（root schema が古い）、別の content set の各候補が採られない。
- **E5 Unity 非依存と非退行:** offline テスト project が Core の file だけを link してビルド・実行でき（exit 0、1 件以上実行、failed 0）、既存の 28 件と §5 の C1〜C30 が全て通る。Unity でのコンパイルが通り、全 EditMode 回帰に base からの退行が無い。`pwsh tools/contract-audit.ps1` が合格する。

### 2.2 受け入れ条件

合否は各 AC の本文で決める。「確認手段」は合否を見る手段で、同じ意味を別の手段で示してもよい。

- **AC1（E5, I-C3）core の純粋さ:** 新規の `.cs` が全て `Runtime/ContentResolution/Core/` の**直下**にあり、`#nullable enable` で始まり、`CoreSources_DoNotNameUnityOrHosts` が拒む `using` と語、`record` 宣言、`init` accessor を持たない。`tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が新規の Core の file を `<Compile Include>` で link し、`PackageReference` を持たない。`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が exit 0 で、最後の行が `ContentResolution offline: <n> passed, 0 failed, <n> executed`（n = 58。既存 28 件 + C1〜C30）。
  - 確認手段: `git diff --name-status` で新規 file の置き場所を見る。C29 が、新規 4 file が `Core/` 直下にあり、`Core/` の子 folder に `*.cs` が無いことを確かめる（既存のテストの file 数の assertion は失敗時しか出力しないため。アーキテクチャゲート G-04、反例レビュー X-08）。`dotnet run` の出力。
  - 不合格の例: `Core/Compatibility/ContentCompatibilityRule.cs` に置き、走査の対象外になる。
- **AC2（I-C5）変更集合の許可リスト:** base から head までの変更（全 path）が次だけである。これ以外の追加・変更・削除が 1 件でもあれば不合格。
  1. 新規 `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentCompatibility*.cs` と各 `.meta`（§3 の 4 file）。
  2. `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` への `<Compile Include>` の追加（新規 file の数だけ）。
  3. `tools/ContentResolutionOfflineTests/Program.cs` の変更: `partial` の付加と、`Main` から新しいテストを呼ぶ行と、§5 C24 の 1 件の追加だけ。既存の 28 件の名前・assertion・本文は変えない。
  4. 新規 `tools/ContentResolutionOfflineTests/CompatibilityTests.cs`。
  5. `docs/handoff/` 配下の HANDOFF の記録（本書と program §8 の S1 行。U-S1-1・U-S1-6 で owner が program の文言の修正を裁定した場合は、その裁定の範囲の program の本文）。
  6. harvest 先の公開文書（Phase D）。
  - 確認手段: `git diff -M --name-status <base> <head>` の全行を照合する。`git diff <base> <head> -- tools/ContentResolutionOfflineTests/Program.cs` で既存のテストの本文に変更が無い。
  - 不合格の例: `M .../Core/IContentCompatibilityRule.cs`（XML doc の書き換えも含む。§2.5 U-S1-5）。`M .../Core/ProbeOutcome.cs`。`M .../Core/ContentResolver.cs`（`RequiredBasis` の公開を含む）。Unity が書き換えた既存の `.meta` が入る。
- **AC3（I-C5）既存の公開型と resolver の不変:** base に存在した `Runtime/ContentResolution/**` の file（`Core/` の 20 file、`ContentRequestLoader.cs`、`ContentLoadOutcomes.cs` とそれらの `.meta`、folder の `.meta`）に変更・削除・改名が 0 件。新規 file は AC2 の許可リストで判定する（PR #174 行コメント P2。AC2 の 1 が同じ folder に新規 file を許すので、folder 全体の変更 0 件とは書かない）。確認は `git diff -M --name-status <base> <head> -- unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution` の行が全て `A` で、`A` の path が AC2 の 1 に当たること。加えて `git grep -n "ContentCompatibility" -- unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem unity/Assets/OneStarMaker/Scripts/Editor unity/Assets/SampleGame` が 0 件（本スライスの型を誰も配線していない）。
  - 確認手段: 上の grep。
- **AC4（E1〜E4, I-C1, I-C2, I-C4, I-C6）offline テストの名前の照合:** AC1 の出力に、§5 の C1〜C30 の各テスト名について `PASS <テスト名>` の行があり、既存の 28 件の `PASS` 行も全てあり、`FAIL` の行が無い。各 C が検証する条件は §5 の表で紐付ける。
  - 確認手段: 出力の `PASS` 行と §5 の一覧の照合。
  - 不合格の例: C を統合して名前が消えている（統合するなら A3 の決定記録で §5 を直す）。
- **AC5（I-C4, I-C6）理由 code の形:** C21 が、規則で落ちた probe の `Reason` が `compat:` + `ContentCompatibilityMismatch` の値の名前 + `:` で始まることを、I-C1 の (1)〜(6) と host 述語の不採用の各場合で確かめる。C16・C28 が、snapshot の採用時の理由が `compat:Compatible:EditorSnapshot` と完全一致し、`digest` / `verified`（大文字小文字を無視）を含まないことを、理由に `digest verified` を返す host 述語を登録した場合も含めて確かめる。C12 が、同じ述語を登録した directory の採用時の理由が `compat:Compatible:ContentDirectory` と完全一致すること（述語の理由を連結しない）を確かめる（外部レビュー 6。snapshot は述語の前に戻るので C16 だけでは directory 側の連結を検出できない）。
  - 確認手段: AC4 の該当テスト名の `PASS`。
- **AC6（I-C3）汎用性の文字列検査:** 新規の Core の file の各行を小文字にし、`cancel` の語を取り除いてから（`CoreSources_DoNotNameUnityOrHosts` と同じ扱い）、`scenedirector|samplegame|spring|summer|autumn|winter|season|cell|runpurpose|sceneresourcemap|uicommon|svn|workset|declaration` のどれも含まない。テスト（`CompatibilityTests.cs`）は host の fixture として `declaration` の語を使ってよいが、SampleGame の実名（`Spring` 等の宣言名・Scene 名）を使わない。
  - 確認手段: C29 がこの検査を新規 4 file に対して行う。`CompatibilityTests.cs` は `declaration` を除いた同じ語の一覧で `git grep -n -i` を見る（`Cancel` を含む語は `cancel` を取り除いて読む）。
- **AC7（E5, I-C5）Unity のコンパイルと全 EditMode 回帰:** GO 候補 head で Unity Editor のコンパイルが通る（Phase B が確認。できなければ未確認と明記し Phase C の実行で代える）。base と GO 候補 head のそれぞれで、同じ機械・同じ Unity 版・同じコマンド（`pwsh tools/run-tests.ps1`、空 filter、EditMode）で全 EditMode を実行し、head の結果 XML が 1 件以上実行かつ failed 0、base で passed のケース ID（full name）が head で全て passed、Inconclusive のケースの集合が base と同じ（`-nographics` の VFX の 25 件。program §8「全 EditMode 回帰の受け入れ条件の書き方」）。`step.json` の `failed` だけを根拠に不合格にしない。
  - 確認手段: 2 つの XML から full name と結果を抜き出して比べる。総数・passed・skipped・failed・inconclusive を記録する。base が failed 0 にならなければ比較の基準が成立しないので、head を判定せずに人間へ返す。
  - 合格の例: base 2,000 件（passed 1,975、inconclusive 25）、head 2,000 件で同じ集合が passed、同じ 25 件が inconclusive。
  - 不合格の例: head で base の passed の 1 件が skipped になる。head の XML が不完全で件数を数えられない。
- **AC8（E5）contract-audit と CI:** `pwsh tools/contract-audit.ps1` が合格する。CI の `dotnet-offline-tests`（既に `ContentResolutionOfflineTests` を含む）が PR で緑であることは Phase D のマージ条件とし、判定 C の合否にしない。
  - 確認手段: audit の出力。Phase D で PR の check。

### 2.3 ここでは答えない問いと所有スライス

| 問い | 所有 |
|---|---|
| 実在の manifest（`ContentTransportManifest` + 検証済みの digest）から候補の `Identity` と互換の事実を作る adapter。`ContentManifestValidation` の `SupportedUnityVersion` / `SupportedRootSchemaVersion` 定数と、host が envelope に渡す値の出所を一本にすること。**一本化の向きは、adapter が Distribution の定数を読んで envelope を組み立てる側で、Core は Distribution を参照しない**（アーキテクチャゲートの判定）。食い違った時: resolver に届いた候補は規則が `RuleRejected` にする。install が先に拒んだ時は候補が無く、終わり方は規則の不採用ではない（`NotFound` / `Rejected` / `Unavailable` 側）。install 済みの cache を Distribution の定数と違う envelope で解決する経路では install は再実行されないので、規則の不採用だけが残る（外部レビュー 4） | S3 `RUNTIME_PROVIDER_INTEGRATION`、S4 `LOCAL_LAN_PROVIDER` |
| 複数 package を 1 つの合成単位として採る可否（共同の build plan / dependency graph の出所、shared asset の所有と重複、同一 logical identity の衝突規則、package 索引の hash 関係。G24）。program §4.1 の「S1 が定める互換 envelope」の後半 | S2 `CONTENT_PACKAGE_DISCOVERY`（§2.5 U-S1-1） |
| 要求をまたぐ revision の一貫（envelope が revision を固定しない時、最初に採った revision に後の要求を揃える）、要求をまたぐ pin。expected identity の無い要求での同名異内容の検出 | S4 `LOCAL_LAN_PROVIDER`（U-S1-4） |
| S0-c の Editor Play 入口が、ledger に書く directory の pair を本スライスの規則で検査すること（program §10 U4 の「S1 が互換判定と接続する」。S0-c の計画 PR #175 は Built の候補を「検証を通った最新の install」とし、「S1 で互換判定に置き換える」としている）。明示の directory-only Play で snapshot の provider を policy から外すこと（G18）。**接続の側に引き継ぐ条件（本スライスでは実装しない）:** (1) `Evaluate` は互換性だけの部分判定で、要求の logical key・kind・表現と候補の一致、probe した供給元の id・由来と候補の一致は見ない（resolver の `CheckRequestInvariants` が規則の前に見る）。pair を確定するなら resolver を通すか、それらの一致を入口の側で別に保証する（PR #174 行コメント P1。反例 (p)）。(2) `Evaluate` は 1 要求（logical key 1 つ）単位の判定で、revision 全体の pair を検査するには、どの要求で代表させるか（起動 Scene 等）を入口の側で決める。(3) snapshot を優先する policy は、要求に directory の expected identity を載せない。pin はその digest が要る要求に限る（載せると identity の空な snapshot が `PinMismatch` で落ち、pin に合う directory が成功する。外部レビュー 3） | S0-c `EDITOR_PLAY_ENTRY_PROOF` の後の接続（S0-c の Phase D 後に、S0-c か S5 の A1 で所有を決める。本スライスは呼べる公開の型を用意するだけで、入口に触れない） |
| 受け入れ根拠の固定表（`ContentResolver.RequiredBasis`、private）を規則と共有すること。本スライスは表を規則の中に複製し、C17 で resolver との一致を横断で確かめる。`ContentSourceKind` に値を足す将来の変更は resolver と規則の 2 箇所を触る（負債） | S0-a の契約の revision（`ContentSourceKind` / `ContentAcceptanceBasis` に値を足すスライスが、同時に表を internal / public の static に出す） |
| host 述語を snapshot の候補にも当てる（host が明示的に選ぶ）こと | U-S1-3 で owner が「当てない」を採った場合、要る host が現れた時の後続（新しい選択の入力が要る） |
| transport の protocol version（`version`、`playerConfigSchemaVersion`、`product`）を候補の採否に含めるか | install 時の `ContentManifestValidation` が既に拒否する。resolver 側で要るかは S3 / S4 の adapter が決める |
| engine の version の範囲一致（同じ major の patch を許す等）、互換の緩和 | 本 program の外。必要になれば Phase A の新しい revision（host 述語は AND なので緩和に使えない） |
| `Full` fallback の実効表現を record に写すこと、採用した候補と実ロード元の同一性 | S3 |
| Editor snapshot の機構 (i) / (ii) の選定 | S0-d `EDITOR_SNAPSHOT_STARTUP_PROOF`（PR #175 で新設予定。マージ前は S0-c / S3 の A1、program §10 U2） |
| 理由 code を `ProbeOutcome` の値として持つこと（文字列の前置ではなく） | 既存の公開 enum の変更なので本スライスでは行わない。要るなら S5 の観測 UI の A1 で、S0-a の契約の revision として |

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約と既存の契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない（本スライスは asmdef に触れない。新規 file は `OneStarMaker.Runtime.asmdef` の配下の `Core/` 直下）。
- 新規の Unity 側 `.cs` は先頭に `#nullable enable`。`record` を使わない。core では `init` accessor も使わない（S0-a と同じ理由: `IsExternalInit` polyfill は `OneStarMaker.Runtime` の internal で、core を将来別 asmdef に分けた時に壊れる）。
- core は UnityEngine・UniTask・OSM の他の部分を参照しない。.NET 8 の offline project で core の file だけを link してビルドできることが閉包の証拠になる（他の OSM 型を参照すればビルドが失敗する）。
- net8 の BCL にあって Unity の API 互換レベル（.NET Standard 2.1）に無い API を使わない（`ArgumentNullException.ThrowIfNull` 等）。offline では通るので Phase B の Unity コンパイルで捕まえる。schema の解析は §2.1 I-C1 (5) の規則を手書きで実装し、`int.TryParse` を使わない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。本スライスのテストは同期の判定と、完了済みの `ValueTask` を返す fake provider だけで組む。取消のテスト（C14）は取消済みの `CancellationTokenSource` を使う。
- 参照 0 を削除理由にしない。`ContentTraceability.Reproducible` 等の先行宣言に触れない。`Evaluate` は resolver からは呼ばれない先行の公開面で、消費者は S0-c の入口の候補検査と S5 の観測（§3）。
- path は `tools/path-budget.json` の予算（150 文字）に従う。最長の新 path は `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentCompatibilityEnvelope.cs.meta`（101 文字）。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。offline テスト（`dotnet run`）は Unity でも Build でもないので Phase B が B exit で実行してよい。Phase B を Cloud（Unity の無い環境）で行う場合、Unity のコンパイルは「未確認」と §6 に書き、Phase C の全 EditMode 回帰（コンパイルを含む）で代える。
- 新規 `.cs` の `.meta`: Unity の無い環境（Cloud）で Phase B を行う場合は、既存の Core の `.meta` と同じ 2 行（`fileFormatVersion: 2` と、新しく生成した 32 桁の 16 進の `guid`）を手で書く。repository 内の既存の guid と重複しないことを `git grep` で確かめる。Phase C の Unity の import の後に、これらの `.meta` が書き換わらないこと（`git status` が clean）を確かめる。書き換わった場合は B 適応（Unity が書いた内容を commit し直す）で、Phase A は再開しない。
- 本スライスは `AbstractApplicationInitializer` と S0-c の file に触れないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` との Phase B の同時実行制約と、S0-c との衝突の対象外。

### 2.5 未決事項と owner の裁定

**owner（root）が 2026-11-09 に U-S1-1〜U-S1-7 の全てを推奨どおりに裁定した。** 以下は各項の論点と推奨（＝裁定）の記録。U-S1-1・U-S1-6 に伴う program の文言の修正（§3.1 の 10、§4.1 の第 3 項と envelope の文、U5）は本スライスの計画 PR に含めた（AC2 の 5）。

- **U-S1-1 program §4.1 の「S1 が定める互換 envelope」の範囲。裁定: 合成の欄は S2。** program は package の合成可否を「S1 が定める互換 envelope（共同の immutable build plan / dependency graph の出所、target / engine / schema、表現、shared asset の所有と重複の方針、同一 logical identity の衝突規則、package 索引の hash 関係）」で判定するとしている。推奨: **本スライスは 1 候補の採否の envelope（content set / target / engine / schema、任意の revision）だけを定め、合成の欄（build plan・graph・shared asset・索引の hash）は S2 へ送る。** 理由: resolver は 1 要求に 1 候補を採る構造で、複数候補の関係を比べる場所が無い。合成の欄に値を書く producer（manifest の欄）が今は無く、先に型を置くと検証できない先行宣言になる。G24 の実証は S2 の gate。採るなら program §4.1 の文言を owner が直す（AC2 の 5 で許す）。
- **U-S1-2 engine / schema の事実の渡し方。裁定: 予約 key。** 推奨: `ContentCandidate.Facts` の予約 key（`osm.compat.engineVersion`、`osm.compat.rootSchemaVersion`。core の定数 `ContentCompatibilityFactKeys`）。アーキテクチャゲートは、依頼者の制約の下で唯一の経路として妥当とした。代替: `ContentCandidate` か `ContentIdentity` に欄を足す（型で強制できるが、依頼者の指示「既存 Runtime の公開契約を変えない」に反するので本スライスでは採らない）。key の取り違えは adapter の責任で、欠けた key は I-C1 で不採用になる（fail-closed）。
- **U-S1-3 snapshot の候補に host 述語を当てるか（A1 の推奨を A3 で反転）。裁定: 当てない。** A1 は「当てる」を推奨したが、反例レビュー X-03 が、宣言名を比べる述語を登録しただけで、利用者が編集中の Scene が黙って directory 版に置き換わる成功経路（反例 (m)）を示した。program §3 の「snapshot を最優先で採用する」（owner 決定 A1）を host の述語の書き方次第で崩すので、**推奨を「当てない」に変える**。本文（I-C2・I-C4・E3・C28）はこの推奨に合わせてある。代償: host は snapshot に条件を課せない（要る host が現れたら、明示の選択を足す後続。§2.3）。
- **U-S1-4 同名異内容の主張の範囲。裁定: expected identity がある時に限る。** program §8 S1 の gate「同名異内容を誤採用しない」を、本書は「expected identity が与えられた時に採らない」と読む。expected identity の無い要求で同名異内容を見分けるには「どの digest が正しいか」の外部の知識が要り、汎用の判定の外にある。owner がこの読み方を認めるか、要求をまたぐ一貫（S4）まで gate を持ち越すかを決める。
- **U-S1-5 `IContentCompatibilityRule` の XML doc。裁定: 本スライスでは変えない。** base の doc は「製品実装が入るまでは、ホストは自前の規則を渡さない限り `ResolutionPolicy` を組めない」と書いており、本スライスの後は事実でなくなる。推奨: 本スライスでは file を変えず（AC2・依頼者の指示）、Phase D の harvest で 13-resource-system.md 側を現況に直す。doc の 1 文の修正は、owner の承認を得て Phase D の harvest commit に含める（宣言は変えない）。owner が「doc comment も公開契約に含める」と判断するなら、後続のどこかで直すまで doc の古さを残す。
- **U-S1-6（A2 で新設。反例レビュー X-01）program §4.1 第 3 項と、互換の不採用の後に下位の別 revision を採ること。裁定: (a)。** §4.1 第 3 項は「hash 不正・version 不整合・権限拒否は記録し、同じ expected digest の正規コピーで修復できる時だけ下位 tier へ進む。別 revision へ黙って切り替えない」と定める。既存の resolver は `RuleRejected` の後に必ず次の供給元へ進むので、規則の不採用（例: Local の schema が古い）をこの「version 不整合」に含めると、resolver を変えずには満たせない。選択肢: **(a)（推奨）** 第 3 項の「hash 不正・version 不整合・権限拒否」を、供給元が自分の申告と bytes の食い違いとして検出し、`Rejected` で返す整合性の失敗に限ると読み、program の文言をそう直す。規則の互換の不一致は理由付きで probe に残り、下位の互換な候補へ進む。別 revision へ切り替えたくない host は envelope の revision か要求の expected identity で固定する。(a) を採る場合、program の次の 2 文も同じ裁定で直す（外部レビュー 1）: U5 の汎用の一致の列を「content set・target・engine・schema は常に、revision は host が固定した時だけ、digest は要求の expected identity がある時だけ」へ。§3.1 の 10「非互換 revision への暗黙 fallback」に、規則の不採用の後に下位の互換な候補へ進むことを含めない、と書く。あわせて §4.1 の「別表現へ黙って切り替えない」は、要求が表現を指定した時だけに数える（外部レビュー 2）。AC2 の 5 はこの裁定の範囲の program の本文を許す。外部レビューは (a) を、出荷済みの resolver の remarks（`ContentResolver.cs` 22〜24 行目。別 revision は pin の比較が防ぐ）と一致するとして支持した。(b) S1 は第 3 項を満たせないと認め、問いを program の A0 へ差し戻す（resolver の変更を伴う別の revision が要る）。(c) 規則の不採用でも探索を止める手段を作る: 既存の契約では規則の例外（`Fault`）しかなく、互換の不採用を fault として記録するので I-C6 に反する。採らない。
- **U-S1-7（A2 で新設。反例レビュー X-04、目的レビュー P-07）content set を envelope の必須にすること。裁定: 必須にし、目的の文言を改める（§1）。** program U5 は汎用の一致に content set を含め、install 時の検査も要求との content set の一致を必ず見る。content set を任意にすると、expected identity の無い要求で別の content set（旧い DLC 等）の候補を採る（反例 (n)、program §3.1 の 5）。推奨: 必須にする。本文の設計はこの推奨に合わせてある。目的の欄の文言「必要なら content set と revision」と食い違うので、§1 の改訂案の文言への置き換えを owner が裁定する。置き換えるなら、未関与の担当で目的レビューを 1 回やり直してから凍結する。

### 2.6 スライスの分割の検討

分割しない。契機の確認: (1) 独立した変更理由の混在 — 汎用の判定と host 述語の合成は「候補を採ってよいかの製品の規則」という 1 つの変更理由で、合成規則（AND・評価順・例外の扱い・snapshot の除外）は汎用の判定の不変（I-C2・I-C4）と切り離して判定できない。ただし U-S1-3 の裁定で合成規則だけが変わりうるので、`ContentCompatibilityRule` の中では「汎用の判定」と「述語の合成」を private の 2 段に分けて書く（アーキテクチャゲートの指示。将来の分割を機械的にする）。(2) 高リスクの初回実機の未知 — 無い（pure な core で、実機の検証を含まない）。(3) 別の問いの判定に使う受け入れ条件の混入 — S0-a から移送された「pin 未充足 + `Fault` → `IntegrityRejected`」のテスト（C24）は resolver の既存挙動を固定するテストで本スライスの問いではないが、program §8 が S1 以降に送った入力で、製品 code を変えず 1 件のテストを足すだけなので同居させる。A2 の 3 件はいずれも分離を求めなかった。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/` からの相対（offline テストは repository の root から）。型名は A3 で凍結する。行数は予想。全ての製品の型は不変の値で、一覧は constructor で防御的に複製する。namespace は既存の `OneStarMaker.Runtime.ContentResolution`。asmdef は既存の `OneStarMaker.Runtime`（変えない）。A1 にあった `ContentCompatibilityVerdict`（struct）はアーキテクチャゲート G-03 を採って削り、公開型は 4 つにした（`default` が互換でないことは enum の `Unset = 0` が担い、理由は既存の `Accepts` と同じ `out string` で返す）。

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 単体テスト | 予想行数 |
|---|---|---|---|---|---|---|---:|
| `ContentCompatibilityEnvelope.cs`（新規） | Play 前に固定する 1 候補の互換の基準: `ContentSet`・`Target`・`EngineVersion`（必須、空白だけを拒む。`ContentSet` の必須は U-S1-7）、`RootSchemaVersion`（必須、1〜999,999,999。事実の正規表記の 9 桁と同じ範囲。G-01）、`Revision`（任意。null は「固定しない」、空白だけは `ArgumentException`）。比較は持たない | 基準の欄が増えるとき | host が作り policy の持ち主が保持する不変の値。寿命は policy と同じ | なし | public sealed class | offline（C19） | 70 |
| `ContentCompatibilityFactKeys.cs`（新規） | 候補の `Facts` に互換の事実を書く予約 key の定数（`EngineVersion = "osm.compat.engineVersion"`、`RootSchemaVersion = "osm.compat.rootSchemaVersion"`）と、予約の接頭辞 `osm.compat.`。接頭辞の下の未知の key は判定に使わない（G-08）。宣言に当たる key は定義しない（I-C3） | 予約 key が増えるとき | 定数 | なし | public static class | offline（C5 が使う） | 25 |
| `ContentCompatibilityMismatch.cs`（新規） | 判定の結果の種別: `Unset = 0`（無効。`default` が互換と読めないため）、`Compatible`、`UnsupportedSource`、`ExpectedIdentityMismatch`、`IncompleteIdentity`、`ContentSetMismatch`、`RevisionMismatch`、`TargetMismatch`、`EngineVersionMissing`、`EngineVersionMismatch`、`SchemaVersionMissing`、`SchemaVersionMalformed`、`SchemaVersionMismatch`、`HostPredicateRejected`。値の名前は理由の文字列 `compat:<名前>:` にそのまま使うので、A3 で凍結する | 種別が増えるとき | 値型 | なし | public enum | offline（C18） | 45 |
| `ContentCompatibilityRule.cs`（新規） | `IContentCompatibilityRule` の製品実装: `Evaluate(request, candidate, out string reason) → ContentCompatibilityMismatch`（§4 の判定順。private の「汎用の判定」と「述語の合成」の 2 段）と、それに委譲する `Accepts`（`Compatible` のときだけ true）。host 述語の一覧は constructor で複製し、null の要素を拒む。述語の例外は捕まえず、包み直さない。`Evaluate` の消費者は resolver ではなく、S0-c の入口の候補検査と S5 の観測（先行の公開面）。`Evaluate` は互換性だけの部分判定で、要求の不変（logical key・kind・表現）と供給元の一致を見ない（XML doc にそう書く。§2.3 の S0-c の行） | 汎用の判定の規則、合成規則が変わるとき | host が作り policy が保持する不変の値。状態を持たず、決定的な述語の下で同じ入力に同じ結果 | 上の 3 つ、既存の `ContentRequest` / `ContentCandidate` / `ContentIdentity` / `ContentSourceKind` / `ContentAcceptanceBasis` / `IContentCompatibilityRule` | public sealed class | offline（C1〜C30） | 180 |
| `tools/ContentResolutionOfflineTests/CompatibilityTests.cs`（新規） | §5 の C1〜C23・C25〜C30 と host 述語の fixture（宣言名・宣言の表現・graph の version を `Facts` の host 固有の key で比べる 3 種。空の期待値・空の事実は一致にしない）。`Program` の `partial` として既存の helper（`Req`、`Cand`、`Provider`、`Policy`、`Resolve`、`Run`、assertion、`CoreDirectory`）を使う | 互換のテストが増えるとき | テスト | linked core | なし | — | 580 |
| `tools/ContentResolutionOfflineTests/Program.cs`（変更） | `partial` の付加、`Main` から互換のテストを呼ぶ行、C24（S0-a から移送された resolver の既存挙動の固定）の 1 件 | — | テスト | linked core | なし | — | 693 → 740 |
| `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`（変更） | 新規の Core の 4 file の `<Compile Include>` | — | — | — | — | — | 33 → 37 |

- 行数警報: 新規の `ContentCompatibilityRule.cs`（180）は 500 行未満。判断は 1 つ（候補を採ってよいか）で、汎用の判定と合成規則は同じ変更理由（§2.6）。`Program.cs` は 50% 以上の増加を避けるため、互換のテストを `CompatibilityTests.cs` に分ける（互換のテストは S1 の規則と一緒に変わり、既存の 28 件は resolver と一緒に変わる。helper は共有するので `partial` にする。`CoreDirectory` の `[CallerFilePath]` は同じ folder なので壊れない）。`CompatibilityTests.cs`（580）は 500 行の警報に掛かるが、同じ変更理由・同じ依存・同じ helper のテストの列で、分割すると helper の共有が増えるだけなので非分割（アーキテクチャゲートが妥当とした）。
- 置き場所の理由: 規則は S0-a の `IContentCompatibilityRule` の製品実装で、同じ pure な core に属する（Unity・AssetManagement・BuildSystem を知らない）。`Core/` 直下に置く理由は、`CoreSources_DoNotNameUnityOrHosts` の汎用性・純粋さの検査（非再帰）の対象に入れるため（反例 (l)）。子 folder を作るなら走査を再帰に変える必要があり、既存テストの本文の変更になる（AC2）。`BuildContent/Distribution/` に置かない理由: install の検査（bytes の検査）と候補の採否は別の層で、Distribution に置くと resolver の core が BuildSystem 本体を参照することになる（ASSET_BUILDSYSTEM_INDEPENDENCE の境界）。アーキテクチャゲートはフォルダ境界の条件のどれも違わないので子 folder を作る根拠が無いとした。
- Policy / orchestration / I/O の区分: 本スライスの製品の型は全て policy（判定の規則と、その入力の値）。orchestration は既存の `ContentResolver`、I/O は後続の adapter。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序:
  1. `ContentCompatibilityMismatch` → `ContentCompatibilityFactKeys` → `ContentCompatibilityEnvelope` → `ContentCompatibilityRule`。
  2. csproj に link を足し、`CompatibilityTests.cs` の C1〜C23・C25〜C30 と、`Program.cs` の C24 を書く。
  3. `dotnet run` で全件を通す。
  4. Unity Editor で compile を確認する（できる環境なら）。`.meta` は Unity が生成した新規 file の分だけを commit する。
  5. `pwsh tools/contract-audit.ps1`（できる環境なら）。
- `Evaluate` の判定順（A3 で凍結する。理由 code はこの順で最初に当たった 1 つ）:
  1. `UnsupportedSource`: 由来が `ContentDirectory` で根拠が `VerifiedDigest`、または由来が `EditorSnapshot` で根拠が `OpenSceneSnapshot`、のどちらでもない（防御として未定義の enum 値も含めて扱うが、未定義の `ContentSourceKind` / `ContentAcceptanceBasis` は `ContentCandidate` の constructor が拒むので規則に届かず、テストもしない）。
  2. `ExpectedIdentityMismatch`: 要求に expected identity があり、候補の identity と全欄一致しない。**由来に関係なく**行う（resolver の pin と同じ答え。X-05）。比べるのは要求の expected identity だけで、修復中の pin（識別子付き `Rejected` 由来）は resolver だけが知る（G-05）。
  3. 由来 `EditorSnapshot` ならここで `Compatible`（理由 `compat:Compatible:EditorSnapshot`）。host 述語は当てない（U-S1-3）。
  4. 由来 `ContentDirectory` の時: `IncompleteIdentity` → `ContentSetMismatch` → `RevisionMismatch`（envelope が固定している時だけ）→ `TargetMismatch` → `EngineVersionMissing` → `EngineVersionMismatch` → `SchemaVersionMissing` → `SchemaVersionMalformed` → `SchemaVersionMismatch`。
  5. host 述語を登録順に評価し、最初に false を返した述語で `HostPredicateRejected`（理由に述語の 0 起点の番号と述語が返した理由を含める）。それ以降の述語は呼ばない。述語の例外はそのまま伝える。
  6. 全て通れば `Compatible`（理由 `compat:Compatible:ContentDirectory`。述語の理由を連結しない。X-06）。
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
| C5 | `Compat_MissingFacts_AreRejected`（engine と schema のそれぞれで key 無し・null の値・空文字・空白だけ → `EngineVersionMissing` / `SchemaVersionMissing`） | E4（壊れた manifest）、I-C1 (4)(5)、反例 (a) |
| C6 | `Compat_MalformedSchema_IsRejected`（全て `SchemaVersionMalformed`: `" 2"`、`"2 "`、`"+2"`、`"-1"`、`"0"`、`"02"`、`"2.0"`、全角の `"２"`、10 桁の値） | I-C1 (5)、反例 (b) |
| C7 | `Compat_IncompleteIdentity_IsRejected`（空の digest、空白だけの revision） | E4（壊れた manifest）、I-C1 (1) |
| C8 | `Compat_SameNameDifferentDigest_WithExpectedIdentity_IsNotAdopted`（resolver 経由で `PinMismatch`、規則の直接呼び出しで `ExpectedIdentityMismatch`。identity が空の snapshot も直接呼び出しで `ExpectedIdentityMismatch`） | E4（同名異内容）、I-C1 (2)、I-C4、反例 (i) |
| C9 | `Compat_ContentSetAndFixedRevision_MismatchIsRejected`（別の content set、envelope が固定した revision との不一致。revision を固定しない envelope では別 revision を採る） | E4（別の content set）、I-C1 (6)、反例 (n) |
| C10 | `Compat_CaseDifference_IsMismatch`（target・engine・content set） | I-C1、反例 (b) |
| C11 | `Compat_WithoutHostPredicate_DeclarationFactsAreIgnored`（`osm.compat.` の下の未知の key も無視） | E2、I-C3、反例 (f) |
| C12 | `Compat_HostPredicates_DeclarationNameRepresentationAndGraph`（一致で採用、不一致で `HostPredicateRejected`、空の期待値・空の事実は一致にしない、宣言の表現の不一致は「別表現」。理由に `digest verified` を返して採る述語を登録した directory の採用時の理由が `compat:Compatible:ContentDirectory` と完全一致する） | E2、E4（別表現）、AC5、反例 (f)、外部レビュー 6 |
| C13 | `Compat_HostPredicate_CannotLoosenGenericRejection` | I-C2、反例 (d) |
| C14 | `Compat_HostPredicateException_PropagatesUnchanged`（(i) pin の無い要求で `Fault` → `ProviderFault`、後続を probe しない。(ii) 要求の expected identity と違う候補の後に述語が例外 → `IntegrityRejected`。(iii) 述語が自分の中で `CancellationTokenSource.Cancel()` を呼んでから、その token の `OperationCanceledException` を投げる → resolver が取消として投げ、`Fault` にならない（resolver は規則の前に token を見るので、取消済みの token を最初から渡すと規則まで届かない）。(iv) token が取消されていないのに述語が `OperationCanceledException` を投げる → `Fault`（S0-a の既存の契約）。(v) 規則の直接呼び出しで、述語が投げた例外の型と instance がそのまま出る） | I-C2、反例 (e)、X-02、X-07 |
| C15 | `Compat_HostPredicates_AllMustAccept_InOrder`（2 番目が拒むと 3 番目は呼ばれず、理由に番号 1） | I-C2 |
| C16 | `Compat_SnapshotWithEmptyIdentity_IsAdopted_WithoutDigestClaim`（要求の expected identity は無し、要求と候補の表現は同じ。理由が `compat:Compatible:EditorSnapshot` と完全一致。理由に `digest verified` を返す述語を登録しても同じ） | E3、I-C4、AC5、反例 (g)、X-06 |
| C17 | `Compat_UnsupportedSourceOrBasis_MatchesResolverTable`（候補の constructor が受け付ける `ContentSourceKind` × `ContentAcceptanceBasis` の全ての組について、規則の直接呼び出しの `UnsupportedSource` と、全件採用の規則での resolver の `NoAcceptanceBasis` が一致する。未定義の値・`None` は候補を作れないので対象外。候補の logical key・kind・表現・provider は要求と probe した供給元に揃え、根拠の表だけで差が出るようにする） | I-C4、反例 (h)、G-02 |
| C18 | `Compat_UnsetIsNotCompatible`（`default(ContentCompatibilityMismatch)` は `Unset` で `Compatible` でない。`Accepts` が true を返すのは `Evaluate` が `Compatible` のときだけ） | I-C6、反例 (c) |
| C19 | `Compat_Envelope_Validation`（空白の content set / target / engine、schema 0・負・1,000,000,000、空白だけの revision、revision の null は許す） | I-C1、G-01 |
| C20 | `Compat_HostPredicateList_Validation_AndDefensiveCopy` | I-C2 |
| C21 | `Compat_RejectionReason_CarriesMismatchCode` | I-C6、AC5 |
| C22 | `Compat_AllIncompatible_IsNoCompatibleCandidate`（全ての probe が `RuleRejected` か `NotFound` / `Unavailable` で、pin が無い場合に限る） | I-C6、反例 (o) |
| C23 | `Compat_PinMatchesButSchemaMismatch_IsNoCompatibleCandidate`（供給元は 1 つ。要求の expected identity と identity が一致する候補が規則で落ちる。既存の終わり方の読み方の固定） | I-C1、I-C6、反例 (o) |
| C24 | `UnmetPin_ThenFault_IsIntegrityRejected`（`Program.cs`。S0-a から移送された入力） | resolver の既存挙動の固定（program §8） |
| C25 | `Compat_EvaluateIsDeterministic_AndDoesNotMutateInputs`（決定的な述語の下で） | I-C1 (d) |
| C26 | `Compat_NoHostPredicate_SameCandidateSameVerdict_AcrossSourceOrder`（供給元の順を入れ替えても、各候補の判定は同じ） | I-C1 の読み方 |
| C27 | `Compat_RepairAfterRejected_RequiresCompatibleCopy`（識別子付き `Rejected` の後、同じ identity でも schema が違えば採らず `IntegrityRejected`、互換なら採る） | I-C1 (g)、I-C6 の非対称、program §4.1 |
| C28 | `Compat_HostPredicate_DoesNotDisplaceSnapshot`（供給元は [EditorSnapshot, Local]、要求の expected identity は無し、要求と両候補の表現は同じ、宣言名を比べる述語を登録、snapshot は宣言の事実を持たない → snapshot が採られ、Local は probe されない） | E3、I-C4、反例 (m)、X-03、外部レビュー 3 |
| C29 | `Compat_NewCoreFiles_AreDirectlyUnderCore_AndGeneric`（新規 4 file が `Core/` 直下に在り、`Core/` の子 folder に `*.cs` が 1 つも無い（後続のスライスが `Core/` 直下に file を足しても壊れないよう、件数の固定はしない）。新規 file に AC6 の語の検査を行う） | AC1、AC6、I-C3、反例 (l)、X-08、G-04 |
| C30 | `Compat_UnspecifiedRepresentation_MayAdoptOtherRepresentation`（要求の表現は空、Local は表現 `Full` で schema 不一致、LAN は表現 `Low` で互換 → LAN を採り、Local は `RuleRejected`。要求が `Full` を指定すると LAN は `RepresentationMismatch` で `NoCompatibleCandidate`） | §2「成果の範囲」と I-C1 の読み方（外部レビュー 2） |

- 差し戻し中の起点 `-Filter`: Unity テストは不要（offline の `dotnet run` が起点）。Unity の差し戻し確認が要る場合だけ `OneStarMaker.Tests.ContentResolution`（既存の `ContentRequestLoaderTests`。本スライスの型を使わないが同じ assembly の compile を見る）。C は凍結済み条件の確認に必要な範囲を根拠付きで変えてよい。
- 判定必須テスト（GO 候補 head）: (1) offline の全件（AC1・AC4・AC5・AC6）。(2) base と head の全 EditMode 回帰（AC7、空 filter、`pwsh tools/run-tests.ps1`）。(3) `pwsh tools/contract-audit.ps1`（AC8）。
- 全 EditMode 回帰の適用除外: なし（新規 file が `OneStarMaker.Runtime` に compile されるため、標準どおり実行する）。
- 統合・Unity テスト: 新しい EditMode テストは書かない。本スライスの製品の型は Unity を使わず、Unity との接続（実 manifest の adapter、実ロード）は S3 / S4 にある。**Unity 実機での動作確認は本スライスの合格条件に無く、行わない。**
- 操作・実行時・目視条件の検証経路: 対象なし（操作・実行時・目視の条件が無い）。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（offline project と全 EditMode 回帰は S0-a で使った既存の経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（offline の出力の `PASS` 行と §5 の表の照合、2 つの XML の比較は、判定 C が凍結済みの手順として手で行う。機械向けの期待値 file は作らない）。
- 機械検査: `dotnet run`（offline）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC2・AC3 の `git diff` / `git grep`。
- A0/A1 主担当・モデル・ベンダー: A0 は program（revision 3.2）。A1 は Claude Code（Cloud）、Anthropic。
- C' 用に予約した担当・モデル・ベンダー: 未定。Phase A では Anthropic の 4 系列（A1、A2 の 3 件、目的レビューのやり直し）と、外部レビューの Grok 系を使った。C' には Phase A・B・C のどれにも関与しないベンダー（例: OpenAI 系）を予約することを推奨する（risk `normal` なので強化条件ではないが、Anthropic と Grok は使い切っている）。
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk `normal`）。

### 5.1 A2 記録と A3 の採否（統合版。凍結前）

- 入力版: A2 入力版 commit `50d6ff2e` の本書（内容 sha256 `14fa0a4f8900912669c34b02193c6a18afa70d6ecd5f4a7b850fcc8f4bfe0757`）。目的レビューの入力は §1 の 2 欄の原文だけ（sha256 `dbe366046a32fbaaedb8c8df434965fda998e2c9c92ca4d78f7481833fcb9cba`。UTF-8、LF、末尾改行なし、ラベルと直後の空白を除いた本文）。3 件は互いの指摘を見ていない。
- 担当（2026-11-08、Claude Code の subagent。主担当の session から起動し、同じ session の会話は渡していない。いずれも Anthropic）:
  - 目的レビュー: Claude Sonnet 系。repository を読まず、2 欄の原文だけ。
  - アーキテクチャゲート: Claude Fable 系。本書全体、`AGENTS.md`、architecture-gates.md、Core と offline テスト、関連文書。
  - 反例レビュー: Claude Opus 系（主担当と同じ系列。独立性は「別 session・会話なし」まで）。本書全体、Core、offline テスト、program。
- A3 統合担当: 主担当（Claude Code、Cloud）。owner の裁定は未了（§2.5）。
- 総合判定: 目的レビューは判定を出さない形式。アーキテクチャゲートは「blocker なし、条件付きで A3 へ進めてよい」。反例レビューは「このままでは凍結できない（X-01・X-04 は owner の裁定が要る、X-02 は文言の修正が必須、X-03 は推奨の見直しが要る）」。

| 指摘 | 要旨 | 採否 | 理由と反映先 |
|---|---|---|---|
| P-01 | 判定は host 非依存の 2 入力に閉じる | 採用（既に満たす） | I-C3 |
| P-02 | expected identity は全欄一致、欄の欠落は不採用 | 採用（既に満たす） | 4 欄がそろわない pin は S0-a の constructor が拒む。I-C1 (2) |
| P-03 | 判定不能は不採用（fail-closed） | 採用（既に満たす） | I-C1 (4)(5)、I-C4 |
| P-04 | host 述語は AND だけ、例外は不採用側、述語の有無で汎用の結果が変わらない | 採用（例外の扱いは読み替え） | I-C2。例外は「不採用」ではなく resolver の `Fault`（探索の停止）に倒す。こちらの方が強い fail-closed で、S0-a の契約 |
| P-05 | 理由は供給元ごとに機械で区別でき、決定的 | 採用（既に満たす） | I-C6、C21、C25 |
| P-06 | 基準は Play 前に固定し変更できない | 採用（既に満たす） | envelope は不変の値（§3） |
| P-07 | 「必要なら content set と revision」の判断主体が曖昧 | 採用 | content set を必須、revision は host が envelope で固定した時だけ（U-S1-7。owner の裁定で §1 の文言を改めた） |
| P-08 | 供給元の申告は未検証の主張 | 採用 | §1 対象外と §2「成果の範囲」に明記 |
| P-09 | 規則を policy に渡せる形で提供するまで | 採用（既に満たす） | §2.3 |
| P-10〜P-14 | 作らないもの（host 語彙、緩和の仕組み、自動修復・fallback、実行時の操作、後続の実装） | 採用（既に満たす） | I-C3、§2.3（緩和は program の外）、対象外 |
| G-01 | envelope の schema の int と事実の解析の範囲の食い違い | 採用 | envelope を 1〜999,999,999 に制限、事実は 1 つの解析関数で int にして比べる（§3、I-C1 (h)、C19） |
| G-02 | 受け入れ根拠の表が resolver の private な表の複製 | 採用 | §2.3 に負債として記録、C17 を resolver との横断 assertion に |
| G-03 | `ContentCompatibilityVerdict` は削れる、`Evaluate` の消費者を名指し | 採用 | 公開型を 4 つに。`Evaluate(…, out string) → Mismatch`、消費者は S0-c の入口と S5（§3、§2.4） |
| G-04 | AC1 の確認手段が観測できない | 採用 | C29 を新設（AC1） |
| G-05 | `ExpectedIdentityMismatch` は修復中の pin を見ない | 採用 | §4 判定順 2 に注記 |
| G-06 | `string.Contains(char)` は .NET Standard 2.1 にある | 採用 | §2.4 から削除 |
| G-07 | 行番号・テスト番号のずれ | 採用 | §1 現況と §2.1 の行番号を直し、テストは名前で照合すると明記 |
| G-08 | 予約の接頭辞の下の未知の key の扱い | 採用 | 無視する（§3、I-C3、C11） |
| G-総合 | 規則を `Core/` 直下、予約 key、既存 interface の再利用、非分割、risk normal は妥当。汎用の判定と述語の合成を private の 2 段に | 採用 | §2.6、§3 |
| X-01 | I-C1 の読み方が program §4.1 第 3 項と矛盾 | 採用（owner 裁定 2026-11-09: U-S1-6 (a)） | U-S1-6 を新設。推奨は (a) 第 3 項を整合性の失敗に限る読み方と program の文言の修正。AC2 の 5 に program の修正を許す |
| X-02 | 終わり方が既存の `DecideFailureCode` と合わず、C27 と矛盾 | 採用 | I-C2・I-C6 を既存の優先順に従う形に書き直し、非対称を既存挙動の読み方として固定。C14 に pin 未充足の場合、C22 の条件を限定、C23 は供給元 1 つ |
| X-03 | host 述語を snapshot に当てると編集中の Scene が黙って置き換わる | 採用（U-S1-3 の推奨を反転、owner 裁定 2026-11-09） | 反例 (m)、I-C4、C28 |
| X-04 | content set を固定しない envelope が U5 と食い違う | 採用（owner 裁定 2026-11-09: U-S1-7） | U-S1-7、反例 (n)、C9。目的の文言の改訂案（§1） |
| X-05 | 単独呼び出しの検査の方針がぶれる | 採用 | expected identity は由来に関係なく比べる（§4 判定順 2、C8） |
| X-06 | 述語の理由の連結で snapshot の理由に digest の語が残る | 採用 | 採用理由に述語の理由を連結しない（§4、C16）。U-S1-3 で snapshot に述語を当てなくなるが、規則として凍結する |
| X-07 | 例外を包み直すと取消が `Fault` に化ける | 採用 | I-C2「包み直さない」、C14 (iii)(iv) |
| X-08 | AC1 の確認手段が実行できない | 採用 | C29（G-04 と同じ） |
| X-09 | テスト番号（T13）と `RequiredBasis` の行番号 | 採用 | G-07 と同じ |
| X-10 | AC6 の `Cancel` の除外、G08 の宣言の範囲、C25 の決定性の前提 | 採用 | AC6 を走査の扱いに揃えた、§2「成果の範囲」、C25 の名前の注記 |

- 目的レビューのやり直し（2026-11-09）: U-S1-7 の裁定で目的の欄を改めたので、改めた 2 欄の原文だけ（sha256 `de6f3a5c2db2699696f62cf449217d9365fc879c640a53639763c795a98b7831`）を、本書・初稿・A2 の指摘を読んでいない担当（Claude Code の subagent、Claude Haiku 系、Anthropic。repository を読まない）に渡した。やり直しは 1 回までで、これ以上 2 欄は変えない。回答は 2 欄の文言の変更を求めず、指摘は全て本文の保証・作らないものとして分類した。

| 指摘 | 要旨 | 採否 | 理由と反映先 |
|---|---|---|---|
| Q-01・Q-05・Q-10・Q-13 | 入力を基準と expected identity に閉じる、述語は AND で例外は fail-closed、判定不能の列挙、OR 合成や無効化フラグを作らない | 採用（既に満たす） | I-C2・I-C3・I-C1 (4)(5)・§4 の判定順 |
| Q-02 | 比べられない欄は不採用。「expected identity の全欄」の意味 | 採用（既に満たす） | 4 欄がそろわない期待値は S0-a の constructor が拒むので、要求が一部の欄だけを与える状態は無い。I-C1 (2) に明記 |
| Q-03 | 要求が revision を指定し基準が固定していない時 | 採用（既に満たす） | 要求は revision だけの部分の期待値を持てない（同上）。revision を含む期待値は全欄の pin として比べる。I-C1 (2) に明記 |
| Q-04 | digest の比較方式と形式の検査 | 採用 | ordinal の全欄一致、正規化しない、形式は検査しない（adapter の責任）を I-C1 (2) に明記 |
| Q-06 | version は完全一致 | 採用（既に満たす） | ordinal の完全一致。範囲一致は program の外（§2.3） |
| Q-07 | 決定性と複数候補の選択規則 | 採用（既に満たす） | 供給元の順で最初に採れた候補（既存の resolver）。C25・C26 |
| Q-08 | 理由は安定した code、複数の理由の扱い | 採用（既に満たす） | `ContentCompatibilityMismatch` の名前の前置。§4 の判定順で最初に当たった 1 つだけを残す |
| Q-09 | 述語の登録は基準の固定の前 | 採用（既に満たす） | 述語は規則の constructor で渡し、不変（§3） |
| Q-11・Q-12・Q-14〜Q-17 | 実行時の操作、近い候補の採用・自動変換、汎用の解決 engine、永続化、実体の再 hash、暗黙の登録を作らない | 採用（既に満たす） | 対象外、§2.3 |
- 外部レビュー（owner が依頼、2026-11-09）: 入力は A3 統合版 commit `09ac3df7`。PR #174 の Cursor の review コメント 2 件（同じ担当の要約と本文。本文は Grok 系の別 session と明記）と、PR #174 の行コメント 2 件（P1・P2）。目的レビューのやり直しではない。総合は「骨格はこのまま凍結できる。1 を裁定に含め、2〜7 を本文に直してから凍結」。U-S1-1・2・3・5・7 の推奨はそのままでよい、U-S1-6 は (a) を支持。

| 指摘 | 要旨 | 採否 | 理由と反映先 |
|---|---|---|---|
| 外 1 | U5 の汎用の一致の列と §3.1 の 10 が裁定の範囲の外に残る | 採用 | U-S1-6 (a) の裁定に、U5 の列の読み替えと §3.1 の 10 の注記を含めた。I-C1 の読み方 |
| 外 2 | 表現を指定しない要求では、規則の不採用の後に別表現の候補が採られる | 採用 | 規則に表現の比較は足さない（U5）。§2「成果の範囲」、E4、I-C1 の読み方、U-S1-6 に「要求が表現を指定した時だけ別表現を誤採用と数える」。C30 で固定 |
| 外 3 | expected identity があると identity の空な snapshot は落ち、pin に合う directory が成功する | 採用 | I-C4 の経路 (a0)、§2.3 の S0-c の行の引継ぎ条件 (3)、C16・C28 の手順（expected identity 無し、表現を揃える） |
| 外 4 | 「install が先に拒むので全て `RuleRejected`」は観測と違う | 採用 | §2.3 の文を直した |
| 外 5 | `ContentCandidate` の行番号が違う | 採用 | constructor 27〜74 行目、`CopyFacts` 96〜115 行目に直した（A3 で G-07 の値を写した誤り） |
| 外 6 | directory の採用理由に述語の理由を連結しないことをテストが固定していない | 採用 | C12、AC5 |
| 外 7 | engine の空白と schema の空文字で理由 code が違う | 採用（揃える方向） | 空文字・空白だけ・null は engine も schema も `…Missing`、空白以外の文字がある非正規の表記は `SchemaVersionMalformed`（I-C1、C5・C6） |
| 外 C17 | 未定義の `ContentSourceKind` は候補を作れない | 採用 | C17 と §4 の判定順 1 に注記 |
| P1（行） | `Evaluate` 単独では要求の不変と供給元の一致を保証しない | 採用 | §2.3 の S0-c の行の引継ぎ条件 (1)・(2)、§3 の規則の行、反例 (p) |
| P2（行） | AC3 の「folder に変更 0 件」は AC2 と両立しない | 採用 | AC3 を base に存在した file の変更・削除・改名 0 件に限定 |

- S0-c の計画（PR #175、別ブランチで A3 の前）との突き合わせ（主担当、2026-11-09）: (1) #175 は snapshot の機構の選定と実 initializer の gate を新スライス S0-d へ切り出す。本書の該当の参照（対象外、§2.3）を S0-d に直した（#175 のマージ前は program 上 S0-c / S3）。(2) #175 は Built の候補を「検証を通った最新の install」とし「S1 で互換判定に置き換える」とする。置き換えは S0-c の Phase D の後の接続で、本書は入口に触れない（§2.3 の S0-c の行に引継ぎ条件を書いた）。(3) #175 と本書はどちらも program §8 の表の隣り合う行を変えるので、後からマージする側に衝突が出る（§0 の停止規則）。

### 5.2 Phase C / C' で突かれそうな点の自己点検（主担当、2026-11-09。凍結前）

owner の依頼で、凍結前に主担当が本書を Phase C / C' の目で読み直した。独立レビューではない。直したものと、C / C' に先回りで渡す読み方を分ける。

- **直した（本文に反映済み）:**
  - C14 (iii) は書かれたとおりには実行できなかった。resolver は provider の回答の後、規則の前に token を見る（`ContentResolver.cs` 68〜69 行目）ので、取消済みの token を渡すと規則まで届かない。述語の中で取消してから投げる手順に直し、取消されていない token の `OperationCanceledException` が `Fault` になる場合 (iv) を足した。
  - C29 の「`Core/*.cs` が 24 件」は、後続のスライスが `Core/` に file を足すと壊れる。子 folder に `*.cs` が無いことの検査に替えた。
  - Cloud で Phase B を行うと、Unity が `.meta` を作らない。AC2 は `.meta` を求めているので、手書きの規則と Phase C での確認を §2.4 に書いた。
  - C17 の候補が要求の不変で先に落ちると、根拠の表の比較にならない。候補を要求に揃えると書いた。
- **C / C' への読み方（変更なし）:**
  - offline テストの判定入力は、判定 C の担当が GO 候補 head で自分で実行した `dotnet run` の出力とする。Phase B の出口の実行結果（Cloud を含む）は判定の入力にしない（phases-and-handoff.md「実行担当と判断担当」）。出力と 2 つの XML は判定 evidence bundle に入れる。
  - AC7 の base は、§0 の implementation base commit（Phase B の着手時に取り直した値）とする。
  - `Evaluate` と `Accepts` の理由が `compat:Compatible:<由来>` で採用時の probe にも残ることは仕様で、「採用時に理由が付く」ことを欠陥にしない（§4 の 3・6）。
  - 要求の表現が空の時に別表現の候補が採られること（C30）、expected identity の無い要求で別 revision・別 digest の互換な候補が採られること（I-C1 の読み方）は、owner の裁定（U-S1-4・U-S1-6）による仕様で、blocker の根拠にならない。違反を言うなら、裁定で直した program の文言（§3.1 の 10、§4.1、U5）との食い違いを示す必要がある。
  - 本スライスは Unity 実機の動作確認を持たない（§5「統合・Unity テスト」）。Unity で確かめるのは compile と全 EditMode 回帰の非退行だけで、実在の manifest・実ロードの未検証は §2「成果の範囲」に書いた既知の範囲であり、C' の「監査できなかった範囲」に書けば足りる。
- **残る点（凍結の前に owner 側で行う）:** 本書の作成環境に pwsh が無く、`pwsh tools/docs-audit.ps1` を一度も通していない（program の変更を含む）。凍結の commit の前に owner の環境で通す。

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
