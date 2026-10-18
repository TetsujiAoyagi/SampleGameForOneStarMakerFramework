# Unity 側 MessagePack 3.1.9 更新と README 版表の現況化

## 0. メタデータ

- type: `slice`
- status: `B`（Phase A は revision 1 で A3 凍結済み。2026-10-18 に人間が凍結を指示）
- branch: `claude/messagepack-3.1.9-unity`
- taskId: `messagepack-unity-3-1-9`（`pwsh tools/workflow-task.ps1 start -Task messagepack-unity-3-1-9` と Evidence の stage に使う。tracked HANDOFF の通常作業で、H1 の `external-current-v1` は採用しない）
- implementation base commit: `e7929955`（PR #136 のマージ。DebugStudio 側は 3.1.9 済み）
- implementation head commit: 未固定
- risk: `normal`（理由は §1 現況の末尾。A2 で同意）
- owner: root
- created: 2026-10-18
- expires: 2026-11-15
- harvest to: なし（恒久的な設計判断を持たない。README の現況化そのものが公開面の更新）
- Phase A snapshot の取得案内・状態: tracked の本ファイルで固定（A2 入力版は commit `233c3b9b`、A3 凍結版 revision 1 は本ファイルを凍結した commit。`git log -1 --format=%H -- docs/handoff/MESSAGEPACK_UNITY_3_1_9.md` を B 着手前に実行して得る）。Artifact Storage への stage は未保存（tracked 文書のため不要）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase B（実装）。新しい session で始める
  - 次の作業: §4 の順序どおりに MessagePack 3 件を 3.1.9 へ上げ、README を AC9 のとおり直し、§6 に結果を書いて commit する。B の出口で引継ぎ票を発見 C 向けに書き換える（動作確認は `対象なし`）
  - 引き継ぐ commit SHA: A3 凍結版 revision 1 の commit（上記の `git log` で得る）。worktree の HEAD と一致し、作業ツリーが clean であることを確かめてから始める
  - 必要な権限: Unity Editor の起動・操作（`osm-unity-editor` Skill）、nuget.org への通信、`pwsh tools/workflow-task.ps1 start`
  - 停止規則: §4 の「Phase A へ差し戻す条件」に当たったら止めて返す。復旧を確かめられなければ代替経路を始めない。Unity テストは実行しない
  - 引き渡さないもの: なし

## 1. 目的と対象外

- 目的:
  1. Unity 側の NuGet パッケージ `MessagePack` / `MessagePack.Annotations` / `MessagePackAnalyzer` を 3.1.7 から 3.1.9 へ上げ、DebugStudio 側（PR #136 で 3.1.9）と版を揃える。
  2. ルート `README.md` の「使用ライブラリ」表から版の列を外し、版の正本ファイルを案内する形にする（A3 決定 U1 = 案 a）。
- 対象外:
  - 他の NuGet パッケージ（`Microsoft.*`、`System.*`、Cysharp 系、VContainer）の版変更。
  - UPM パッケージ（manifest / lock）の変更。
  - DebugStudio 側の csproj 変更（#136 で完了済み）。
  - `csc.rsp`（`-langVersion:10`）の変更。
  - `MessagePackSerializerOptions` の変更（`UntrustedData` 化など）。DebugSocket の受信を信頼できない入力として扱うかは別の問い。
  - IL2CPP Player 上の DebugSocket 疎通（§2「ここでは答えない問い」）。
- 現況:
  - `unity/Assets/packages.config` は MessagePack 3 件が 3.1.7。`unity/Assets/Packages/` に `MessagePack.3.1.7` / `MessagePack.Annotations.3.1.7` / `MessagePackAnalyzer.3.1.7` と、それぞれ横に同名の `.meta` が tracked で置かれている。
  - nuget.org の 3.1.9 の nuspec は、netstandard2.1 group の依存が 3.1.7 と同一（`MessagePackAnalyzer` / `MessagePack.Annotations` が同版、`Microsoft.NET.StringTools` 17.11.4、`System.Collections.Immutable` 8.0.0）。他の NuGet パッケージの版を動かす必要は無い。
  - 3.1.9 の nupkg の配置は 3.1.7 と同じ（A3 で nupkg を展開して確認）: `MessagePack` は `lib/netstandard2.1/MessagePack.dll`、`MessagePack.Annotations` は `lib/netstandard2.0/MessagePack.Annotations.dll`、`MessagePackAnalyzer` は `analyzers/roslyn4.3/cs/` に `MessagePack.SourceGenerator.dll` と `MessagePack.Analyzers.CodeFixes.dll`。
  - NuGetForUnity は nupkg の内容を変換せずに取り込む（A3 で 3.1.7 を確認）: tracked の 3.1.7 の非 `.meta` ファイル 12 件（3 パッケージ × `.signature.p7s`・nuspec・dll・xml。analyzer は xml 無しで dll 2 件）は、**commit 内の blob**（`git show <commit>:<path>`）が nuget.org の 3.1.7 nupkg の同じ相対 path の entry と全件 byte 一致した。作業ツリーの nuspec / xml は `core.autocrlf=true` で CRLF に変わるので一致しない。比較は blob で行う。
  - `unity/Assets/NuGet.config` は `slimRestore=true`、`packageInstallLocation=CustomWithinAssets`、`repositoryPath=./Packages`。`packages.config` の blob は BOM 付き・LF。
  - checkout のルート長はこの worktree で 98 文字（末尾の区切りを含む）で、`tools/path-budget.json` の repo 相対上限 150 との和は 259 以内。`unity/Assets/Packages/` は vendor 例外。
  - asmdef は `OneStarMaker.Foundation` と `OneStarMaker.Tests` が `MessagePack.dll` / `MessagePack.Annotations.dll` をファイル名で precompiled 参照しており、GUID 参照ではない。3.1.7 の dll の GUID を参照しているのは、その dll 自身の `.meta` だけ。`link.xml` に MessagePack は出てこない。
  - `MessagePackAnalyzer` の 2 つの dll の `.meta` は label `NuGetForUnity` / `RoslynAnalyzer` と全 platform 無効の PluginImporter を持つ。`RoslynAnalyzer` label の analyzer は、Unity が生成する**全アセンブリ**のコンパイルに暗黙に適用される（asmdef の参照とは無関係）。したがって今回の更新は依存の辺を増やさないが、source generator の出力を通して全アセンブリのコンパイルに及ぶ。本体 `MessagePack.dll.meta` は label `NuGetForUnity` だけで、`Any: enabled: 1` / `Editor: enabled: 0`。
  - wire 互換は `protocol/debugsocket/` の golden fixture を Unity（`OneStarMaker.Tests.Foundation.ProtocolGoldenCrossContractTests`）と DebugStudio（`DebugStudio.Contracts.Tests` の同名テスト）の双方が読むことで検査している。DebugStudio 側は #136 の CI で 3.1.9 のまま green。source generator の出力（Foundation の DTO 用 formatter）が変わっても diff には出ないが、wire 形式の変化はこの golden fixture テストで検出する。
  - 3.1.5〜3.1.9 の変更点は主にセキュリティ修正（深い入れ子・LZ4・長さ検証の拒否、disallow list の追加）と、`DateTimeOffset` 符号化変更の取り消し（3.1.5 内で revert 済み）。DebugSocket の DTO は `MessagePackSerializerOptions.Standard` を使い、Typeless は使っていない。
  - NuGetForUnity（`com.github-glitchenzo.nugetforunity`、lock は既存のまま）は公開 API `NugetForUnity.NugetPackageUpdater.Update(currentVersion, newVersion, refreshAssets, uninstallReason)` を持つ。旧版を uninstall してから新版を install する。Manage NuGet Packages ウィンドウの Update と同じ処理。
  - README の版表は「2026-06時点の記録」と自称し、現物と違う行がある（URP 17.5.0→17.6.0、Addressables 2.9.1→2.11.2、Input System 1.19.0→1.20.0、uGUI 2.5.0→2.6.0、Test Framework 1.7.0→1.8.0、R3 1.3.0→1.3.1、MessagePack は Unity 側 3.1.7・DebugStudio 側 3.1.9 なのに両方 3.1.4）。UniTask は manifest の Git 参照なのに NuGet の表に版付きで載っている。docs 方針の公開面ルール 1 に反している。
  - risk を `normal` とする理由: 同一 major・minor 内の patch 更新で、依存の辺、asmdef、公開 API、寿命を変えない。phases-and-handoff の高リスク例「依存・asmdef を変える」は依存の辺を変えることと読む。全アセンブリに及ぶ source generator の出力変化は、全アセンブリの compile（S2）と golden fixture テスト（I2・AC8）で検出する。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Unity 側を MessagePack 3.1.9 に上げても、ビルド構成・wire 互換・既存テストを壊さずに DebugStudio と同版にできるか。
- 進める最低条件:
  - 不変条件:
    - I1. MessagePack 3 件以外の NuGet パッケージの版と取り込み物（`.meta` を含む）、`unity/Packages/manifest.json` / `packages-lock.json` が変わらない。
    - I2. `protocol/debugsocket/` の golden fixture が変わらない（wire 形式を書き換えて合わせない）。
    - I3. 3.1.9 の取り込み物の `.meta` が、対応する 3.1.7 の `.meta` と guid 以外で一致する（analyzer dll は label `RoslynAnalyzer` と全 platform 無効のまま、本体 dll は `Any: enabled: 1` / `Editor: enabled: 0` のまま）。
    - I4. asmdef、`csc.rsp`、C# ソースが変わらない。
    - I5. `tools/path-budget.json` の予算を超えない。
  - 成立条件:
    - S1. `unity/Assets/packages.config` の MessagePack 3 件が 3.1.9 で、`unity/Assets/Packages/` に 3.1.9 の 3 ディレクトリとその横の `.meta` だけがあり、3.1.7 のディレクトリと `.meta` が残っていない。3.1.9 の各ディレクトリの非 `.meta` ファイルは、3.1.7 と同じ相対構成で、nuget.org の 3.1.9 nupkg の対応 entry と内容が一致する。
    - S2. 3.1.9 で全アセンブリのコンパイルが完了し、compile error が 0。判定範囲は「全アセンブリがコンパイルでき、golden fixture と全 EditMode 回帰を通る」までとし、source generator の出力が 3.1.7 と同一であることまでは主張しない。
    - S3. 判定必須テスト（§5）が green。
    - S4. README の使用ライブラリ表に版の列が無く、版の正本ファイルを案内している。
- 受け入れ条件:
  - AC1（I1）: `git diff --name-status <base>..<head>` に出る path が `unity/Assets/packages.config`、`unity/Assets/Packages/MessagePack*`（ディレクトリと横の `.meta`）、`README.md`、本 HANDOFF に限られる（`--stat` は量の補助にだけ使う）。`git diff <base>..<head> -- unity/Assets/packages.config` の内容差分が MessagePack 3 行の `version` 属性だけで、BOM・改行・属性の順・`manuallyInstalled` は base のまま。失敗例: NuGetForUnity の Update が他パッケージを再取得し、`Microsoft.*` の `.meta` や版が変わる → 不合格（§4 の切替条件）。失敗例: Update で `MessagePack` 行の `manuallyInstalled="true"` が落ちる → 不合格（B が base の値へ戻せば合格に戻る）。
  - AC2（I2）: `git diff <base>..<head> -- protocol/` が空。失敗例: golden テストが 3.1.9 で落ち、fixture を再生成して合わせる → 不合格（wire 形式の変化として A へ戻す）。
  - AC3（I3）: 3.1.9 の各 `.meta`（dll、xml、nuspec、ディレクトリ、横の `.meta`）を、path の `3.1.9` を `3.1.7` に置き換えた base commit の `.meta` と、`guid:` 行を除いて比較し、差が無い（`.signature.p7s` は Unity が `.meta` を作らないので対象外）。失敗例: 手動複写で analyzer の `.meta` が既定 importer で生成され `Any: enabled: 1` になる、または label `RoslynAnalyzer` が付かない → 不合格。
  - AC4（I4）: `git diff <base>..<head> -- '*.asmdef' '*.cs' '*.rsp'` が空。失敗例: 3.1.9 の generator 出力が C# 11 構文を要求し、`csc.rsp` の `-langVersion` を上げて compile を通す → 不合格（A へ戻す）。
  - AC5（I5）: `pwsh tools/contract-audit.ps1` が検査 9 を含めて pass。失敗例: 取り込み物の path が予算を超え、検査 9 が fail する → 不合格（構成の違いとして A へ戻す）。
  - AC6（S1）: 固定 head の木を `git ls-tree -r --name-only <head> -- unity/Assets/Packages` で列挙し、`MessagePack` を含む path が、`MessagePack.3.1.9`、`MessagePack.Annotations.3.1.9`、`MessagePackAnalyzer.3.1.9` の配下と、その横の `MessagePack.3.1.9.meta`、`MessagePack.Annotations.3.1.9.meta`、`MessagePackAnalyzer.3.1.9.meta` だけで、`3.1.7` を含む path が無い。3.1.9 の配下の path 集合は、base の 3.1.7 の配下の path 集合の `3.1.7` を `3.1.9` に置き換えたものと一致する（欠落も余分も無い）。Phase B の取り込み直後（stage 前）は、`git ls-files` が未追跡を数えないので、ファイルシステムの実在と `git status --porcelain --untracked-files=all -- unity/Assets/Packages` で同じ 3 点（3.1.9 が揃う、3.1.7 が残らない、予定外の MessagePack 関連 path が増えない）を確かめる。判定はコミット後の固定 head で行う。
  - AC10（S1）: 3.1.9 の各ディレクトリの非 `.meta` ファイルについて、固定 head の blob（`git show <head>:<path>`）が、nuget.org から取得した同名パッケージ 3.1.9 の nupkg の同じ相対 path の entry と byte 一致する。`.meta` はこの照合から外し、AC3 で検査する。失敗例: 3.1.7 の dll が 3.1.9 のディレクトリに残る、または nupkg に無いファイルが混ざる → 不合格。
  - AC7（S2）: 判定 C の全 EditMode 回帰（§5）の実行ログで、スクリプトのコンパイルが完了し compile error が無く、その後にテストが実行されて XML が生成されている（コンパイルが完了しなければテストは走らないので、XML の生成と実行件数 1 以上がコンパイル完了の観測になる）。Phase B の Editor での compile 確認は B の出口の自己確認とし、判定の入力にしない。
  - AC8（S3）: §5 の判定必須テストの XML で failed 0、実行件数 1 以上。
  - AC9（S4）: README の使用ライブラリ表（Unity UPM・NuGet・DebugStudio）に版の列が無い。「2026-06時点の記録」の文言が無い。版の正本として `unity/Packages/manifest.json`、`unity/Packages/packages-lock.json`、`unity/Assets/packages.config`、`tools/DebugStudio` の各 csproj を案内している。UniTask は取得元 Git (UPM) として UPM 側の表に載る。`pwsh tools/docs-audit.ps1` が pass。
- ここでは答えない問いと所有する後続スライス:
  - IL2CPP Player 上で DebugSocket が 3.1.9 で動くか → 後続候補「DebugSocket Player 互換性検証」（未承認の候補。HANDOFF は未作成）。owner（root）が Player 上の DebugSocket 疎通を要する作業を始めるときに A0 で引き受けるかを決める。今回の更新で動的コード生成や resolver 構成は変えないため、本スライスの問いには必要ない。
  - DebugSocket の受信を `MessagePackSecurity.UntrustedData` にすべきか → 後続候補「DebugSocket 受信入力の信頼境界整理」（未承認の候補。HANDOFF は未作成）。owner（root）が DebugSocket を開発機の外へ公開する作業を始めるとき、またはそれより前に owner が必要と判断したときに A0 で引き受けるかを決める。
  - VContainer の削除（README で「将来削除予定」） → 所有スライス無し。本スライスでは触らない。
  - 今回の最低条件に、IL2CPP Player の実行と `UntrustedData` 化を加えない。
- 判定定義: 不変条件 I1〜I5 と成立条件 S1〜S4 を全て満たせば GO。どれか一つでも満たさなければ NO-GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity側 C# で `record` を使わない、`#nullable enable` を外さない（本スライスは C# を変えない）。
  - Editor を開く前に、対象 project と既存 Editor、`unity/Temp/UnityLockfile` を確認する。人間所有の Editor を無断で終了しない。Editor 操作は `osm-unity-editor` Skill に従う。
  - Unity テストは `pwsh tools/run-tests.ps1` だけを使い、`unity test` / `unity run` は使わない。Phase B では Unity テストを実行しない。
  - 本番の Scene / Prefab / Addressables を変更しない。
  - `.meta` の GUID や label を手で書き換えて AC3 に合わせない。
- 未決事項:
  - なし（A3 で閉じた。C' の担当は人間が後で割り当てる。B・C と異なるモデル、新規 session、blind audit bundle の契約を守る。A2-2 は A2-1 の採否を読んだ版への review で、独立性制約ありのまま人間が凍結を承認した）

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|
| `unity/Assets/packages.config` | NuGetForUnity の復元入力 | MessagePack 3 行の版（NuGetForUnity の Update が書き換える） | NuGetForUnity / 永続 | 公開面なし | AC1、AC6 | 27 → 27 |
| `unity/Assets/Packages/MessagePack*.3.1.9/**` と横の `.meta` | NuGet の取り込み物（dll、xml、nuspec、署名、`.meta`） | 3.1.7 から置換 | NuGetForUnity が生成 / 永続 | 本体 2 件は asmdef からファイル名で参照。analyzer 2 件は label `RoslynAnalyzer` により全アセンブリのコンパイルに暗黙に適用される（既存の暗黙の依存で、本スライスで増えない） | AC3、AC6、AC7、AC10 | 3.1.7 と同構成（dll 4、xml 2、nuspec 3、署名 3 と各 `.meta`） |
| `README.md` | 使用ライブラリの紹介 | 版の列を外し、正本ファイルを案内する | 公開面 / 永続 | なし | AC9 | 版表 約 30 行 → 同程度以下 |

新しいコード、責務、型は追加しない。500 行・3 責務・50% 増の警報は発火しない。

## 4. 実装計画

- 変更対象: §3 の 3 種。
- 順序（既定経路: NuGetForUnity の Update。`packages.config` は手で直さない）:
  0. `pwsh tools/workflow-task.ps1 start -Task messagepack-unity-3-1-9` を済ませる（済んでいれば `status` で確認）。作業ツリーが clean で HEAD が引継ぎ票の SHA と一致することを確かめる。
  1. 更新前の状態を記録する: `packages.config` の MessagePack 3 行、`git status --porcelain --untracked-files=all -- unity/Assets/Packages` が空であること、Editor を開いた後に NuGetForUnity が installed として認識している MessagePack 3 件の版（3.1.7）。記録は §6 に書く。
  2. Editor が閉じていることと `unity/Temp/UnityLockfile` を確認し、Editor を開く（`osm-unity-editor` Skill の手順）。
  3. `unity/Library/PackageCache` の NuGetForUnity の中身で `NugetPackageUpdater.Update` の引数の型と、installed / 新版のパッケージを得る方法を確かめる。API 名や手順を推測で書かない。
  4. `tools/unity-editor.cmd eval` から `NugetPackageUpdater.Update` を呼び、`MessagePack`、`MessagePack.Annotations`、`MessagePackAnalyzer` をそれぞれ 3.1.9 にする（依存として自動で上がったものは重ねて呼ばない）。
  5. AC6 の stage 前の確認（ファイルシステムと未追跡を含む `git status`）と、`packages.config` が 3 件とも 3.1.9 になったことを確かめる。`packages.config` の BOM・改行・属性の順・`manuallyInstalled` が base と違えば、版以外を base のとおりに手で戻す（意味を変えない書式の復元で、B 適応）。
  6. AC3 を確認する。崩れていれば NuGetForUnity の再 import で直す。
  7. Editor の AssetDatabase 処理とスクリプトコンパイルの完了を待ち（`EditorApplication.isCompiling` と `isUpdating` が false）、Console と Editor.log に compile error が無いことを確認して、Editor を正常終了する。これは B の自己確認で、判定は AC7 による。
  8. `git status` で AC1・AC4 の範囲外の変化が無いことを確認する。
  9. README を AC9 のとおり直す。
  10. `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` を通して commit する。commit 後に AC6・AC10 を固定 head で確かめる。
- 既定経路が失敗したときの復旧（Git と Editor の二層）:
  1. 失敗の観測（例外、Console のエラー、途中で止まった状態）をそのまま §6 に残す。
  2. Editor を正常終了する。終了できない場合は、今回自分で起動した Editor の PID だけを確認して終了する。人間所有の Editor には触れない。
  3. Git の層: `git restore --source=<base> --staged --worktree -- unity/Assets/packages.config unity/Assets/Packages` で追跡ファイルを base に戻す。`git status --porcelain --untracked-files=all -- unity/Assets/Packages` に出る未追跡 path は、`MessagePack` を含み今回の更新で生じたもの（3.1.9 の配下と横の `.meta`）に限って一覧を §6 に残してから削除する。作業ツリー全体への `git clean` / `git reset --hard` は使わない。対象外の path に変化があれば、復旧せずに停止する。
  4. Editor の層: Editor を開き直し、コンパイルの完了を待って、NuGetForUnity が MessagePack 3 件を 3.1.7 として認識し、3.1.9 を認識していないこと、compile error が無いことを確かめる。確かめられなければ代替経路を始めずに停止する。
  5. 失敗を次のどれかに分類して §6 に書く: API 呼出しの問題（引数・型の取り違えなど。既定経路をやり直してよい）、環境の問題（ネットワーク、Library の破損など。§4 の範囲で直せなければ停止）、取り込み結果の問題（I1・I3・S1 に反する。代替経路へ）、計画の前提の崩れ（下記の差し戻し条件。A へ戻す）。
- 代替経路への切替条件と代替経路:
  - 切替条件（どれか 1 つ）: Update の API が見つからない・呼び出せない。Update で MessagePack 3 件以外の版や `.meta` が変わる（I1）。Update の途中で 3.1.7 と 3.1.9 が同時に置かれ compile error になる。
  - 切替前に、上記の復旧を終えて手順 4 まで確かめる。
  - 代替経路: Editor を閉じたまま、3.1.7 の 3 ディレクトリと横の `.meta` を削除し、`packages.config` の MessagePack 3 行の `version` だけを 3.1.9 に直してから Editor を開き、NuGetForUnity の復元に取り込ませる。以降は手順 5〜10 と同じ。3.1.7 と 3.1.9 が同時に置かれる窓を作らないため、config の編集を先にして Editor を開く順は取らない。代替経路が失敗したときも同じ復旧を行い、停止する。
  - どちらの経路でも合否は AC1・AC3・AC6・AC10 で判定する。経路の切替は B 適応として §6 に記録し、Phase A を再開しない。
- Phase B から Phase A へ差し戻す条件:
  - 3.1.9 の取り込みに、MessagePack 3 件以外の版変更や新しい NuGet 依存が必要になった。
  - asmdef、`csc.rsp`、C# ソースの変更が必要になった（compile error の解消を含む）。
  - NuGetForUnity の import で AC3 の形にできない。
  - 取り込まれた 3.1.9 の構成が §1 現況に書いた配置と違う。
  - 両方の経路で切替条件に当たった。
- 対象外を維持する方法: AC1・AC4 の差分照合を B の出口で行う。

## 5. テストとレビュー計画

- 単体テスト: 追加しない。新しいロジックが無い。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Foundation`（DebugSocket・Protocol・MessagePackZLoggerFormatter のテストを含む namespace）。
- 判定必須テスト:
  - 全 EditMode 回帰（`pwsh tools/run-tests.ps1`、空 filter）。XML に `ProtocolGoldenCrossContractTests`、`ProtocolBackwardCompatibilityTests`、`DebugSocketProtocolRoundtripTests`、`MessagePackZLoggerFormatterTests` が含まれることを確認する。
  - DebugStudio 側の `dotnet test tools/DebugStudio/DebugStudio.sln` は #136 の CI で 3.1.9 のまま green。本スライスは DebugStudio を変えないので再実行を必須にしない。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 上記のみ。PlayMode と Build は実行しない（Player は §2 で答えない問い）。
- 操作・実行時・目視条件の検証経路:
  - S2（AC7）: 判定の証拠は判定 C の全 EditMode 回帰の実行ログと XML（実行担当は判定 C。ローカル Windows、Editor が閉じた状態から `pwsh tools/run-tests.ps1` を sandbox 外の承認済み経路で起動）。保存する証拠は対象 head、run-tests の実行ログ（コンパイル完了からテスト開始までの範囲を含む）、XML。C / C' へは evidence bundle に収録して渡す。
  - Phase B の Editor での compile 確認（§4 手順 7）は B の自己確認で、対象 Editor、head、コンパイル完了の観測根拠（`isCompiling` / `isUpdating` の値）、エラー判定に使ったログ範囲を §6 に書く。判定の入力にはしない。
  - それ以外の条件はテストと git の出力で閉じる。動作確認の対象条件は無い（`対象なし`）。
- 未知の操作経路の疎通結果: `tools/unity-editor.cmd eval` から `NugetPackageUpdater.Update` を呼ぶ経路は未確認。初回確認は Phase B の手順 2〜3。不成立なら §4 の代替経路へ切り替える（既存の復元経路で、nuget.org への通信が要る）。
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: 新しい script は作らない。判定 C が固定 head に対して次の git コマンドと nupkg 照合を実行し、出力を evidence bundle に収録する。git コマンドは offline で済み、AC10 だけ nuget.org への通信が要る。
  - AC1: `git diff --name-status <base>..<head>`、`git diff <base>..<head> -- unity/Assets/packages.config`
  - AC2・AC4: `git diff <base>..<head> -- protocol/`、`git diff <base>..<head> -- '*.asmdef' '*.cs' '*.rsp'`
  - AC3: 3.1.9 の各 `.meta` の blob と、対応する base の 3.1.7 の `.meta` の blob を `guid:` 行を除いて比較した結果
  - AC6: `git ls-tree -r --name-only <head> -- unity/Assets/Packages` と `git ls-tree -r --name-only <base> -- unity/Assets/Packages` の MessagePack 部分
  - AC10: nuget.org の `https://api.nuget.org/v3-flatcontainer/<id>/3.1.9/<id>.3.1.9.nupkg`（`<id>` は小文字の `messagepack`、`messagepack.annotations`、`messagepackanalyzer`）を空の作業 directory に取得して展開し、3.1.9 の各非 `.meta` ファイルの `git show <head>:<path>` の sha256 と、nupkg の同じ相対 path の entry の sha256 を並べた表。取得した nupkg 自体の sha256 も残す。期待値は「全件一致」で、実装から独立している（A3 で 3.1.7 に同じ手順を当てて全 12 件一致を確認済み）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: Claude Code（Opus 5.5、Anthropic）
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:
  - A2-1 アーキテクチャゲート: Claude Code の別 session（Opus 5.5、Anthropic）。入力版は `docs/handoff/MESSAGEPACK_UNITY_3_1_9.md` の sha256 `29b7f232b9dff36c5a6201e9cbdd06130e398c64fe2a538229308f7a58d49960`（commit `233c3b9b`）。主担当と同じモデル系列のため、独立性はモデルの面で制約がある。
  - A2-2 凍結前の計画レビュー（復旧・取り込み検証・観測条件）: ChatGPT の Web 版（OpenAI）。人間が依頼した。入力版は A3 統合版（未 commit、sha256 `2a2d0a87ef2bb2b9f6d656d52d7229acde2fd577e0663358751f1329d15dbec8`）。A2-1 の採否表を含む版を読んだので、A2-1 の指摘からは独立していない（独立性制約あり）。モデル系列とベンダーは主担当・A2-1 と異なる。
  - A3 主担当の自己点検（Phase C / C' で指摘されそうな点の見直し）: Claude Code（Opus 5.5、Anthropic）。独立レビューではない。
- A3 統合担当・モデル・採否: Claude Code（Opus 5.5、Anthropic）が統合した。A2-1 の採否は人間が決めた（下の「A2 指摘の採否」）。A2-2 の採否と自己点検の反映は主担当が案を作り、2026-10-18 に人間が承認して revision 1 で凍結した。
- C' 用に予約した担当・モデル・ベンダー: 未割当。人間が後で、Phase B・C と異なるモデルを割り当てる。
- 独立性の強化条件を満たせない場合の理由: risk `normal` のため強化条件は対象外

### A2 指摘の採否

| # | 指摘 | 採否 | 理由と反映先 |
|---|---|---|---|
| 1 | `packages.config` を直してから Editor を開くと、3.1.7 と 3.1.9 が同時に置かれ、compile error か Safe Mode になる | 採用 | 人間の決定で既定経路を NuGetForUnity の Update にし、config の手編集をやめた。切替条件と代替経路（Editor を閉じたまま 3.1.7 を消してから config を直して開く）を §4 に置いた |
| 2 | 同じ形を求めているのが analyzer の `.meta` だけ | 採用 | I3・AC3 を 3.1.9 の全 `.meta` を 3.1.7 と guid 以外で比較する形にした |
| 3 | AC4 が `csc.rsp` を見ていない | 採用 | I4・AC4・差し戻し条件・対象外に `csc.rsp` を入れた |
| 4 | 3.1.9 の配置が 3.1.7 と同じか未確認 | 採用 | A3 で nupkg を展開し、同じ配置だと確かめて §1 に記録した。違う構成が取り込まれたら A へ戻す条件も足した |
| 5 | generator が全アセンブリに適用されることが書かれていない | 採用 | §1・§3 に暗黙の依存として記録し、S2・AC7 を全アセンブリの compile error 0 にした |
| 6 | U1 は案 a。UniTask の取得元、「2026-06時点の記録」、DebugStudio 表の版も含める | 採用 | 人間の決定で案 a。AC9 に全部入れた |
| 7 | U2 は `normal` のまま。generator 出力の検出経路を書く | 採用 | §1 の risk の理由に追記した |
| 8 | AC6 が横の `.meta` を数えない。`grep -i` は pwsh で動かない | 採用 | AC6 を横の `.meta` を含めた形にし、`Select-String` に書き換えた |

A2 で確認された事実のうち、README の MessagePack 行が 3.1.7 ではなく 3.1.4 と書かれていた点は §1 現況を直した。

### A2-2 指摘の採否

| # | 指摘 | 採否 | 理由と反映先 |
|---|---|---|---|
| P1-1 | Update 途中失敗時の復旧を Git と Editor の二層で決める | 採用 | §4 に復旧手順（更新前の記録、Editor の正常終了、対象 path に限った Git の復元と未追跡の確認、Editor を開き直して 3.1.7 だけが認識されることの確認、失敗の分類）を置いた。復旧を確かめられなければ代替経路を始めずに停止する |
| P1-2 | `.meta` だけでなく取り込み物の実体を検証する | 採用 | S1 に実体の条件を足し、AC10（固定 head の blob と nuget.org の nupkg entry の byte 一致）を置いた。作業ツリーは autocrlf で CRLF になるので blob で比べる。A3 で 3.1.7 に同じ手順を当て、全 12 件一致を確認した。`.meta` は AC3 で別に検査する |
| P2-1 | AC6 を作業ツリーと固定 head で分ける | 採用 | AC6 の判定を `git ls-tree <head>` にし、B の取り込み直後は未追跡を含む `git status` とファイルシステムで確かめる形にした。path 集合が 3.1.7 と一致することも足した |
| P2-2 | S2 のコンパイル完了を観測する | 採用（形を変えて） | B の session が取った観測は判定の入力にできない（phases-and-handoff「実行担当と判断担当」）ので、AC7 の判定を判定 C の run-tests のログと XML（コンパイル完了後にしかテストは走らない）に移した。B の Editor での確認は `isCompiling` / `isUpdating` を見る自己確認として §4・§5 に残した。source generator 出力の同一性は主張しないと S2 に書いた |
| P2-3 | 対象外の問いに所有先を書く | 採用 | 後続候補名と、owner が引き受けるかを決める時点を §2 に書いた。最低条件には加えない |
| P3-1 | AC1 を name-status で判定する | 採用 | AC1 を `--name-status` にし、`--stat` は補助にした |

### A3 主担当の自己点検（Phase C / C' で指摘されそうな点）

| # | 点検で見つけたこと | 反映先 |
|---|---|---|
| R1 | taskId が無く、`workflow-task.ps1 start` と Evidence の stage の所在 task を決められない | §0 に taskId、§4 手順 0 に start を置いた |
| R2 | `packages.config` を NuGetForUnity が書き直すと、BOM・属性の順・`manuallyInstalled` が変わりうるのに、AC1 の「3 行の版だけ」が書式の差をどう扱うか決まっていない | AC1 に書式も base のままと明記し、§4 手順 5 で版以外を base へ戻す B 適応にした |
| R3 | 作業ツリーのファイルを nupkg と比べると autocrlf のせいで nuspec / xml が必ず不一致になり、C が偽の欠陥を出す | AC10 と §1 現況に「blob で比べる」と書いた |
| R4 | AC3 の「対応する 3.1.7 の `.meta`」の対応付けと、`.meta` を持たない `.signature.p7s` の扱いが曖昧 | AC3 に path の置換規則と `.signature.p7s` の除外を書いた |
| R5 | checkout のルート長が予算に入るかを確かめていない（worktree は path が長い） | §1 現況に 98 + 150 ≤ 259 を記録した |
| R6 | 動作確認の扱いが書かれていない | §5 に `対象なし` と書いた |

## 6. Phase B 実装結果

未着手。
