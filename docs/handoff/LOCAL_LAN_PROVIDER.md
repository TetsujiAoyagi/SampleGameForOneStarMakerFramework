# 検証済み content の取得元（Local と LAN）— Phase A0 planning packet

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3。A0 は S4-3 の行の全体を入力にし、行を 4 つに分けた（§2.2）。本書は最初の行 S4-3a（Local）の HANDOFF である）
- status: `A`（A0 の初版。A1 は未着手。owner は 2026-11-25 に §2.2 の 4 行の分け方を承認した（§2.7 の Q-1）。残る未決事項を owner が確認した後に A1 を始める）
- branch: `claude/trusting-ramanujan-kxphtr`（PR の base は `develop`）
- implementation base commit: `1fd200b5ba835c833035d153579216e14cb823af`（`develop` の head、2026-11-24、S4-1a の merge の後。§1 の現況はこの base。S4-2 は未 merge なので、A1 の着手時に S4-2 の Phase D の後の `develop` で取り直す）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: program の行は `normal`。A0 の見立ては `high`（取得元の差込口を公開 API に足し、cache の lease と並行に触れる。高リスクの例の「公開 API」「並行処理」に当たる）。A1 で決める
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to（A1 で確定する）: [13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「DIST transport と disk cache の境界」と「まだ保証しないこと」、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」、repo の README の「試す」、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-3 の行
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: owner が §2.3 の案と §2.7 の未決事項（Q-2〜Q-7。Q-3 は A0 の推奨つき）を確認する。その後に Phase A の A1（主担当）。
  - 次の作業: 確認の結果を §2 に反映し、A1 の初稿（進める最低条件を不変条件と成立条件に分ける、受け入れ条件、UC ごとの利用者の入力、責務マップ、テスト方針、検証経路、分割の判断）を書く。program §8 の S4-3 の行は A0 では変えていない（S4-2 の branch が隣の行を変えているので、S4-2 の merge の後、A1 で行を本書への link と分割の案に直す）。
  - 引き継ぐ commit: この票を含む commit（SHA は tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: S4-2 の Phase D より前に本書を凍結しない（program §8 の S4-3 の行の「実機の経路（S4-2）が決まった後に A1 を始める」。A0 は owner の指示で先行した）。§2.7 の Q-3 の答えが無いまま A1 で差込口の形を決めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base の code と文書を読み、`grep` と `wc -l` で確かめた。Unity は起動していない。
- S4-2 の HANDOFF は branch `claude/player-content-overlay` の head `86a04d1`（PR #212、Phase B と発見 C の往復中）の版を読んだ。S4-2 の結論は確定していない。
- 作成環境に `pwsh` が無く、`tools/docs-audit.ps1` と `tools/workflow-task.ps1 start` は未実行。link 先の存在は手で照合した。

## 1. 目的と対象外

- 目的（A0 の案。A1 で改めてよい）: 検証済みの content（目的 build）を手元に持たない開発者が、取得元の種類・所在・digest を Play のたびに入力しなくても、設定した順の取得元（手元の publish 済みの directory、LAN。後から足す cloud など）から、期待する identity と digest に一致する revision を取得・検証・install して、Editor の Play の base に使えるようにする。取得元を足す・差し替える時に、検証・install・cache・選択・記録の code を変えずに済むようにする。取得できない時と一致しない時は、別の revision や未検証の bytes に黙って切り替えず、理由を示す。
- 利用者（A0 の案）: (a) 編集する開発者（base を取得し、自分の編集の上書きを重ねて Play する。担当外の source は手元に無くてよい）。(b) 遊ぶだけの人（source を持たず、検証済み content だけで Play する）。(c) 取得元を足す人（host の開発者。LAN の共有や、後で cloud を取得元として足す）。
- 対象外（所有は §2.5）:
  - cloud（R2 など）の取得元の実装。program §7 の D5 のとおり、差込口が塞がないことの確認までにする。
  - 要求の時点の install と登録（S4-4）。本書の取得は Play の前（入口・先読み・hook）に行う。13 章の「Runtime は network から起動しない」は変えない。
  - Player の中での取得（program §10 の U10）、Android・iOS（S4-5）。
  - CI が検証済み content を publish する仕組み（program §10 の U9。program の外）、VCS の adapter（S5）。
  - 配布の単位を変えること（revision 全体の取得のまま。program §5.3 の Stage II は対象外）。署名による検証。
  - 観測 UI と menu の整理（S5）。Delivery の窓を作り直すこと。
- 現況（base `1fd200b5`）:

| 事実 | 場所 | 本書に効く点 |
|---|---|---|
| bytes の取得口 `IContentArtifactSource`（相対 path から stream を開く）は public で、実装は `LocalContentArtifactSource` と `HttpContentArtifactSource` の 2 つ | `Runtime/BuildContent/Distribution/ContentArtifactSource.cs`（162 行） | bytes の差し替えは既にできる。Local と LAN（UNC の path）は同じ実装を通る。HTTP は 1 request に 60 秒の固定の期限、redirect を追わない、認証の header を足す口が無い、再試行と再開が無い |
| `ContentInstaller.InstallAsync(request, source, token)` が、`transport.json` の bytes の SHA-256 を request の期待 digest と照合し、staging で全 file を検証してから immutable な `installed/<contentSet>/<revision>` を公開する。既にある revision は同じ digest の完全な再検証だけで再利用する | 同 `ContentInstaller.cs`（185 行） | 検証と install は取得口の種類に依らず 1 つの経路（13 章「DIST transport と disk cache の境界」）。期待 digest は呼び手が渡す |
| `ContentCacheStore` は `Inspect`・`ValidateInstalled`・`MarkKnownGood`・`Evict` を持つ。製品の code に `MarkKnownGood` と `Evict` の呼び手は無い（test を除く `grep`） | 同 `ContentCacheStore.cs`（438 行） | cache は増える一方。18 章「BuildSystem の後続」の「known-good の pin の呼び手」は未着手のまま |
| install の記録（receipt）は digest・contentSet・revision・target・時刻だけで、どこから取得したかの欄が無い | 同 `ContentDeliveryFiles.cs` | 取得の出所を後から説明できない（program §1 の Q5） |
| 取得元が持つ revision の一覧と、各 revision の期待 digest を得る口が無い。Delivery の窓は digest・contentSet・revision・cache root・budget を手で入れる。標準の経路（From published directory）は batch の結果 file `last-result.json` を file dialog で選び、「published directory とは別の経路で受け取る」ことを前提に digest を取る | `SampleGame/DependOnAll/Editor/Build/ContentDeliveryWindow.cs`（212 行）、`VerifyPlayLauncher.ReceiveAsync` | 差し替えの本体はここ（§2.3）。期待 digest の出所が今の信頼の根拠になっている |
| Play ボタンの入口の経路 Built は installed cache だけを見る。宣言と target が合う entry を install の新しい順に検証し、最初に通った物を使う。cache に無ければ候補 0 で、取得はしない | `SampleGame/DependOnAll/Editor/Build/PlayEntryCandidates.cs`（106 行）、`PlayEntryInterceptor.cs:241` | 「同じ目的の複数 revision から選ぶ規則」は今は「新しい install が先」だけ。program §8 の「S0-c から移送した入力」の Warning の文の食い違いも残る |
| `ContentPurposeIndex.Select` は manifest の bytes と期待 digest の組の列から、要求を含む最初の目的 build を選ぶ。install できること・互換・`content/` の実在は保証しない。呼び手は test だけ | `Runtime/BuildContent/Distribution/ContentPurposeIndex.cs`（282 行） | 選んだ build は install と登録の検証を省かずに通す（13 章「目的 build の索引と選択」） |
| 要求時解決の供給元（`IContentProvider`）は、登録済みの session の層と Editor snapshot だけ。契約は「候補の有無を答えるだけで、bytes の取得・install・Build はしない」 | `Runtime/ContentResolution/Core/IContentProvider.cs`、13 章「まだ保証しないこと」 | Local / LAN を `IContentProvider` として足す形は、この契約と合わない（§2.1） |
| cache root の既定は `LocalApplicationData` の下の `OSM/ContentCache`。target は `StandaloneWindows64-Player` の固定。受け入れる Unity の版は literal | `VerifyPlayLauncher.ResolveCacheRoot`、`ContentManifestValidation` | program §8 の「Unity 6000.6.9f1 への更新から移送した入力」の literal の扱いは S4-3 の入力 |
| R2 への Content の保存と取得は `tools/artifacts.ps1 content collect / fetch` にある。file 単位の object で、取得は `reference` と、それとは別の期待 manifestSha256 を要る。別 PC から R2 を直接読む経路は無く、取得物は手で運ぶ。Unity の側からは自動で起動しない。R2 の側に上書きを防ぐ仕組みは無い | [tools/Artifacts/README.md](../../tools/Artifacts/README.md)「開発Content 1件の保存と取得」「別PCで取得したPlayerとContentを使う手順」 | cloud の経路は既に 1 つあり、Unity の取得口とはつながっていない。Spring 221 object の fetch は 742 秒（program §2.1） |
| CI は検証済み content を publish していない | program §10 の U9 | 供給のある現場の体験は program の外に依る |
| S4-2（未 merge）は、Player の base を Player の build identity から決め、別の PC の base、CI からの取得、取得した transport の install を入口が代わりに行うこと、CI の Player を取る時の改行の正規化、digest から登録までの窓と cache の lease を S4-3 へ送っている | S4-2 の HANDOFF §2.3（PR #212） | S4-2 の Phase D で確定する。A1 の着手時に読み直す |

- UC ごとの利用者の入力: A1 で書く。A0 が挙げる UC の候補は、(1) 取得元を設定する（1 回）、(2) Play を押す（取得が要れば取得して入る）、(3) 取得を明示して始める・取り消す、(4) 取得できなかった理由を読む、の 4 つ。

## 2. 意思決定と受け入れ境界

### 2.1 用語（A1 の用語の検査に使う）

「供給元」は既に `IContentProvider` を指す語なので、本書が扱う物に同じ語を使わない。

- **供給元（既存）:** `IContentProvider`。Play の間、要求に対して候補の有無を答える。取得しない。
- **取得元（仮称。本書が扱う）:** 検証する前の bytes を持つ場所。手元の publish 済みの directory、LAN の共有や HTTP の server、cloud の object storage。
- **到着点（既存）:** installed cache。どの取得元から来た revision も、検証を通ればここに同じ形で入る。installed cache は取得元ではない。

流れは「到着点に有るか → 無ければ取得元を順に当たる → 検証して到着点へ install → Play で登録 → 供給元が要求に答える」になる。取得元の順（Play の前）と、供給元の順（`ResolutionPolicy`、Play の間）は別の規則で、混ぜない（program §3.1 の 8「供給元の tier と、その Scene が今どこに cache されているか」）。program が候補に挙げた HANDOFF 名 `LOCAL_LAN_PROVIDER` の PROVIDER は `IContentProvider` と紛れるので、A1 で名前を決め直す。

### 2.2 検討 1: Local と LAN を別のスライスにする

**A0 の結論: 分ける。ただし切り口は transport の種類ではなく、何を証明するかに置く。**

- 理由 1: bytes の取得口は、Local と LAN で既に同じである（上の現況の 1 行目）。transport の違いは分ける理由にならない。
- 理由 2: 独立して失敗しうる未知が別々にある（[分割の契機](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー)）。Local の未知は、差込口・選択・pin の契約で、network の無い test と 1 台の PC で閉じる。LAN の未知は、不達・timeout・切断、遅い転送の間の取消と lease、期待 digest をどの経路で得るか、別の PC で作った base の 4 つで、検証に 2 台目の PC か模擬が要る（検証経路の成立が別の問題になる）。
- 理由 3: Local だけでも利用者に届く物がある。手元で publish した content（検証 Play・batch の出力）を、Delivery の窓の手入力なしに base にできる。CI の無い現場（U9）の経路になる。

**行（owner の提起は a と b。c と d は、S4-3 の行の「prefetch と base の選び方をさらに分けるかは A1 が決める」に対する A0 の案。owner は 2026-11-25 に 4 行とも承認した）:**

| 行 | 答える問い | gate の主な中身 |
|---|---|---|
| S4-3a（Local。本書） | 取得元の差込口を決め、到着点の hit、手元の publish 済みの directory からの取得・検証・install、複数の候補からの選択を、network なしで縦に通せるか。取得元を 1 つ足しても、検証・install・cache・選択・記録の code が変わらないか | Local hit / miss、tamper、取消、immutable revision、publish の成功だけで Play 可能と言わないこと。別の `Library` で build した base に手元の上書きを重ねた時の答え（Editor）。敵対的な fake の取得元が同じ適合 test を通ること（§2.3） |
| S4-3b（LAN） | 同じ差込口に LAN の取得元を足して、Local miss → LAN hit を、network の失敗と、取得元を信用しない前提の下で通せるか | G09、LAN timeout、切断、遅い転送の間の取消と cache lease、G13。期待 digest の出所（§2.7 の Q-3）。別の PC で build した base に手元の上書きを重ねた時の答え（Editor） |
| S4-3c（先読みと hook） | Play の入口の先読みと、checkout hook が呼ぶ公開入口（U7）を、取得元の種類に依らずに持てるか | program §4.2 の表の S4-3 の行。Editor が開いている時と閉じている時の hook の呼び方 |
| S4-3d（base の選び方） | 「自分の base」をどの規則で選ぶか（program §5.3 の未詰め (2)） | CI の publish（U9）と VCS（S5）に依る。S4-3a は「順は host が渡す」選択の口までを持つ |

計測だけの項目（G19 の段ごとの所要、Built の Play の繰り返しの再検証、streaming の cold load）は、取得元が出来た後の調査記録にする。

**分ける時に見ておく点:**

- **Local の都合が契約に入る。** Local の実装だけを見て差込口を凍結すると、「列挙が無料」「遅延が無い」「認証が無い」「同じ機械なので信用できる」が契約に入り、S4-3b で差込口を作り直すことになる。防ぐ手は 2 つ。§2.3 の「cloud から逆算した条件」を S4-3a の受け入れ条件の素材にする。A2 に、A0 だけを読んで代替の構成を出す担当を置く（高リスクの扱い）。
- **「別の PC の base」の gate の置き場。** program の行は、A1 の最初の gate を「別の PC（無ければ別の `Library`）で build した base に手元の上書きを重ねた時の答え（Editor）」にしている。Local の行は定義上、同じ PC の base しか扱わないので、S4-3a は別の `Library` で測り、実の別の PC は S4-3b の最初の gate に置く案にした。S4-3b で否定的な答えが出ると、S4-3a が凍結した選択の規則に戻る。2 台目の PC が使えるなら、手で運んだ base で先に測る方が安い（§2.7 の Q-4）。
- **G09 は S4-3b の gate。** S4-3a が示すのは「Local で見つからなければ次の取得元へ進む」までで、次の取得元は fake である。

### 2.3 検討 2: 取得元を差し替えられるようにする

**A0 の結論: 差し替えの口は 3 つあり、足りないのは 1 つだけである。その 1 つを足し、他の 2 つの契約は変えない。**

| 口 | 今 | 本書での扱い |
|---|---|---|
| 供給元 `IContentProvider`（Play の間、候補の有無を答える） | ある。取得しない契約 | **変えない。** Local・LAN・cloud をここに足さない。install の後は、どの取得元から来ても同じ「検証済みの Content Directory」（由来 `ContentDirectory`、受け入れ根拠 `VerifiedDigest`）で、`ContentSourceKind` に `Lan` や `Cloud` を足すと、network と配信形式が lifetime の側に漏れる（program §6 の I3） |
| bytes の取得口 `IContentArtifactSource` | ある。public。実装 2 つ | **形は保つ。** 足りない点（進捗と全体量、再試行、認証を足す口、HTTP の固定の期限と redirect）を足すかは、S4-3a が Local で要る物だけ、残りは S4-3b が決める |
| 一覧と期待 digest の口（この取得元に、ある contentSet と target の revision が何個あり、それぞれの期待 digest は何か） | **無い。** 人が手で入れるか、結果 file を選ぶ | **足す。** 差し替えの本体。形は A1 が決める |

**取得元に答えさせる物と、共通に残す物（A1 が形を決める時の境界）:**

- 取得元が答えるのは 2 つだけにする。何があるか（候補の一覧。contentSet・target・revision・期待 digest の申告）と、bytes を開くこと（今の `IContentArtifactSource`）。
- 共通に残し、取得元に実装させない物: 期待 digest の pin と検証、install（staging から immutable の公開まで）、cache・budget・lease、互換の判定、複数の候補から選ぶ順、記録。13 章の「HTTP と local directory は同じ validation/install 経路を使う」を、取得元が増えても保つ。
- 取得元を信用しない。取得元が返した一覧と bytes は、検証を通るまで使用対象にしない（program §6 の I2）。取得元の種類を理由に検証を省かない。
- 期待 digest の出所を区別して持つ。取得元自身が申告した digest（bytes と同じ経路）は破損の検出にしかならない。取得元と別の経路で得た digest（結果 file、VCS で管理する file、人の入力）は差し替えの検出にもなる。今の Delivery の窓は後者だけを受け付ける。前者を認めるかは owner の裁定が要る（§2.7 の Q-3）。
- どの取得元から得たかを install の時に残す（今の receipt に欄が無い）。`ContentLoadRecord` の由来（`ContentSourceKind`）は変えず、取得の出所は別の欄にする。
- 差込口は `UnityEditor` に依存させない。取得元の連鎖を回すのは Play の前の Editor の側だが、S4-4 が同じ口を要求の時点で使う余地を残す。置き場所（`Runtime/BuildContent/Distribution/` か、連携部分か）は A1 が [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の境界に照らして決める。

**cloud（R2 など）から逆算した、差込口が満たす条件（S4-3a の受け入れ条件の素材。cloud の実装はしない）:**

1. directory の列挙に依らない。一覧は 1 つの file / object として読める形にする（HTTP と object storage は安い列挙を保証しない）。
2. 資格情報を OSM の Runtime に入れない。認証は、host が組み立てて渡す取得元の実装の中に閉じる（Artifacts の資格情報は pwsh の process の中にあり、Unity から自動で起動しない契約である）。
3. 遅延・部分的な失敗・切断を普通のこととして契約に持つ: 取消、timeout、進捗、同じ revision の取得の合流。Local の実装はこれらを自明に満たす。
4. 取得元の側の不変を当てにしない（R2 は上書きを防げない）。immutable の保証は、期待 digest の pin と到着点の側にだけ置く。
5. file 単位の取得の形を崩さない（Artifacts の Content は file 単位の object）。後で sha256 でまとめる取得（program §8 の「S2-0 から移送した入力」）を塞がない。
6. 取得元ごとの鍵の形（Artifacts の `reference` は opaque）を、共通の側が解釈しない。
7. cloud は遅い最後の tier として扱える順の規則にする（742 秒の実測）。

**「差し替えられる」の確かめ方（D5 の「差込口だけ確認」の中身）:** 製品の Local の実装と、test 用の fake の取得元（遅い、途中で切れる、列挙できない、digest を偽る、同じ revision に違う bytes を返す）が、同じ適合 test を通ること。取得元を 1 つ足した時に、検証・install・cache・選択・記録の側の差分が 0 であること（構造の確認）。既存の `HttpContentArtifactSource` を取得元として配線するのは S4-3b。

### 2.4 このスライス（S4-3a の案）の問いと、進む最低条件の素材

- このスライスが答える問い（案）: 取得元の差込口（一覧と期待 digest、bytes）を決めた上で、到着点の hit と、手元の publish 済みの directory からの取得・検証・install・選択を、network なしで Play の入口まで通せるか。取得元を足しても共通の側が変わらないと言えるか。
- 次スライス（S4-3b）へ進む最低条件の素材（A1 が不変条件と成立条件に分ける）:
  - 起きてはいけないこと: 検証を通っていない bytes が到着点に公開される。期待と違う revision・digest の物が黙って使われる。取得元の失敗・取消が半端な install を残す。取得元の種類が `ContentSourceKind`・`SceneSystem`・`IAssetManagement` に現れる。resolver を使わない利用者の compile と動作が変わる（program §6 の I8）。明示設定のある Play に入口が介入する（program §4.3）。
  - 起きなければならないこと: 到着点に有れば取得しない。無ければ設定した順に取得元を当たり、見つけた物を検証して install し、Play の base に使う。どこから得たかが記録に残る。見つからない時と一致しない時に、取得元ごとの理由が読める。fake の取得元が適合 test を通る。別の `Library` の base に手元の上書きを重ねた Editor の Play の答えが記録される。
- 誤った合格の例（A1 が反例として書く）: 「検証 Play が通った」だけで取得元の連鎖の成立としない（検証 Play は自分で publish した directory を直接 install する）。「fake が 1 つ通った」だけで差し替え可能としない（fake が Local と同じ性質しか持たなければ証明にならない）。「publish が成功した」だけで Play 可能としない（program §3.1 の 3）。

### 2.5 ここでは答えない問いと所有する後続

program §8 の「S4-0 の A1 が振り分けた入力」が S4-3 へ送った項目と、S4-2・S5-0 が送った項目の振り分けの案。項目の中身は元の箇所を正とする。

| 入力 | 行き先（案） |
|---|---|
| 供給元が持つ目的 build の一覧と期待 digest の得方、`ContentPurposeIndex.Select` で選んだ build を install と登録の検証に通すこと、同じ目的の複数 revision から 1 つを選ぶ口（順は host が渡す）、上書きの base の選択に索引を使う接続 | S4-3a |
| 要求をまたぐ pin と revision の一貫、`IContentProvider.Id` / `SourceKind` の getter の契約、expected identity の無い要求での同名異内容の検出 | S4-3a（取得した revision を Play の間の基準に固定する所まで。A1 が範囲を決める） |
| 受け入れ版 `SupportedUnityVersion` の literal、transport の protocol の版を採否に含めるか | S4-3a（install と検証を扱う最初の行） |
| 別の `Library` の base と手元の上書き（Editor）、上書きに依存として入った同じ id の物の読み先 | S4-3a の最初の gate |
| 別の PC の base と手元の上書き（Editor）、CI の Player を取る時の改行の正規化（S4-2 から） | S4-3b |
| G09、G13、LAN timeout、遅い転送の間の取消、digest から登録までの窓と cache lease（S4-2 から）、`ContentInstaller`・`ContentCacheStore` の移動の再試行（S5-0 から） | S4-3b（再試行は、S4-3a が installer を触るなら S4-3a） |
| R2 から取った `transport.json` の索引が同じ bytes であることの実測、目的をまたぐ catalog が要るか | cloud の取得元の行（D5 の後続。S4-3 の中では測らない）。catalog の要否は S4-3b |
| Play の入口の先読み、checkout hook が呼ぶ公開入口と最小の hook（U7）、取得した transport の install を入口が代わりに行うこと（S4-2 から） | S4-3c（入口が取得して install する最小の経路は S4-3a が持つ） |
| 「自分の base」を選ぶ規則、base より古い checkout、記録の無い base で root の宣言を足した場合、記録に無い root の依存だけが無い場合、`NotInBase` の root、manifest の `index` からの見分け、複数の Player から選ぶ UI（S4-2 から） | S4-3d |
| 閉包の記録が manifest の上限を超える時の置き場と実データでの大きさ、配布と保存を sha256 でまとめる実装 | 実データの集計の後に owner が決める（2026-11-12 の裁定のまま） |
| G19、Built の Play の繰り返しの再検証、streaming の cold load、D7 の「より安い解決の比較」 | 取得元が出来た後の調査記録 |
| 片方の directory の file が disk から消えた時の読み先 | S4-4（登録の寿命の実測と一緒に）。A1 が要否を決める |
| known-good の pin の呼び手、cache の掃除（`Evict` の呼び手） | 所有なし。取得が自動になると cache が増える速さが変わるので、S4-3a の A1 が持つか送るかを決める |
| 要求の時点の install（S4-4）、Android・iOS（S4-5）、観測 UI と VCS の adapter（S5）、Player での取得と cloud の tier（U10） | 既存の行のまま |

### 2.6 本文へ転記した制約

- 依存は Game → Framework の一方向。asmdef の参照の追加は設計判断で、A3 の凍結なしに行わない。`UnityEditor` 依存を Runtime の asmdef に置かない。
- 公開 API のログは `ILogger<T>`。`ZLogger*` の型を公開面に出さない。
- Unity 側の C# で `record` を使わない。新規・編集する `.cs` は先頭に `#nullable enable`。
- test で `Task.Delay` / `Thread.Sleep` を使わない。timeout・遅い取得元・取消の test は、signal で待つか時間を注入する（適合 test の fake もこの形で書く）。
- path は `tools/path-budget.json` の予算（repo 相対 150 字）に従う。
- 参照 0 を削除理由にしない（`MarkKnownGood`・`Evict` は呼び手が無いが、先行宣言である）。
- program の不変条件のうち本書に効く物: I2（検証を通るまで使用対象にしない）、I3（認証・network・配信形式を lifetime / SceneSystem / BuildSelection に漏らさない）、I5（順序と利用可能・互換を区別する）、I6（Play の準備で全 Scene の列挙・download を必須にしない）、I8（AssetManagement 単独と BuildSystem 単独が resolver 無しで動く）、I10（Player の protected config と fail-closed を弱めない）。
- program §4.1: not found・未接続・timeout は次の取得元へ進む。整合性の失敗（hash 不正など）の後は、同じ期待 digest の正規の複製で直せる時だけ次へ進み、別の revision へ黙って切り替えない。resolver は暗黙に Build を始めない。
- program §4.3: どこかの層に `content:runtimeMode` があれば入口は設定に介入しない。通知は必要な警告だけで、Play のたびの popup は出さない（D6）。
- 13 章: install は caller が明示した local NTFS の cache の staging で検証し、同じ volume の rename で公開する。同じ revision の違う manifest は上書きしない。known-good は register / load の成功の後に caller が明示して昇格する。

### 2.7 未決事項（owner の確認を待つ。A1 の前に要る物に ★）

- **Q-1（行の分け方）: 決定済み。** owner（root）が 2026-11-25 に、§2.2 の 4 行（a Local、b LAN、c 先読みと hook、d base の選び方）を承認した。program §8 の行への反映は A1 で行う（§0 の票）。
- **Q-2（名前）:** 本書が扱う物を「取得元」と呼び、`IContentProvider` の「供給元」と分けてよいか。HANDOFF 名は A1 で決め直す。
- ★ **Q-3（期待 digest の出所）:** 取得元と同じ経路で得た digest（共有の上の一覧に書いた digest）を認めるか。認めれば設定は取得元の場所 1 つで済むが、保証は破損の検出までになる。認めなければ、digest を運ぶ別の経路（VCS で管理する file、CI の結果 file）が要り、「自分の base」の選び方（S4-3d）とつながる。Local（自分の PC で publish した物）と LAN で答えを分けてよい。
  - **A0 の推奨（owner の裁定ではない）:** どちらか一方に決めず、digest の出所を 2 種類（pin: bytes と別の経路で得た物。申告: 取得元の一覧に書いてあった物）として持ち、install の記録と Play の記録に残す。installer が必ず期待 digest を受け取る契約は変えない。
  - 既定の受け入れ: Local は申告を認める（書いた本人の同じ disk で、別の経路を求めても保証が増えない。検証 Play は今も自分の publish の結果から digest を取る）。LAN は申告を認め、記録に「破損の検出まで」と残す（pin を必須にすると、CI と VCS の連携が出来るまで LAN を使えない）。cloud は pin だけ（最終の決定は cloud の行）。Player は今のまま pin だけ（I10）。host は policy で「pin だけ」に締められる。
  - pin があれば申告は使わない。pin と一致しなければ fail-closed にし、申告の digest や別の revision に落とさない。
  - 申告を認める時の手当て: 一覧は 1 つの file で、bytes を全て置いた後に最後に書く（半端な publish が一覧に出ない）。一度 install した revision に、同じ名前で違う digest が来たら拒む（今の cache の契約「同じ revision の違う manifest は上書きしない」で成り立つ）。
  - 理由: 開発の LAN で起きる事故は、半端な publish・上書き・取り違えが主で、申告と上の手当てで防げる。pin が効くのは「自分の checkout に合う base はどれか」の方で、commit ごとに contentSet・revision・digest を書いた file があれば、base の選択と信頼の格上げが同時に片付く（S4-3d）。差込口を「呼び手が pin を渡すのが主で、一覧は pin が無い時の探し方」の形にしておけば、S4-3d は差込口を変えずに pin の出所を足せる。弱い根拠を隠さず記録に残す扱いは、I2 の Editor snapshot と同じである。
  - 払う物: LAN の既定は、共有に書ける人による差し替えを検出しない（記録には出るが止めない）。出所の区別を記録に足す分、S4-3a の範囲が増える。`ContentAcceptanceBasis` に値を足すかは A1 が決める。
- **Q-4（2 台目の PC）:** 別の PC で build した base を測る環境があるか。あれば、手で運んだ base で S4-3a の前に測る。無ければ、S4-3b の「LAN」は同じ PC の中の模擬（loopback の HTTP、同じ PC の共有）で判定し、実の別の PC は未確認として残す。
- **Q-5（LAN の形）:** 想定する LAN の取得元は、file 共有（UNC）、HTTP の server、その両方のどれか。今の Delivery の窓は両方を持つ。
- **Q-6（cloud の読み方の見込み）:** cloud の取得元を後で足す時、Unity から直接読む（公開か期限つきの URL の HTTP）形と、Artifacts の `content fetch` の取得物を手元の directory として読む形のどちらを見込むか。前者なら §2.3 の条件 2（資格情報）と HTTP の取得口の拡張が S4-3b に効き、後者なら cloud は「手元の directory の取得元」に還元されて S4-3a で足りる。
- **Q-7（cache の増え方）:** 取得が自動になると cache が黙って増える。`Evict` と known-good の呼び手を S4-3a が持つか、別の行に送るか。

- スライス分割の判断: §2.2 の 4 行（owner が 2026-11-25 に承認した）。本書は S4-3a の HANDOFF になる。A1 の終わりに、S4-3a をさらに分ける契機が無いかだけを検討する。
- 判定定義・停止規則・受け入れ条件: A1 で書く。
- A3 後の例外承認: なし。決定記録: なし。

## 3. 責務マップ

A1 で書く。

## 4. 実装計画

A1 で書く。

## 5. テストとレビュー計画

A1 で書く。A0 が決めているのは次の 2 点だけである。

- A0 主担当・モデル・ベンダー: Claude Code の cloud session（Anthropic）。A1 以降の担当は未定。
- C' 用に予約した担当: 未定。risk を `high` にするなら、Phase A で全系列を使い切らない。

### 5.1 A2 の独立レビューと A3 の採否

未着手

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
