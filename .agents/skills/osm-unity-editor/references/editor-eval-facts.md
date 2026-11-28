# 実 Editor を eval で操作する時の実測の事実

> **適用範囲: `tools/unity-editor.cmd eval` で長い処理・観測・測定を行う担当。**
> ここにあるのは観測した挙動で、Unity の仕様として保証されたものではない。各節に観測した時期と版を書く（この project は 2026-11-17 に 6000.6.0f1 から 6000.6.9f1 へ上がった）。版や package が変われば確かめ直す。
>
> **この文書は [SKILL.md](../SKILL.md) の契約を上書きしない。** Editor の終了は、自分で起動した Editor だけに、未保存の変更を確かめてから行う。Unity テストと Build の実行は SKILL.md の「テスト」と Phase の制限に従う。

## 起動と終了

観測: 2026-11-14（6000.6.0f1）、2026-11-20（6000.6.9f1、Library の無い worktree）。

- `unity open <project>` は Editor が終わるまで戻らない。background で流す。登録済みの同名 project が複数あると「一致する登録済みプロジェクトが複数」で即終了するので、完全な path を渡す。
- `status` が `ready` でも、起動の直後と import の間は `eval` が 503（Server Busy）を返した。`eval` が通るのを待つ時は、待つ上限を先に決める。上限を超えたら起動は未確定として報告し、無期限に再確認しない。
- Library の無い worktree では、package の解決と初回 import に 10 分ほどかかった。
- 自分で起動した Editor を、未保存の変更を確かめた後に終える時は、`eval` で `UnityEditor.EditorApplication.Exit(0)` を呼べる。応答は「Invalid response format」の error になった。終了は応答ではなく、`unity/Temp/UnityLockfile` と process の消滅で確かめる。

## eval と main thread

観測: 2026-10-31・2026-11-14（6000.6.0f1）。Play の移行の項だけ 2026-11-20（6000.6.9f1）。

- ラッパーは `menu` を通さない。menu の中身は、public static method を reflection で呼ぶ `eval` で代える（`System.Type.GetType("<FullName>, <asmdef 名>")`）。
- **「Main thread operation timed out after 5000ms」の error 応答は、結果が不明という意味で、成功でも失敗でもない。** 同期に呼んだ長い処理（content build）は、この応答の後も main thread で走り続けたことがある。同じ処理を再送しない。完了は、今回の呼び出しに対応する終端の記録（処理が最後に書く report や record）と、生成物の整合（今回の入力に対応する identity、開始より後の時刻、manifest と中身の一致）で確かめる。生成物が在るだけでは、途中で失敗した処理や前の run の残りと区別できない。確かめられなければ未確定として扱う。
- `EditorApplication.delayCall` に積んで即時に戻す方式は、Editor が idle か非フォーカスの時に tick が来ず、実行されなかった。
- 長い処理の間に送った `eval` は timeout で捨てられ、積まれなかった。前の処理の終端を確かめてから次を送る。
- asset を全件読む走査は 5000ms を超えて error になった。対象を repo 側の検索で絞ってから `eval` する。
- Play の開始・停止の移行中に送った `eval` は、Console に `Failed to handle /api/exec request: Main thread operation timed out` の Error を残した。Error の件数を数える検証では、この Error が混ざる。menu の実行と `ExitPlaymode` の後に 25〜40 秒待ってから 1 回読んだ時は残らなかったが、これは観測例で、移行の完了を保証する待ち時間ではない。Play の停止で AppDomain の中の記録が消えることがあるので、件数は `LogEntries.GetCountsByType` でも読む。
- reflection で呼んだ async の method が返す UniTask は誰も受けないので、失敗が log に出ない。

## working tree に残る差分

観測: 2026-11-20〜2026-11-23（6000.6.9f1）。

**作業の開始時に `git status --short` を取り、それを基準にする。戻す・消すのは、自分の run が生んだ差分だけである。** 開始時に既にあった差分と、人間の変更には触れない。終了後の確認は Editor を閉じた後に行う。

- **Dirty の asset は file に保存された。** `EditorSceneManager.SaveScene(scene, path, saveAsCopy: true)` と Editor の終了が、Scene が参照しない物も含めて Dirty の asset を書いた。Play snapshot の準備（`PlaySnapshotSession.Prepare`）もこの保存を通る。自分の測定で asset を Dirty にしたら、Editor を閉じる前に、その asset に限って値を戻して `EditorUtility.ClearDirty` を呼ぶか、reimport する。
- Dirty の object は `Resources.FindObjectsOfTypeAll` と `EditorUtility.IsPersistent`・`EditorUtility.IsDirty` で列挙できる。`Assets/` で絞らないと built-in と package の object が数百件出る。
- Editor を開くと `unity/Assets/MobileDependencyResolver/` の下に `*.pdb.meta` が未追跡で出た。開始時に無かった物だけを、閉じた後に消す。commit に混ぜない。
- Library の無い worktree で最初に Unity を起動すると（Editor でも batch でも）、`unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の `m_RuntimeSettings.m_List` が書き換えられて保存された。開始時に差分が無かった時だけ、閉じた後に戻す。戻した後の起動では再発しなかった。戻さないまま Player を build すると、repo と違う構成の build になる。

## 新しい worktree の Content の候補

観測: 2026-11-20（6000.6.9f1）。

- 新しい worktree には `UserSettings/` が無く、cache root は既定の場所（repo の外）になる。別の checkout が `UserSettings/OSMContentDelivery.json` で別の場所を指していると、候補の有無が checkout の間で食い違う。
- installed の候補は manifest の `unityVersion` を照合する。Editor の版を上げた後は、古い revision が「Manifest is not compatible with this runtime.」で全て落ちた。検証 Play（`VerifyPlayLauncher`）をやり直すと候補ができる。

## 値が取れる観測の対象（SampleGame）

観測: 2026-11-27（6000.6.9f1、PR #214 の判定）。asset と code の今の形に依る。

観測の対象を計画・凍結する前に、その対象から値が 1 回取れることを確かめる。次は何度待っても空だった。

- `SceneResource` の `_payloads` が空の論理 node（`HomeScene`、`OutGameSession`、`InGameScene`）は、`SceneDirector.GetContentLoadRecord` が null を返す。記録が取れたのは payload を持つ `Title` と `OutGameScene`。
- UI は `UICommon` の `PanelRenderer` に載る。`UIDocument` を探しても見つからない。`UIScene` に uGUI の Canvas は無い。
- 経路 Built + local edits は、保存済みの Scene が 1 つも開いていないと `SnapshotNothingToCarry` で拒む。開いている Scene は snapshot で運ばれ、上書きには入らない。上書きの経路を踏むには、patch が触らない Scene を開いておき、Play の後に対象の Scene を足す。

## PlayMode テスト

観測: 2026-11-10（6000.6.0f1）。6000.6.9f1 では確かめていない。原因も調べていない。

この節は、Editor で PlayMode テストを回してよい範囲を広げない。回してよいかは SKILL.md の「テスト」と Phase の制限で決まる。

この project は Play に入る時の domain reload を無効にしている（`ProjectSettings/EditorSettings.asset` の `m_EnterPlayModeOptions`）。Editor で PlayMode テストを回すと、script の reload の後の最初の 1 回だけ実行され、以降は Test Runner window からも `TestRunnerApi.Execute` からも `testcasecount=0`・`result=Passed` になった。その時は、実行の前に `EditorUtility.RequestScriptReload()` を呼ぶと再び実行された。緑かどうかではなく XML の `total` を見る。
