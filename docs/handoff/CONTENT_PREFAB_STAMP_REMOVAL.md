# Scene に焼き込む prefab の版の印の撤去判定（S2-2c）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-2c）
- status: `A`（A3 統合案、外部計画レビュー 2 巡目を反映。未凍結）
- branch: `claude/content-prefab-stamp-removal`（PR の base は `develop`）
- implementation base commit: `4aef3dd4`（`develop` の head、2026-11-17。Unity 6000.6.9f1 への更新（PR #198）を含む）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `high`（Unity の差分 build が黙って古い Scene を出す不成立への守りを外す。判断を誤ると、build は成功し Warning も出ずに古い出力が配られる）
- owner: root（依頼者）
- created: 2026-11-17
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」の「Unity の修正と 6000.6.9f1 での確認」の段落を、本スライスの実測に置き換える。「製品の build 経路での印」の節の扱い）、program §8 の S2-2c の行。18 章 §4・13 章の契約の書き換えは Phase B の差分に入る（§3）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§5.3、§8 の S2-2b・S2-2c の行、§8 の「Unity 6000.6.9f1 への更新から移送した入力」）。印を入れたスライスの HANDOFF は commit `88e9d8d9^` の `docs/handoff/CONTENT_PREFAB_STAMP.md`、harness は commit `2bc0c108` の `unity/Assets/OneStarMaker/Tests/Editor/Spikes/PrefabStamp/`（PR #193）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script を持たない。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: 未作成（A3 凍結の後に書く）

## 1. 目的と対象外

- 目的: 製品の build 経路で Content Directory を build する時、Scene に置いた prefab（入れ子・共有・variant を含む）と model の instance を編集した後に同じ `Library` で build し直しても、出力の Scene が build した時点の手元の版を焼き込んだものになることを保ったまま、build の間に利用者の source を書かない形にする。Unity 6000.6.9f1 の差分 build が自分でその編集を拾うなら、印と、印が持ち込んだ制約（印を書く Scene か前回の印の戻しの対象の Scene が Editor で開いていると build が止まる、build の間だけ Scene の `.meta` を書いて戻す、`Library` に戻しの記録を残す）を外す。拾わない場合が 1 つでもあれば印を外さない。cache を捨てず、prefab が変わっていない Scene の再利用を失わない。
- 利用者: 製品の build 経路（`BuildContentCoordinator`）で Content Directory を build する開発者（主に、実機へ転送する build をローカルの Editor で作る人）と CI。目的 build を作る人と、上書きを作る人の両方。build の呼び手（host application の Editor code）に新しい入力を求めない。
- 目的・利用者の文言の経緯: 2 欄は本書で初めて書いた文。利用者の欄は S2-2b の承認済みの欄（2026-11-15）と同じ文。2026-11-17 に owner（root）がこの 2 欄を承認した（§2.8 U1）。
- 対象外:
  - `Scripts/Runtime/`、coordinator、snapshot の作り方、SampleGame の変更。
  - Player の build（`BuildPipeline.BuildPlayer`）の差分 build。
  - prefab・model の instance 以外の理由で Scene の出力が古くなる経路（script の code・field の既定値、lightmap、sprite atlas、import 設定・stripping の設定の違い）。印もこれらを扱っていない。
  - override を持つ instance と unpack した instance の実機の確認（S2-2b も測っていない。§2.3）。
  - 別の PC・`Library` の間、CI の agent をまたぐ cache。
  - 6000.6.3f1 より前の Unity での動作。project の Unity は `ProjectVersion.txt` で 6000.6.9f1 に固定されており、版の検査は足さない（§2.7）。
  - 印の撤去より前の版の build が異常終了して残した印の自動の戻し（§2.8 U2）。
- 現況（base `4aef3dd4`）:
  - **印は製品の build 経路にある（S2-2b、PR #193、2026-11-15）。** `UnityContentDirectoryAdapter.Build`（`Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs`、261 行）が、`ValidateTarget` → `CleanupOrphans` → `ScenePrefabStamp.Prepare`（検査。止める理由があれば何も書かずに例外）→ `try` の中で `DisallowAutoRefresh` → `Apply`（前回の残りの戻しと印の書き込み）→ 生成 folder と marker → 製品の `AssetDatabase.Refresh()` → `AllowAutoRefresh` → root の照合と `BuildContentRoot` の作成 → Unity の build（internal の継ぎ目 `Func<BuildContentDirectoryParameters, UnityContentBuildResult>`）→ `finally` で段ごとに例外を受け止める片付け（`AllowAutoRefresh`、`stamp.Dispose`（印の戻しと `ImportAsset`）、生成物の削除）の順。印の I/O は `ScenePrefabStamp.cs`（355 行）、純粋な規則は `ScenePrefabStampRules.cs`（194 行、接頭辞 `osm-prefab-stamp-`）。test は `Tests/Editor/Build/ScenePrefabStampEditorTests.cs`（485 行）と `ScenePrefabStampRulesTests.cs`（197 行）。印の記録は `Library/OneStarMaker/ScenePrefabStamp/<Scene の GUID>.json`。
  - **印が持ち込んだ利用者に見える制約:** 印を書く Scene か前回の印の戻しの対象の Scene が Editor で開いている（変更の有無を問わない）、その `.meta` が読み取り専用、`userData` が空でも自分の印でもない、のどれかで build は `BuildFailure` で止まる（18 章 §4）。開いている Scene の `.meta` を書くと、次の Editor の更新で「modified externally」の dialog が main thread を止めることを S2-2b が観察した。
  - **印が無かった時の不成立は 6000.6.0f1 で測った。** prefab を編集した後に同じ `Library` で build すると、Scene の build instruction が変わらず前回の Scene の serialized file が再利用され、焼き込まれた prefab が古い版のまま出た（`Full` でも上書きでも。build は成功し Warning も出ない）。cache の鍵は Scene の file と `.meta` のバイトで、prefab の状態は鍵に入らなかった（[事実の文書](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「製品の build 経路で作った上書きと、prefab を編集した後の差分 build」）。
  - **Unity の修正と 6000.6.9f1 での最小の確認（2026-11-17、PR #198）。** Unity は 6000.6.3f1 の release note に、Scene が使う prefab だけを編集した後の Content Directory の差分 build がその Scene を作り直さない不具合の修正（Build System、UUM-151103）を載せている。release note の文が言うのは「prefab だけを編集した後の差分 build で Scene が作り直される」ことまでで、入れ子・variant・model、上書きの経路、作り直しの範囲（変わっていない Scene まで作り直すか）は言っていない。OSM を含まない最小の再現（入れ子でない prefab 1 つ、Editor の build、`CleanBuildCache()` なしの 2 回）では、6000.6.9f1 で 2 回目の出力に編集が入った。入れ子・共有・variant の prefab、model、製品の経路（印を外した状態）、上書き、再利用の範囲は測っていない。
  - **model の頂点は印なしでも入る（6000.6.0f1）。** Mesh は Scene に焼き込まれない。Scene に焼き込まれる model の階層と Transform の編集は、印の有無にかかわらず測っていない（事実の文書「印の形と検知の鍵」）。
  - **測り直しの harness が git にある。** commit `2bc0c108` の `Tests/Editor/Spikes/PrefabStamp/`（`PrefabStampHarness.cs` 416 行、`PrefabStampProcesses.cs` 426 行）。第 1 process が fixture（`Inner`・`Outer`（`Inner` を nest）・`Shared`、Scene `SOuter`・`SShared1`・`SShared2`・`SPlain`）を作り B1〜B6・B5' を製品の coordinator で build して record を書き、第 2 process が record と `Library/BuildHistory` から判定を計算し直す。印の文字列は `PrefabStampProcesses.cs` が `ScenePrefabStampRules.Prefix` を参照して探す（A2 R2 F7。撤去後は literal に置き換える。§4）。prefab の編集は `SaveAsPrefabAsset` で行い、build の前に import 済みにする。
  - **この repository の Scene の `.meta` に印は無い。** `unity/Assets` の `.meta` に `osm-prefab-stamp-` を含む物は無い（2026-11-17 に base で確認）。
  - **`run-tests.ps1` は Unity を `ProjectVersion.txt` の版で探す。** Harness の `ApprovedSpecifications.psm1` と `UnityChecks.psm1` は 6000.6.0f1 のまま残っているが（program §8 の移送）、本スライスは Harness の Unity gate を使わない。
- UC ごとの利用者の入力:

  **UC-1 Content Directory を build する**（目的 build と上書きの build。呼び手は host の Editor code か CI）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | （新しい入力なし）既存の build の入力（plan、snapshot、contentSet、mode 等） | 既存のまま | 目的の「呼び手に新しい入力を求めない」 |

  機械が決めるもの: なし（印の対象・値・記録・戻しが全て消える）。利用者の操作は減る: 印のために build の前に Scene を閉じる操作が要らなくなる（製品の検査が消える。対話の Editor で Unity の build 自体が開いている Scene に何をするかは確かめていない。§2.8 U4）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Unity 6000.6.9f1 で、印を外した製品の build 経路でも、Scene に置いた prefab（入れ子・共有・variant）と model の編集が、同じ `Library` の差分 build の目的 build と上書きの出力に入り、prefab が変わっていない Scene の再利用が残ると言えるか。言えるなら、印とその制約を外した製品の経路がそれを満たすか。

### 2.1 進める最低条件

**不変条件**（起きてはいけないこと）

- **I1 古い版を焼き込んだ Scene を出さない。** 印を外した build の出力のどの Scene の file にも、その build の直前の手元の source（disk に保存され Unity が import 済みの版。未保存の編集と Prefab Mode の編集は含まない）より前の prefab・model の版が残らない。ただし (e) の古い cache は本スライスの主張から除く（§2.3、§2.8 U3）。
  - 主張を崩しうる経路: (a) Unity の修正（UUM-151103）の範囲が入れ子でない prefab に限られ、入れ子の内側だけの編集（外側の prefab の file は変わらない）、variant の基の prefab の編集、model の階層の編集を取りこぼす。(b) 上書きの build は毎回新しい `BuildContentRoot` を root にするので、Scene の build instruction の作り方が目的 build と違う経路を通る。(c) `Full` も同じ `Library` の build cache を使う（6000.6.0f1 で `Full` でも古い版が出た）。(d) 前の版へ戻した source が、同じ鍵で古い cache の項目に当たる（戻した版の cache なら正しく、別の版の cache なら誤り）。(e) 印ありの build（S2-2b）と印の無い 6000.6.0f1 の build が残した cache を持つ `Library` で、`.meta` が元のバイトに戻った Scene が、印より前に作った古い cache の項目に当たる。
  - 探索した境界: release note の該当行の文言、事実の文書の 6000.6.0f1 の観察と 6000.6.9f1 の最小の再現、S2-2b の harness の build の列。未確認: Unity の差分 build の鍵が 6000.6.9f1 で何を含むか（trace から読めるのは作り直したかどうかまで）、(e) の古い cache（harness は毎回新しい fixture を作り GUID が新しいので当たらない。撤去で `.meta` が印より前のバイトに戻ることが、この経路を再び開く。A2 R2 F6）、override を持つ instance。
- **I2 build は利用者の source を書かない。** build の成否にかかわらず、adapter は Scene の file と `.meta`、既にある file の中身を書かず、`Library/OneStarMaker/ScenePrefabStamp/` に何も書かない。adapter の生成 folder は build の後に残らない。除くもの（印より前からの既存の経路で、本スライスで変えない）: 製品の `Refresh` が `.meta` の無い file に `.meta` を作る import、adapter が所有する生成 subtree（`CleanupOrphans` が消す前回の孤児、`DeleteIfEmpty` が消す空の `Assets/OneStarMakerGenerated`）（A2 R2 F3）。
  - 主張を崩しうる経路: (a) 印の code を一部だけ外し、`.meta` の書き込みか戻しの `ImportAsset` が残る。(b) 生成 folder の片付けが例外で止まる（S2-2b の段ごとの片付けを外すと、1 段の失敗で残りを飛ばす）。(c) 製品の `Refresh` か `BuildContentRoot` の import に、project の `AssetPostprocessor` が反応して別の file を書く（印と無関係な既存の経路）。
  - 探索した境界: adapter の base の手順、S2-2b の harness の source の照合（`Assets/` と `ProjectSettings/` の全 file の sha256）。未確認: host の project の `AssetPostprocessor`（SampleGame 以外）。
- **I3 保存していない編集を壊さない。** build の root の Scene が Editor で開かれて未保存の変更を持つ時も、build はその Scene の file と `.meta` を書かず、未保存の変更はそのまま残り、出力はその Scene の disk の版を焼き込む。
  - 主張を崩しうる経路: 印の撤去で検査が消え、開いている Scene を持つ build が Unity の build まで進む。製品の `Refresh` は変わった file だけを import するので開いている Scene を読み直さない見込み。Unity の build 自体が開いている Scene を保存させるか・読み直すかは、印より前の経路と同じで測っていない。
  - 探索した境界: adapter の手順、S2-2b の開いている Editor での観察（`.meta` を書かなければ dialog は出ない）。batch の実 build で 1 回観測する（§5 の B3o、A2 R2 F4）。未確認: 開いている Editor（batch でない）での実 build。

**成立条件**（起きなければならないこと）

- **S1 焼き込んだ prefab と model の編集が、印なしで同じ `Library` の差分 build の出力に入る。** 入れ子の内側だけの編集、複数の Scene が共有する prefab の編集、variant の基の prefab の編集、model の階層の編集を含む。目的 build（`Incremental` と `Full`）と上書きの build の両方。入れ子・共有・variant・model の各編集は、少なくとも 1 回ずつ `Incremental` の回で確かめる（A2 R2 F2）。
- **S2 prefab が変わっていない Scene は作り直されない。** prefab を変えずに build し直した時、Scene の serialized file は 1 つも書かれない。prefab を 1 つ編集した build は、それを焼き込む Scene だけを書く。prefab を前の版に戻した build は、その版で build した時と同じ Scene の file を出す。
- **S3 印とその制約が製品から消える。** 印を書く・戻す・記録する code と test が無く、開いている Scene・読み取り専用の `.meta`・空でない `userData` を理由に build が止まらない。

### 2.2 受け入れ条件

AC1〜AC5b は §5 の harness（印を外した head の製品の coordinator、Unity 6000.6.9f1、batch）で、AC6 は EditMode test で、AC7・AC8 は差分と機械検査で閉じる。「その回の版」は、その build の直前の手元の source の版をいう。

- **AC1（S1・I1）入れ子・共有・variant・model の編集が入る。** §5 の build の列 B1〜B3・B3o・B4・B4b・B5・B6・B5' の各回の出力に含まれる各 Scene の file が、その Scene が焼き込む prefab・model の、その回の版の識別値を全て持ち、他の版の識別値を持たない（B6 は上書きなので、出力に含まれる Scene だけを見る）。識別値は版ごとに一意で、prefab は子の位置の float 列、model は §5 の版ごとの object の名前である。Scene ごとに見るのは、`SOuter` は `Inner` と `Outer`、`SShared1`・`SShared2` は `Shared`、`SVariant` は `Shared`（variant の基）、`SModel` は `Model` の識別値だけで、`SPlain` はどの識別値も持たない（Y1）。合格例: B2（`Inner` だけ v2）で、`Inner` を `Outer` 経由でだけ焼き込む `SOuter` の file が `Inner` v2 の列と `Outer` v1 の列を持つ。B4b で `SVariant` の file が `Shared` v2 の列を持ち、`SModel` の file が `Model` v2 の識別値を持つ。不合格例: B2 の `SOuter` が `Inner` v1 の列を持つ（6000.6.0f1 で印なしに起きた形）。
- **AC2（S1・I1）上書きにも入る。** B6 は、B5 と同じ `Library` で、`Outer` だけを v1 に戻した source から、編集した prefab をもとに `ContentOverlaySelection` が逆引きした root だけを、別の contentSet の上書きとして build する。上書きの出力の `SOuter` の file が `Outer` v1 の列と `Inner` v2 の列を持ち、`Outer` v2 の列を持たず、sha256 が B2 の `SOuter` と一致する（(`Outer` v1, `Inner` v2) は B2 と同じ組み合わせで、直前の B5 とは違う）。逆引きした root は `SOuter` だけである（`2bc0c108` の harness も見ていた。X3）。
- **AC3（S2）再利用。** `Library/BuildHistory/<build>/BuildContentTEP.json` の `BuildPipeline::WriteSerializedFile` の event で、`Incremental` の回だけを数える（S2-2b と同じ数え方。`Full` は workspace を空にするので数えない）。B3（変更なし。Scene を開いていない）は Scene の serialized file を 1 つも書かない。B2 は Scene の serialized file を `SOuter` の分ちょうど 1 つ書き、B4b は `SShared1`・`SShared2`・`SVariant`・`SModel` の分ちょうど 4 つ書く（この 2 回は、書き出しの数え方が 6000.6.9f1 の trace で働いていることの対照を兼ねる。event が 1 つも見つからない時は B3 の 0 を合格に数えず、harness の欠陥とする。H1）。B3o（開いている未保存の Scene の回）の書き出しは AC3 に使わない。B4（`Inner` を v1 に戻す）の各 Scene の file の sha256 が B1 と一致する。B4 の sha256 だけが B1 と違い、識別値と書き出しの数が期待どおりの時は、再利用の喪失ではなく §2.5 の「識別値の外の差」に入れる（X4）。合格例: B2 で書いた Scene が `SOuter` だけ。不合格例: B2 で `SShared1` や `SPlain` も書かれる（Unity が prefab の編集で Scene を広く作り直す形。出力は正しいが再利用を失う）。
- **AC4（S1・I1）clean な build と同じ。** B5 の出力の各 Scene の file が、`BuildPipeline.CleanBuildCache()` の後に同じ source を `Full` で build した B5' の Scene の file と同じ sha256 である。Scene 以外の file の B5 と B5' の差は record に残すが、合否に使わない（印と無関係にも起きる。事実の文書「build をまたぐ同じ file」）。
- **AC5（I2）source を書かない。** harness の各 build で、`coordinator.Build` が戻った直後（harness が `Refresh`・`ImportAsset` を呼ぶ前）に、`Assets/` と `ProjectSettings/` の全 file の sha256 が build の前に harness が記録した値と一致し、fixture の各 Scene の file と `.meta` の更新時刻が変わらず、adapter の生成 folder が残っておらず、`Library/OneStarMaker/ScenePrefabStamp/` が run の始めと同じ状態（無ければ無いまま）である。全ての build の出力に `osm-prefab-stamp-` の文字列が無い。照合から除くのは I2 の「除くもの」だけで、harness はそれを避けるため fixture の folder と marker を build の前に import しておく（前例どおり）。
- **AC5b（I3）開いている未保存の Scene の実 build（batch）。** B3o は、B3 の後に、`SShared1` を開いて root の GameObject の名前を変えた未保存の状態で、source を変えずに `Incremental` で build する。build は成功し、`SShared1` の file と `.meta` の sha256 が build の前と同じで、build の後も `SShared1` が開かれたまま `isDirty` で変更が残り、出力の `SShared1` の file が変えた名前の文字列を持たない（disk の版を焼き込む）。この回の書き出しの数は合否に使わない（X1）。対話の Editor（batch でない）での実 build はこの AC で確かめたことにしない（§2.8 U4）。
- **AC6（I2・I3・S3）開いている Scene と失敗経路（EditMode test、adapter の Unity の build の継ぎ目を差し替える）。** (a) root の Scene（閉包に prefab を持つ）を Editor で開いて未保存の変更を持たせ、その `.meta` を読み取り専用にし、build（継ぎ目は成功を返す）すると、build は成功し、継ぎ目は 1 回呼ばれ、Scene の file と `.meta` の sha256 が変わらず、未保存の変更が Editor に残っている。(b) root の Scene の `.meta` の `userData` が空でない時も build は成功し、`.meta` は変わらない。(c) 継ぎ目が失敗を返した時と例外を投げた時、build は失敗し、生成 folder が残らず、root の Scene の file と `.meta` の sha256 が変わらない。
- **AC7（S3）印が消える。** 差分の後の `unity/Assets/OneStarMaker/Scripts/` と `Tests/`（harness を除く）に `ScenePrefabStamp`・`osm-prefab-stamp-`・`DisallowAutoRefresh` の参照が無い。18 章 §4 と 13 章から、印の手順・止める条件・記録の場所の記述が消え、代わりに §2.7 の契約の文がある。`unity/Assets` の `.meta` に `osm-prefab-stamp-` を含む物が無い。
- **AC8（全体）非退行と範囲。** 全 EditMode 回帰が通る（XML の failed 0 と root result `Passed`）。implementation base から head までの差分は、§3 の file（code・test・18 章・13 章）、§5 の harness、Phase A 以降の HANDOFF と program §8 の S2-2c の行だけで、`Scripts/Runtime/`、coordinator、SampleGame の source、事実の文書を変えない（X5）。

### 2.3 ここでは答えない問いと所有する後続

- override を持つ instance・unpack した instance・実データ（SampleGame）での確認: S5（実データの接続）。印も測っていない範囲で、本スライスの撤去で守りを失う範囲は「印の規則が理屈の上で含んでいた override」だけである。
- Player の build の差分 build で同じ取りこぼしが起きるか: 所有 program 未定（program §8 の S3 の Player の非退行の入力）。
- 印ありの build や 6000.6.0f1 の build が残した cache を持つ `Library` での古い出力（I1 (e)）: 所有 program 未定。利用者の手元の `Library` の扱いは S5 の明示回復の入力に送る。本スライスでの文書の扱いは §2.8 U3。
- 別の PC・`Library`、CI の agent をまたぐ cache: S4。
- 6000.6.3f1 より前の Unity を拒む検査: 所有 program 未定（install と検証の受け入れ版の扱い、program §8 の S4 の移送と一緒に決める）。

### 2.4 スライス分割の判断

- 契機: 高リスクの初回実機スライスで、独立して失敗しうる未知が複数ある（入れ子・共有・variant・model のそれぞれを Unity が拾うか、上書きの経路、再利用の範囲）。測定（印を外した経路で古い出力が出ないか）と撤去（code と文書の削除）は変更理由も違う。
- 判断: 分割しない。未知は全て同じ 1 つの build の列の各回で別々に判定でき（AC1〜AC4 は Scene・回ごとに合否が出る）、測る経路は撤去後の製品の経路そのものなので、測定だけを先に別スライスにすると、印を外す継ぎ目を製品に足すか、撤去と同じ差分を 2 回作ることになる。撤去の差分は削除が主で小さい。測定が不成立（古い出力が 1 つでも残る）なら、本スライスは撤去を merge せずに終わり、測定の結果だけを残す（§2.5）。印を残す範囲の再設計はその結果を入力にした新しい revision か後続で行う。

### 2.5 判定定義と停止規則

- GO: §2.1 の不変条件と成立条件が、§2.2 の AC で全て成り立つ。
- 発見 C の限定実行で返す時の証拠: 進行役は、返す前にその実行の record・XML・log を evidence に固定する（判定に使う証拠と同じ扱い。H2）。owner は、その証拠で決めるか、判定 C の実行を求めるかを選べる。
- 帰属の確認（A2 R2 F1）: AC1〜AC4 の不成立を「答え」と扱うのは、次の 2 つが両方成り立つ時だけである。(1) B5'（cache を捨てた build）の全 Scene の識別値が期待どおりで、harness の識別値と fixture が働いている。(2) 不成立の回の record で、その回に harness が書いた asset の file の sha256 が harness の書いたバイトと一致し、その asset の `GetAssetDependencyHash` が直前の回と違い（import が新しいバイトを見ている）、その回に変えない file の sha256 が直前の回と同じである（source の編集は届いている）。入れ子の外側や variant の `GetAssetDependencyHash` は、file を変えなくても内側・基の編集で変わる（事実の文書「印の形と検知の鍵」）ので、変わることも変わらないことも要求しない（X2）。どちらかが崩れていれば harness か adapter の欠陥として B へ戻す。発見 C の限定実行で owner へ返す経路にも、先にこの確認を当てる。
- NO-GO（答え・古い出力）: 帰属の確認の後、AC1・AC2・AC4 のどれかで、Scene の file がその回より前の版の識別値を持つか、その回の版の識別値を欠く。6000.6.9f1 では印を外せない。実装の欠陥ではないので B への差し戻しにせず、判定 C（発見 C の限定実行で先に分かった時はその時点）で止め、owner に返す。
- 答え・識別値の外の差: 識別値は全て期待どおりで、AC2・AC4 の sha256 だけが違う。古い出力の証拠ではないので、差の出た file と回を添えて owner に返し、撤去を続けるかを owner が決める（Phase A の新しい revision か決定記録）。
- 答え・再利用の喪失: AC1・AC2・AC4 は成り立ち、AC3 だけが成り立たない（出力は正しいが、変わっていない Scene も作り直す）。owner に返し、撤去を続けるかを owner が決める（A2 R2 F8、R3）。古い出力の時は撤去の差分を merge しない。測定の record は evidence に残し、事実の文書への反映と、印を残す範囲の決定は owner が決める（本スライスの外の文書の変更か、新しい revision。program §8 の「残す範囲を決める」は、本スライスが結果を返した後の owner の決定として読む。A2 R3）。B は AC1〜AC4 を満たすために印の一部を戻したり harness の期待値を変えたりしない。
- NO-GO（欠陥）: AC5・AC5b・AC6〜AC8 のどれかが成り立たないか、帰属の確認が崩れる。通常の差し戻し。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。

### 2.6 A3 後の例外承認・決定記録

- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- **adapter の手順:** `ValidateTarget` → `CleanupOrphans` → 生成 folder と marker → 製品の `AssetDatabase.Refresh()` → root の照合と `BuildContentRoot` の作成 → Unity の build → `finally` の片付け。`ScenePrefabStamp` の生成・`Prepare`・`Apply`・`Dispose`、`DisallowAutoRefresh` / `AllowAutoRefresh`、記録の置き場の引数を外す。`DisallowAutoRefresh` は S2-2b が印の `.meta` を製品の `Refresh` より前に import させないために足した物で、印より前の adapter には無かった（生成 folder と marker は製品の `Refresh` で import され、自動 refresh で先に import されても build の入力は変わらない。A2 R3 の確認）。
- **残すもの:** Unity の build の internal の継ぎ目（`Func<BuildContentDirectoryParameters, UnityContentBuildResult>`、`UnityContentBuildResult`）は AC6 の test に使うので残し、internal のコンストラクタの引数から記録の置き場を外す。`try` を生成 folder の作成の前から始める形と、`finally` の片付けを段ごとに受け止めて最初の例外を投げ直す形は、印と独立に I2 (b) を守るので残す。
- **消すもの:** `ScenePrefabStamp.cs`・`ScenePrefabStampRules.cs` とその `.meta`、`ScenePrefabStampEditorTests.cs`・`ScenePrefabStampRulesTests.cs` とその `.meta`。adapter の印に触れる comment。
- **版の検査は足さない:** 印を外した経路は Unity 6000.6.3f1 以降の差分 build に依拠するが、project の版は `ProjectVersion.txt` で固定されているので、adapter に Unity の版の検査を足さない。依拠することは 18 章 §4 に書く。
- **残った印の扱い（§2.8 U2、2026-11-17 の owner 決定）:** 撤去より前の版の build が異常終了して `.meta` に残した印を戻す code は残さない。18 章 §4 に、`userData` が `osm-prefab-stamp-` で始まる `.meta` は撤去より前の異常終了の残りで、`userData` を空に戻してよく、`Library/OneStarMaker/ScenePrefabStamp/` は消してよいと書く。
- **18 章 §4 の書き換え:** 「Scene に焼き込む prefab の版の印」の項を、「Scene に焼き込む prefab と model の編集」の項に置き換える。中身は、adapter は Scene の file と `.meta` を書かず、焼き込む prefab・model の編集が差分 build に入るのは Unity（6000.6.3f1 以降、UUM-151103）の差分 build に依拠すること、確かめた範囲と測っていない範囲は実測の事実にあること（開いている未保存の Scene の build は batch で確かめ、対話の Editor では確かめていないことを含む。U4）、上の残った印の扱い、U3 の古い cache の扱い。印の版の履歴（S2-2b で入れ S2-2c で外した）は 1 文に留める。
- **13 章の書き換え:** 「既知の不成立」の「Scene に置いた prefab の編集」の行を、Unity の修正で解消し印を外したこと、印を書く Scene が開いていると止まる制約が無くなったことに直し、契約は 18 章 §4、実測は事実の文書へ送る。
- **事実の文書:** 本スライスの実測の結果は、merge 前のこの PR の Phase D の harvest の commit で書く（結果に依存するので Phase B の受け入れ条件にしない。program §8 の「同じ PR で直す」はこれで満たす。X5）。Phase B は事実の文書を変えない。
- 本スライスは public な型・メソッドと issue code を増やさず、減らさない（`ScenePrefabStamp*` は internal）。

### 2.8 owner の決定と未決事項

- **U1 目的・利用者の文言の承認:** 2026-11-17 に owner が §1 の 2 欄を承認した。
- **U2 撤去より前に残った印:** 2026-11-17 に owner が (a) を選んだ。(a) **推奨:** 戻す code を残さず、18 章 §4 に見分け方と戻し方を書く。印が残るのは 2026-11-15〜撤去の間に印ありの版で build が異常終了し、その後に印ありの版で build しなかった `.meta` だけで、この repository の `.meta` には無い（§1 現況）。残っても `userData` の値が cache の鍵に入るだけで、出力の正しさは変わらない（S2-2b の AC6）。(b) 記録と `userData` の形から戻す処理（S2-2b の「残りの戻し」）だけを残す。残した印を戻せるが、`.meta` を書く経路と、開いている Scene で止める検査も残り、S3 と両立しない。
- **U3 古い cache を持つ `Library`（I1 (e)、A2 R2 F6、X）:** 2026-11-17 に owner が (a) を選んだ。(a) **推奨（外部計画レビューの限定を採った版）:** 18 章 §4 に「6000.6.3f1 より前の Unity で Content Directory を build したことのある `Library` は、印の無い `.meta` を鍵にした古い Scene の cache を持つことがあり、印を外すとその鍵に戻る。当たると build は成功し Warning も出ずに古い Scene を出す。そうした `Library` では撤去の後に `BuildPipeline.CleanBuildCache()` を 1 回実行する」と書く。「疑う時」とは書かない（この失敗は Warning を出さない）。6000.6.9f1 で印ありの build だけをした `Library` は条件にしない（その鍵は印のある `.meta` の側にある）。build の手順には入れない。(b) 何も書かない（撤去が開き直す無警告の経路が契約の文書から消える）。
- **U4 対話の Editor での実 build（外部計画レビュー、OpenAI）:** 2026-11-17 に owner が (a) を選んだ。(a) **推奨:** 追加の確認をせず、18 章 §4 と事実の文書に「開いている未保存の Scene の build は batch で確かめ、対話の Editor では確かめていない」と書く。印の撤去で外れるのは製品の検査（`.meta` を書く前に止める）で、対話の Editor で Unity の build 自体が dirty な Scene に何をするかは、印より前と同じ Unity の振る舞いで、本スライスが変えるものではない。(b) 判定 C の前に、対話の Editor で dirty な Scene を開いて製品の実 build を 1 回実行し、未保存の状態・disk・出力・dialog を観察する smoke を足す（Editor の eval は build 中に捨てられることがあり、観測の経路の確認が要る）。
- 未決事項: なし。

## 3. 責務マップ

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `Scripts/Editor/Build/Content/UnityContentDirectoryAdapter.cs` | Unity の Content Directory build の唯一の adapter（生成 root の作成と片付け、build の継ぎ目）。印の呼び出しを外す | 印の撤去 | 既存（1 回の build） | 既存から `ScenePrefabStamp` を外す | internal（コンストラクタの引数を 1 つ減らす） | AC6 の EditMode test | 既存 | 261 → 約 225 |
| `Scripts/Editor/Build/Content/ScenePrefabStamp.cs`・`ScenePrefabStampRules.cs`（削除） | — | 印の撤去 | — | — | internal | — | — | 549 → 0 |
| `Tests/Editor/Build/ScenePrefabStampEditorTests.cs`・`ScenePrefabStampRulesTests.cs`（削除） | — | 同上 | — | — | — | — | — | 682 → 0 |
| `Tests/Editor/Build/UnityContentDirectoryAdapterTests.cs`（新規） | AC6 (a)〜(c) の EditMode test | — | test ごとの一時 folder | adapter の internal のコンストラクタ | — | 開いている Editor で走る | 既存の `Tests/Editor/Build` | 0 → 約 150 |
| `Tests/Editor/Spikes/NoStamp/`（新規、Phase D で削除） | AC1〜AC5 の実 build の harness（`[Explicit]`）。commit `2bc0c108` の `PrefabStamp/` を戻し、§5 の fixture を足し、印に固有の観測を外す | — | process ごとの record | 製品の coordinator・`ContentOverlaySelection` | — | — | 実 build の確認は製品の test に残さない前例（S2-0〜S2-2b） | 0 → 約 900 |
| `Docs/Architecture/18-asset-description.md` §4・`13-resource-system.md` | §2.7 の書き換え | 契約の変更 | — | — | — | AC7 | 既存 | 18 章の印の項（22 行）→ 約 8 行、13 章 1 行 |

- 行数警報: adapter は減る。harness は 2 file に分け、どちらも 500 行未満にする（第 1 process の fixture と build、第 2 process の判定。前例と同じ分け方）。
- 依存の向きと asmdef は変えない。`OneStarMaker.Build.Content` から型が減るだけ。

## 4. 実装計画

- 順序: (1) adapter から印を外し、印の 2 file と test 2 file を消す。(2) `UnityContentDirectoryAdapterTests`。(3) 18 章 §4・13 章。(4) harness（`2bc0c108` の `PrefabStamp/` から戻して改める）。
- Phase B から Phase A へ差し戻す条件: §2.7 の外の変更が要る時（coordinator・public な型・Runtime の変更、印の一部を残す、版の検査を足す）。
- harness の改め（B 適応で行ってよい範囲）: `ScenePrefabStampRules.Prefix` の参照を literal `osm-prefab-stamp-` に置き換える。印に固有の観測（`userData` の戻し）を外す。§5 の fixture・build の列・record の値を足す。期待値は fixture の source から導く。
- Phase B がしないこと: Unity の build を伴う実行（harness の実行は Phase C）。AC1〜AC4 の期待値を実装の出力から作ること。
- 対象外を維持する方法: `Scripts/Runtime/`、coordinator、SampleGame を変えない（AC8）。

## 5. テストとレビュー計画

- 単体テスト: 新しい中核ロジックは無い（削除と手順の短縮だけ）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Build`（C が根拠付きで変更可）。
- 判定必須テスト（GO 候補 head）。別々の Unity process で、この順に:
  1. `pwsh tools/run-tests.ps1`（全 EditMode 回帰）。合格は XML の failed 0 と root result `Passed`（`step.json` は VFX の Inconclusive で常に `failed` なので使わない）。他の Unity を止めてから実行する。
  2. harness の第 1 process（fixture を作り、B1〜B3・B3o・B4・B4b・B5・B6・B5' を build し、record を書き、fixture を消す）。
  3. harness の第 2 process（record と写した `Library/BuildHistory` から AC1〜AC5 を計算し直す）。
  - 2 と 3 は XML の total が 1 以上で failed 0、record に終了行があることを harness が最後まで動いた確認とし、判定 C が record を AC ごとに読む。process の自然終了をゲートにしない（既知の終了 stall は `tools/Harness/README.md` の規則）。
- fixture と build の列（名前は Phase B が決めてよい。S2-2b の列に `SharedVariant` と `Model` を足した）: 一時 folder に、内側の prefab `Inner`（子の位置 v1 (1,2,3) / v2 (4,5,6)）、`Inner` を nest した `Outer`（自分の子 v1 (7,8,9) / v2 (10,11,12)）、共有の `Shared`（v1 (13,14,15) / v2 (16,17,18)）、`Shared` の variant `SharedVariant`（override を持たない）、`.obj` の model `Model`（v1 は共通の object 1 つと v1 だけの object 1 つ、v2 は共通の object 1 つと v2 だけの object 1 つ。識別値は版だけが持つ object の名前の文字列で、Scene に焼き込まれる子の GameObject の名前として出る見込み。§5 の未知の経路。Y1）。Scene は `SOuter`（`Outer` の instance）、`SShared1`・`SShared2`（どちらも `Shared` の instance）、`SVariant`（`SharedVariant` の instance）、`SModel`（`Model` の instance）、対照の `SPlain`（prefab なし）。B1: 全て v1 で `Full`。B2: `Inner` だけ v2、`Incremental`。B3: 変更なし、`Incremental`（Scene を開いていない）。B3o: B3 の後、変更なし、`SShared1` を開いて未保存の変更を持たせた状態で `Incremental` で build する。build の後、変更を捨てる前に、`SShared1` が開いているか・`isDirty`・file と `.meta` の sha256 を record に書き、その後に変更を捨てて閉じる（AC5b。Y2）。B4: `Inner` を v1 に戻す、`Incremental`。B4b: `Shared` と `Model` を v2、`Incremental`（共有・variant の基・model の編集を `Incremental` で見る回。A2 R2 F2）。B5: `Inner`・`Outer` も v2 にして全て v2、`Full`。B6: B5 の後、`Outer` だけ v1 に戻した source から、`ContentOverlaySelection` で逆引きした root を、別の contentSet の上書きとして build。B5': B6 で戻した `Outer` を v2 に戻し、`CleanBuildCache()` の後に B5 と同じ source を `Full` で build。harness は file を書いた後、snapshot を作る前に、書いた asset を import させる（`SaveAsPrefabAsset` か、`.obj` は書いてから `ImportAsset`。製品の snapshot の前提）。期待値は fixture の source の値から導き、実装の出力から作らない。
- record が必ず持つ値: head、Unity の版（`Application.unityVersion`）、各 build の直前の fixture の prefab・model の file の sha256 と `AssetDatabase.GetAssetDependencyHash`（帰属の確認、A2 R2 F1）、B3o の `SShared1` の開いている状態と `isDirty`、各 build の identity・mode・root、build の前と `coordinator.Build` の直後の各 Scene の file と `.meta` の sha256・更新時刻、`Assets/` と `ProjectSettings/` の全 file の sha256、`Library/OneStarMaker/ScenePrefabStamp/` の有無と中身の一覧、生成 folder が残っているか、各出力の file の名前と sha256、各 Scene の file の識別値の有無、`BuildContentTEP.json` の該当値（Scene の serialized file を書いた event の数と対象）、終了行。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の 2 と 3。
- 発見 C の限定実行: harness が最後まで動く確認として 2 と 3 を各 1 回実行してよい。この実行で AC1〜AC4 のどれかが不成立なら、§2.5 の帰属の確認を当て、答えなら判定 C を待たずに進行役が owner に返す。
- 操作・実行時・目視条件の検証経路: 全て上の Unity バッチと EditMode test で閉じる。担当は Phase C の実行担当で、sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1 -Filter <完全名>` を起動する。初期状態は Editor を閉じ、active build target が StandaloneWindows64 の Player、作業ツリーが clean。目視は無い。証拠は record・XML・log を evidence bundle に入れる。動作確認は `対象なし`。
- 未知の操作経路:
  - `2bc0c108` の harness が 6000.6.9f1 で compile し最後まで動くか: 未確認。初回確認は発見 C の限定実行（Unity の build を伴う）。compile の不成立は B が Editor の compile で先に直す（B 適応）。
  - `.obj` の版だけが持つ object の名前が、Scene の出力の file に子の GameObject の名前として出るか（AC1 の model の識別値）: 未確認。初回確認は Phase B（Editor で `.obj` を import し、Scene に置いた instance の子の名前を見る。Unity の build を伴わない）。不成立時: B が、Scene に焼き込まれる階層か Transform を変える別の model の編集を選び、識別値を source から導く形にする（B 適応。§6 に記録）。頂点だけの編集は Mesh の file に入り Scene の file に入らないので、識別値に使わない。
  - 成立済み: 実 build の harness を EditMode の `[Explicit]` test から別の Unity process で流す経路、`.cf` のバイト走査で float 列を探す観測、`BuildContentTEP.json` の読み取り（S2-2b、6000.6.0f1）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（build 9 回と `CleanBuildCache()` 1 回。`CleanBuildCache()` は実行した PC の `Library` の build cache を全て捨てるので、判定 C の実行担当はその後の build が遅くなることを受け入れる）。
- 判定 C の照合 script の方式: 判定 C の offline 実行（harness の第 2 process）。期待値は fixture の source の値から導く。機械向けの期待値 file は持たない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC7 の `grep`。
- A0/A1 主担当・モデル・ベンダー: 本 session（Anthropic）。A0 は program §8 の S2-2c の行と「Unity 6000.6.9f1 への更新から移送した入力」、事実の文書の該当 3 節、S2-2b の HANDOFF（`88e9d8d9^`）、base の adapter と印の source、`2bc0c108` の harness を入力とした。
- C' 用に予約した担当・モデル・ベンダー: なし。Phase A に Anthropic（A1・A2・A3）、xAI（外部計画レビュー、cursor の Grok 系）、OpenAI（外部計画レビュー、owner 経由）の全系列が関与した。C' は Phase B・C に関与していない担当（候補は Codex CLI。使う前に最小のプローブで確かめる）で行い、`独立性制約あり` と記録する。
- 独立性の強化条件を満たせない場合の理由: Phase A で使える全系列（Anthropic・xAI・OpenAI。Google 系は使えない）が関与したため、Phase A に未関与の系列は残っていない。

### 5.1 A2 の独立レビューと A3 の採否

- A2 入力版: commit `675a5ce7`（HANDOFF だけ）。HANDOFF の固定 copy の sha256 `35e58132dd72fa0602cbe742e4c75b0486bad05a5dea3c29a7bb1dd85f6710dd`。base は `4aef3dd4`。
- 目的レビューの入力: §1 の目的と利用者の欄の原文。sha256 `6b364f73eace586226719c245a4490508e018f86f564f5540fdfca5ebe67ecb5`（目的欄の本文、LF、利用者欄の本文。UTF-8、BOM なし、末尾改行なし、1,241 bytes）。
- A2 独立レビュー（2026-11-17、3 件とも別の新しい subagent で、互いの回答を見せていない。担当は全て Anthropic の系列）:
  - R1 目的レビュー: 入力は上の原文だけ（ツールを使わせていない）。問いは定めの 2 つ。
  - R2 アーキテクチャゲート: 入力は A2 入力版の HANDOFF の固定 copy、`architecture-gates.md`、`AGENTS.md`、`675a5ce7` の checkout と `2bc0c108` の harness。枠として「§3・§4 の変更は実装済みと仮定し、現コードに無いことを欠陥の根拠にしない」を指示し、不変条件を破る順序、AC の識別力、判定定義の穴も求めた。
  - R3 A0 だけからの代替構成: 入力は A0 の抜粋（§1、§2.8、program §8 の S2-2c の行と 6000.6.9f1 の移送、事実の文書の該当節。初稿の §2〜§5 を含まない）だけ。R1 とは別の担当で、目的レビューのやり直し用の未関与の担当は残っている。
- A3 統合担当: 本 session（Anthropic、A1 と同じ主担当）。owner は A3 の統合までを指示し、凍結はまだとした（2026-11-17）。採否:

  | 指摘 | 内容 | 採否 | 理由・反映先 |
  |---|---|---|---|
  | R1 | 保証すべきこと（4 種の編集が入る、事実で決め 1 つでも拾わなければ残す、制約を全て外す、source を書かない、cache を捨てない、目的 build と上書きで同じ、呼び手を変えない、印の残った `Library` から移っても壊れない） | 一部採用（X3 で直した） | 「印の残った `Library` から移っても壊れない」以外は I1〜I3・S1〜S3・AC1〜AC8 が持つ。本スライスが保証するのは、本スライスの build が同じ `Library` で続ける差分 build で、6000.6.3f1 より前の cache を持つ `Library` は I1 (e) のとおり主張の外（U3 は案内で保証ではない）。U2 は残った `userData` の扱いで、出力の正しさの保証ではない。目的の欄の文言は変えない |
  | R1 | 作らないもの（新しい入力・操作・source を書く別の仕組み・保険の cache 全消し・独自の依存追跡・二重の経路・実行時の確認） | 採用（初稿に該当なし） | U3 (a) の `CleanBuildCache()` は、6000.6.3f1 より前の Unity で build したことのある `Library` への案内で、build の手順に入れない（Y3） |
  | R2 F1 | 「答え」の判定に harness・adapter の欠陥が紛れる。source の状態が record に無い。識別値が正しく sha256 だけ違う時の扱いが無い | 採用 | §2.5 に帰属の確認（B5' の識別値と、prefab・model の sha256・`GetAssetDependencyHash`）と、識別値の外の差の分類を足した。record の値に足した。発見 C の限定実行にも当てる |
  | R2 F2 | 共有・variant・model の編集が `Full` の回にしかなく、`Incremental` の S1・S2 の証拠が無い | 採用 | B4b（`Shared`・`Model` を v2、`Incremental`）を足し、AC1・AC3 に期待を書いた |
  | R2 F3 | 製品の `Refresh` の `.meta` 作成と生成 subtree の片付けが「`Assets/` の全 file が同じ」を破る | 採用 | I2 を adapter が書かない物に絞り、既存の経路を除くと明記した。AC5 の照合は前例どおり fixture を先に import して行う |
  | R2 F4 | 開いている未保存の Scene を実 build で測っていない。「手元の source」が未保存の編集を含むか曖昧 | 採用 | I1 に「手元の source は disk の import 済みの版」と書いた。B3 の後の B3o で `SShared1` を開いて未保存のまま build する AC5b を足した（B3 と分けたのは X1） |
  | R2 F5 | 編集を import 済みにしてから build するので、製品の `Refresh` が外れても合格する。import していない編集を 1 つ入れる | 不採用 | 製品の snapshot は import 済みの source を前提に作り（S2-2b §1 の対象外）、import していない編集は印の有無と無関係な前提の変更で、本スライスの問いの外。`Refresh` を残すことは AC7・差分の確認で見る |
  | R2 F6 | 撤去で `.meta` が印より前のバイトに戻り、古い cache の項目に当たる経路が再び開く | 採用（文と U3） | I1 で (e) を主張から明示的に除いた。18 章への案内の文を U3 として owner に問う |
  | R2 F7 | harness は `ScenePrefabStampRules.Prefix` を参照している | 採用 | §1 現況を直し、literal への置き換えを §4 の B 適応の範囲に書いた |
  | R2 F8 | AC3 だけの不成立を「外せない」と呼ぶのは価値判断 | 採用 | §2.5 で「再利用の喪失」を分け、owner が決めるとした |
  | R3 | 問いを印ありとの比較（同等か）で立てる。同じ列を印ありの base でも回す | 不採用 | 期待値は fixture の source から絶対に導け、B5'（cache を捨てた build）が対照になる。印ありの列を 6000.6.9f1 で回す分の build を増やさない。再利用の比較の要否は R2 F8 の分類で owner が決める |
  | R3 | 「1 つでも拾わなければ外さない」（A0）と「残す範囲を決める」（program）の食い違い | 採用 | §2.5 で、本スライスは結果を返し、範囲は owner がその後に決めると読むと書いた |
  | R3 | 2 段の入れ子（`Mid`）、上書きを入れ子と variant で各 1 回、戻しの回を足す | 不採用 | 1 段の入れ子で「外側の file が変わらない内側の編集」の経路を通る。戻しは B4、上書きは B6 で、上書きは目的 build と同じ adapter を通る。build の回を増やさない |
  | R3 | 6000.6.0f1 の Editor で撤去 head の B2 が v1 になる感度の対照 | 不採用 | 6000.6.0f1 の印なしの不成立は事実の文書で測り済みで、識別値の働きは B5' と B4b の帰属の確認で見る。旧版の Editor を用意する費用に見合わない |
  | R3 | `DisallowAutoRefresh` は生成 folder のためにも要るかもしれない | 採用（確認して外す） | 印より前の adapter に無く、S2-2b が印のためだけに足した。§2.7 に理由を書いた |
  | R3 | 最も安い確認として、OSM を含まない最小の project で入れ子・variant・model を先に測る | 不採用 | Phase A・B は Unity の build を行わない。発見 C の限定実行が撤去 head の製品の経路で同じ確認を最初に行い、答えなら判定 C を待たずに owner へ返す（§5） |
  | R3 | 事実の文書は 6000.6.0f1 の節を書き換えず新しい節を足す | 採用（harvest の方針） | Phase D の harvest で、6000.6.0f1 の観察は残し 6000.6.9f1 の結果を足す |

- 凍結前の外部計画レビュー 1 巡目（2026-11-17、PR #199。X: cursor のコメント 2 件（同じ所見の要約と本文）、担当は xAI の Grok 系、入力は `c7d05692` の HANDOFF と base の code・事実の文書。O: owner が貼った OpenAI の追加レビュー、入力は同じ head）と、主担当が Phase C・C' の観点で行った点検（H）の採否:

  | 指摘 | 内容 | 採否 | 理由・反映先 |
  |---|---|---|---|
  | X1 | B3 が再利用（Scene を書かない）と開いている未保存の Scene の実 build を同じ回で数え、Unity が書き出すと「再利用の喪失」と「欠陥」のどちらにも落ちる | 採用 | B3 は Scene を開かずに数え、未保存の回は B3o に分けた。B3o の書き出しは AC3 に使わない（AC3・AC5b・§5） |
  | X2 | 帰属の確認で外側・variant の `GetAssetDependencyHash` を file の版と同一視すると、正しい結果ほど欠陥になる | 採用 | 帰属の確認を、書いた file の sha256、その asset の dependency hash が直前と違うこと、変えない file の sha256 が同じことに限った（§2.5） |
  | X3 | R1 の「印の残った `Library` から移っても壊れない」は、採用先が保証していない | 採用 | §5.1 の R1 の行を一部採用に直した。目的の欄は変えない |
  | X U3 | U3 は (a) を、6000.6.3f1 より前の `Library` に限り、「疑う時」と書かずに | 採用（推奨案を直した。2026-11-17 に owner が (a) を選んだ） | §2.8 U3 |
  | X4 | B4 の sha256 だけが違う時は「識別値の外の差」 | 採用 | AC3 |
  | X5 | AC2 に逆引きの root が `SOuter` だけであること。AC8 の文書を Phase B の文書と HANDOFF・program に分ける。事実の文書は merge 前のこの PR の Phase D で | 採用 | AC2・AC8・§2.7 |
  | X6 | model の識別値（子の名前）を凍結前に Editor で見ておく | 不採用（凍結前には行わない） | Unity の build を伴わない確認で、不成立時の B 適応（識別値の選び直し）を §5 に定めてある。Phase B の最初の確認に置いたまま凍結しても、判定の意味は変わらない |
  | O1 | OpenAI が Phase A に関与したので、C' の予約（OpenAI）と食い違う | 採用 | §5 の予約を外し、C' は `独立性制約あり` で行うと書いた |
  | O2 | 対話の Editor での実 build は batch の AC5b から推論できない | 採用（2026-11-17 に owner が (a) を選んだ） | §2.8 U4。smoke は足さず、18 章と事実の文書に対話の Editor は未確認と書く。AC5b に「対話の Editor は確かめたことにしない」と書いた |
  | H1 | B3 の「Scene を 1 つも書かない」は、6000.6.9f1 で trace の event 名が変わり何も数えられない時にも合格する | 採用 | B2・B4b の書き出しを「ちょうど」の数にし、対照を兼ねさせた。event が見つからない時は harness の欠陥とする（AC3） |
  | H2 | 発見 C の限定実行で owner に返す時、その実行の証拠の扱いが無い（前例では限定実行の結果を判定に使わない） | 採用 | §2.5 に、返す前に evidence に固定し、owner が判定 C を求めてもよいと書いた |
  | H3 | `.obj` を書いた後に import させないと、製品の snapshot の前提（import 済み）を外れ、R2 F5 で不採用にした経路を harness が踏む | 採用 | §5 の fixture に、書いた asset を snapshot の前に import させると書いた |

- 凍結前の外部計画レビュー 2 巡目（2026-11-17、PR #199。Y: owner が貼った xAI の Grok 系のコメントと誤変換の訂正、入力は `05dcdfe5` の HANDOFF と base の code。owner はこれを最後の外部レビューとした）の採否:

  | 指摘 | 内容 | 採否 | 理由・反映先 |
  |---|---|---|---|
  | Y1 | AC1 の model の識別値が v2 だけの名前で、v1 の回の `SModel` の合否が空になる。識別値を Scene ごとに書く | 採用 | model の版ごとに一意の object の名前（v1 だけ・v2 だけ）を識別値にし、AC1 に Scene ごとに見る識別値と「他の版の識別値を持たない」を書いた |
  | Y2 | B3o の観測を変更を捨てる前に record に書く | 採用 | §5 の B3o |
  | Y3 | R1「作らないもの」の行が U3 の「疑う時」のまま | 採用 | §5.1 の R1 の行 |
  | Y4 | R2 F4 の行が B3 のまま | 採用 | §5.1 の R2 F4 の行 |
  | Y5 | UC-1 にも対話の Editor が未確認と添える | 採用 | §1 の UC-1 |
  | Y6 | program §8 の S2-2c の行を本計画の列（variant、再利用、B3o・B4b）に合わせる | 採用 | program §8 の S2-2c の行の問いと gate を直した |

## 6. Phase B 実装結果

（未着手）

## 7. Phase C

（未着手）

## 8. Phase C'

（未着手）

## 9. Phase D

（未着手）
