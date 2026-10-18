# Unity 側 MessagePack 3.1.9 更新と README 版表の現況化

## 0. メタデータ

- type: `slice`
- status: `D`（Phase D 完了（2026-10-18）。発見 C は blocker 0 件、判定 C は GO、C' は阻害する欠陥なし。HANDOFF は harvest 不要のため本 commit の次で削除し、PR #138 を develop へマージする）
- branch: `claude/messagepack-3.1.9-unity`
- taskId: `messagepack-unity-3-1-9`（`pwsh tools/workflow-task.ps1 start -Task messagepack-unity-3-1-9` と Evidence の stage に使う。tracked HANDOFF の通常作業で、H1 の `external-current-v1` は採用しない）
- implementation base commit: `e7929955`（PR #136 のマージ。DebugStudio 側は 3.1.9 済み）
- implementation head commit: `0e8904fc`（Phase B 出口。判定 C が 2026-10-18 に GO 候補 head として固定し、ローカル tag `evidence/messagepack-unity-3-1-9/1` を付けた。push しない）
- risk: `normal`（理由は §1 現況の末尾。A2 で同意）
- owner: root
- created: 2026-10-18
- expires: 2026-11-15
- harvest to: なし（恒久的な設計判断を持たない。README の現況化そのものが公開面の更新）
- Phase A snapshot の取得案内・状態: tracked の本ファイルで固定（A2 入力版は commit `233c3b9b`、A3 凍結版 revision 1 は本ファイルを凍結した commit。`git log -1 --format=%H -- docs/handoff/MESSAGEPACK_UNITY_3_1_9.md` を B 着手前に実行して得る）。Artifact Storage への stage は未保存（tracked 文書のため不要）
- Phase B result snapshot の取得案内・状態: tracked の本ファイル §6 で固定（Phase B 出口の commit `0e8904fc` の内容）。Artifact Storage への stage は未保存（tracked 文書のため不要）
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。task `messagepack-unity-3-1-9` に `evidence stage` で固定し、別 process の fetch で hash 照合と XML・AC10 表の閲覧を確認済み。取得案内はメイン checkout の手元層（計画用の git 管理外 directory）の `MESSAGEPACK_UNITY_3_1_9.judgment-c.guide.md`。引渡し先: Phase D（owner）
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格）。判定 C と同じ head・同じ生証拠から別に作り、同じ task に `evidence stage` で固定し、別 process の fetch で確認済み。取得案内は同じ手元層の下位 directory `msgpack-cprime` の `cprime-r1.guide.md`（所見と分離）。引渡し先: C' の担当、Phase D
- 引継ぎ票:
  - 次の Phase・役割: C'（独立監査、巡 1 = 最終 C'。高コスト証拠は無い）。人間が Phase B（Opus 5.5）・発見 C（Sonnet 5.5）・判定 C（Fable 5.1）のどれとも異なるモデルを割り当て、新しい session で始める。session の prompt は本ファイル下の「Phase C / C' の担当と入口」の C' 用の一文だけとし、取得案内 `cprime-r1.guide.md`（メイン checkout の手元層の下位 directory `msgpack-cprime`）の絶対 path を渡す
  - 次の作業: 判定 C は完了した（§7 判定 C）。C' は取得案内で blind audit bundle を fetch し、bundle 内の指示 file `README-cprime-instructions.md` に従って監査し、結果を指示 file の書き先（同じ下位 directory の `cprime-r1.findings.md`）に置く。C' の後は Phase D（人間）が判定 C と C' を突合する。push・PR 作成・task の終了は人間の指示を待つ
  - 凍結した Phase A: revision 1（commit `9cc336f3`）。implementation base: `e7929955`。implementation head: `0e8904fc`（固定済み。tag `evidence/messagepack-unity-3-1-9/1`）
  - 引き継ぐ commit SHA: worktree の HEAD は `0e8904fc` の後の HANDOFF だけの commit（判定 C の記録 commit を含む）。`git diff --name-only 0e8904fc..HEAD` が本ファイルだけで、作業ツリーが clean であることを Phase D が確かめる。C' は worktree も HANDOFF も読まず、fetch した写しだけを入力にする
  - 必要な権限: C' は非仮想化 pwsh からの `fetch`（`staged` の local 分岐。network・資格情報を使わない）だけ。Artifacts の Release build がある checkout から実行する
  - 停止規則: C' は指示 file の分類規則に従い、現在の問いを阻害する欠陥には違反する凍結済み条件または常時契約と一次証拠を記録する。C' の指摘による差し戻しは 3 巡まで
  - 引き渡さないもの: 発見 C・判定 C の所見と結論、§10 決定記録、本ファイルの現行版、PR（C' の bundle にはどれも入れていない）
- Phase C / C' の担当と入口（引継ぎ票が書き換わっても残す。session の prompt には書かない）:
  - オーケストレーター（Phase C から C' までの進行役）を置く場合: 自分ではレビュー・テスト・修正をせず、各 Phase の担当 session を順に起動し、各 session の出口で引継ぎ票と HEAD を照合して次へ進める。順序は 発見 C →（差し戻しがあれば B → 発見 C、4 巡まで）→ 判定 C → C'。担当の session は下記のモデル条件で選び、モデルの指定が無い担当や条件を満たせない担当があれば起動せずに人間へ返す。C' の session には下記の C' 用 prompt だけを渡し、発見 C・判定 C の所見、本ファイル、PR、自分の要約を渡さない。push、PR 作成、§10 決定記録の追加、Phase A の再開、Phase D、task の終了は人間の指示を待つ。停止条件（4 巡で収束しない、Phase A へ戻す条件、Unity テストの承認が得られない、照合が食い違う）に当たったら止めて人間へ返す。C' が終わったら、判定 C と C' の結果の所在を人間へ報告して終える（突合は Phase D）。
  - 発見 C と判定 C は Phase B（Claude Code、Opus 5.5、Anthropic）と異なるモデルで、それぞれ新しい session で始める。prompt は「本ファイルの §0 引継ぎ票に従い、<発見 C / 判定 C> を担当する」の一文だけとする。
  - 判定 C の作業: §5 の判定必須テストと照合（AC1〜AC10。AC10 は D1 の読み方）を GO 候補 head で実行する。Unity テストは Editor が閉じていることを確かめ、sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1`（空 filter）を起動する。GO 候補 head に軽量 tag `evidence/messagepack-unity-3-1-9/1` を付ける（push しない）。
  - 判定 C の evidence bundle: `.agents/skills/osm-workflow/references/review-evidence.md` の「保存先とリモートへの配布」に従い、task `messagepack-unity-3-1-9` に `evidence stage` で固定する。収録は完全 diff（`e7929955..<head>`）、`--name-status`、`--stat`、AC1〜AC10 の生出力、run-tests の実行ログと XML、`contract-audit` と `docs-audit` の出力。取得案内はメイン checkout の手元層（計画用の git 管理外 directory）の `MESSAGEPACK_UNITY_3_1_9.judgment-c.guide.md` に置き、本ファイルには所在と状態だけを書く。
  - C' blind audit bundle: 判定 C の担当が、判定 C と同じ head・同じ生証拠から別に作り、同じ task に `evidence stage` で固定する。収録するのは、凍結版 revision 1 の本ファイル（commit `9cc336f3` の内容）、Phase B result（commit `0e8904fc` の §6）、判定 C と同じ生証拠、指示 file の 4 つだけ。指示 file には入力 file の一覧、観点（I1〜I5 を破る順序の有無、S1〜S4 と AC1〜AC10 の生出力の照合）、finding の分類規則（現在の問いを阻害する欠陥 / 後続スライスの入力）、結果の書き先だけを書く。発見 C・判定 C の所見と結論、§10 決定記録、可変な本ファイルの全文、PR、疑念候補や探索先の例示は入れない。取得案内は所見と分けた同じ手元層の下位 directory `msgpack-cprime` の `cprime-r1.guide.md` に置く。
  - C' は Phase B・C と異なるモデルを人間が割り当て、新しい session で始める。prompt は「上記 `cprime-r1.guide.md` の取得案内（絶対 path で渡す）で blind audit bundle を fetch し、その中の指示 file に従って C' を担当する。HANDOFF、PR、手元層の他の file は読まない」とする。C' の担当は fetch した写しだけを入力にし、結果は指示 file が示す書き先に置く。本ファイルへの転記は Phase D が行う。

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
- 決定記録: §10 の D1（版 1、2026-10-18、Phase B 完了後・発見 C 前に適用）。状態は有効
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

- 担当: Claude Code（Opus 5.5、Anthropic）。新しい session で、引継ぎ票の SHA `9cc336f3` と HEAD の一致、clean な作業ツリーを確かめてから始めた。
- 経路: 既定経路（NuGetForUnity の Update）で完了した。代替経路と復旧は使っていない。
- 変更: `unity/Assets/packages.config`（MessagePack 3 行の `version`）、`unity/Assets/Packages/MessagePack*`（3.1.7 を削除し 3.1.9 を追加）、`README.md`（使用ライブラリ）、本 HANDOFF。

### 手順ごとの記録

- 手順 0: `pwsh tools/workflow-task.ps1 start -Task messagepack-unity-3-1-9` は `status: ok`、`kind: started`、version 1。
- 手順 1（更新前）: `packages.config` は `MessagePack` 3.1.7 `manuallyInstalled="true"`、`MessagePack.Annotations` 3.1.7、`MessagePackAnalyzer` 3.1.7。`git status --porcelain --untracked-files=all -- unity/Assets/Packages` は空。Editor を開いた後の `InstalledPackagesManager.InstalledPackages` も 3 件とも 3.1.7（`MessagePack` だけ manual=True）。
- 手順 2: Editor は起動しておらず、`unity/Temp/` も無かった。`unity open` で 6000.6.0f1 のこの worktree の `unity/` を開き、`tools/unity-editor.cmd status` が ready（PID 5512）になるのを確かめた。
- 手順 3: NuGetForUnity（`com.github-glitchenzo.nugetforunity@1c1553136be4`）のソースで次を確かめた。`NugetPackageUpdater.Update(INugetPackageIdentifier currentVersion, INugetPackage newVersion, bool refreshAssets = true, PackageUninstallReason ...)` は旧版を uninstall して新版を install する。installed は `InstalledPackagesManager.InstalledPackages`、新版は `ConfigurationManager.GetSpecificPackage(new NugetPackageIdentifier(id, "3.1.9"))` で得る。新版の install は netstandard2.1 group の依存を `allowUpdateForExplicitlyInstalled: false` で入れ、manual でない古い依存は上げる。nuget.org には 3.1.12 まであるが、版の範囲を持たない識別子は完全一致の版を返すので、3.1.9 が選ばれる。
- 手順 4: eval の main thread 処理は 5 秒で timeout し、nuget.org への通信を含む呼出しはこれを超えた（1 回目の取得で timeout。状態は変わっていない）。そこで `EditorApplication.delayCall` で実行して結果をファイルへ書く形にした（B 適応。呼出す API は計画どおり）。取得した 3 件は `NugetPackageV3` の 3.1.9。`MessagePack` に `Update(installed 3.1.7, 3.1.9, refreshAssets: false)` を 1 回呼び、`result=True`。`MessagePack.Annotations` と `MessagePackAnalyzer` は依存として 3.1.9 に上がっていたので重ねて呼んでいない。最後に `AssetDatabase.Refresh()` を 1 回呼んだ。呼出し後の installed は MessagePack 3 件が 3.1.9（`MessagePack` は manual=True のまま）で、他の 22 件の版は手順 1 の `packages.config` と同じ。
- 手順 5: `packages.config` の差分は MessagePack 3 行の `version` だけ。index の blob は BOM（`ef bb bf`）付きで CR を含まず、base と同じ。属性の順と `manuallyInstalled` も base のまま。書式の手戻しは不要だった。stage 前の AC6: ファイルシステムに 3.1.9 の 3 ディレクトリと横の `.meta` があり、3.1.7 の path は残っていない。未追跡の path 集合は base の 3.1.7 配下の path 集合（`3.1.7` を `3.1.9` に置換）と一致した。
- 手順 6（AC3）: 3.1.9 の `.meta` 19 件（横の `.meta` 3、ディレクトリ 7、nuspec 3、xml 2、dll 4）を、base の対応する 3.1.7 の `.meta` と、改行を正規化し `guid:` 行を除いて比べ、差は 0 件。再 import は不要だった。
- 手順 7（B の自己確認。判定の入力にしない）: 対象 Editor は PID 5512、head は base `e7929955` + 作業ツリーの変更（未 commit）。Update 後の Refresh が `[ScriptCompilation] Requested script compilation because: AssetDatabase observed changes in script compilation related files` から始まり、`Reloading assemblies after forced synchronous recompile.` と `Asset Pipeline Refresh ... Total: 209.390 seconds`（`domain reloads=1`、`compile time=170685 ms`）で終わった。その後の eval で `isCompiling=False`、`isUpdating=False`、`EditorUtility.scriptCompilationFailed=False`、開いている Scene は dirty でない。エラー判定に使ったログ範囲は `unity/Logs/Editor.log` の全体（Editor 起動からこの終了まで）で、`error CS` は 0 件。ログに 16 件ある `Failed to handle /api/exec request: Main thread operation timed out after 5000ms` は、コンパイル中に送った私の状態確認 eval が timeout したもので、Pipeline が出したログ。その後 `EditorApplication.Exit(0)` で正常終了させ、PID 5512 は約 38 秒で終わり、`unity/Temp/UnityLockfile` は残っていない。
- 手順 8: Editor の副作用として、範囲外の tracked ファイル 6 件が変わった。git の `.pdb` 無視（`.gitignore` の `*.pdb`）で `.pdb` 本体が無いため、Editor を開いた直後に Unity が `unity/Assets/MobileDependencyResolver/Editor/` の孤立した `.pdb.meta` 5 件を削除した（Update の前の `git status` で観測）。また Update 後の Refresh で `Microsoft.NET.StringTools.17.11.4/MSBuild-NuGet-Icon.png` が TextureImporter で再 import され、その `.meta` に Unity 6000.6 の既定フィールド 2 行（`spriteTessellationMethod: 0`、`spriteGeometrySubdivision: -1`）が足された（Editor.log の 2818 行、ファイル時刻は Update の 9 秒後）。StringTools の他のファイルと版は変わっておらず、NuGetForUnity による再取得（AC1 の失敗例、切替条件）ではない。代替経路でも Editor を開けば同じ再 import が起きるので、経路は切り替えていない。6 件は Editor 終了後に `git restore --source=e7929955` で base に戻した（意味を変えない副作用の除去で、B 適応）。戻した後の `git status` は MessagePack と `packages.config` と `README.md` だけ。`git diff --cached --name-status e7929955 -- protocol '*.asmdef' '*.cs' '*.rsp' unity/Packages` は空。
- 手順 9: README の使用ライブラリ 3 表から版の列を外し、「2026-06時点の記録」の文言を消して、版の正本として `unity/Packages/manifest.json`、`unity/Packages/packages-lock.json`、`unity/Assets/packages.config`、`tools/DebugStudio` 配下の各 `.csproj` の `PackageReference` を案内した。UPM 表は「取得元」の列（Unity Registry / Git (UPM)）にし、UniTask を NuGet 表から UPM 表へ移した（manifest の Git 参照）。推移的依存の文の版（8.0.0、8.0.5）も外した。
- 手順 10: `pwsh tools/contract-audit.ps1` は `AUDIT_RESULT kind=contract files=645 errors=0 warnings=0 checks=1,2,3,4,5,6,7,8,9`。`pwsh tools/docs-audit.ps1` は `AUDIT_RESULT kind=docs files=111 errors=0 warnings=0 checks=1,2,3`。

### 固定 head で判定 C が使う事実（AC6・AC10）

stage した index の blob で照合した（commit 後の blob と同じもの）。

- AC6: index の `unity/Assets/Packages` 配下で `MessagePack` を含む path 集合は、base の 3.1.7 配下の集合の `3.1.7` を `3.1.9` に置き換えたものと一致し、`3.1.7` を含む path は 0 件。
- AC10: nuget.org の 3.1.9 nupkg の sha256 は `messagepack.3.1.9.nupkg` = `52438b796a20c947d25d8d306cd0d8a7fdd7357f404d3007bc9640e61e1c128a`、`messagepack.annotations.3.1.9.nupkg` = `2b41bb26405e5589cb589874f3e71254751cbed0e6b1877093928c1df739b77e`、`messagepackanalyzer.3.1.9.nupkg` = `3fffad66e950e6833a3bd1658b6a40938dc4e58983a5a545d278af1ee34bec14`。非 `.meta` 12 件のうち、`.signature.p7s` 3、nuspec 3、xml 2 の 8 件は `git show :<path>` の sha256 が nupkg entry と一致した。
- **AC10 の手順に関する新しい事実:** dll 4 件は `.gitattributes` で Git LFS（`filter=lfs`）の対象で、commit 内の blob は LFS pointer（例: `MessagePack.dll` の blob は 131 byte）。そのため AC10 の字義どおりの `git show <head>:<path>` の sha256 は、dll 4 件で nupkg entry と一致しない。base の 3.1.7 の dll 4 件も同じく LFS pointer である。dll 4 件は、pointer の `oid sha256:` と、`git cat-file --filters <rev>:<path>`（LFS の smudge を通した実体）の sha256 の両方が nupkg entry の sha256 と一致した（`MessagePack.dll` `76fe22b9…`、`MessagePack.Annotations.dll` `d6d77e65…`、`MessagePack.Analyzers.CodeFixes.dll` `993f43dc…`、`MessagePack.SourceGenerator.dll` `7d6776f3…`）。B は AC10 を書き換えていない。LFS の dll に対して「blob の byte 一致」をどの比較で読むかは判定 C の判断で、必要なら人間の決定記録にする（凍結条件の意味を変えない読み替えの候補）。
- push 時には dll 4 件の LFS object も送る必要がある（git-lfs の pre-push で送られる）。

### 未実行と申し送り

- Unity テスト（§5 の全 EditMode 回帰）は実行していない（Phase B の範囲外。判定 C が実行する）。
- 動作確認: `対象なし`（§5 のとおり、検証経路に操作・実行時・目視の条件が無い）。

## 7. Phase C

- 種別: 発見
- 使用したevidence bundle の取得案内・状態（§0と同じ案内）: bundle は作っていない。固定 commit（base `e7929955`、implementation 候補 `0e8904fc`）を git から直接読んだ。HEAD は `0e8904fc` の後の HANDOFF だけの commit（`git diff --name-only 0e8904fc..HEAD` は本ファイルのみ、作業ツリー clean）
- 構造適合: §3 の責務マップと一致する。`e7929955..0e8904fc` の変更 path は `unity/Assets/packages.config`、`unity/Assets/Packages/MessagePack*`（A 5・D 5・R 22 で 3.1.7 から 3.1.9 への改名）、`README.md`、本 HANDOFF だけ。コード・型・責務・依存の辺は増えていない（asmdef・`*.cs`・`*.rsp`・`protocol/` の差分は 0 byte）。新しいロジックは無いので、500 行・3 責務・50% 増の警報は対象外
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: なし
- 後続スライスへ移送する findings: なし。観察を 1 件だけ残す（欠陥ではない）。nupkg の `MessagePack` は net472・net8.0・net9.0・netstandard2.0 の lib と `MessagePackAnalyzer` の `build/MessagePackAnalyzer.targets` も持つが、取り込み物は 3.1.7 と同じ netstandard2.1 だけの slim 構成で、AC6 の path 集合の一致（3.1.7 を 3.1.9 に置換した集合と同一）で足りる
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: Unity テストは実行していない（発見 C の範囲外。判定 C が実行する）。offline の git 照合と機械検査を実行した（下記）
- テスト結果（XML 上の実行テスト名と件数）: 該当なし（テスト未実行）。照合結果（生出力の要点。判定 C が固定 head で取り直す）:
  - AC1: 範囲外の path 無し。`packages.config` の内容差分は MessagePack 3 行の `version` だけ（BOM・属性の順・`manuallyInstalled="true"` は base のまま）
  - AC2・AC4: `protocol/` と `*.asmdef` `*.cs` `*.rsp` の差分は空
  - AC3: 3.1.9 の `.meta` 19 件を、`3.1.9`→`3.1.7` で対応づけた base の `.meta` と、改行を正規化し `guid:` 行を除いて比べ、差 0 件
  - AC5: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`（`AUDIT_RESULT kind=docs files=111 errors=0 warnings=0 checks=1,2,3`）とも違反なし。検査 9 を含む contract-audit は HEAD で違反無しと出た（文字化けした日本語の出力。判定 C が取り直して収録する）
  - AC6: 3.1.9 配下の path 集合は base の 3.1.7 配下の集合の置換と一致（31 path）。`3.1.7` を含む path は 0 件
  - AC9: README の使用ライブラリ 3 表に版の列が無く、「2026-06時点の記録」の文言が無い。正本として manifest・lock・`packages.config`・DebugStudio の csproj を案内している。manifest と突き合わせ、UPM 表の取得元（URP・Addressables・Input System・uGUI・Test Framework は Unity Registry、UniTask・LitMotion・CsprojModifier・NuGetForUnity は Git）は現物と一致する。UniTask は NuGet 表から UPM 表へ移っている
  - AC10: nuget.org から取得した 3.1.9 の nupkg の sha256 は §6 の値と全件一致（`52438b79…`、`2b41bb26…`、`3fffad66…`）。非 `.meta` 12 件は全件一致する。dll 4 件は D1 の読み方（`git cat-file --filters` の実体）で一致し、`.signature.p7s` 3・nuspec 3・xml 2 は `git show` の sha256 が一致する。nuspec と xml に `--filters` を当てると autocrlf で不一致に見えるが、D1 は LFS 対象外に `git show` を使うと定めているので偽の不一致である
- 判定必須のうち未実行: 全 EditMode 回帰（`pwsh tools/run-tests.ps1`、空 filter）と、AC7・AC8 の観測（判定 C が実行する）。XML に `ProtocolGoldenCrossContractTests`、`ProtocolBackwardCompatibilityTests`、`DebugSocketProtocolRoundtripTests`、`MessagePackZLoggerFormatterTests` が含まれることの確認も判定 C
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし
- 未確認事項: S2（compile error 0）と S3（テスト green）は未確認。B の自己確認（§6 手順 7）は判定の入力にしていない。source generator の出力の同一性は S2 のとおり主張しない
- 担当・モデル: Claude Code（Sonnet 5.5、Anthropic）。Phase B（Opus 5.5、Anthropic）と異なるモデル。新しい session

### 判定 C

- 種別: 判定
- 使用した evidence bundle の取得案内・状態（§0 と同じ案内）: ローカル固定済み（未昇格）。base `e7929955` / head `0e8904fc`（tag `evidence/messagepack-unity-3-1-9/1`）で生成し、別 process の fetch で hash 照合と内容の閲覧を確認した。開始時の照合: HEAD `9863ecae` は `0e8904fc` の後の HANDOFF だけの commit（`git diff --name-only 0e8904fc..HEAD` は本ファイルのみ）、作業ツリー clean、task `messagepack-unity-3-1-9` は `active`
- 構造適合: 発見 C の記録のとおり。差分の path は責務マップ §3 の 3 種と本 HANDOFF だけで、`--name-status` は A 5・D 5・R 22・M 2（`README.md`、`packages.config`）
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: なし
- 後続スライスへ移送する findings: なし
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1`（空 filter、EditMode。§5 の判定必須＝全 EditMode 回帰）。Editor が起動しておらず `unity/Temp/UnityLockfile` も無いことを確かめ、sandbox 外の非仮想化 pwsh から起動した。ライセンス接続は `LicenseClient-void` に成功（unity.log 84 行）
- テスト結果（XML 上の実行テスト名と件数）: total 1238、passed 1238、failed 0、inconclusive 0、skipped 0、result `Passed`（test-run 09:57:59Z〜10:09:32Z）。`ProtocolGoldenCrossContractTests` 8、`ProtocolBackwardCompatibilityTests` 2、`DebugSocketProtocolRoundtripTests` 5、`MessagePackZLoggerFormatterTests` 4 がいずれも Passed で収録されている。unity.log に `error CS` は 0 件、35620 行に `Test run completed. Exiting with code 0 (Ok)`
- Unity の終了: 結果 XML の書出し（19:09 JST）と `MemoryLeaks` の出力の後、native 終了処理で停滞した。36 分後も process 一覧に残る一方で `Get-Process` / `Stop-Process` は「実行中でない」と返し、process handle が signal されない状態だった。`tools/Harness/README.md` の「Unity終了時の既知native stallと調査の停止規則」の既知事象と同じ症状なので原因調査はせず、自分で起動した Unity に付随する `UnityCrashHandler64`（PID 31812）と runner の pwsh（PID 18232）だけを終了した。そのため `step.json` と run-tests の exit code は無い。AC7・AC8 は凍結どおり実行ログと XML で判定し、この終了は raw のまま evidence bundle の `unity-tests-run-record.txt` に記録した。受容判断: 既知リスクとして受け入れる（テスト失敗・欠測・compile error のどれにも当たらない）。副作用として、Editor が `unity/Assets/MobileDependencyResolver/Editor/` の孤立 `.pdb.meta` 5 件を削除した（§6 手順 8 と同じ）ので、`git restore` で戻した
- AC1〜AC10 の照合（生出力は evidence bundle）:
  - AC1: `--name-status` の path は `README.md`、本 HANDOFF、`unity/Assets/packages.config`、`unity/Assets/Packages/MessagePack*` だけ。`packages.config` の内容差分は MessagePack 3 行の `version` だけで、BOM（`ef bb bf`）は base・head とも有り、CR は 0、属性の順と `manuallyInstalled="true"` は base のまま → 合格
  - AC2・AC4: `protocol/` と `*.asmdef` `*.cs` `*.rsp` の差分は 0 byte → 合格
  - AC3: 3.1.9 の `.meta` 19 件を base の 3.1.7 の `.meta` と、改行を正規化し `guid:` 行を除いて比較し、差 0 件 → 合格
  - AC5: `pwsh tools/contract-audit.ps1` は `AUDIT_RESULT kind=contract files=645 errors=0 warnings=0 checks=1,2,3,4,5,6,7,8,9` → 合格
  - AC6: head の `MessagePack` を含む path は 31 件で、base の 3.1.7 配下の集合の `3.1.7`→`3.1.9` 置換と完全一致（欠落・余分 0）。`3.1.7` を含む path は 0 件 → 合格
  - AC7: unity.log に compile error 無し、XML が生成され実行件数 1238 → 合格
  - AC8: failed 0、実行件数 1238、§5 の 4 テスト群を収録 → 合格
  - AC9: head の README の使用ライブラリ 3 表に版の列が無く（版らしい数値のセル 0）、「2026-06」の文言 0 件、版の正本として manifest・lock・`packages.config`・DebugStudio の csproj を案内、UniTask は UPM 表に Git (UPM) として載る。`pwsh tools/docs-audit.ps1` は `AUDIT_RESULT kind=docs files=111 errors=0 warnings=0 checks=1,2,3` → 合格
  - AC10: nuget.org から取得した 3.1.9 nupkg の sha256 は §6 の 3 値と一致。非 `.meta` 12 件は全件一致（dll 4 件は D1 のとおり `git cat-file --filters` の実体で一致し、LFS pointer の `oid` も同値。`.signature.p7s` 3・nuspec 3・xml 2 は `git show` の sha256 が一致） → 合格
- 判定: 不変条件 I1〜I5 と成立条件 S1〜S4 を全て満たす → **GO**（§2 の判定定義）。高コスト証拠は無く、動作確認は `対象なし` なので、操作・目視証拠のゲートに欠けは無い
- 判定必須のうち未実行: なし
- 未確認事項: source generator の出力の同一性は S2 のとおり主張しない。Unity の native 終了 stall は既知リスクとして記録した（上記）。DebugStudio 側の `dotnet test` は §5 のとおり再実行していない
- 担当・モデル: Claude Code（Fable 5.1、Anthropic）。Phase B（Opus 5.5）・発見 C（Sonnet 5.5）と異なるモデル。新しい session（同じ session が先に Opus 5.5 で起動し、モデル条件を満たさないとして何もせず停止した後、人間が Fable 5.1 に切り替えて再指示した）

## 8. Phase C'

- 担当方式: AI
- 種別と巡: 最終 C'（高コスト証拠は無い）、巡 1（全体）
- 受け取った決定記録の版: なし
- 使用したblind audit bundle の取得案内・状態（§0と同じ案内）: ローカル固定済み（未昇格）。取得案内は手元層の下位 directory `msgpack-cprime` の `cprime-r1.guide.md`。オーケストレーターが案内どおり fetch し（期待 packageSha256 の照合が通過）、その写しだけを C' の作業 directory に置いた
- 確認範囲・方法: bundle 内の生出力の全件読み取り。I1〜I5 を破る順序の探索と、S1〜S4・AC1〜AC10 の生出力と凍結文の照合（字義 / 読み替えの区別）
- 判定: 現在の問いを阻害する欠陥なし
- 現在の問いを阻害する findings: なし
- 後続スライスへ移送する findings: なし
- 残存リスク（C' の記載の要約）: (1) AC10 の dll 4 件は `git show` の blob ではなく LFS の実体と oid で一致しており、意味を変えない読み替えとして受け入れた。(2) テストと audit が走った checkout が `0e8904fc` そのものであることは bundle 内で閉じていない。(3) dll 4 件の LFS object を remote へ送った記録が bundle に無い。(4) 判定 C の Unity は既知の native 終了 stall で runner の exit code が無い
- 監査できなかった範囲: bundle 外の git 履歴・作業ツリー・nuget.org、`0e8904fc..9863ecae` の差分、`.meta` の platform 行の全文、audit script の検査定義、DebugStudio のテスト・PlayMode・IL2CPP Player・Build
- 独立性: 入力は blind bundle の写しと起動指示 file だけ。作業 directory は手元層の `msgpack-cprime` に限定し、HANDOFF・PR・他の所見は渡していない
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: なし
- 担当・モデル: cursor-agent（Grok 4.7 High、xAI）。Phase B（Opus 5.5）・発見 C（Sonnet 5.5）・判定 C（Fable 5.1）と異なるモデル。新しい実行
- 結果の所在: 手元層の `msgpack-cprime/cprime-r1.findings.md`。task `messagepack-unity-3-1-9` に `evidence stage` で固定（取得案内は手元層の `MESSAGEPACK_UNITY_3_1_9.cprime-findings.guide.md`）

## 9. Phase D

- C / C' の突合: 結論は一致する（判定 C は GO、C' は阻害する欠陥なし。どちらも後続スライスへの移送なし）。AC10 の dll 4 件は、C' が D1 を知らずに同じ読み方（LFS の実体で比べる）へ独立に到達した。C' の残存リスクは次のとおり閉じた。(2) `git diff --name-only 0e8904fc..9863ecae` は本ファイルだけ。(3) push 時に LFS object 4 件を origin へ送り、PR のレビューコメント（cursor[bot]）も origin から取得できることを確認している。(4) 既知の native 終了 stall として判定 C が受容済み。(1) は D1 のとおり
- マージ判断: マージする（owner の指示、2026-10-18）
- 必要なEvidenceの保存・取得・必須内容の閲覧確認: 判定 C の evidence bundle と C' blind bundle は、判定 C が stage し別 process の fetch で確認済み（blind bundle は C' の起動時にもオーケストレーターが fetch し、hash 照合が通った）。最終のレビュー所見である C' の findings を同じ task に `evidence stage` で固定し、別 process の fetch で hash 照合と閲覧を確認した。`evidence schedule status` は `passed`
- 取得案内の引渡し確認: 3 件ともメイン checkout の手元層（git 管理外）に置いた。判定 C の `MESSAGEPACK_UNITY_3_1_9.judgment-c.guide.md`、C' の blind 入力の `msgpack-cprime/cprime-r1.guide.md`、C' 所見の `MESSAGEPACK_UNITY_3_1_9.cprime-findings.guide.md`。削除予定の worktree には置いていない。引渡し先は owner
- harvest: なし（§0 のとおり恒久的な設計判断を持たない。README の現況化はこのスライスの差分そのもの）
- 削除確認: 本 commit を最終の記録とし、次の commit で本ファイルを削除する
- 通常のtask終了・後始末の状態と未完了事項: task `messagepack-unity-3-1-9` は owner の完了指示を Decision `chat:20261018T1115Z:messagepack-unity-3-1-9:completed` として `workflow-task.ps1 end -Reason completed` で記録する（マージ後）。Evidence は未昇格（`local`）のまま task 終了 + 30 日で満了する。未完了事項: なし

## 10. 決定記録

凍結した Phase A revision 1（commit `9cc336f3`）は書き換えない。以下は A3 後に人間が下した裁定で、凍結条件の意味を変えないものに限る。

### D1. AC10 の「blob の byte 一致」を Git LFS の dll でどう読むか

- 版・適用時点: 版 1。2026-10-18、Phase B 完了（commit `0e8904fc`）後、発見 C の前から適用する。
- 裁定者: 人間（owner）
- 経緯: dll 4 件は `.gitattributes` で Git LFS（`filter=lfs`）の対象で、commit 内の blob は LFS pointer になる。AC10 を字義どおり `git show <head>:<path>` の sha256 で比べると、dll は内容に関係なく必ず不一致になる。A3 で §1 現況に「3.1.7 の 12 件は blob が全件 byte 一致」と書いたが、主担当は dll 4 件を作業ツリーの実体で比べており、blob で比べたのは非 dll の 8 件だけだった。§1 のその記述は dll について不正確である。
- 裁定: AC10 の比較対象を、Git LFS の対象 path では LFS の実体とする。具体的には `git cat-file --filters <head>:<path>` の出力の sha256（LFS の smudge を通した内容）を nupkg entry の sha256 と比べる。LFS pointer の `oid sha256:` 行との一致は補助の確認として併記してよい。LFS の対象でない path は従来どおり `git show <head>:<path>` の sha256 で比べる。
- 凍結条件の意味を変えない理由: S1 は「nupkg の対応 entry と内容が一致する」ことを求めており、AC10 はその検証例である。LFS pointer は内容ではなく内容への参照なので、実体で比べる読み方は S1 の意味と合否を変えない。nupkg に無いファイルや旧版の dll が混ざれば、この読み方でも不合格になる。
- 影響範囲: このスライスの判定 C の照合手順だけ。他のスライス、常時契約、凍結済みの他の条件は変えない。本 HANDOFF は Phase D で削除する。
- C' への受け渡し: C' 自身の前巡指摘への裁定ではないので、C' 用決定記録には入れない。C' は AC10 の照合結果を evidence bundle の生出力として読む。
