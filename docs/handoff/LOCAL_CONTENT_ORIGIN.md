# 検証済み content の取得元（Local）— S4-3a

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 を 4 行に分けた最初の行 S4-3a。分け方は §2.2）
- status: `A`（A1 の初稿。A2 の入力版。**未凍結。** owner の裁定待ちは §2.9）
- branch: `claude/trusting-ramanujan-kxphtr`（PR の base は `develop`）
- implementation base commit: `1fd200b5ba835c833035d153579216e14cb823af`（`develop` の head、2026-11-24、S4-1a の merge の後。§1 の現況と A2 の固定 base もこれ。S4-2 は未 merge なので、凍結の前に S4-2 の Phase D の後の `develop` で現況を取り直す）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `high`（Framework の公開 API に取得元の差込口を足し、install の記録の形と cache への書き込みの経路に触れる。program の行は `normal` だったが、高リスクの例の「公開 API」に当たるので上げた）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「DIST transport と disk cache の境界」（取得元の差込口、一覧の file、digest の出所、取得の調停）と「まだ保証しないこと」、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「配信用 transport の公開境界」（一覧への登録）と「宣言からの Play 経路」（取得の操作）、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（別の `Library` の base と上書きの答え）、repo の README の「試す」、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 の行と後続へ送る入力
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と base の code だけ）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（owner の裁定と、Draft PR での外部計画レビュー）。
  - 次の作業: owner が §2.9 の裁定待ちの項目を決める。外部計画レビューの指摘の採否を §5.1 に足す。S4-2 の Phase D の後に base を取り直し、現況（§1）と責務マップの行数（§3）を確かめ直してから凍結する。program §8 の S4-3 の行は本書では変えていない（S4-2 の branch が隣の行を変えているので、S4-2 の merge の後に、行を本書への link と 4 行の分割に直す）。
  - 読む範囲: 既定のとおり（全節）。
  - 引き継ぐ commit: この票を含む commit（SHA は PR の状態ブロックで渡す）。
  - 必要な権限: なし（文書のみ）。凍結の後の Phase C は Unity（Windows）と Editor の操作が要る。
  - 停止規則: S4-2 の Phase D より前に凍結しない（program §8 の S4-3 の行。A0〜A3 の統合案は owner の指示で先行した）。凍結の記録が無いまま Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base の code と文書を読み、`grep` と `wc -l` で確かめた。Unity は起動していない。
- S4-2 の HANDOFF は branch `claude/player-content-overlay` の head `86a04d1`（PR #212、Phase B と発見 C の往復中）の版を読んだ。S4-2 の結論は確定していない。
- 作成環境に `pwsh` が無く、`tools/docs-audit.ps1` と `tools/workflow-task.ps1 start` は未実行。link 先の存在は手で照合した。凍結の前に owner の側で通す。

## 1. 目的と対象外

- 目的: 自分の PC で publish した検証済みの content（目的 build）を、所在・digest・revision を入力せずに、Editor の 1 つの操作で取得元から取得・検証・install して、Play（経路 Built）の base に使えるようにする。同時に、取得元の種類（LAN、cloud）を後から足す時に、検証・install・cache・選択・記録の code を変えずに済む差込口を、network を使わずに確定する。取得できない時と一致しない時は、別の revision や未検証の bytes に黙って切り替えず、取得元ごとの理由を示す。
- 利用者: (a) 自分の PC で content を build・publish し、それを base に Editor で Play する開発者。(b) 取得元の実装を足す host の開発者（次の行で LAN を足す担当と、後で cloud を足す担当を含む）。
- 目的・利用者の改訂の記録（A1、2026-11-25）: A0 の文言は、目的が「検証済みの content（目的 build）を手元に持たない開発者が、取得元の種類・所在・digest を Play のたびに入力しなくても、設定した順の取得元（手元の publish 済みの directory、LAN。後から足す cloud など）から、期待する identity と digest に一致する revision を取得・検証・install して、Editor の Play の base に使えるようにする。取得元を足す・差し替える時に、検証・install・cache・選択・記録の code を変えずに済むようにする。取得できない時と一致しない時は、別の revision や未検証の bytes に黙って切り替えず、理由を示す。」、利用者が「(a) 編集する開発者 (b) 遊ぶだけの人 (c) 取得元を足す人」だった。変更の理由は 2 つ。owner が 2026-11-25 に S4-3 を 4 行に分けることを承認したので、LAN と「遊ぶだけの人」（content を別の PC から受け取る人）を S4-3b へ移した。取得を始めるきっかけ（Play の入口、先読み、hook）を S4-3c にまとめたので、本書の取得は「1 つの操作」にした（§2.2）。利用者の入力は増やしていない。**文言そのものへの owner の承認は得ていない**（owner の「A3 まで進める」の指示で A2 に進んだ）。A3 で owner が確認し、変える時は目的レビューをやり直す（§2.9 の D-1）。
- 対象外（所有は §2.6）:
  - LAN と cloud の取得元の実装、network の失敗（timeout、切断、再試行、進捗）、別の PC から受け取る content（S4-3b）。cloud は program §7 の D5 のとおり、差込口が塞がないことの確認までにする。
  - Play ボタンを押した時に取得すること、先読み、checkout hook（S4-3c）。本書の取得は利用者が始める操作で、Play の判定（`PlayEntryDecision`）と transaction は変えない。
  - 「自分の base」を選ぶ規則と、pin を運ぶ経路（S4-3d）。本書は、呼び手が pin を渡せる口までを持つ。
  - 要求の時点の install と登録（S4-4）。13 章の「Runtime は network から起動しない」は変えない。Player の中での取得（program §10 の U10）、Android・iOS（S4-5）。
  - CI が content を publish する仕組み（program §10 の U9）、VCS の adapter、観測 UI と menu の整理（S5）。
  - Delivery の窓、検証 Play、From published directory の動き。今のまま残す。
  - cache の掃除（`Evict` と known-good の呼び手）。配布の単位を変えること（revision 全体の取得のまま）。署名による検証。
- 現況（base `1fd200b5`）:

| 事実 | 場所 | 本書に効く点 |
|---|---|---|
| bytes の取得口 `IContentArtifactSource`（相対 path から stream を開く）は public で、実装は `LocalContentArtifactSource` と `HttpContentArtifactSource` の 2 つ | `Runtime/BuildContent/Distribution/ContentArtifactSource.cs`（162 行） | bytes の差し替えは既にできる。Local と LAN（UNC の path）は同じ実装を通る。HTTP は 1 request に 60 秒の固定の期限、redirect を追わない、認証の header を足す口が無い、再試行と再開が無い |
| `ContentInstaller.InstallAsync(request, source, token)` が、`transport.json` の bytes の SHA-256 を request の期待 digest と照合し、staging で全 file を検証してから immutable な `installed/<contentSet>/<revision>` を公開する。既にある revision は同じ digest の完全な再検証だけで再利用する。install と同時の 2 つ目の操作は cache の lock で `Busy` になる（待たない） | 同 `ContentInstaller.cs`（185 行）、`ContentCacheStore.AcquireTransaction` | 検証と install は取得口の種類に依らず 1 つの経路（13 章「DIST transport と disk cache の境界」）。期待 digest は呼び手が渡す。`await` は `ConfigureAwait(false)` を付けていないので、Editor の main thread で結果を同期に待つと止まる |
| install の記録（`receipt.json`）は version・digest・contentSet・revision・target・時刻だけで、どこから取得したかの欄が無い。検証（`InstalledRevisionVerifier`）は version が 1 であることと 4 つの値の一致と時刻の形を見て、他の欄は見ない | 同 `ContentDeliveryFiles.cs`（155 行）、`InstalledRevisionVerifier.cs`（139 行） | 任意の欄を足しても、古い読み手（Player を含む）は読める |
| `ContentCacheStore` は `Inspect`・`ValidateInstalled`・`MarkKnownGood`・`Evict` を持つ。製品の code に `MarkKnownGood` と `Evict` の呼び手は無い（test を除く `grep`） | 同 `ContentCacheStore.cs`（438 行） | cache は増える一方（18 章「BuildSystem の後続」の「known-good の pin の呼び手」は未着手） |
| 取得元が持つ revision の一覧と、各 revision の期待 digest を得る口が無い。Delivery の窓は digest・contentSet・revision・cache root・budget を手で入れる。標準の経路（From published directory）は batch の結果 file `last-result.json` を file dialog で選び、そこから digest を取る | `SampleGame/DependOnAll/Editor/Build/ContentDeliveryWindow.cs`（212 行）、`VerifyPlayLauncher.ReceiveAsync` | 差し替えの本体はここ（§2.3） |
| publish は `ContentTransportPublisher.Publish(result, publishRoot)` が、呼び出し専用の staging に全 bytes と `transport.json` を揃えてから、同じ volume の rename で `publishRoot`（1 revision の directory）を公開する。既にある公開先は更新しない。batch と検証 Play の公開先は `artifacts/bs2b/transport/<identity>/` | `Editor/Build/Content/ContentTransportPublisher.cs`（227 行）、`SampleGameContentBatch.cs`（202 行）、`VerifyPlayLauncher.cs:454` | 親の `transport/` が「取得元の root」になる。publisher は親の directory の file を書かない契約である |
| Play ボタンの入口の経路 Built は installed cache だけを見る。宣言と target が合う entry を install の新しい順に検証し、最初に通った物を使う。cache に無ければ候補 0 で拒否し、回復操作（検証 Play の build など）を示す | `PlayEntryCandidates.cs`（106 行）、`PlayEntryDecision.cs`（360 行）の `PlayEntryRecovery`、`PlayEntryWindow.cs`（210 行） | 取得した revision が最新の install になれば、入口は今の規則のままそれを使う |
| 要求時解決の供給元（`IContentProvider`）の契約は「候補の有無を答えるだけで、bytes の取得・install・Build はしない」 | `Runtime/ContentResolution/Core/IContentProvider.cs` | Local / LAN を `IContentProvider` として足す形は、この契約と合わない（§2.1） |
| cache root の既定は `LocalApplicationData` の下の `OSM/ContentCache`、disk budget の既定は 10 GiB（どちらも Delivery の窓の設定）。target は `StandaloneWindows64-Player` の固定。受け入れる Unity の版は literal | `VerifyPlayLauncher.ResolveCacheRoot`、`ContentDeliveryWindow.Settings`、`ContentManifestValidation` | 取得の操作は検証 Play と同じ設定を読む |
| R2 への Content の保存と取得は `tools/artifacts.ps1 content collect / fetch` にある。file 単位の object で、取得には `reference` と、それとは別の期待 manifestSha256 が要る。Unity の側からは自動で起動しない。R2 の側に上書きを防ぐ仕組みは無い | [tools/Artifacts/README.md](../../tools/Artifacts/README.md)「開発Content 1件の保存と取得」 | cloud の経路は既に 1 つあり、Unity の取得口とはつながっていない。Spring 221 object の fetch は 742 秒（program §2.1） |
| S4-2（未 merge）は、別の PC の base、CI からの取得、取得した transport の install を入口が代わりに行うこと、CI の Player を取る時の改行の正規化、digest から登録までの窓と cache の lease を S4-3 へ送っている。別の `Library` の base に上書きを重ねた **Player** の答えは「同じ」（G0 (a)） | S4-2 の HANDOFF §2.3・§2.5（PR #212） | S4-2 の Phase D で確定する。Editor での同じ問いは未測定 |

- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの（誰がいつ） | 目的からの正当化 |
|---|---|---|---|
| U1 content を publish する（batch。今ある操作） | 宣言名（今の `-osmPurpose` のまま。増やさない） | 取得元の一覧への登録（publish の成功の後に batch が自動で行う。自動導出）。一覧の置き場所（公開先の親の directory。既定値） | 一覧に載せる操作を利用者に足さない（目的の「入力せずに」） |
| U2 取得する | 「Acquire content」のボタンを押すこと（実行時の操作）。対象の宣言は、Play の入口で既に選んである RunPurpose（今ある永続の選択。新しい入力ではない） | 取得元とその順（host の既定値: repo の `artifacts/bs2b/transport`）。候補（一覧の先頭から。自動導出）。期待 digest（取得元の申告。自動導出）。target・Unity の版・root schema（固定値）。cache root と budget（Delivery の窓の今ある設定。既定値あり） | 目的の「1 つの操作」。押すこと以外の入力は無い |
| U3 取得を取り消す | 「Cancel」を押すこと（実行時の操作） | 半端な install の片付け（installer の今ある動き） | 目的の「未検証の bytes に切り替えない」の裏側。待ちを利用者が止められる |
| U4 結果と理由を読む | なし（窓と Console を読むだけ） | 結果の文（どの取得元から何を得たか、取得元ごとの理由、digest の出所） | 目的の「取得元ごとの理由を示す」 |
| U5 取得元の場所を変える（任意。既定で足りない人だけ） | directory の path 1 つ（開発時の宣言。`UserSettings` に残る） | 取得元の id と信頼の既定（host の既定値） | 既定の場所以外に publish する人のため。使わない人には入力が無い |
| U6 取得元の実装を足す（利用者 (b)） | `IContentOrigin` の実装と、host の取得元の列への追加（開発時の宣言） | 検証・install・cache・選択・記録（共通の側。変えない） | 目的の「code を変えずに済む差込口」。足す人が書くのは一覧と bytes だけ |

自己点検: 目的の「所在・digest・revision を入力せずに」に対して、U2 に残る入力はボタンの押下だけで、所在は既定値（U5 は任意）、digest と revision は自動導出である。

## 2. 意思決定と受け入れ境界

### 2.1 用語

「供給元」は既に `IContentProvider` を指す語なので、本書が扱う物に同じ語を使わない。

- **供給元（既存）:** `IContentProvider`。Play の間、要求に対して候補の有無を答える。取得しない。
- **取得元（本書が足す。code の名前は `ContentOrigin`）:** 検証する前の bytes を持つ場所。手元の publish 済みの directory、LAN の共有や HTTP の server、cloud の object storage。
- **到着点（既存）:** installed cache。どの取得元から来た revision も、検証を通ればここに同じ形で入る。installed cache は取得元ではない。
- **pin と申告:** 期待 digest の出所の 2 種類。pin は bytes と別の経路で得た digest（VCS の file、CI の結果 file、人の入力）。申告は取得元の一覧に書いてあった digest。

流れは「取得元の一覧を読む → 候補を選ぶ → 到着点に有るか → 無ければ bytes を取得して検証し、到着点へ install → Play で登録 → 供給元が要求に答える」になる。取得元の順（Play の前）と、供給元の順（`ResolutionPolicy`、Play の間）は別の規則で、混ぜない（program §3.1 の 8）。program が候補に挙げた HANDOFF 名 `LOCAL_LAN_PROVIDER` は、PROVIDER が `IContentProvider` と紛れるので使わない。

### 2.2 行の分け方（owner が 2026-11-25 に承認した）

切り口は transport の種類ではなく、何を証明するかに置く。bytes の取得口は Local と LAN で既に同じで（§1 の現況の 1 行目）、独立して失敗しうる未知が別々にあるからである。

| 行 | 答える問い | gate の主な中身 |
|---|---|---|
| S4-3a（Local。本書） | 取得元の差込口（一覧と期待 digest、bytes）を決め、手元の publish 済みの directory からの取得・検証・install を、network なしで Play の base になる所まで通せるか。取得元を 1 つ足しても、検証・install・cache・選択・記録の code が変わらないか | §2.4 |
| S4-3b（LAN） | 同じ差込口に LAN の取得元を足して、Local miss → LAN hit を、network の失敗と、取得元を信用しない前提の下で通せるか | program §9 の G09・G13、LAN timeout、切断、遅い転送の間の取消と cache lease、進捗、同時の取得の合流。別の PC で build した base に手元の上書きを重ねた時の答え（Editor） |
| S4-3c（取得を始めるきっかけ） | Play を押した時の取得、先読み、checkout hook が呼ぶ公開入口（U7）を、取得元の種類に依らずに持てるか | program §4.2 の表の S4-3 の行。Play の transaction に非同期の段を足す時の再入・domain reload。cache の掃除 |
| S4-3d（base の選び方） | 「自分の base」をどの規則で選び、pin をどの経路で運ぶか（program §5.3 の未詰め (2)） | CI の publish（U9）と VCS（S5）に依る |

計測だけの項目（G19 の段ごとの所要、Built の Play の繰り返しの再検証、streaming の cold load）は、取得元が出来た後の調査記録にする。

本書が「取得の中身（何を、どう確かめて取るか）」を持ち、S4-3c が「取得を始めるきっかけ」を持つ。Play の transaction（`PlayEntryTransaction`）は 1 つの同期の呼び出しの中で判定から再 Play までを行う作りで、取得（非同期で長い）を中に入れるには状態機械に段を足す必要がある。これは先読み・hook と同じ「いつ取得するか」の変更なので、S4-3c にまとめた。

### 2.3 取得元の差し替えの境界

差し替えの口は 3 つあり、足りないのは 1 つだけである。

| 口 | 今 | 本書での扱い |
|---|---|---|
| 供給元 `IContentProvider` | ある。取得しない契約 | **変えない。** Local・LAN・cloud をここに足さない。install の後は、どの取得元から来ても同じ「検証済みの Content Directory」（由来 `ContentDirectory`、受け入れ根拠 `VerifiedDigest`）で、`ContentSourceKind` に `Lan` や `Cloud` を足すと、network と配信形式が lifetime の側に漏れる（program §6 の I3） |
| bytes の取得口 `IContentArtifactSource` | ある。public。実装 2 つ | **変えない。** 進捗、再試行、認証を足す口、HTTP の期限と redirect は S4-3b が決める |
| 一覧と期待 digest の口 | **無い** | **足す**（`IContentOrigin`。§3） |

**取得元に答えさせる物と、共通に残す物:**

- 取得元が答えるのは 2 つだけ。何があるか（候補の一覧。contentSet・target・revision・申告の digest・取得元だけが解釈する所在）と、候補の bytes を開くこと（今の `IContentArtifactSource` を返す）。
- 共通に残し、取得元に実装させない物: 期待 digest の照合、互換の判定、install（staging から immutable の公開まで）、cache・budget・lease、候補を当たる順と止まる規則、記録。13 章の「HTTP と local directory は同じ validation/install 経路を使う」を、取得元が増えても保つ。
- 取得元を信用しない。一覧も bytes も、検証を通るまで使用対象にしない（program §6 の I2）。取得元の種類を理由に検証を省かない。共通の側は取得元の型で分岐しない。
- 期待 digest の出所（pin か申告か）を、install の記録と取得の結果に残す。`ContentLoadRecord` の由来（`ContentSourceKind`）と受け入れ根拠（`ContentAcceptanceBasis`）は変えない。
- 差込口は `UnityEditor` に依存させない（S4-4 が同じ口を要求の時点で使う余地を残す）。

**期待 digest の出所の規則（owner が 2026-11-25 に A0 の推奨を採った）:**

- installer が必ず期待 digest を受け取る契約は変えない。
- 取得元ごとに、host が「申告を認める」か「pin だけ」かを決めて渡す。Framework の既定は「pin だけ」。SampleGame の手元の publish 済みの directory は「申告を認める」（書いた本人の同じ disk で、別の経路を求めても保証が増えない）。S4-3b の LAN の既定は「申告を認め、記録に残す」、cloud は「pin だけ」（最終の決定は cloud の行）。Player は今のまま（I10）。
- pin があれば申告は使わない。pin と一致する候補だけを当たり、無ければ失敗する。申告の digest や別の revision に落とさない。
- 申告を認める時の手当て: 一覧は 1 つの file で、bytes を全て置いた後に最後に書く。到着点に同じ revision が違う digest で入っている時は、その候補を拒む。
- 払う物: 申告だけの取得元は、そこに書ける人による差し替えを検出しない（記録には出るが止めない）。

**cloud（R2 など）から逆算した条件と、本書の設計での満たし方:**

| 条件 | 満たし方 |
|---|---|
| 1. directory の列挙に依らない | 一覧は取得元の root の 1 つの file（`content-origin.json`）で、bytes の取得口から読む。製品の directory の取得元も列挙を使わない |
| 2. 資格情報を OSM の Runtime に入れない | 取得元の実装は host が組み立てて渡す。差込口に資格情報の引数は無い |
| 3. 遅延・部分的な失敗・切断を契約に持つ | 一覧と bytes は非同期で、取消の token を受ける。取消の後に届いた答えは採らない。timeout の値、進捗、同時の取得の合流は S4-3b（進捗は共通の側の installer に足すので、差込口は変わらない） |
| 4. 取得元の側の不変を当てにしない | 期待 digest の照合と、到着点の「同じ revision の違う digest を拒む」だけに置く |
| 5. file 単位の取得の形を崩さない | bytes は今の相対 path の取得口のまま |
| 6. 取得元ごとの鍵の形を共通の側が解釈しない | 候補の所在は不透明な文字列で、開くのは取得元だけ |
| 7. 遅い取得元を最後に置ける | 取得元を当たる順は host が渡す列の順 |

**「差し替えられる」の確かめ方（D5 の「差込口だけ確認」の中身）:** directory でも HTTP でもない memory の取得元（test 用。file system を使わず、答える時点を signal で制御できる）が、製品の directory の取得元と同じ適合 test を通り、同じ調停で install まで進むこと。振る舞いの悪い取得元（一覧が読めない、申告と bytes が食い違う、途中で切れる、取消に応じない、同じ revision に違う digest を言う）に対して、共通の側が §2.4 の不変条件を保つこと。取得元を足した時の、共通の側の差分が 0 であること。

### 2.4 このスライスの問いと、進める最低条件

- このスライスが答える問い: 取得元の差込口（一覧と期待 digest、bytes）を決め、手元の publish 済みの directory からの取得・検証・install を、network なしで Play（経路 Built）の base になる所まで通せるか。取得元を足しても共通の側が変わらないと言えるか。

#### 不変条件（起きてはいけないこと）

| ID | 守る主張 | 主張を崩しうる既存の経路と、探索した境界 |
|---|---|---|
| N1 | 期待 digest と互換の検証を通っていない bytes が、到着点（`installed/`）に現れない | 到着点へ書く経路は `ContentInstaller.InstallCore` の rename だけ（`Runtime/BuildContent/Distribution/` の `Directory.Move` を `grep`: installer と、削除の tombstone）。新しい調停が自分で cache に書く経路を作ると崩れる。調停が先に読んだ manifest を install の根拠に使い回すと、読んだ後の差し替えを見落とす（installer は自分で読み直す）。未確認: Editor の他の folder から `installed/` へ直接書く code の有無は、`Scripts/Editor` と `SampleGame` の `grep` までで、`tools/` は見ていない |
| N2 | 期待と違う revision・digest の物を、黙って結果にしない（pin と違う物、申告と bytes が食い違う物、到着点の同じ revision と digest が違う物、整合性の失敗の後の別の revision） | installer の「既にある revision の再利用」は request の digest で再検証するので、違う digest は `IntegrityMismatch` になる。入口の `PlayEntryCandidates` は検証に落ちた新しい entry を Warning を出して飛ばす（今の動き。変えない）。調停が整合性の失敗の後に次の候補へ進むと崩れる |
| N3 | 取得の失敗・取消・例外が、半端な install、壊れた一覧、置きっぱなしの lock を残さない | installer の staging の片付け（失敗しても元の例外を保ち、残った path を例外に付ける）。一覧の書き換えの途中の失敗。取得の間の script の compile（domain reload）で task が失われた時の staging の残り（`staging-owner.json` の印はあるが、残りを掃除する呼び手は未確認） |
| N4 | 取得元の種類が、要求時解決・lifetime・起動に現れない | `Runtime/ContentResolution/`、`Runtime/AssetManagement/`、`Runtime/SceneSystem/`、`Runtime/Bootstrap/` に差分が無いこと。`ContentSourceKind` と `ContentAcceptanceBasis` に値を足さないこと |
| N5 | 取得を使わない利用者と Player の動きが変わらない（program §6 の I8・I10） | receipt の欄を足すと、installed の pair の検証（Player の起動も通る）が古い receipt・新しい receipt のどちらかを拒むようになりうる。`ContentInstallRequest` の形を変えると、Delivery の窓・検証 Play・test の呼び手が壊れる |
| N6 | Play の入口の判定と、利用者の明示設定を変えない | `PlayEntryDecision` の行、`PlayEntryTransaction`、`PlayEntryInterceptor`、ledger（`ContentDeliveryPlayBridge`）。取得の操作が Play の間・compile の間に走ると、Play が見る cache が途中で変わる |
| N7 | 共通の側が取得元の型・id で分岐しない。差込口が `UnityEditor` と資格情報を要らない | 調停に「directory の取得元なら」の分岐が入ると、次の取得元で共通の側を直すことになる |

#### 成立条件（起きなければならないこと）

| ID | 条件 |
|---|---|
| S1 | batch の publish が成功すると、公開先の親の `content-origin.json` の先頭にその revision の entry がある。publish か登録が失敗した時は、その revision の entry も公開先も無い |
| S2 | 取得の操作で、到着点に無い revision が取得元から取得・検証・install され、結果と receipt に取得元の id と digest の出所（申告）が残る |
| S3 | 選んだ候補が既に到着点にあれば、content の bytes を取得元から読まずに、再検証だけで「install 済み」と答える |
| S4 | 取得元が複数ある時、先の取得元が「一覧が無い・読めない」「候補が無い」「bytes に届かない」なら、理由を残して次の取得元へ進み、そこから取得する |
| S5 | pin を渡した時、pin と一致する候補だけが取得され、取得元の申告は使われない |
| S6 | memory の取得元が、directory の取得元と同じ適合 test を通り、同じ調停で install まで進む。その間、共通の側の code に取得元ごとの分岐が無い |
| S7 | 取得した revision が最新の install である時、実 Editor の Play（経路 Built）がその revision を base にする（入口の code は変えない） |
| S8 | 別の checkout（別の `Library`）で publish した base を取得元から取得し、経路 Built + local edits で手元の上書きを重ねた実 Editor の Play の答え（上書きの root と base の root がどの版で出たか）が記録される |

#### 候補を当たる順と止まる規則（N2 と S4 の境目。読み違いを防ぐために表にする）

取得元は host が渡した順、取得元の中の候補は一覧の順に当たる。pin が無い時は「申告を認める」取得元だけを当たる。

| 起きたこと | 次にすること | 例 |
|---|---|---|
| 一覧が無い、版が未対応、形が壊れている、取得元が答えない | その取得元を理由つきで飛ばし、次の取得元へ | `content-origin.json` が無い directory |
| 候補が contentSet・target に合わない、pin と違う | その候補を飛ばす | 他の宣言の entry |
| 候補の manifest か content の file に届かない（`TransportFailure`） | 理由を残して次の候補へ（別の revision でもよい） | 一覧に残っているが directory を消した revision |
| manifest は申告どおりだが、互換でない（Unity の版、root schema、protocol）か、host の述語が拒んだ | 理由を残して次の候補へ（program §3.1 の 10: 互換の規則が上位を採らず下位へ進むことは、暗黙の fallback ではない） | 前の Unity の版で作った publish |
| 整合性の失敗（manifest の bytes が期待 digest と違う、file の size・hash が manifest と違う、manifest が壊れている、到着点の同じ revision と digest が違う） | **その (revision, digest) と同じ組の候補だけ**を残りから当たる。無ければ失敗で終わる。別の revision へは進まない | 合格例: 取得元 1 の R1 が壊れていて、取得元 2 に同じ digest の R1 があり、そこから取得する。不合格例: 取得元 1 の R1 が壊れていたので、同じ取得元の古い R0 を取得する |
| 到着点の側の失敗（lock が取れない、budget が足りない、I/O） | 失敗で終わる（取得元を替えても直らない） | 別の Editor が install 中 |
| 取消 | 取消で終わる。以後の答えは採らない | |

### 2.5 スライス分割の判断（A1 の終わり）

- 契機 1（独立した変更理由の混在）: 差込口と調停（Framework の Runtime）、一覧への登録（Framework の Editor と batch）、取得の操作（SampleGame の Editor）の 3 つがある。**分けない。** 一覧の書き手が無いと、読む形だけを相手なしで凍結することになる（§2.2 で避けた「Local の都合が契約に入る」と同じ失敗）。操作が無いと製品の呼び手が無く、設定と結果の形を S4-3c で初めて決めることになる。3 つとも小さい（§3）。
- 契機 2（別の問いの判定に使う受け入れ条件）: S8（別の `Library` の base と上書き）は、本書の問いではなく「取得した base に上書きを重ねてよいか」の測定である。program の行が S4-3 の最初の gate に置いているので本書が持つが、**答えの中身は GO の条件にしない**（記録されることだけを条件にする。§2.7 の判定定義）。別の spike の行に分ける案もある（§2.9 の D-3）。
- 契機 3（高リスクの初回実機で独立して失敗しうる未知）: 実機の未知は S7 と S8 の 2 つで、どちらも今ある経路（batch、経路 Built、経路 Built + local edits）の上にある。分けない。

### 2.6 ここでは答えない問いと所有する後続

program §8 の「S4-0 の A1 が振り分けた入力」が S4-3 へ送った項目と、S4-2・S5-0 が送った項目の振り分け。項目の中身は元の箇所を正とする。

| 入力 | 行き先 |
|---|---|
| 供給元が持つ目的 build の一覧と期待 digest の得方 | **S4-3a（本書）** |
| 別の `Library` の base と手元の上書き（Editor）、上書きに依存として入った同じ id の物の読み先 | **S4-3a**（S8 の測定。答えが「違う」なら owner へ返す） |
| 別の PC の base と手元の上書き（Editor）、CI の Player を取る時の改行の正規化（S4-2 から）、受け入れ版 `SupportedUnityVersion` の literal、transport の protocol の版を採否に含めるか | S4-3b（別の機械が作った物を初めて受け取る行） |
| G09、G13、LAN timeout、進捗、遅い転送の間の取消、同時の取得の合流、digest から登録までの窓と cache lease（S4-2 から）、`ContentInstaller`・`ContentCacheStore` の移動の再試行（S5-0 から）、既存の `HttpContentArtifactSource` を取得元にすること、transport.json を root に持つ 1 revision の directory（Artifacts の `content fetch` の取得物）を取得元として読むこと | S4-3b |
| R2 から取った `transport.json` の索引が同じ bytes であることの実測、目的をまたぐ catalog が要るか | cloud の取得元の行（D5 の後続）。catalog の要否は S4-3b |
| Play の入口の先読み、checkout hook が呼ぶ公開入口と最小の hook（U7）、取得した transport の install を入口が代わりに行うこと（S4-2 から）、known-good の pin の呼び手と cache の掃除（`Evict` の呼び手） | S4-3c |
| 「自分の base」を選ぶ規則、同じ目的の複数 revision から 1 つを選ぶ規則、`ContentPurposeIndex.Select` で選んだ build を install と登録の検証に通すこと、上書きの base の選択に索引を使う接続、base より古い checkout、記録の無い base で root の宣言を足した場合、記録に無い root の依存だけが無い場合、`NotInBase` の root、manifest の `index` からの見分け、複数の Player から選ぶ UI（S4-2 から）、expected identity の無い要求での同名異内容の検出 | S4-3d（本書は「一覧の先頭から」と「pin を渡せる口」までを持つ） |
| 要求をまたぐ pin と revision の一貫、`IContentProvider.Id` / `SourceKind` の getter の契約、片方の directory の file が disk から消えた時の読み先 | S4-4（供給元を足す・登録の寿命を扱う行） |
| 閉包の記録が manifest の上限を超える時の置き場と実データでの大きさ、配布と保存を sha256 でまとめる実装 | 実データの集計の後に owner が決める（2026-11-12 の裁定のまま） |
| G19、Built の Play の繰り返しの再検証、streaming の cold load、D7 の「より安い解決の比較」 | 取得元が出来た後の調査記録 |
| 取得の途中の domain reload で残った staging の掃除 | 所有なし（program §10 の U11 に足す）。本書は、残りうることを 13 章に書く |
| Android・iOS（S4-5）、観測 UI と VCS の adapter（S5）、Player での取得と cloud の tier（U10） | 既存の行のまま |

### 2.7 受け入れ条件・判定定義・停止規則

受け入れ条件（括弧は検証する条件）。test の名前は §5。

- **AC1（N1）:** 調停の全ての終わり方（成功、install 済み、見つからない、整合性の失敗、到着点の失敗、取消）で、`installed/` に在る revision は、installer の検証を通った物だけである。失敗経路: content の file が manifest と違う取得元、manifest が申告と違う取得元、途中で切れる取得元、取消。どの場合も `installed/<contentSet>/<revision>` が無い。
- **AC2（N2、S5）:** §2.4 の止まる規則の表の各行が、表のとおりに進むか終わる。失敗経路: 表の「整合性の失敗」の不合格例（別の revision を取得しないこと）、pin と申告が違う取得元しか無い時に失敗で終わること、到着点に同じ revision が違う digest で在る時にその候補を拒むこと。
- **AC3（N3）:** 取消と失敗の後に、cache の `staging/` に今回の取得の directory が残らない（片付け自体が失敗した時は、残った path が結果に出る）。一覧の書き換えが途中で失敗した時、`content-origin.json` は書き換えの前の bytes のままで、別名の file が残らない。失敗経路: 書き換えの移動を失敗させる注入、lock を持ったままの 2 つ目の登録。
- **AC4（N4・N7）:** Phase B の commit が変える file に、`Runtime/ContentResolution/`・`Runtime/AssetManagement/`・`Runtime/SceneSystem/`・`Runtime/Bootstrap/` の file が無い。調停と installer の code に、取得元の具象の型・id の値での分岐が無い（`grep` と構造レビュー）。差込口の型が `UnityEditor` を参照しない（`OneStarMaker.Runtime` の asmdef に置くので compile が示す）。失敗経路: 取得元の型で分岐した調停は、memory の取得元の適合 test（AC8）で同じ結果にならない。
- **AC5（N5）:** 欄の無い古い receipt と、欄を足した新しい receipt のどちらの revision も、installed の検証（`InstalledRevisionVerifier.Verify`、`ContentCacheStore.ValidateInstalled`、`Inspect`）を通る。古い receipt の出所は「不明」と読む。`ContentInstallRequest` の今の constructor の呼び手（Delivery の窓、検証 Play、test）は変えずに compile と test が通る。失敗経路: receipt の出所の欄に未知の値が入った revision を、検証が拒まず「不明」と読む。
- **AC6（N6）:** Phase B の commit が `PlayEntryTransaction.cs`・`PlayEntryInterceptor.cs`・`PlayEntryCandidates.cs`・`ContentDeliveryPlayBridge.cs`・`ContentDeliveryWindow.cs`・`VerifyPlayLauncher.cs` を変えない。`PlayEntryDecision` の変更は、経路 Built で候補が無い時の回復操作に取得を 1 つ足すことだけで、既存の判定の表の test が変更なしで通る。取得の操作は、Play の間・Play への移行の間・compile の間は始まらず、理由を返す。失敗経路: Play の間に操作を呼ぶと、cache に触れずに拒む。
- **AC7（S1）:** batch の成功の後、`content-origin.json` の先頭の entry が、その publish の contentSet・target・revision と、公開した `transport.json` の bytes の SHA-256 を持つ。同じ revision を同じ digest でもう 1 回登録しても entry は増えない。違う digest での登録は失敗する。登録の時に、directory が無くなった entry は一覧から外れる。publish の後の登録が失敗した batch は、終了コードが publish の失敗で、今回の公開先を消す（今の「成功結果の確定より前に失敗すれば、この実行が消す」のまま）。
- **AC8（S2・S3・S4・S6）:** 適合 test（取得元の契約: 一覧の entry の形、bytes の取得口が申告どおりの manifest を返すこと、取消の token に応じること）を、directory の取得元と memory の取得元の両方が通る。調停の test で、到着点に無い時は取得して receipt に取得元の id と出所が残り、在る時は content の bytes を 1 つも開かず（取得口の呼び出しを数える）、先の取得元が使えない時は次から取得する。
- **AC9（S7。実 Editor）:** 手順は §5 の E1。batch で publish した revision を取得の操作で取得した後の Play（経路 Built）の record の revision と digest が、batch の結果 file の値と等しい。
- **AC10（S8。実 Editor。測定）:** 手順は §5 の E2。答え（上書きの root が手元の版で出たか、上書きに入らない root が base の版で出たか、Error の有無）が、対象の head と一緒に記録される。答えの中身は合否にしない。
- **AC11（回帰）:** 全 EditMode 回帰が、XML の failed 0 と root result `Passed`（`-nographics` の VFX 25 件の Inconclusive は既知。program §8 の「全 EditMode 回帰の受け入れ条件の書き方」）。

- 判定定義: **GO** は AC1〜AC11 が全て満たされたこと。AC10 は「記録された」ことで満たす。**NO-GO** はそれ以外。CONDITIONAL ACCEPT は使わない（spike ではない）。AC10 の答えが「違う」「Error が出た」の時は、GO のまま owner へ返し、S4-3b の前に owner が裁定する。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。決定記録: なし。

### 2.8 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の参照を足さない（本書の配置は、今ある参照の中で閉じる。§3）。`UnityEditor` 依存を Runtime の asmdef に置かない。
- 公開 API のログは `ILogger<T>`。`ZLogger*` の型を公開面に出さない。本書の Runtime の新しい型は log を出さず、結果の値で理由を返す。
- Unity 側の C# で `record` を使わない。新規・編集する `.cs` は先頭に `#nullable enable`。
- test で `Task.Delay` / `Thread.Sleep` を使わない。遅い取得元・取消・lock の競合の test は signal（`TaskCompletionSource` など）で順を作る。再試行の待ちは注入する（`ContentStagingMove` と同じ形）。
- path は `tools/path-budget.json` の予算（repo 相対 150 字）に従う。
- 参照 0 を削除理由にしない（`MarkKnownGood`・`Evict` は触らない）。
- `await` で Editor の main thread を同期に待たない（installer は `ConfigureAwait(false)` を付けていない）。取得の操作は Delivery の窓の `Prepare` と同じく、非同期のまま結果を受ける。
- program §4.1: 整合性の失敗の後は、同じ期待 digest の正規の複製で直せる時だけ次へ進む。resolver も取得も、暗黙に Build を始めない。
- program §4.3: 通知は必要な警告だけで、Play のたびの popup は出さない（D6）。取得の結果は窓の文と Console の 1 行で示し、dialog を出さない。
- 13 章: install は caller が明示した local NTFS の cache の staging で検証し、同じ volume の rename で公開する。同じ revision の違う manifest は上書きしない。known-good は caller が明示して昇格する（本書は昇格しない）。
- 一覧の file の移動は、開発の PC の監視 tool が一時的に妨げうる（program §8 の S5-0 の行）。`ContentStagingMove` の再試行の条件と上限をそのまま使い、新しい再試行の規則を作らない。
- Runtime を触る Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない（program §0 の停止規則）。

### 2.9 owner の裁定を待つ項目と、決定済みの項目

**裁定待ち（凍結の前に要る）:**

- **D-1（目的・利用者の文言）:** §1 の改訂後の文言でよいか。変える時は、変えた 2 欄で目的レビューをやり直す（やり直しは 1 回まで）。
- **D-2（S4-2 より前の凍結）:** program の行は「S4-2 が決まった後に A1 を始める」としている。A1〜A3 の統合案は owner の指示で先行した。凍結は S4-2 の Phase D の後に、現況を取り直してから行う案である。
- **D-3（S8 の置き場）:** program の行は、別の `Library` の base と上書きの答えを「A1 の最初の gate」に置いている。本書は、凍結の前の harness ではなく、Phase C の実 Editor の測定（AC10）に置いた。理由は 3 つ。答えは差込口の設計を変えない。S4-2 の G0 (a) が Player で「同じ」だった。本書の製品の経路（別の checkout の publish を取得元から取得する）がそのまま測定の手順になる。答えが「違う」時は GO のまま owner へ返す。凍結の前に測る案、別の spike の行にする案に変えるなら、ここで決める。
- **D-4（risk）:** `high` に上げた。C' を B・C と別の系列の担当にする予約が要る（§5）。
- **D-5（A2 の指摘のうち owner の判断が要る物）:** §5.1 の「owner の裁定待ち」の行。

**決定済み:**

- Q-1（行の分け方）: owner が 2026-11-25 に §2.2 の 4 行を承認した。
- Q-3（期待 digest の出所）: owner が 2026-11-25 に A0 の推奨を採った（§2.3）。
- Q-2（名前）: 「取得元」（code は `ContentOrigin`）とした。owner から異論が無ければこのまま凍結する。

**後続の行の A0 へ送る問い（本書の凍結には要らない）:**

- Q-4（S4-3b）: 別の PC で build した base を測る環境があるか。無ければ、LAN は同じ PC の中の模擬で判定し、実の別の PC は未確認として残す。
- Q-5（S4-3b）: 想定する LAN の取得元は、file 共有（UNC）、HTTP の server、その両方のどれか。
- Q-6（S4-3b と cloud の行）: cloud を後で足す時、Unity から直接読む形と、Artifacts の `content fetch` の取得物を手元の directory として読む形のどちらを見込むか。
- Q-7（S4-3c）: 取得が自動になった時の cache の掃除の所有。本書の取得は利用者が始める操作なので、cache は利用者が押した分だけ増える。

## 3. 責務マップ

新しい asmdef と参照は足さない。Runtime の新しい型は `OneStarMaker.Runtime` の `Runtime/BuildContent/Distribution/`（namespace `OneStarMaker.Runtime.BuildContent.Distribution`）に置く。理由は、installer・cache・bytes の取得口と同じ変更主体（Content Distribution。program §4.4）で、installer の internal の検証（`ContentManifestValidation`）を使うからである。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の境界では Distribution は BuildSystem の側で、AssetManagement と要求時解決からの参照は足さない。

| file（新規は ＋） | 責務（1 文） | 種別 | 状態・所有・寿命 | 依存 | 公開面 | 単体テスト | 行数（今 → 予想） |
|---|---|---|---|---|---|---|---|
| ＋ `Runtime/BuildContent/Distribution/ContentOrigin.cs` | 取得元の差込口と、一覧の値の型を宣言する | 契約 | 状態なし | 同 folder の `IContentArtifactSource` だけ | `IContentOrigin`（`Id`、`ListAsync(query, token)`、`OpenArtifacts(entry)`）、`ContentOriginQuery`、`ContentOriginEntry`（contentSet・target・revision・申告の digest・不透明な所在）、`ContentOriginListing`（`Listed` / `Unavailable` と理由）、`ContentDigestAuthority`（`Unknown` / `Declared` / `Pinned`）、`ContentOriginTrust`（`PinnedOnly` / `AcceptDeclared`） | 値の型の constructor の検査 | 0 → 約 130 |
| ＋ `…/Distribution/ContentOriginIndex.cs` | 一覧の file（`content-origin.json`）の形と、bytes からの読みと検査を持つ | 永続化の形（読み） | 状態なし | `ContentManifestValidation`（相対 path と hash の形の検査）、`JsonUtility` | 一覧の DTO（public。書き手が Editor の asmdef に居る）、`Read(bytes)` と結果（`Indexed` / `NoIndex` / `Unsupported` / `Invalid` と理由） | Unity の `AssetDatabase` なしで全て（形の表） | 0 → 約 150 |
| ＋ `…/Distribution/IndexedContentOrigin.cs` | 一覧の file を bytes の取得口から読んで答える取得元（directory 用の組み立てを含む） | infrastructure | 状態なし（呼ばれるたびに読む）。寿命は組み立てた host に従う | `ContentOriginIndex`、`IContentArtifactSource`、`LocalContentArtifactSource` | `IndexedContentOrigin`（id と「相対の所在から bytes の取得口を作る関数」を受ける）、`ContentOrigins.Directory(id, root)` | 適合 test（temp の directory） | 0 → 約 110 |
| ＋ `…/Distribution/ContentAcquisition.cs` | 取得元の列と pin から、§2.4 の規則で候補を当たり、installer で install して結果を返す | orchestration と policy（止まる規則） | 1 回の呼び出しの間だけ。cache の所有は `ContentCacheStore` のまま | `IContentOrigin`、`ContentInstaller`、`ContentCacheStore`、`ContentManifestValidation` | `ContentAcquisition.AcquireAsync(request, origins, store, token)` | memory の取得元と temp の cache で全ての行（Unity の Editor API なし） | 0 → 約 260 |
| ＋ `…/Distribution/ContentAcquisitionTypes.cs` | 取得の要求・結果・試行の記録の値の型 | 契約 | 状態なし | 同 folder | `ContentAcquisitionRequest`（contentSet・target・Unity の版・root schema・budget・任意の pin・任意の host の述語）、`ContentAcquisitionOrigin`（取得元と信頼）、`ContentAcquisitionResult`（状態、install の結果、取得元の id、digest の出所、試行ごとの理由）、`ContentPin` | constructor の検査 | 0 → 約 170 |
| `…/Distribution/ContentTransportManifest.cs` | （既存）transport の DTO と install の要求・結果 | — | — | — | `ContentInstallRequest` に、取得元の id と digest の出所を持つ任意の引数を足す（今の constructor は残す）。`ContentInstallResult` に同じ 2 つの getter | 既存 + 欄の往復 | 217 → 約 235 |
| `…/Distribution/ContentDeliveryFiles.cs` | （既存）receipt と marker の形、file の補助 | — | — | — | internal。receipt に任意の欄 `originId`・`digestAuthority`（version は 1 のまま） | AC5 の新旧の receipt | 155 → 約 160 |
| `…/Distribution/ContentInstaller.cs` | （既存）1 回の取得・検証・公開 | — | — | — | 変えない。receipt に request の 2 つの値を書くだけ（移動の再試行は足さない。S4-3b） | 既存 + 欄 | 185 → 約 190 |
| `…/Distribution/ContentCacheStore.cs` | （既存）cache の transaction | — | — | — | `ContentCacheEntry` に receipt の 2 つの欄の getter | 既存 + 欄 | 438 → 約 446 |
| ＋ `Editor/Build/Content/ContentOriginIndexWriter.cs` | 公開済みの 1 revision の directory を、親の directory の一覧に登録する（読み直し・追加・無くなった entry の除去・置き換え） | 永続化（書き） | 一覧の file の所有は、その root に publish する host。書き換えの間だけ lock の file を持つ | `ContentOriginIndex`（Runtime）、`ContentStagingMove`（再試行）、`System.IO` | `ContentOriginIndexWriter.Register(originRoot, publishedDirectory)`（public。host が呼ぶ） | temp の directory。移動と待ちは注入 | 0 → 約 140 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBatch.cs` | （既存）batch の入口 | — | — | — | publish の段の後、成功の確定の前に登録の段を足す（`Stages` に注入の口を 1 つ） | 既存の batch の test に段の成功・失敗 | 202 → 約 220 |
| ＋ `SampleGame/…/Build/ContentOriginSettings.cs` | この host の取得元の列（既定の directory、信頼、場所の上書きの保存）を組み立てる | host の policy | `UserSettings`（git 管理外）に path 1 つ | `ContentOrigins`、`EditorUserSettings` | internal | path の解決の表 | 0 → 約 60 |
| ＋ `SampleGame/…/Build/ContentAcquireLauncher.cs` | 取得の操作（宣言から要求を組み立て、1 つずつ走らせ、取消と結果の文を持つ） | Editor の orchestration | 走っている操作 1 つを static に持つ（Editor の process の間。domain reload で失われる） | `ContentAcquisition`、`ContentOriginSettings`、`VerifyPlayLauncher`（cache root、target、宣言の照合）、`ContentDeliveryWindow.LoadSettings` | internal（test と `eval` が呼ぶ static の入口を含む） | port を差し替えた単体（Play の間の拒否、二重の開始、取消、結果の文） | 0 → 約 170 |
| `SampleGame/…/Build/PlayEntryWindow.cs` | （既存）入口の窓 | 表示 | — | — | 経路が Built の系統の時のボタン 1 つ、Cancel、直近の結果の文、回復操作のボタン 1 つ | 表示は test しない（launcher の側を test） | 210 → 約 250 |
| `SampleGame/…/Build/PlayEntryDecision.cs` | （既存）入口の規則 | policy | — | — | `PlayEntryRecovery` に `AcquireFromOrigins` を足し、経路 Built で候補が無い拒否に付ける | 既存の表 + 回復の flag | 360 → 約 368 |

行数警報（500 行、3 責務、50% 増）は発火しない。調停の値の型を別 file にしたのは、型は取得の操作と test が読み、規則は調停だけが変えるからである（変更理由が違う）。一覧の読み（Runtime）と書き（Editor）を分けたのは、読みは Player を含む全ての取得元が使いうる形の規則で、書きは publish する host の file system の操作だからである（依存と寿命が違う）。

**一覧の file の形（`content-origin.json`。取得元の root に 1 つ）:** `version`（1）と `entries`（各 entry は `contentSet`・`target`・`revision`・`manifestSha256`・`path`）。`path` は root からの相対の directory で、その下に `transport.json` と `content/` がある。並びは取得元が勧める順（先頭が先）で、書き手は新しい publish を先頭に置く。時刻の欄は持たない（順が新しさを運ぶ。取得元の時計を信用の材料にしない）。上限は 4 MiB（manifest と同じ）。読みの規則は `ContentPurposeIndex` に揃える: file が無い・版 0 は「一覧なし」、版 2 以上は「未対応」、形の失敗（型、欄の欠け、相対でない path、hash の形、同じ contentSet・target・revision の重複）は「壊れている」。どれも、その取得元を理由つきで飛ばす。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序: (1) 一覧の形と読み、差込口の型。(2) directory の取得元と適合 test（memory の取得元を test の側に作る）。(3) receipt と install の要求の任意の欄。(4) 調停と止まる規則の test。(5) 一覧への登録と batch の段。(6) host の取得元の列、取得の操作、窓と回復操作。(7) 実機を伴う統合の test。
- Phase B から Phase A へ差し戻す条件: [アーキテクチャゲート](../../.agents/skills/osm-workflow/references/architecture-gates.md)の停止条件に加えて、次のどれか。asmdef の参照を足さないと置けない。`IContentArtifactSource`・`IContentProvider`・`ContentSourceKind`・`ContentAcceptanceBasis` を変えないと満たせない。`PlayEntryTransaction`・`PlayEntryInterceptor`・`PlayEntryCandidates` を変えないと AC9 が満たせない。receipt の version を上げないと AC5 が満たせない。installer の検証の順か公開の点を変える必要がある。調停に取得元の型の分岐が要る。
- 対象外を維持する方法: AC4 と AC6 の「変えない file」を Phase B の commit の差分で確かめる。範囲の条件は「Phase B の commit が変える file」で書いた（program §8 の「差分は責務マップの file だけ」の書き方）。

## 5. テストとレビュー計画

- 単体テスト（どれも EditMode。Unity の Editor API を使わない物は `OneStarMaker.Tests`、使う物は `OneStarMaker.Tests.Editor` と `SampleGame.Tests.Editor`）:
  - `ContentOriginIndexTests`（読みの規則の表）。
  - `ContentOriginConformance`（抽象の fixture。適合 test）と、その 2 つの具象 `DirectoryContentOriginTests`・`MemoryContentOriginTests`（AC8）。memory の取得元は test の側に置く。
  - `ContentAcquisitionTests`（§2.4 の止まる規則の表の全ての行、pin、信頼、install 済み、複数の取得元、取消、片付け。AC1・AC2・AC3・AC8）。振る舞いの悪い取得元は memory の取得元の設定で作る。
  - `ContentInstallReceiptOriginTests`（AC5）。
  - `ContentOriginIndexWriterTests`（AC3・AC7。移動と待ちは注入）。
  - `SampleGameContentBatchTests` の追加（AC7 の batch の段）、`ContentAcquireLauncherTests`（AC6）、`PlayEntryDecision` の既存の表への回復の flag の 1 件。
- 統合・Unity テスト: `PublishedOriginAcquireIntegrationTests`（合成の小さい宣言を製品の coordinator で build・publish し、登録 → 取得 → `PlayEntryCandidates.Select` がその revision を返すまで。build を伴うので、process の自然終了を合否にしない）。
- 差し戻し中の起点 `-Filter`: `ContentOrigin|ContentAcquisition|ContentInstallReceipt|SampleGameContentBatch|ContentAcquireLauncher|PlayEntryDecision`（C が根拠付きで変えてよい）。
- 判定必須テスト（GO 候補 head）: 上の単体と統合の全てと、全 EditMode 回帰（AC11）。
- 全 EditMode 回帰の適用除外: なし。
- 操作・実行時・目視条件の検証経路（どちらも、担当は判定 C の実行担当。環境は Windows の開発の PC、Unity 6000.6.9f1、GO 候補 head の clean な checkout。Editor の操作は今ある `eval` の経路で行う。18 章「宣言からの Play 経路」の観測の注意に従う）:
  - **E1（AC9）:** 初期状態は、cache root を空の temp の directory にした Delivery の設定、`artifacts/bs2b/transport` に一覧が無い状態、Variant profile の選択なし。(1) batch（`SampleGameContentBatch.BuildAndPublish -osmPurpose spring-full`）を実行し、終了コード 0 と `last-result.json` を得る。(2) `artifacts/bs2b/transport/content-origin.json` を保存する。(3) Editor を開き、RunPurpose を `spring-full`、経路を Built にする。(4) 取得の操作の static の入口を `eval` で呼び、終わりを結果の値で待つ（時間で待たない）。結果の文を保存する。(5) Play を押し、`Title` が出た後に Play の record（`PlayEntryRecord`）を保存して Play を止める。観測と合否: 結果が「取得した」で、取得元の id が既定の directory、出所が申告。record の revision と manifest の SHA-256 が `last-result.json` の `identity`・`manifestSha256` と等しい。Console に Error が無い。保存する証拠: `last-result.json`、`content-origin.json`、結果の文、record、Console の log。
  - **E2（AC10。測定）:** 2 つ目の checkout（同じ head、別の `Library`）で E1 の (1) を行う。1 つ目の checkout で、取得元の場所（U5）を 2 つ目の `artifacts/bs2b/transport` にし、cache を空にして取得する。経路を Built + local edits にし、`Title` の root の閉包の asset を 1 つ編集して保存し、Play を押す。観測: record の上書きの欄、上書きの root（`Title`）が手元の版で出たか（編集した値を Play の中で読む）、上書きに入らない root（`AddScene` する `HomeScene`）が base の record で載ったか、Console の Error。保存する証拠: 2 つの checkout の `last-result.json`、record、Console の log、編集の差分。合否は付けず、答えを対象の head と一緒に記録する。
- 未知の操作経路の疎通結果: 未確認。初回確認は Phase C。理由は、本書の作成環境に Unity が無いからである。E1・E2 の操作は今ある経路（batch、`eval`、Play の record）の組み合わせで、新しいのは取得の操作の static の入口だけ（Phase B が作る）。不成立の時の対応: `eval` から入口を呼べない時は、実行担当が窓のボタンを押して同じ観測を取る（操作の入口が変わるだけで、観測は同じ）。E2 で 2 つ目の checkout を用意できない時は、AC10 を未実施として owner へ返す。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（Player の build、実の R2、人の観察は無い）。
- 判定 C の照合 script の方式: なし（合否は test の assertion と、E1 の値の一致を目で照合する 2 組の文字列）。
- 機械検査: AC4・AC6 の「変えない file」と分岐の `grep`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: Claude Code の cloud session（Fable 5.2、Anthropic）。
- C' 用に予約した担当: Anthropic 以外の系列（Phase A は Anthropic の model だけを使ったので、他の vendor は全て未関与で残っている）。割り当ては owner が行う。
- 独立性の強化条件を満たせない場合の理由: Phase A の A2 は 3 件とも主担当と同じ vendor である（下の §5.1）。別の vendor の観点は、Draft PR の外部計画レビューで補う。

### 5.1 A2 の独立レビューと A3 の採否

A2 の記録は、A2 を終えた後にこの節へ書く。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
