# Unity 側 MessagePack 3.1.9 更新と README 版表の現況化

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿。A2 入力版）
- branch: `claude/messagepack-3.1.9-unity`
- implementation base commit: `e7929955`（PR #136 のマージ。DebugStudio 側は 3.1.9 済み）
- implementation head commit: 未固定
- risk: `normal`（理由は §1 現況の末尾）
- owner: root
- created: 2026-10-18
- expires: 2026-11-15
- harvest to: なし（恒久的な設計判断を持たない。README の現況化そのものが公開面の更新）
- Phase A snapshot の取得案内・状態: 未生成（A2 入力版は本ファイルの commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A2（独立レビュー。1件以上、うち1件はアーキテクチャゲート）
  - 次の作業: 本ファイルの A2 入力版を読み、§2 の不変条件を破る順序と、§7 未決事項への意見を返す
  - 引き継ぐ commit SHA: A2 入力版の commit（本ファイルを追加した commit）
  - 必要な権限: なし（読むだけ）
  - 停止規則: A2 は互いの指摘を見せない。実装しない
  - 引き渡さないもの: なし

## 1. 目的と対象外

- 目的:
  1. Unity 側の NuGet パッケージ `MessagePack` / `MessagePack.Annotations` / `MessagePackAnalyzer` を 3.1.7 から 3.1.9 へ上げ、DebugStudio 側（PR #136 で 3.1.9）と版を揃える。
  2. ルート `README.md` の「使用ライブラリ」版表を、公開面の規則（今この瞬間に真であること）に合わせる。
- 対象外:
  - 他の NuGet パッケージ（`Microsoft.*`、`System.*`、Cysharp 系、VContainer）の版変更。
  - UPM パッケージ（manifest / lock）の変更。
  - DebugStudio 側の csproj 変更（#136 で完了済み）。
  - `MessagePackSerializerOptions` の変更（`UntrustedData` 化など）。DebugSocket の受信を信頼できない入力として扱うかは別の問い。
  - IL2CPP Player 上の DebugSocket 疎通（§2「ここでは答えない問い」）。
- 現況:
  - `unity/Assets/packages.config` は MessagePack 3 件が 3.1.7。`unity/Assets/Packages/` に `MessagePack.3.1.7` / `MessagePack.Annotations.3.1.7` / `MessagePackAnalyzer.3.1.7` が tracked で置かれている。
  - nuget.org の 3.1.9 の nuspec は、netstandard2.1 group の依存が 3.1.7 と同一（`MessagePackAnalyzer` / `MessagePack.Annotations` が同版、`Microsoft.NET.StringTools` 17.11.4、`System.Collections.Immutable` 8.0.0）。他の NuGet パッケージの版を動かす必要は無い。
  - asmdef は `OneStarMaker.Foundation` と `OneStarMaker.Tests` が `MessagePack.dll` / `MessagePack.Annotations.dll` をファイル名で precompiled 参照しており、GUID 参照ではない。
  - `MessagePackAnalyzer` の 2 つの dll（`MessagePack.SourceGenerator.dll`、`MessagePack.Analyzers.CodeFixes.dll`）の `.meta` は label `NuGetForUnity` / `RoslynAnalyzer` と全 platform 無効の PluginImporter を持つ。これが崩れると source generator が動かないか、analyzer dll が Player に入る。
  - wire 互換は `protocol/debugsocket/` の golden fixture を Unity（`OneStarMaker.Tests.Foundation.ProtocolGoldenCrossContractTests`）と DebugStudio（`DebugStudio.Contracts.Tests` の同名テスト）の双方が読むことで検査している。DebugStudio 側は #136 の CI で 3.1.9 のまま green。
  - 3.1.5〜3.1.9 の変更点は主にセキュリティ修正（深い入れ子・LZ4・長さ検証の拒否、disallow list の追加）と、`DateTimeOffset` 符号化変更の取り消し（3.1.5 内で revert 済み）。DebugSocket の DTO は `MessagePackSerializerOptions.Standard` を使い、Typeless は使っていない。
  - README の版表は「2026-06時点の記録」と自称し、MessagePack 以外にも現物と違う行がある（URP 17.5.0→17.6.0、Addressables 2.9.1→2.11.2、Input System 1.19.0→1.20.0、uGUI 2.5.0→2.6.0、Test Framework 1.7.0→1.8.0、R3 1.3.0→1.3.1、UniTask は Git 参照なのに 2.5.10 と記載）。docs 方針の公開面ルール 1 に反している。
  - risk を `normal` とする理由: 同一 major・minor 内の patch 更新で、依存グラフ、asmdef、公開 API、寿命を変えない。phases-and-handoff の高リスク例「依存・asmdef を変える」には、参照先 dll の版だけが変わり参照関係が変わらないので当たらないと判断する。A2 で異論があれば A3 で上げる。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Unity 側を MessagePack 3.1.9 に上げても、ビルド構成・wire 互換・既存テストを壊さずに DebugStudio と同版にできるか。
- 進める最低条件:
  - 不変条件:
    - I1. MessagePack 3 件以外の NuGet パッケージの版と、`unity/Packages/manifest.json` / `packages-lock.json` が変わらない。
    - I2. `protocol/debugsocket/` の golden fixture が変わらない（wire 形式を書き換えて合わせない）。
    - I3. analyzer dll 2 件の `.meta` が label `RoslynAnalyzer` を持ち、全 platform 無効のまま（Player に analyzer が入らない）。
    - I4. asmdef と `#nullable` 等の常時契約に関わる C# ソースが変わらない。
    - I5. `tools/path-budget.json` の予算を超えない。
  - 成立条件:
    - S1. `unity/Assets/packages.config` の MessagePack 3 件が 3.1.9 で、`unity/Assets/Packages/` に 3.1.9 の 3 ディレクトリだけがあり、3.1.7 のディレクトリが残っていない。
    - S2. Unity Editor が 3.1.9 で compile error 0 で開く。
    - S3. 判定必須テスト（§5）が green。
    - S4. README の版表が、現物（manifest / lock / packages.config / DebugStudio csproj）と食い違わない（形式は §7 の未決事項 U1 で A3 に決める）。
- 受け入れ条件:
  - AC1（I1）: `git diff <base>..<head> --stat` の変更が `unity/Assets/packages.config`、`unity/Assets/Packages/MessagePack*`、`README.md`、本 HANDOFF に限られる。`packages.config` の差分は MessagePack 3 行の版だけ。失敗例: NuGetForUnity の restore が他パッケージを再取得し、`Microsoft.*` の `.meta` や版が変わる → B で元に戻す。
  - AC2（I2）: `git diff <base>..<head> -- protocol/` が空。
  - AC3（I3）: 3.1.9 の analyzer dll 2 件の `.meta` に `RoslynAnalyzer` label があり、`platformData` の `Any` / `Editor` / 各 platform が `enabled: 0`。失敗例: 手動複写で `.meta` が既定 importer で生成され `Any: enabled: 1` になる → 不合格。
  - AC4（I4）: `git diff <base>..<head> -- '*.asmdef' '*.cs'` が空。
  - AC5（I5）: `pwsh tools/contract-audit.ps1` が検査 9 を含めて pass。
  - AC6（S1）: `git ls-files unity/Assets/Packages | grep -i messagepack` が 3.1.9 の 3 ディレクトリ配下だけを返す。
  - AC7（S2）: Phase B の Editor compile 確認ログに error が無い。
  - AC8（S3）: §5 の判定必須テストの XML で failed 0、実行件数 1 以上。
  - AC9（S4）: README の MessagePack 2 行が 3.1.9、または版の列が無く正本ファイルへの参照になっている（U1 の決定による）。`pwsh tools/docs-audit.ps1` が pass。
- ここでは答えない問いと所有する後続スライス:
  - IL2CPP Player 上で DebugSocket が 3.1.9 で動くか → 所有スライス無し。Player の DebugSocket 疎通を扱う次のスライスの着手時に入れる。今回の更新で動的コード生成や resolver 構成は変えないため、本スライスの問いには必要ない。
  - DebugSocket の受信を `MessagePackSecurity.UntrustedData` にすべきか → 所有スライス無し。必要と判断したときに別スライスを切る。
  - VContainer の削除（README で「将来削除予定」） → 所有スライス無し。本スライスでは触らない。
- 判定定義: 不変条件 I1〜I5 と成立条件 S1〜S4 を全て満たせば GO。どれか一つでも満たさなければ NO-GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity側 C# で `record` を使わない、`#nullable enable` を外さない（本スライスは C# を変えない）。
  - Editor を開く前に、対象 project と既存 Editor、`unity/Temp/UnityLockfile` を確認する。人間所有の Editor を無断で終了しない。
  - Unity テストは `pwsh tools/run-tests.ps1` だけを使い、`unity test` / `unity run` は使わない。Phase B では Unity テストを実行しない。
  - 本番の Scene / Prefab / Addressables を変更しない。
- 未決事項: §7 に記載。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|
| `unity/Assets/packages.config` | NuGetForUnity の復元入力 | MessagePack 3 行の版 | NuGetForUnity / 永続 | 公開面なし | AC1、AC6 | 27 → 27 |
| `unity/Assets/Packages/MessagePack*.3.1.9/**` | NuGet の取り込み物（dll、xml、nuspec、署名、`.meta`） | 3.1.7 から置換 | NuGetForUnity が生成 / 永続 | asmdef からファイル名で参照 | AC3、AC6、AC7 | 3.1.7 と同構成（dll 4、xml 2、nuspec 3、署名 3 と各 `.meta`） |
| `README.md` | 使用ライブラリの紹介 | 版表の現況化 | 公開面 / 永続 | なし | AC9 | 版表 約 30 行 → 同程度以下 |

新しいコード、責務、型は追加しない。500 行・3 責務・50% 増の警報は発火しない。

## 4. 実装計画

- 変更対象: §3 の 3 種。
- 順序:
  1. Editor が閉じていることを確認する。
  2. `packages.config` の MessagePack 3 行を 3.1.9 にする。
  3. Editor を開き、NuGetForUnity の復元（または Manage NuGet Packages の Update）で 3.1.9 を取り込む。3.1.7 のディレクトリが残れば削除する。
  4. AC3 を確認する。analyzer の `.meta` が崩れていれば、NuGetForUnity の再 import で直す。手で GUID や label を書き換えて合わせることはしない（それでも直らなければ A へ戻す）。
  5. compile error 0 を確認し、Editor を正常終了する。
  6. `git status` で AC1 の範囲外の変化が無いことを確認する。範囲外の変化（他パッケージの `.meta` 再生成など）は戻す。
  7. README を §7 U1 の A3 決定どおりに直す。
  8. `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` を通して commit する。
- Phase B から Phase A へ差し戻す条件:
  - 3.1.9 の取り込みに、MessagePack 3 件以外の版変更や新しい NuGet 依存が必要になった。
  - asmdef か C# ソースの変更が必要になった（compile error の解消を含む）。
  - analyzer `.meta` を NuGetForUnity の import で AC3 の形にできない。
- 対象外を維持する方法: AC1 の `--stat` 照合を B の出口で行う。

## 5. テストとレビュー計画

- 単体テスト: 追加しない。新しいロジックが無い。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Foundation`（DebugSocket・Protocol・MessagePackZLoggerFormatter のテストを含む namespace）。
- 判定必須テスト:
  - 全 EditMode 回帰（`pwsh tools/run-tests.ps1`、空 filter）。XML に `ProtocolGoldenCrossContractTests`、`ProtocolBackwardCompatibilityTests`、`DebugSocketProtocolRoundtripTests`、`MessagePackZLoggerFormatterTests` が含まれることを確認する。
  - DebugStudio 側の `dotnet test tools/DebugStudio/DebugStudio.sln` は #136 の CI で 3.1.9 のまま green。本スライスは DebugStudio を変えないので再実行を必須にしない。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 上記のみ。PlayMode と Build は実行しない（Player は §2 で答えない問い）。
- 操作・実行時・目視条件の検証経路: S2 の compile 確認は Phase B の Editor 操作で取る（B の実行担当、ローカル Windows、Editor 未起動の状態から project を開き、Console と Editor.log の compile error を観測）。それ以外の条件はテストと git 差分で閉じる。
- 未知の操作経路の疎通結果: なし（NuGetForUnity の復元は既存の経路。nuget.org への通信が要る）。
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（AC1〜AC6 は git コマンドの出力で判定する）
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: Claude Code（Opus 5.5、Anthropic）
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 未実施
- A3 統合担当・モデル・採否: 未実施
- C' 用に予約した担当・モデル・ベンダー: 未定（A3 で決める）
- 独立性の強化条件を満たせない場合の理由: risk `normal` のため強化条件は対象外

## 6. Phase B 実装結果

未着手。

## 7. 未決事項（A3 で決める）

- U1. README 版表の直し方。
  - 案 a（推奨）: 版の列を外し、ライブラリ名・用途・取得元（UPM / NuGet / Git）だけを残す。版は正本ファイル（`unity/Packages/manifest.json`、`packages-lock.json`、`unity/Assets/packages.config`、DebugStudio の各 csproj）を見るよう一文で案内する。同じ事実が README と正本の 2 箇所にあると、片方だけ更新されて腐る。今回の 7 行のずれがその実例。
  - 案 b: 版の列を残し、全行を現物に合わせる（MessagePack 2 行に加え、上記 7 行も直す）。「2026-06時点の記録」の文言を外す。次の依存更新で再び腐るので、更新時に README も直す運用が要る。
  - 案 c: MessagePack 2 行だけ 3.1.9 にする。他の 7 行のずれが残り、公開面ルール 1 に反したままになる。
- U2. risk を `normal` のままにするか（§1 の理由に A2 の異論があれば `high` にし、独立レビューを増やす）。
