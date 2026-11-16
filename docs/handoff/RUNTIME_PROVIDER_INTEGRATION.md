# 登録済み Content Directory を要求時解決の供給元にする（S3-1）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S3-1。S3 を S3-1・S3-2・S3-3 に分け、要求時の install を S4 へ送った判断は §2.5）
- status: `A`（A3 凍結済み。次は Phase B）
- branch: `claude/runtime-provider-integration`（PR の base は `develop`）
- implementation base commit: `36d069e8`（`origin/develop` の head、2026-11-17。§1 の現況と §3 の行数はこの base）
- implementation head commit: `未固定`
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠）: 承認者 owner（root）、2026-11-17、Phase A revision 1（本記録を含む commit。SHA は起動指示と PR 状態ブロックで渡す）。根拠: A2 の 4 件と外部計画レビュー 3 件（2 巡の cursor の PR レビューと、owner のアカウントの計画レビュー）の採否を §5.1 に記録し、2 巡目以降に blocker と設計の変更を求める指摘が無いことを確かめたうえで、owner が会話で「確認したら凍結して」と指示した。§2.8 の U3〜U8 は同日に owner が決めた
- risk: `high`（program の行の値。public な型と member を足し、record の意味、SceneDirector の Scene の読み方、起動の配線を変える）
- owner: root（依頼者）
- created: 2026-11-17
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「要求時の Content 解決」（供給元、record と実ロード元の一致、表現の fallback、まだ保証しないこと）、[04-app-startup.md](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.6（directory の Play での配線）、program §8・§10、`unity/Assets/README.md`（`ContentResolution` の説明。directory の Play で既定で配線される層になる）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§3、§4.1、§4.4、§6、§8 の S3 と各スライスからの移送、§9）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本。§2.1 の分類で、供給元と配線は連携部分に置く）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（同じ起動 code を触る。2026-11-17 時点で A0 だけで、Phase B を同時に走らせない）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase B（実装）。
  - 次の作業: §4 の順で §3 の file を実装し、§5 の起点 filter と offline の test、AC5 の実機の test を通し、出口で §5 の検証経路の動作確認（AC7）を 1 回行って、§6 に結果を書く。
  - 読む範囲: 既定（Phase B は §0〜§5）。§1 の現況と §3 の行数は base `36d069e8` の事実。
  - 引き継ぐ commit: A3 凍結版の commit（SHA は起動指示と PR 状態ブロックで渡す）。implementation base は上の `36d069e8`、implementation head は `未固定`。
  - 入力の snapshot・bundle: なし（機械向けの期待値・照合 script は無い。§5）。
  - 必要な権限: Unity（EditMode の test、Content Directory の build を伴う test、`tools/unity-editor.cmd eval` での Play の操作。`tools/Harness/README.md` と osm-unity-editor の規則に従う）。
  - 停止規則: §4 の「Phase B から Phase A へ差し戻す条件」のどれかに当たったら実装を止めて Phase A へ戻す。§2.6 の NO-GO に当たったら、何がどの段で得られなかったかを記録して owner へ返す。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 検証済みの installed revision から登録した 1 つの Content Directory を、要求時解決の最初の実在の供給元にする。呼び出し側が Scene か asset を要求すると、供給元の探索・互換の判定・既存の content 入口でのロードがつながり、得た物の所有と解放は既存の契約のまま、「何をどの directory から読んだか」の記録が実際に読んだ directory と一致する。SceneDirector を通す経路と、IAssetManagement を直接使う経路の両方で成り立つ。
- 利用者: (a) SceneDirector で Scene を出し入れする host の開発者。新しい入力は無く、directory の Play で記録が付く。(b) SceneDirector を使わず、resolver と AssetManagement だけで Scene / asset を読む利用者。(c) 後続（上書きの登録、snapshot の候補、Local / LAN）で供給元を足す実装者。
- 目的・利用者の文言の経緯: 上の 2 欄は program §8 の S3 の行と、各スライスが S3 へ送った入力から本書で初めて書いた。A2 の前に owner（root）が 2026-11-17 に承認した（§2.8 U1）。同じ時に S3 の分割（§2.5）も承認した。
- 対象外:
  - base と上書きの 2 つの directory を登録すること（13 章「base と上書きの登録契約」の Runtime の強制）。S3-2。
  - Editor snapshot を resolver の候補にすること、既ロードの Scene を loader の経路にすること、開いているが要求していない Scene を lifecycle に入れないこと（program §9 の G21・G23）。S3-3。
  - 要求の時点で install して登録すること、起動の順序の変更、Local・LAN の供給元。S4。
  - 起動時の読み込み（SceneResourceMap、UICommon の Scene）と、`SceneBase` が読む付属 asset を resolver に通すこと。これらは今の直接の経路のままで、record は付かない。
  - bootstrap を使わない利用者が directory を登録するための public な入口（`ContentDirectorySession` と `InstallContentDirectory` は internal のまま）。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP`。
  - Player での要求時解決（program §10 U10）。Player は今の起動経路のままにする。
  - 採用の後で load が失敗した時に、probe と候補を呼び手へ残す形（S0-a からの移送）。S5（理由 code の扱いと一緒に）。
  - 観測の UI と log の書式。S5。
- 現況（base `36d069e8`。code の読みと `grep` で確かめた。Unity は動かしていない。この節と §3 の path は `unity/Assets/OneStarMaker/Scripts/` からの相対で、test だけ `unity/Assets/` からの相対）:
  - **実在の供給元は無い。** `IContentProvider`（`Runtime/ContentResolution/Core/IContentProvider.cs`）の実装は test の fake だけで、製品の code は `ContentRequestLoader` を使っていない。
  - **record は実ロード元を確かめていない。** `ContentRequestLoader`（98 行）は resolver が採用した候補の表現で `IAssetManagement.LoadContentSceneAsync` / `LoadContentAssetAsync<T>` を呼び、`ContentLoadRecord.SourceBindingVerified` に常に false を入れる。`IAssetManagement` は登録済みの directory の identity を返さない。
  - **directory は 1 つで、起動時に決まる。** `AbstractApplicationInitializer.InstallConfiguredContentDirectoryAsync`（653〜677 行）が `ContentDirectorySession` を 1 つ登録し、`AssetManagement.InstallContentDirectory`（internal、2 件目は拒否）へ渡す。
  - **4 欄の identity を持つのは、検証済みの installed revision から登録した session だけ。** `RegisterVerified` は `VerifiedInstalledRevision`（contentSet・revision・target・manifest の SHA-256）を受ける。path と build identity で呼ぶ `Register` は、path が installed cache の中（`receipt.json` がある）なら receipt を読んで検証済みの登録になり、そうでなければ（Editor の `directoryPath` の指定、Player に同梱した directory）contentSet と digest を持たない登録になる。本書の「検証済みでない登録」は、`VerifiedInstalledRevision` を持たない session を指す。検証は manifest の Unity の版と root schema の版を読むが、`VerifiedInstalledRevision` に載せずに捨てており、session も保持していない。
  - **directory は表現を黙って `Full` に読み替える。** `ContentDirectoryIndex` は要求した表現の entry が無ければ `Full` の entry を返す（Scene と Object の両方）。空の表現の entry は `Full` と読む。
  - **resolver は表現の違う候補を採らない。** `ContentResolver.CheckRequestInvariants`（175〜181 行）は、要求が表現を指定していれば、候補の表現が ordinal で一致しない時に `RepresentationMismatch` にする。供給元が実効の表現（`Full`）を申告すると採られず、要求した表現を申告すると record が実際と食い違う。
  - **SceneDirector は content 入口を直接呼ぶ。** `PerformUnitySceneLoad`（`SceneDirector.Loading.cs` 628〜658 行）は、同じ名前の Scene が既に `SceneManager` にあれば読まずに再利用し、無ければ `_useContentDirectory` の時に `LoadContentSceneAsync(scene, _sceneVariant, options, CancellationToken.None)` を呼ぶ。`_sceneVariant` は全 Scene に同じ値で、その表現を持たない Scene は directory の `Full` への読み替えで載っている。
  - **AssetManagement の Scene の台帳は identity ごとに 1 つ。** 既に載っている Scene を同じ表現の名前で要求すると同じ handle を返し、違う表現の名前で要求すると `EntryAmbiguous` で拒む（`AssetManagement.ContentDirectory.cs` 116〜123 行。比べるのは要求に書いた表現の文字列）。
  - **互換の基準を実在の manifest から作る code は無い。** `ContentCompatibilityRule` は `ContentCompatibilityEnvelope` と候補の事実（`osm.compat.engineVersion`・`osm.compat.rootSchemaVersion`）を比べるが、値を作るのは test だけである。install と登録の検証は `ContentManifestValidation.SupportedUnityVersion` / `SupportedRootSchemaVersion`（internal の定数）と一致しない manifest を拒む。
  - **単独の利用の境界。** `AssetManagement` と BuildSystem 本体は、共通契約 `Runtime/AssetBuildContracts/`（今は `AssetType` と `ContentDirectoryException` の 2 file）だけを共有する。asmdef は `OneStarMaker.Runtime` 1 つで、境界は compile では守られていない。
- UC ごとの利用者の入力:

  **UC-1 directory の Play で Scene を出し入れする**（利用者 (a)。Play ボタンの入口で経路 Built を選ぶ既存の操作）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | （新しい入力なし） | — | 目的の「SceneDirector を通す経路」は、既に登録している directory を供給元にするだけで成り立つ。利用者に供給元や基準を書かせない |

  機械が決めるもの: 供給元（登録した directory）、互換の基準（登録した revision と Distribution の定数から）、表現の fallback（起動時に決まる表現が `Full` でなければ `Full` を fallback にする。今の directory の挙動と同じ結果になる）、record。

  **UC-2 host の code が loader で Scene / asset を直接要求する**（利用者 (b)）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | 要求（logical key・kind・表現） | 呼び手が実行時に渡す（既存の `ContentRequest`） | 目的の「Scene か asset を要求する」そのもの |
  | fallback の表現（任意。新規） | 呼び手が実行時に渡す。既定は「fallback しない」 | 要求した表現を黙って変えないため（program §6 I1）。fallback を望む呼び手だけが書く |
  | asset の owner（`AssetOwner`） | 呼び手が実行時に渡す（既存） | 目的の「所有と解放は既存の契約のまま」 |
  | loader の入手 | host の initializer が開発時に、Framework が作った loader を accessor で受け取る | 供給元と基準を host に組み立てさせない |

  機械が決めるもの: 供給元、互換の基準、候補の identity と実効の表現、record。

  利用者 (c) は開発者としての操作を持たない（code を足す人）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **検証済みの installed revision から登録した 1 つの Content Directory について、実在の供給元 → resolver → 既存の content 入口 → 所有 / unload の接続が、SceneDirector 経由と `IAssetManagement` 直接の両方で成り立ち、record が実際に読んだ directory と表現を指すと言えるか。**
- 誤って YES を出す道（各 AC はこれを塞ぐ形にする）:
  - (a) 供給元が申告した identity を record に写すだけで「一致を確かめた」とする。→ 登録した directory と候補が食い違う組を、実物の `AssetManagement` で拒むことを見る（AC1）。
  - (b) fake の供給元と fake の directory だけで確かめる。→ 実際に install した manifest から identity と事実を作り（AC2・AC4）、実機の directory から読む（AC5）。
  - (c) SceneDirector の test が `PerformUnitySceneLoad` を差し替えていて、loader を通っていない。→ 差し替えない SceneDirector で見る（AC6）。実 Editor の Play でも見る（AC7）。
  - (d) Scene だけ確かめて Object を確かめない。→ AC5 に Object を入れる。
  - (e) 全ての Scene が要求した表現を持つ fixture で、fallback が試されない。→ 表現が `Full` だけの Scene を混ぜる（AC2・AC5）。
  - (f) 成功だけ見て、解放と close を見ない。→ 解放の後に directory を close できることを見る（AC5）。
  - (g) 実 Play で配線が黙って無効になり、直接の経路で Play が成功する。→ record が実際に付いたことを見る（AC7）。
  - (h) 候補の互換の事実を、基準と同じ定数から作る。互換の判定が常に通る。→ 事実は install した manifest の値から作り、値を変えた候補が落ちることを見る（AC2・AC4）。

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 record は実際に読んだ directory と食い違わない。** loader が成功として返す record の候補の identity（contentSet・revision・target・manifest の digest）は、その handle の中身を読んだ directory の identity と 4 欄とも一致する。一致を確かめられない時は、native の load を始めない。SceneDirector が返す record は、今 current にあるその Scene を loader が読んだ時の record だけである。
  - 主張を崩しうる経路: (1) 供給元の申告だけを信じる実装。(2) 検証済みでない登録（digest が無い）。(3) 実物の `AssetManagement` でない `IAssetManagement`（fake、wrapper、別実装）。(4) 解決と load の間の directory の入れ替え（`CloseContentDirectoryAsync` の後の再登録。起動の失敗の後始末と Player の smoke が close を呼ぶ）。(5) 別の session から作った供給元を policy に入れる誤った配線。(6) 既に載っている同じ identity の Scene の再利用（`LoadContentSceneAsync` は台帳の handle を返す）。(7) resident cache から取り出す asset（cache の key は directory の path・identity・target を含む）。(8) SceneDirector に残る古い record（loader が成功した後に `ExecuteLoaded` が投げる、取消になる、などで Scene が後始末され、次に同じ名前の Scene が `SceneManager` から再利用される）。
  - 塞ぎ方: 照合は供給元（連携部分）が答える。供給元は自分の session を知っていて、「渡された `IAssetManagement` が実物の `AssetManagement` で、今登録している directory がこの session で、session が受付中で、候補の identity が session の検証済みの値と 4 欄とも一致する」時だけ真を返す（`AssetManagement` には internal の問い合わせを 1 つ足す。public の面は増やさない）。loader は採用した候補の供給元にこれを尋ね、真の時だけ content 入口を呼び、照合と呼び出しの間に `await` を置かない（content 入口は、最初の `await` より前に、呼ばれた時点の directory を掴み、その後は掴んだ directory だけを使う。だから入れ替えは照合で落ちるか、掴んだ directory の受付停止で失敗する。この掴む位置は I-3 で固定する）。照合を答えられない供給元の候補は読まない。(6) は、台帳にある directory 由来の Scene は今の session が読んだものだけである（close が全て unload する）ことに依る。(8) は、record を SceneDirector の Scene ごとの組（`ScenePair`）に持たせ、loader で読んだ分岐でだけ入れることで塞ぐ（組が消えれば record も消える）。
  - 探索した境界: `ContentRequestLoader`、`AssetManagement.ContentDirectory.cs`、`ContentDirectorySession` の登録・close・Play 停止、`AbstractApplicationInitializer` の install と close の呼び出し、`SceneDirector` の load・unload・失敗の後始末（`_currentScenes` から組を外す箇所）。未確認: `AssetResidentCache` の中の key の作り方は `CachePrefix` の定義を読んだだけで、cache の全経路は読んでいない。Play 停止で unregister が失敗した session が受付中のまま残るか。
- **I-2 要求した表現を黙って変えない。** loader に渡した要求が表現を指定した時、loader が別の表現の中身を返すのは、要求が fallback の表現を明示した時だけである。loader が返す record の表現は、実際に読んだ表現である。resolver を使わずに content 入口を直接呼んだ時の、索引での `Full` への読み替えは、この条件の対象にしない（今のまま残す。I-3）。
  - 経路: (1) directory の `Full` への読み替え（content 入口に要求の表現をそのまま渡すと、directory が黙って `Full` を読む）。(2) 空の表現の entry（`Full` と読む）。(3) 供給元が要求の表現をそのまま申告する実装。(4) 同じ Scene を、直接の経路が別の表現の名前で既に載せている時の再利用（今も `EntryAmbiguous` で拒む）。
  - 塞ぎ方: 供給元は directory の索引が返した entry の実効の表現を、そのまま申告する（採るかどうかを供給元で判定しない）。resolver だけが、候補の表現が要求の表現か、要求が明示した fallback と一致する時に採る。loader は候補の表現（実効の値）で content 入口を呼ぶ。directory が fallback する先は `Full` だけなので、要求が別の表現を fallback に書いても、その表現の候補は出ない。
- **I-3 resolver を使わない利用を変えない。** `IAssetManagement` の member、既存の content 入口の挙動、`SceneDirector` の public な constructor、Player の起動経路、Addressables の経路は変わらない。`Runtime/AssetManagement/` は `ContentResolution` の型を参照しない。content 入口の挙動には次の 2 つを含む: 索引での `Full` への読み替えと、`LoadContentSceneAsync`・`LoadContentAssetAsync` が最初の `await` より前に directory を掴み、load gate の後に登録中の directory を読み直さないこと（I-1 の塞ぎ方がこれに依る）。
  - 経路: (1) `IAssetManagement` への member の追加。(2) `AssetManagement` に public の型や member を足す。(3) loader を渡さない SceneDirector の分岐。(4) Player で配線が走る。(5) `IContentDirectoryBackend`（internal の port）を変えて既存の fake を壊す（変えない）。
- **I-4 既存の所有・解放・fail-closed を弱めない。** resolver 経由で得た Scene と asset は、既存の台帳に同じ owner で入り、既存の API で解放でき、解放の後の close を妨げない。供給元が候補を出せない時、照合が合わない時に、Addressables・source・別の directory へ逃げない。封じ、protected config、起動の fail-closed は変えない。
  - 経路: (1) loader の失敗の後に SceneDirector が直接の経路で読み直す実装。(2) 供給元が session の token を保持して close を妨げる。(3) record を保持するために Scene の解放を遅らせる。(4) 配線の失敗を握って、黙って直接の経路で起動する。
  - 塞ぎ方（配線）: Editor の directory の Play で、登録が検証済みなら配線は必ず成功しなければならず、失敗（例外）は起動の失敗にする。登録が検証済みでない時と Player では配線せず、今までどおり直接の経路で動く（検証済みでない時は理由を 1 行出す）。この 2 つ以外の分岐を持たない。

**成立条件（起きなければならないこと）**

- **S-1** 検証済みの installed revision から登録した directory について、loader で Scene と Object を要求すると、載った物の handle と、供給元・identity・実効の表現・「一致を確かめた」を持つ record が返り、既存の API で解放して directory を close できる。
- **S-2** loader を渡した SceneDirector が、loader の分岐で Scene を読んで追加すると、その Scene の record を SceneDirector から読める。unload すると読めなくなり、解放は今までと同じ順で行われる。既に `SceneManager` にある Scene を再利用した時と、payload の無い論理 node は loader を通らず、record は無い（AC6）。
- **S-3** Play ボタンの入口の経路 Built の Play で、利用者の新しい入力なしに、SceneDirector が directory から読んだ Scene に record が付く。
- **S-4** 表現が `Full` だけの Scene を、別の表現と `Full` への fallback を明示して要求すると `Full` が載り、record の表現は `Full` になる。fallback を明示しなければ載らない。

### 2.2 受け入れ条件

fixture は SampleGame を使わない合成（program D10）。実機の build は既存の `PurposeBuildIndexIntegrationTests` と同じ手順（製品の coordinator で `Full` build → publish → Local の供給元から install）で、目的 1 つを作る: Scene `Entry`（`Full` と `Whitebox`）、Scene `Next`（`Full` だけ）、Object `Prop`（Representation の tag の無い prefab）。実 Editor の Play だけは SampleGame の入口を使う（AC7。汎用の合否は AC1〜AC6 で決まる）。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | I-1 | 単体（実物の `AssetManagement`、fake の directory の port、照合を答える fake の供給元）。loader は、採用した候補の供給元が照合に真を返した時だけ content 入口を呼び、record の `SourceBindingVerified` は true になる。**失敗経路:** 照合が偽の時は `SourceBindingMismatch` で拒み、port の load は 0 回。照合を答えない供給元（interface を実装しない）の候補は `SourceBindingUnavailable` で拒み、load は 0 回。供給元の `FindAsync` の中で directory を close して別の port を登録すると、前の候補の record で成功しない。loader が成功を返して `SourceBindingVerified` が false の record は無い |
| AC2 | I-1・I-2・S-1 | 単体（fake の native directory と、temp の cache に実際に install した revision）。検証済みで登録した session から作った供給元は、ある entry に `Available` を返し、候補の identity は install した manifest の contentSet・revision・target と、install の要求に渡した digest（`ContentDeliveryFiles.Sha256` の形式）に ordinal で等しく、受け入れ根拠は `VerifiedDigest`、経路は `ContentDirectory`、事実は install した manifest の Unity の版と root schema の版である（定数からは作らない）。候補の表現は索引が返した entry の実効の表現: 要求と同じ表現があればその表現、指定なしは `Full`、要求の表現が無く `Full` があれば `Full`、空の表現の entry は `Full`。照合: 実物の `AssetManagement` にこの session を登録している間は真。**失敗経路:** 無い key と kind の違いは `NotFound`。close した session は `Unavailable` で、照合は偽。別の `AssetManagement`、この session を登録していない `AssetManagement`、`AssetManagement` を包んだ別の `IAssetManagement`、identity を 1 欄変えた候補（4 通り）は照合が偽。検証済みでない登録（`VerifiedInstalledRevision` を持たない session）からは供給元を作れない（作る関数が「作らない」と理由を返す）。供給元は session の token を取らず、供給元が生きていても close は完了する |
| AC3 | I-2 | 単体（`tools/ContentResolutionOfflineTests` と EditMode）。`ContentRequest` は任意の fallback の表現を持てる（constructor の末尾の任意引数として足し、既存の引数の位置と既定値を変えない）。resolver は、候補の表現が要求の表現と一致する時と、要求が fallback を明示していてそれと一致する時だけ採る。**失敗経路:** fallback を明示しない要求に `Full` の候補を返すと、今までどおり `RepresentationMismatch`。表現の指定が無いのに fallback だけがある要求、fallback が表現と同じ要求、空白だけの fallback は constructor が拒む。既存の offline の test は変更なしで通る |
| AC4 | S-1・I-4 | 単体。route が作る互換の基準は、contentSet と target が session の検証済みの値、engine と schema が `ContentManifestValidation` の定数で、revision は固定しない（null）。AC2 の供給元の候補は `ContentCompatibilityRule` で `Compatible` になる。**失敗経路:** 事実の engine の版を 1 文字変えた候補、schema の版を変えた候補、contentSet の違う候補は `RuleRejected` で採られない。検証済みの session から route を作る途中の失敗は例外で返り、「作らない」（検証済みでない登録）と区別できる |
| AC5 | S-1・S-4・I-1・I-4 | 実機（Content Directory の build を伴う EditMode の `[UnityTest]`。Play mode に入って読む）。合成の目的を build・publish・install・検証して登録し、起動の配線と同じ関数で loader を作る。`Entry` を `Whitebox` で要求すると `Whitebox` が載る。`Next` を `Whitebox`・fallback `Full` で要求すると載り、record の表現は `Full`。`Prop` を表現の指定なしで `AssetOwner.Manual` で要求すると載り、record の表現は `Full`。3 つとも record の identity は install した manifest の値と digest に等しく、`SourceBindingVerified` は true。`UnloadSceneAsync` → `ReleaseScene` と `Release` の後、`CloseContentDirectoryAsync` が `ResourcesInUse` を出さずに完了する。**失敗経路:** `Next` を `Whitebox`・fallback なしで要求すると `ContentResolutionException`（`NoCompatibleCandidate`、probe は `RepresentationMismatch`）で、`SceneManager.sceneCount` は変わらない。無い key は `NoCandidate`（probe は `NotFound`）。別の digest の expected identity を付けた要求は採られず、何も載らない |
| AC6 | S-2・I-1・I-3・I-4 | 単体（`PerformUnitySceneLoad` を差し替えない SceneDirector、実物の `AssetManagement`、fake の port、照合を答える fake の供給元）。record の読み口は Scene の id を受け、record が無い時は null を返し、例外にしない（下の「読めない」「record は無い」は全て null を指す。追加していない id も null）。loader を渡すと、`AddScene` の後にその Scene の record を読め、unload の後は読めない。unload と release の呼び出しは loader なしの時と同じ。要求の組み立て: 起動時の表現が `Whitebox` で fallback が `Full` の時、`Full` だけの Scene は載って record の表現は `Full`。起動時の表現が `Full`（fallback と同じ）か空の時は fallback を付けずに要求し、constructor の拒否で失敗しない。loader を渡さない SceneDirector は、今までどおり `LoadContentSceneAsync(scene, variant)` を呼び、record は無い（既存の `SceneVariantForwardingTests` が変更なしで通る）。同じ名前の Scene が既に `SceneManager` にある時と、payload の無い論理 node では loader を呼ばず、record は無い。**失敗経路:** loader が拒んだ時（照合の偽、`NotFound`）、`AddScene` は失敗し、content 入口は直接呼ばれず（port の load は 0 回）、SceneDirector は既存の load の失敗と同じ後始末をして、別の Scene の `AddScene` が続けて成功する。loader が成功した後に Scene の `ExecuteLoaded` が投げた時と、取消で後始末された時は、後始末が組を外した後、その Scene の record は読めない（test はこの形で見る）。既存の回収が外せずに log を出して残した個体は、組と一緒に record が残ってよく、その record はその個体を loader が読んだ時の物である（record を組の外に持たないことを構造レビューで見る。この場合の test は求めない）。loader を渡して content directory を使わない指定にした constructor は拒む |
| AC7 | S-2・S-3・I-4 | 実 Editor の Play（§5 の検証経路）。経路 Built の Play を 2 回行う。1 回目: 起動の log に配線の 1 行（有効、供給元の id、identity の 4 欄）が出て、その revision と digest は入口がその Play に置いた installed revision と `content:manifestSha256` に等しい。初回の Scene `Title` の record を SceneDirector から読め、identity の 4 欄は log の行と同じで `SourceBindingVerified` は true。Play 中に `AddScene` した Scene も同じで、unload した後は record を読めない。initializer の accessor の loader で、bootstrap の SceneResourceMap の logical key を表現 `Full`・`AssetOwner.Manual` で要求すると record が返り、`Release` できる。2 回目: 配線の 1 行と `Title` の record を同じように読める。観測窓（1 回目の Play の開始から 2 回目の停止まで）の Error / Exception は 0 件（`eval` の timeout の行を除く） |
| AC8 | I-3・I-4 | 既存の test と検査。`IAssetManagement` と `AssetManagement` の public の member、`IContentDirectoryBackend` は増えない。次の参照が無い（`grep` と C の構造レビュー）: `Runtime/AssetManagement/` → `ContentResolution`、`Runtime/ContentResolution/Core/` と `ContentRequestLoader.cs` → `BuildContent`、`Runtime/BuildContent/` → `ContentResolution`、`Runtime/SceneSystem/` → `ContentResolution/Directory/` の型。Player の構成（`UseRequiredPlayerFileConfiguration` が真）では配線の関数を呼ばず、SceneDirector に loader を渡さない（code の検査）。検証済みでない登録の Play は、配線しない理由を log に 1 行出し、今までどおり直接の経路で動く（単体は route の関数の戻り値で見る）。検証済みの登録で route の関数が投げた例外を、起動の code が握らない（code の検査）。既存の `OneStarMaker.Tests.ContentResolution`・`ContentDirectorySessionTests`・SceneDirector の test・offline の test は、AC1 で意味を改める loader の test（照合を答える供給元に替える）以外の変更なしで通る |
| AC9 | 範囲 | Phase B の commit が変える file は、§3 の file とその `.meta`、§3 の test の folder の file、`docs/handoff/` の文書だけで、asmdef の `references`、`AssemblyInfo.cs` の friend、`SceneState`、`tools/Artifacts` に変更が無い |

### 2.3 ここでは答えない問いと所有する後続

program が「S3」へ送った入力の引き取りと送り先を、ここに全て書く。

| 問い（出所） | 所有 |
|---|---|
| 採用した候補と実際に読んだ directory の同一性、`SourceBindingVerified` を true にできる条件（S0-a） | **本書**（I-1、AC1・AC5） |
| `Full` fallback の実効値を record に写すこと（S0-a） | **本書**（I-2、AC2・AC3・AC5） |
| 実在の manifest から候補の identity と事実を作る adapter、基準を Distribution の定数から組み立てる向き（S1） | **本書**（AC2・AC4） |
| resolver 無しで先に載った同じ identity の Scene を再利用する時の record（S0-a） | **本書**。record は「この handle の中身はこの directory の物」を言い、新しく読んだか再利用かは言わない。違う表現の名前で載っていれば今までどおり拒む（I-2 の経路 (4)） |
| 採用の後の load の失敗で、probe と候補を呼び手へ残す方法（S0-a）。`Rejected` の expected identity が record に残らないこと（S0-a） | S5（理由 code の扱いと一緒に） |
| base → 上書きの登録の順、個数、時点、解除の順、重ねてよい組の照合、両方の root からの key の解決、Domain Reload 有効の Play（S2-1・S2-2、13 章）。directory が 2 つの時に record がどちらを指すか（本書の照合は session ごとに答えるので、供給元を session ごとに足す形で続けられる見込み） | S3-2 |
| 上書きの base の選択に索引を使う接続（S2-3）。Editor の Play の結論を Player で確かめ直すか（S2-0b・S2-1） | S3-2 の A1 |
| 開いているが要求していない Scene を lifecycle に入れないこと、要求時の `AddScene` と `RegisterAlreadyLoadedScenes` の責務の分離（S0-b。program §8 が「S3 の受け入れ条件から落とさない」とした項目） | S3-3（受け入れ条件として引き継ぐ） |
| 起動の後に再要求した Scene を snapshot から読み直すこと、取り込みの段の key と mode の組、map にある Scene を開いた混成の Play、由来の label の一般化、`Title` 以外の Scene での実証（S0-d） | S3-3 |
| `SceneDirector` の取消が Unity の Scene の load に届かないこと（S0-b） | S3-3 の A1（load の経路を触る時に判断する。本書は `CancellationToken.None` のまま変えない。§2.8 U8） |
| 要求の時点の install と登録、`ContentPurposeIndex.Select` で選んだ build を install と登録の検証に通すこと（S2-3）、transport の protocol の版を採否に含めるか（S1）、要求をまたぐ pin（S0-a・S1） | S4 |
| Play の入口の Built の候補に互換の規則を接続すること（S1） | S5 の A1 |
| 起動時の読み込みと `SceneBase` の付属 asset の record | S5 の A1（観測に要るかで決める） |
| `Runtime/ContentResolution/` を別 asmdef にするか、P 検査の常設（S0-a） | [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) の asmdef スライス |

### 2.4 代替案の比較

**record と実ロード元の一致の確かめ方**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| B1 | `AssetManagement` が今の directory の binding（4 欄の値）を public の interface で答え、loader が比べる（初稿の案） | AssetManagement に public の型が増え、値の型の置き場が要る。「directory は 1 つ」を公開面に固定し、2 つになる S3-2 で改訂が要る（A2 のゲートの指摘 1・2） | 採らない |
| B2 | handle が由来を持つ。loader は load の後に照合する | load の前に拒めないので、食い違った Scene が載ってから気づく。Scene の台帳は共有で、後から unload すると他の利用者の Scene を壊しうる | 採らない |
| B3 | loader が AssetManagement の internal を見て確かめる | 「loader は public だけを使う」（13 章）を崩す | 採らない |
| B4 | `IAssetManagement` に identity を返す member を足す | 単独の利用者の interface を変える（S0-a が避けた） | 採らない |
| B5 | 供給元（連携部分）が「この候補は、渡された `IAssetManagement` が今読む directory の物か」を答える。供給元は自分の session を知っていて、`AssetManagement` の internal の問い合わせ 1 つで登録中かを確かめる。loader は `ContentResolution` の internal の interface 越しに尋ねる | public の面は増えない。連携部分が AssetManagement の internal を 1 つ使う（session が internal の port を実装しているのと同じ種類の辺）。wrapper の `IAssetManagement` は確かめられず拒む。session ごとに答えるので directory の数に依らない | **採る**（A2 の代替構成とゲートの指摘から） |

**表現の fallback**

| 案 | 内容 | 費用と危険 | 推奨 |
|---|---|---|---|
| R1 | 供給元は要求の表現をそのまま申告し、実効の表現は事実（文字列の辞書）に書く | core は変えない。record の主な欄が実際と違う表現を言う。fallback が黙って起きる（program §4.1 の「別表現へ黙って切り替えない」に合わない） | 採らない |
| R2 | 要求が fallback の表現を明示でき、候補は実効の表現を申告する | `ContentRequest` と `ContentResolver`（S0-a で凍結した core）を改める。直接の経路と loader の経路が同じ Scene を別の表現の名前で要求すると `EntryAmbiguous` になる（今も、表現の名前が違えば起きる） | **採る**（§2.8 U3） |
| R3 | fallback を policy（供給元の option）に持たせ、resolver は供給元の申告を信じる | 要求した表現と違う候補を resolver が見分けられず、I1 の検査が供給元ごとの実装になる | 採らない |
| R4 | core は変えない。供給元は実効の表現を申告し、呼び手が「要求の表現で 1 回、採られなければ `Full` でもう 1 回」と 2 回要求する（A2 の代替構成の案） | 表現が `Full` だけの Scene のたびに 1 回目が失敗の例外になる。2 回要求する判断が呼び手ごと（SceneDirector と直接の利用者）に要る。1 回目の probe は record に残らない | 採らない（§2.8 U3 の代替） |

**SceneDirector への接続**

| 案 | 内容 | 推奨 |
|---|---|---|
| D1 | internal の constructor に、任意の `ContentRequestLoader` と fallback の表現を足す。public の constructor は変えない。record は SceneDirector が Scene ごとに持ち、public の読み口 1 つで返す | **採る**。型を増やさない。配線するのは起動だけ |
| D2 | SceneSystem 側に Scene の読み口の port（interface）を作り、直接の経路と resolver の経路の 2 実装にする | 採らない。実装が 2 つで足り、差し替える利用者がまだ居ない（port の先取り） |
| D3 | `PerformUnitySceneLoad` を override した派生 SceneDirector を連携部分に置く | 採らない。`protected virtual` の戻り値は record を運べず、起動が派生型を知る |

**配線**

| 案 | 内容 | 推奨 |
|---|---|---|
| W1 | Editor の directory の Play で、検証済みの revision から登録した時は、Framework が既定で配線する。Player と、検証済みでない登録では配線しない | **採る**（§2.8 U5）。承認済みの利用者 (a) の「新しい入力は無く」に合う |
| W2 | host が initializer の hook で明示した時だけ配線する | 採らない。利用者の入力が増え、既定の Play では実在の供給元が使われないまま残る |

### 2.5 スライス分割の判断

- 契機: program の S3 の行には、独立して失敗しうる未知が 4 つあった。(1) 実在の供給元と、record と実ロード元の一致。(2) base と上書きの 2 つの directory の登録（`InstallContentDirectory` と `ContentRevisionGate` の単一の前提、close の順）。(3) Editor snapshot を候補にすることと、要求していない既ロード Scene の扱い。(4) 要求の時点の install と登録（起動の順序、封じ、bootstrap の読み込みとの関係）。
- 判断: **分ける。** S3-1（本書）は (1)、S3-2 は (2)、S3-3 は (3) を持つ。(4) は S4 へ送る（実在の取得元が無いと fake の取得でしか確かめられず、進行中の install への合流と取消は既に S4 の入力である）。owner（root）が 2026-11-17 に承認した。program §8 の行を同じ commit で改める。
- 本書の中: 分けない。供給元・照合・表現・SceneDirector・配線は、1 つの問い（record が実際に読んだ物を指す接続が両方の経路で成り立つか）に要る。単体で閉じる部分（AC1〜AC4・AC6）と実機（AC5）と実 Editor（AC7）は別々に判定する。実 Editor の Play だけが成り立たない時の扱いは §2.6。

### 2.6 判定定義と停止規則

- 判定定義:
  - **GO:** AC1〜AC9 が全て成り立ち、判定必須テスト（§5）が通る。
  - **NO-GO（owner へ返す）:** (i) I-1 を、`AssetManagement` に public の型か member を足さずには満たせない。(ii) AC7 で、実際の起動の順序の下では配線できない（例: SceneDirector を作る時点で session の検証済みの値が得られない）。何がどの段で得られなかったかを記録して返す。
  - それ以外の不成立は通常の差し戻しで B へ戻す。
  - スパイクではないので CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の `references` と `InternalsVisibleTo` を足さない（足す必要が出たら Phase A へ戻す）。
- アセットは `IAssetManagement` 経由で、`AssetOwner` で寿命を宣言する。`IAssetManagement` に member を足さない。
- `SceneState` の 14 値を変えない。状態の変更は `SceneLifecycleManager` が所有する。
- 公開 API のログは `ILogger<T>`。`ZLogger*` 型を公開面へ出さない（起動の 1 行は既存の `[AppInit]` の log と同じ出し方に揃える）。
- 新規・編集する Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。Editor の code を Runtime の assembly に置かない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない。
- repo 相対 path は `tools/path-budget.json` の予算に収める。
- `Runtime/ContentResolution/Core/` は Unity と `BuildContent` を参照しない（`tools/ContentResolutionOfflineTests` が source を直接 compile する）。
- loader は AssetManagement の public だけを使い、同じ assembly の internal を呼ばない。
- loader の照合は、`LoadSceneAsync` と `LoadAssetAsync` のそれぞれの中で、解決の `await` が終わった後、content 入口の呼び出しの直前に同期で行う。照合を、`await` される別の async method（今の `ResolveForLoadAsync`）の中に置かない（method の戻りが `await` の境界になり、I-1 の「照合と呼び出しの間に `await` を置かない」が code から読めなくなる）。
- `LoadContentSceneAsync`・`LoadContentAssetAsync` が directory を掴む位置（最初の `await` より前）を動かさず、load gate の後に登録中の directory を読み直す code を足さない（I-1・I-3）。
- `ContentRequest` の constructor の既存の引数の位置と既定値を変えない（fallback は末尾の任意引数）。
- 封じ（`SealAddressables`）、protected config の照合、起動の fail-closed、`CompleteContentDirectoryPlayStop` の順序を変えない。
- 機械向けの記録を git に入れない。

### 2.8 owner の判断を求める点（未決事項）

全て決定済み。U1・U2 は 2026-11-17 の A2 の前に、U3〜U8 は同日の外部計画レビュー 1 巡目の後に、owner が担当の推奨どおりに決めた（A2 と外部計画レビュー 1 巡目は推奨の形で見た）。

- **U1 目的と利用者の文言。** 決定: 承認（owner、2026-11-17。経緯は §1）。
- **U2 S3 の分割。** 決定: S3-1（本書）・S3-2（上書きの登録）・S3-3（snapshot の候補）に分け、要求の時点の install は S4 へ送る（owner、2026-11-17。§2.5）。
- **U3 表現の fallback を要求の明示にする（core の改訂）。** 決定: R2（§2.4）。`ContentRequest` に任意の fallback の表現を足し、resolver はそれと一致する候補も採る。S0-a で凍結した core の契約の追加になる。R4（呼び手が 2 回要求する）は採らない。
- **U4 一致を確かめられない時は読まない。** 決定: loader に「確かめずに読む」option を持たせない。照合を答えない供給元の候補と、照合が偽の候補は、load の前に拒む（S0-a の loader の test のうち、確かめずに成功していたものは、照合を答える供給元に替える）。成功した record の `SourceBindingVerified` は常に true になるが、欄は残す（後続の経路が別の値を持ちうる）。A2 の代替構成は「拒まずに false の record を返す」案だったが、load の前に分かる食い違いを読ませる理由が無いので採らない。
- **U5 既定で配線する。** 決定: W1（§2.4）。経路 Built・検証 Play・Delivery の Play は全て検証済みの revision から登録するので、SceneDirector の Scene の読み込みが resolver を通るようになる。Player は配線しない。
- **U6 Player の build を判定必須にしない。** 決定: しない。Player の構成では配線の関数を呼ばず、SceneDirector に渡る loader は null で、分岐は今の code と同じ（AC6・AC8）。program の S3 の gate「Player の非退行」は、Player の経路を変える S3 の後続（S3-2 か S3-3）の A1 で実機の要否を決める。
- **U7 外部計画レビューと C' の独立性。** 決定: Draft PR の外部計画レビューは cursor の PR レビューで行う（1 巡目は実施済み。§5.1）。A2 は Anthropic 系の 4 件。使える系列は Anthropic 系と Grok 系だけ（Codex と Google 系は使えない）なので、C' は Grok 系で行い「独立性制約あり」と記録する。
- **U8 `SceneDirector` の取消。** 決定: 本書では変えない（`PerformUnitySceneLoad` は今も `CancellationToken.None` で読み、遷移の commit の後は取り消さない設計である）。S3-3 の A1 が load の経路を触る時に要否を決める。

## 3. 責務マップ

行数は base `36d069e8` の `wc -l`。増分は見込み。型と file の名前は仮で、B が決めて §6 に記録する（責務と置き場は変えない）。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs` | directory の読み込みと台帳 | 「渡した port が、今登録している directory か」を答える internal の問い合わせを 1 つ足す | 既存（App） | 既存 | internal の method 1 つ。public は変えない | AC2 | 164 → 168 |
| `Runtime/BuildContent/Distribution/InstalledRevisionVerifier.cs` | installed revision の検証と、その結果の値 | `VerifiedInstalledRevision` に、検証で読んだ manifest の Unity の版と root schema の版を載せる（今は読んで捨てている） | 値 | 既存 | internal のまま | AC2 | 128 → 136 |
| `Runtime/BuildContent/ContentDirectorySession.cs` | directory の登録と native の処理の所有 | 検証済みの登録の値（`VerifiedInstalledRevision`）と受付中かを internal で見せ、索引で entry を引く internal の口を足す | 既存（登録から close まで） | 既存 | internal のまま | AC2 | 481 → 492 |
| `Runtime/ContentResolution/IContentSourceBinding.cs`（新規） | 「この候補は、渡された `IAssetManagement` が今読む directory の物か」を答える interface | I-1 の照合を loader から供給元へ尋ねる口 | — | `ContentResolution/Core`、`IAssetManagement` | internal の interface（member 1 つ） | AC1 | 0 → 20 |
| `Runtime/ContentResolution/Directory/RegisteredDirectoryProvider.cs`（新規） | 登録した 1 つの directory を供給元として答える（entry の有無、実効の表現、identity、事実）。上の照合も答える | 新しい責務 | session と同じ寿命。token は取らない | `BuildContent`（session・索引）、`AssetManagement`（internal の問い合わせ）、`ContentResolution/Core` | internal | AC2 | 0 → 120 |
| `Runtime/ContentResolution/Directory/RegisteredDirectoryRoute.cs`（新規） | session と `AssetManagement` から、供給元・互換の基準・policy・loader を組み立てる。検証済みでない登録には「作らない」と理由を返し、それ以外の失敗は例外にする | 起動と test が同じ組み立てを使う（AC5） | static | 上の供給元、`Distribution` の定数、`ContentResolution` | internal（起動と friend の test が呼ぶ） | AC2・AC4・AC8 | 0 → 70 |
| `Runtime/ContentResolution/Core/ContentRequest.cs` | 要求の値 | 任意の fallback の表現 | 値 | なし | public の constructor の末尾の任意引数と property | AC3 | 62 → 80 |
| `Runtime/ContentResolution/Core/ContentResolver.cs` | 探索 | 候補の表現の照合に fallback を足す | static な処理 | なし | 変えない | AC3 | 318 → 328 |
| `Runtime/ContentResolution/Core/ContentLoadRejection.cs` | loader の拒否の理由 | `SourceBindingUnavailable`・`SourceBindingMismatch` を足す | enum | なし | public の enum の値の追加 | AC1 | 17 → 25 |
| `Runtime/ContentResolution/ContentRequestLoader.cs` | 採用した候補を content 入口へ渡す | load の前の照合（2 つの load method の中で、解決の `await` の後、content 入口の呼び出しの直前に同期で行う。§2.7）、record の `SourceBindingVerified` | 呼び手が持つ | `IAssetManagement`（public だけ）と上の interface | 変えない（挙動は U4） | AC1 | 98 → 120 |
| `Runtime/SceneSystem/SceneDirector.cs` | SceneDirector の構成と公開面 | internal の constructor の任意引数（loader、fallback の表現。loader と「content directory を使わない」の組は拒む）、Scene ごとの組（`ScenePair`）に record、record の読み口 | 既存。record は組と同じ寿命 | `ContentResolution`（新しい辺。`Directory/` は知らない） | public の method 1 つ（record の読み口。Scene の id を受け、record が無ければ null を返し、例外にしない） | AC6 | 280 → 310 |
| `Runtime/SceneSystem/SceneDirector.Loading.cs` | Scene の load | loader があればそれで読み、record を組に入れる（要求の fallback は、起動時の表現が空でも fallback と同じでもない時だけ付ける） | 同上 | 同上 | 変えない | AC6 | 685 → 705 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 起動の順序 | directory の登録の後に route の関数を呼び、loader を SceneDirector へ渡し、protected の accessor で host に見せ、1 行出す | 既存（App） | 上の route | protected の accessor 1 つ | AC7・AC8 | 1149 → 1170 |
| test（単体は `OneStarMaker/Tests/ContentResolution/`・`OneStarMaker/Tests/Scene/`・`OneStarMaker/Tests/Editor/Build/`、offline は `tools/ContentResolutionOfflineTests/`、実機は `SampleGame/Tests/Editor/Build/`） | — | — | — | — | — | AC1〜AC6 | 新規と追記 |

- 変えない file: `IAssetManagement.cs`、`Internal/IContentDirectoryBackend.cs`（port の fake は 4 つの test にあり、どれも変えない）、`SceneDirector.Unloading.cs`、`SceneDirector.FailedLoadCleanup.cs`（record は組と一緒に消えるので、消す code を足さない）。
- 行数の警報:
  - `AbstractApplicationInitializer.cs` は既に 1,149 行で、ここに組み立ての判断を置かない（route の関数 1 回の呼び出しと accessor と log だけ）。分割は `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` が持つ。
  - `SceneDirector.Loading.cs` は既に 685 行（既存の partial の分け方）。足すのは `PerformUnitySceneLoad` の 1 分岐で、責務は増えない。
  - `ContentDirectorySession.cs` は 500 行に近づく。供給元の判断（候補の組み立て、照合）は session に置かず、新しい file に置く。session が足すのは保持と公開だけ。
- 配置の理由:
  - 供給元と route は、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の分類で**連携部分**である（BuildContent の internal の session・索引・定数と、AssetManagement の internal の問い合わせを使う）。`Runtime/ContentResolution/Directory/` に置き、`Core/`（Unity と BuildContent を知らない）と loader（AssetManagement の public だけ）とは folder を分ける。依存の向きは `Directory/` → `BuildContent`・`AssetManagement`・`Core` の一方向で、BuildSystem 本体と AssetManagement が `ContentResolution` を参照する辺は作らない（AC8 の検査）。将来 asmdef を分ける時は `Directory/` が連携部分の assembly に入る。
  - 照合の interface は `ContentResolution/`（`Core/` の外）に internal で置く。loader が知るのはこの interface だけで、供給元の実装と BuildContent は知らない。
  - SceneSystem → `ContentResolution` の辺が新しく入る（同じ asmdef の中）。SceneDirector は loader と record の型だけを知り、供給元と基準は知らない。record の読み口を public にするのは、利用者 (a) の host の code と後続の観測（S5）が Scene の record を読む口が他に無いためである。
  - 互換の基準の revision は固定しない。供給元が登録した directory 1 つの間は固定しても結果は変わらず、要求をまたぐ固定は S4 が決める。
  - asmdef は全て既存の `OneStarMaker.Runtime` の中で、参照を足さない。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (1) core の fallback（AC3。offline の test で閉じる）。(2) 照合の interface と loader（AC1）。(3) 検証の値と session の公開、供給元と route（AC2・AC4）。(4) SceneDirector（AC6）。(5) 起動の配線（AC8）。(6) 実機の test（AC5）。(7) 動作確認（AC7）。
- Phase B から Phase A へ差し戻す条件: asmdef の参照か friend が要る。`IAssetManagement` に member が要る。§3 に無い public の型・member が要る。`AssetManagement` に public の面か、§3 の 1 つを超える internal の口が要る。`IContentDirectoryBackend` を変える必要がある。SceneDirector の既存の失敗の後始末を変えないと AC6 の失敗経路を満たせない。起動の順序を変えないと配線できない。
- 対象外を維持する方法: `InstallContentDirectory` の単一の前提、`ContentRevisionGate`、`EditorPlaySnapshot*`、`RegisterAlreadyLoadedScenes`、`BootstrapUiCommonLoader`、`SceneBase` の付属 asset の読み込み、`Runtime/BuildContent/Distribution/`（§3 の `VerifiedInstalledRevision` の 2 欄を除く）、`ContentDirectoryIndex` の照合の規則、`tools/Artifacts`、SampleGame の入口（`SampleGame/DependOnAll/Editor/Build/`）を変えない。供給元を足す hook、host の述語の登録口、install の経路を作らない。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC4・AC6（Unity の build を伴わない EditMode）と、AC3 の offline（`dotnet run --project tools/ContentResolutionOfflineTests`。全行 `PASS` で exit 0）。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.ContentResolution`、`OneStarMaker.Tests.SceneSystem.SceneVariantForwardingTests`、`OneStarMaker.Tests.Editor.Build.ContentDirectorySessionTests`、AC2・AC4・AC6 の新しい test の class（B が名前を決め、§6 に記録する）。
- 判定必須テスト（GO 候補 head）: 上の起点 filter、offline の test、AC5 の実機の test、最終の全 EditMode 回帰（空 filter。合否は「XML の failed 0 と root result `Passed`」で読む。`-nographics` の VFX 25 件の Inconclusive で `step.json` が `failed` になるため）、AC7 の観測。
- 全 EditMode 回帰の適用除外: なし。**Player の build は判定必須にしない**（§2.8 U6。代替証拠は AC6 と AC8 の検査）。
- 統合・Unity テスト: AC5 は Content Directory の build を伴う。他の Unity が同時に動くと build を伴う test が失敗した既知の事例がある（program §8）ので、判定 C は他の Unity を止めて走らせる。build を伴う batch の後の Unity の終了 stall は既知（`tools/Harness/README.md`）で、XML の完成で判定する。
- 操作・実行時・目視条件の検証経路（AC7）:
  - 担当: 実行担当（判定 C）。動作確認は B の出口で同じ手順を 1 回行う。環境: `unity/` の Editor（`tools/unity-editor.cmd eval`）。`eval` の前に Editor の window を前面にする（非フォーカスの Editor は main thread の操作が 5 秒で timeout する）。
  - 準備: installed cache に `spring-full` の検証を通る候補が無ければ、`Verify Play` を 1 回実行して作る（Phase C の通常の Build）。Variant profile の選択を外す（選択があると入口が拒否する。実行担当は元の選択を記録して最後に戻す）。入口の経路を Built にする（実行担当は元の値を記録して最後に戻す）。開いている Scene は空の新規 1 つにする（起動が再利用しうる Scene が開いていると入口が拒否する）。
  - 操作: `EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play")` → Play の入口の record の `state` が `EnteredPlay` / `Blocked` / `StartupFailed` になるまで短い `eval` で読む → 下の観測 → `EditorApplication.ExitPlaymode()` → `isPlaying` が false になってから同じ操作をもう 1 回。移行中の `eval` は捨てられる。`EnteredPlay` 以外で終われば、その state と理由を記録して観測を止める。
  - 観測（構造と値で判断する。画面は見ない）。1 回目の Play: (1) 起動の log の配線の 1 行（有効か、供給元の id、identity の 4 欄）と、入口がその Play に置いた installed revision の path と `content:manifestSha256`。(2) `eval` の reflection で initializer の SceneDirector を取り、`Title` の record（identity の 4 欄、表現、`SourceBindingVerified`）を読む。(3) 同じ SceneDirector で、map にあって payload を持ち、その時点で載っていない Scene を 1 つ `AddScene` し、record を読み、その Scene を unload して record が読めないことを見る（Scene の名前は B が動作確認で選んで §6 に固定し、判定 C は同じ名前を使う）。(4) initializer の accessor の loader で、bootstrap の SceneResourceMap の logical key を表現 `Full`・`AssetOwner.Manual` で要求し、record を読み、`Release` する。2 回目の Play: (1) と (2) だけ。(5) 観測窓（1 回目の Play の開始から 2 回目の停止まで）の Error / Exception の件数。B が実際に動いた `eval` を §6 に残し、判定 C はそれを使う。
  - 合否の読み方: (1) の行が「有効」でない、(2)〜(4) の record が読めない・identity が (1) と違う・`SourceBindingVerified` が false、(3) の unload の後に record が読める、(5) が 1 件以上、のどれかで不合格。
  - 設定: この project の Enter Play Mode の設定（Reload Domain 無効）で行う。配線は static な状態を持たないので、Reload Domain 有効では行わない。
  - 証拠: 観測を 1 file にして evidence bundle へ収録する。C / C' はそれを読む。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし。`eval` での Play の開始と停止、Play の入口の record の読み取り、reflection での SceneDirector の取得と `AddScene` は、S0-d（PR #176）の AC7・AC11 で成立した経路である。record の読み口と accessor は本書が作るので、初回の確認は B の動作確認。**未確認。初回確認は B の動作確認:** Editor の経路 Built の Play で `UseRequiredPlayerFileConfiguration` が偽であること（真なら配線されず、AC7 の (1) で分かる。その時は配線の条件の決め方を Phase A へ戻す）。経路 Built は Editor snapshot の取り込みの印（`editor:playSnapshotExpected`）を置かないこと（置くと `Title` は取り込みから再利用され record が付かない。18 章では Built + open scenes の経路だけが置く）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし（準備の Content Directory の build は Phase C の通常の Build として 1 回行う）。
- 判定 C の照合 script の方式: なし（test の XML と、AC7 の観測 file を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC8 の 4 つの `grep`。
- A0/A1 主担当・モデル・ベンダー: AI（Claude Code、Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: cursor-agent の Grok 系（§2.8 U7。外部計画レビューを cursor の PR レビューで行ったので「独立性制約あり」）。
- 独立性の強化条件を満たせない場合の理由: 使える系列が Anthropic 系と Grok 系の 2 つで、Phase A〜C に関与しない第 3 の系列が無い（§2.8 U7）。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-17、同一 PC）。入力版は固定 base `36d069e8` の clean な detached checkout と、HANDOFF copy（sha256 `850bea9065e530f75dc5c787d778499e0c849619759120a1878cf3456b16adb2`。A1 初稿）。4 件とも Claude Code の subagent（Anthropic 系。主担当と同じ系列で、主担当より下位のモデル）。互いの指摘は渡していない。
  - アーキテクチャゲート: HEAD・clean・hash の照合は一致。結論は blocker 0、major 5、minor 4。
  - 不変条件の反証と AC の読み違い: 照合は一致。結論は blocker 0、major 5、minor 5。I-2・I-3 は破れず、I-1 は SceneDirector が持つ record の寿命で、I-4 は配線の失敗の扱いで穴があった。
  - 目的レビュー: §1 の目的と利用者の本文だけを渡した別の担当（道具を使わない）。入力の sha256 `16938d48718147632950adf55500ce3434c4e4615950a4a02e3ade21ae45b36f`。
  - A0 だけからの代替構成（risk `high` の追加分）: 初稿を読まない別の担当。入力は A0 の packet（sha256 `e20c6b719e577926d84a2b977eb1170b7dc73790fafb7ea7418018733b4067c3`。目的、利用者、問い、現況、対象外、要求、制約、未決事項）と固定 base。
- A3 の分類（A2 の指摘。担当の案を、owner が 2026-11-17 の凍結判断で承認した。保留だった代替構成 2 は owner が U3 で決めた。目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは無い）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | ゲート 1・2・6、代替構成 1 | binding の値を共通契約に置く理由が無い。public の interface が「directory は 1 つ」を固定する。cast による隠れた能力判定。代替構成は AssetManagement を変えずに供給元が照合を答える形 | 採用 | 照合を供給元（連携部分）が答える B5 に替えた。AssetManagement の public と共通契約と port は変えない（§2.4、I-1、§3） |
  | ゲート 3 | port に member を足すと `SealedAssetBackendTests` の fake も壊れる | 解消 | port を変えない形にした（§3「変えない file」） |
  | ゲート 4 | 表現を採るかの判定が供給元と resolver の 2 か所に入る | 採用 | 供給元は実効の表現を申告するだけにし、判定は resolver だけに置いた。AC5 の probe を `RepresentationMismatch` に改めた（I-2、AC2・AC5） |
  | ゲート 5 | `Directory/` は連携部分。分類の明記と、参照の向きの検査 | 採用 | §3 の配置の理由、AC8 の 4 つの `grep` |
  | ゲート 7、反証 major 1・2 | SceneDirector の record を消す場所が未指定で、失敗・取消の後に古い record が残りうる。I-1 が SceneDirector の record を覆っていない。loader と「directory を使わない」の組 | 採用 | record を `ScenePair` に持たせ、I-1 に SceneDirector の record と経路 (8) を足し、AC6 に失敗経路を足した。constructor が組を拒む |
  | ゲート 8・9 | record の読み口を public にする理由。harvest 先に README | 採用 | §3 の配置の理由、§0 の harvest to |
  | ゲートの事実の指摘 | `EntryAmbiguous` の行番号、path の起点 | 採用 | §1 |
  | 反証 major 3 | 検証済みの登録で配線に失敗した時の振る舞いが無く、I-4 の経路 (4) を塞ぐ AC が無い | 採用 | I-4 の塞ぎ方（配線）、AC4・AC8 |
  | 反証 major 4 | 「path だけで登録」の定義が `Register` の挙動（managed な path は検証済みになる）と食い違う | 採用 | 「検証済みでない登録 = `VerifiedInstalledRevision` を持たない session」に統一（§1、AC2、AC8） |
  | 反証 major 5 | SceneDirector の fallback の組み立て（表現が空・`Full`・それ以外）の AC が無く、道 (e) が SceneDirector の層で開いている | 採用 | AC6、§3 |
  | 反証 minor | 照合の範囲を実物の `AssetManagement` に限る。payload の無い node。AC7 の identity は 4 欄、観測窓、2 回目の観測項目、名前の固定。AC5 の Scene の数。AC9 が文書を含むか。digest の形式 | 採用 | I-1、AC2、AC5〜AC7、AC9、§5 |
  | 反証・ゲートの未確認 | 経路 Built で `UseRequiredPlayerFileConfiguration` が偽か、snapshot の取り込みの印が立たないか | 保留（初回確認は B の動作確認） | §5「未知の操作経路」 |
  | 目的レビュー | 保証すべきこと 9 件（記録と実ロード元の一致、2 経路で同じ結果、所有と解放、既存の入口、検証済みだけ、互換をロードの前、新しい入力なし、後続が供給元を足せる形、失敗時の挙動は文言から決まらない） | 初稿にある | 一致・2 経路・所有・入口・検証済み・互換は I-1〜I-4 と S-1〜S-3。「供給元を足せる形」は既存の `ResolutionPolicy`（順のある供給元の列）で足り、hook は作らない。失敗時の挙動は I-4 で fail-closed に決めた |
  | 目的レビュー | 作らないべきもの 8 件（複数 directory、後続の供給元、host 向けの新しい設定・引数、専用のロード経路と解放 API、SceneDirector に依存した記録、未検証の revision、汎用の層、記録の UI） | 初稿と一致 | fallback の引数は利用者 (b) の任意の入力で、利用者 (a) には無い（UC-1・UC-2）。他は対象外と §4 に同じ |
  | 代替構成 1 | 照合が合わない時は拒まずに false の record を返す | 不採用 | load の前に分かる食い違いを読ませる理由が無い（§2.8 U4） |
  | 代替構成 2 | fallback は core を変えず、呼び手が 2 回要求する | 不採用（owner が U3 で R2 に決めた。2026-11-17） | §2.4 R4、§2.8 U3 |
  | 代替構成 1・4 | `VerifiedInstalledRevision` に manifest の事実を載せる。互換の基準の revision は固定しない | 採用 | §3、AC4 |
  | 代替構成 3 | record は `ScenePair` が持つ。record を `SceneEvent` に載せない | 採用 | §3（初稿も event には載せていない） |
  | 代替構成 4 | initializer に public の accessor | 不採用 | 読むのは initializer を継承する host なので protected で足りる |
  | 代替構成の検証 | 実 Editor の確認を SampleGame を使わない合成の app で行う。Player の起動の非退行 | 不採用・保留 | 合成の initializer を持つ app が無く、作ると本書の範囲を超える（汎用の合否は AC1〜AC6、実機は AC5）。Player は U6 |

- A2 の再レビューの要否: 行わない。照合の形の変更（B1 → B5）は、ゲートの指摘 1・2・6 と代替構成の構成に沿って公開面を減らすもので、新しい辺は「連携部分 → `AssetManagement` の internal の問い合わせ 1 つ」だけである。この辺と I-1 の塞ぎ方は、Draft PR の外部計画レビューで見てもらう。
- 外部計画レビュー 1 巡目（2026-11-17、Draft PR #197、対象は統合案の commit `f28e7cd1`）。cursor の PR レビュー（PR のコメントの mention で起動。U7 の前提どおり Grok 系として扱う。系列は担当が確かめていない）。結論は blocker 0 で、「U3〜U8 は推奨どおり、上の採否の表はそのままで凍結してよい。凍結前に文言を合わせる」。担当は、指摘の根拠のうち content 入口が directory を掴む位置と、失敗の回収が外せなかった個体を残すことを、base の code で確かめた。owner は 2026-11-17 に下の採否と U3〜U8 を推奨どおりとし、凍結はまだしないと指示した。

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | I-1 の成功経路に、record が別の directory を指したまま成功する順序は残っていない。これは content 入口が最初の `await` の前に directory を掴み、gate の後に読み直さないことに依る。B はこの位置を動かさない | 採用 | I-1 の塞ぎ方、I-3、§2.7 |
  | AC6 の「`ExecuteLoaded` が投げた後は record を読めない」は、回収が外せずに残した個体では保証できない。残った record が実際に読んだ directory と一致する、と書く | 採用 | AC6 |
  | I-2 は loader が返す record の話である。直接の content 入口の `Full` への読み替えは残る | 採用 | I-2、I-3 |
  | S-2 は loader の分岐で読んだ Scene の話である。既に `SceneManager` にある Scene に record は無い | 採用 | S-2 |
  | record の public の読み口は Scene の id を受け、無い時は null を返し、例外にしない | 採用 | AC6、§3 |
  | fallback は `ContentRequest` の末尾に足し、既存の位置引数をずらさない | 採用 | AC3、§2.7、§3 |
  | U3〜U8 は担当の推奨と同じ結論 | owner が決定 | §2.8 |
  | このレビューの指摘を C' の入力に入れない | 保留（workflow の規則で決める） | C' は PR の会話を継承せず、凍結した Phase A snapshot を読む。この表は A2 の表と同じく snapshot に入る。凍結の時に owner から扱いを変える指示は無く、workflow の既定のままとした |

- 外部計画レビュー 1 巡目の反映に伴う A2 の再レビューの要否: 行わない。反映は、条件の対象を狭める文言（I-2、S-2）、既存の挙動の明記（I-3、§2.7）、読み口の戻り値の明記、AC6 の 1 文の修正で、設計と責務の配置は変えていない。
- 外部計画レビュー 2 巡目（2026-11-17、対象は 1 巡目を反映した commit `4c25182f`）。cursor の PR レビュー。指摘は 0 件。結論は「1 巡目の反映は意図どおりで、新しい矛盾や判定できない文は無い。AC6 の残った個体は test と構造レビューの分担で判定できる。凍結を妨げる点は無い」。反映は無い。
- 外部計画レビュー 3 件目（2026-11-17、対象は同じ commit `4c25182f`）。owner のアカウントの計画レビュー。blocker は 0 件で、結論は「凍結に進んでよい」。Phase B・C の確認事項が 1 件あった。

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | 今の loader は `await ResolveForLoadAsync(...)` の結果を受けてから content 入口を呼ぶ。照合を `ResolveForLoadAsync` の末尾に足すと、照合と入口の呼び出しの間に `await` の境界が残る。2 つの load method で、解決の `await` の後に照合し、その直後に入口を呼ぶ配置にする。AC1 の「`FindAsync` の中の directory の差し替え」は照合の前の競合の検証で、この配置の確認の代わりにならない。新しい test と Phase A の差し戻しは求めない | 採用（実装制約の明記） | §2.7、§3 の loader の行。担当は base の `ContentRequestLoader.cs` で今の形を確かめた。確認は C の構造レビューで行い、test は足さない |

- 2 巡目以降の反映に伴う A2 の再レビューの要否: 行わない。足したのは I-1 の既存の条件（照合と呼び出しの間に `await` を置かない）を code の配置として書いた実装制約 1 つで、条件の意味と責務の配置は変えていない。
- A3 統合担当・採否: 統合は AI（Claude Code、Anthropic）。採否は owner が 2026-11-17 に承認し、同日に凍結した（§0 の凍結記録）。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
