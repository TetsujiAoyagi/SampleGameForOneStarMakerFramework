# Phase B result

implementation base: `2c29c99806788406551affba6cc795e67e614748`
implementation head: `af6c5c9fa904847fe6abde214598bc4869dbfcc1`
担当: このセッションの Grok 4.7

## 実装

`OneStarMaker.Runtime` の `ScriptSystem` に、凍結した 9 命令、`ScriptInstruction`、`ScriptProgram`、`ScriptRegisters`、`ScriptMachine`、`ScriptUpdateElement` を置いた。テストは `OneStarMaker.Tests` の `ScriptSystem`。asmdef、SampleGame、DebugStudio、telemetry、公開 Architecture は変えていない。

行数: `ScriptOpcode.cs` 21、`ScriptMachineStatus.cs` 21、`ScriptInstruction.cs` 86、`ScriptProgram.cs` 40、`ScriptRegisters.cs` 74、`ScriptMachine.cs` 238、`ScriptUpdateElement.cs` 48、`ScriptMachineTests.cs` 312、`ScriptUpdateElementTests.cs` 122。

## HANDOFF との差

公開メンバーと命令の効果は Phase A の表のまま。加減乗の適用は private な `BinaryKind` で分岐する。メソッドグループをデリゲートへ渡すと tick で確保しうるため、凍結済みの「tick で確保しない」を守る実装詳細としてこうした。命令の追加、故障の意味、所有者は変えていない。

## この head で実行した観測

- リポジトリ外の一時 net8 プロジェクトで、Unity 参照の無い機械本体と `ScriptMachineTests` をコンパイルして実行した。exit 0。18 件成功。ログは `script-machine-tests.txt`。
- 同じソースの Release ビルドで、ウォームアップ後に `Tick(32)` を 1000 回呼び、`GC.GetAllocatedBytesForCurrentThread` の差分は sum 0、max 0。ログは `tick-allocation.txt`。この測定は全 EditMode の代替ではない。
- `pwsh tools/contract-audit.ps1` を implementation head の作業ツリーで実行した。exit 0。ログは `contract-audit.txt`。
- `pwsh tools/docs-audit.ps1` を同じ時点で実行した。exit 0。ログは `docs-audit.txt`。このログは、後から足すレビュー記録用の Markdown を含む前のものである。

## 未実行

- Unity Editor でのコンパイル確認。
- `ScriptUpdateElementTests`。`UpdateCoordinator` 側が Unity パッケージ型を参照するため、上の一時プロジェクトには含めていない。
- `pwsh tools/run-tests.ps1`。判定用の空 filter を含む。
