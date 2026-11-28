# 実 Editor を eval で操作する時の実測の事実

> **適用範囲: `tools/unity-editor.cmd eval` で長い処理・観測・測定を行う担当。**
> Unity 6000.6.9f1 と Pipeline server で確かめた挙動で、版が変われば確かめ直す。入口と権限は [SKILL.md](../SKILL.md)。

## 起動と終了

- `unity open <project>` は Editor が終わるまで戻らない。background で流す。登録済みの同名 project が複数あると「一致する登録済みプロジェクトが複数」で即終了するので、完全な path を渡す。
- `status` が `ready` でも、起動の直後と import の間は `eval` が 503（Server Busy）を返す。`eval` が通るまで待つ。
- Library の無い worktree では、package の解決と初回 import に 10 分ほどかかる。
- 終了は `eval` で `UnityEditor.EditorApplication.Exit(0)` を呼ぶ。応答は「Invalid response format」の error になるが正常である。`unity/Temp/UnityLockfile` と process の消滅で確かめる。

## eval と main thread

- ラッパーは `menu` を通さない。menu の中身は、public static method を reflection で呼ぶ `eval` で代える（`System.Type.GetType("<FullName>, <asmdef 名>")`）。
- **同期に呼んだ長い処理は、「Main thread operation timed out after 5000ms」の error 応答が返った後も、main thread で最後まで走る。** error を失敗と読まない。完了は、その処理が書く生成物の出現で判定する。
- `EditorApplication.delayCall` に積んで即時に戻す方式は使わない。Editor が idle か非フォーカスだと tick が来ず、実行されない。
- 長い処理の間に送った `eval` は timeout で捨てられ、積まれない。前の処理の完了を確かめてから次を送る。
- asset を全件読む走査は 5000ms を超えて error になる。対象を repo 側の検索で絞ってから `eval` する。
- Play の開始・停止の移行中に送った `eval` は、Console に `Failed to handle /api/exec request: Main thread operation timed out` の Error を残す。Error の件数を数える検証では混ざるので、menu の実行と `ExitPlaymode` の後は 25〜40 秒待ってから 1 回読む。Play の停止で AppDomain の中の記録が消えることがあるので、件数は `LogEntries.GetCountsByType` でも読む。
- reflection で呼んだ async の method が返す UniTask は誰も受けないので、失敗が log に出ない。

## working tree に残る差分

`git status --short` の確認は Editor を閉じた後に行う。

- **Dirty の asset は file に保存される。** `EditorSceneManager.SaveScene(scene, path, saveAsCopy: true)` と Editor の終了が、Scene が参照しない物も含めて Dirty の asset を全て書く。Play snapshot の準備（`PlaySnapshotSession.Prepare`）もこの保存を通る。測定で asset を Dirty にしたら、Editor を閉じる前に値を戻して `EditorUtility.ClearDirty` を呼ぶか、reimport する。
- Dirty の object は `Resources.FindObjectsOfTypeAll` と `EditorUtility.IsPersistent`・`EditorUtility.IsDirty` で列挙できる。`Assets/` で絞らないと built-in と package の object が数百件出る。
- Editor を開くと `unity/Assets/MobileDependencyResolver/` の下に `*.pdb.meta` が未追跡で出る。閉じた後に消し、commit に混ぜない。
- Library の無い worktree で最初に Unity を起動すると（Editor でも batch でも）、`unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の `m_RuntimeSettings.m_List` が書き換えられて保存される。閉じた後に戻す。戻した後の起動では再発しない。Player を build する前に必ず戻す。

## 新しい worktree の Content の候補

- 新しい worktree には `UserSettings/` が無く、cache root は既定の場所（repo の外）になる。別の checkout が `UserSettings/OSMContentDelivery.json` で別の場所を指していると、候補の有無が checkout の間で食い違う。
- installed の候補は manifest の `unityVersion` を照合する。Editor の版を上げた後は、古い revision が「Manifest is not compatible with this runtime.」で全て落ちる。検証 Play（`VerifyPlayLauncher`）を 1 回やり直す。開いている Scene は空の 1 つにしておく。

## 値が取れる観測の対象（SampleGame）

観測の対象を計画・凍結する前に、その対象から値が 1 回取れることを実 Editor で確かめる。次は何度待っても空になる。

- `SceneResource` の `_payloads` が空の論理 node（`HomeScene`、`OutGameSession`、`InGameScene`）は、`SceneDirector.GetContentLoadRecord` が常に null を返す。記録が取れるのは payload を持つ `Title` と `OutGameScene`。
- UI は `UICommon` の `PanelRenderer` に載る。`UIDocument` を探しても見つからない。`UIScene` に uGUI の Canvas は無い。
- 経路 Built + local edits は、保存済みの Scene が 1 つも開いていないと `SnapshotNothingToCarry` で拒む。開いている Scene は snapshot で運ばれ、上書きには入らない。上書きの経路を踏むには、patch が触らない Scene を開いておき、Play の後に対象の Scene を足す。

## PlayMode テスト

この project は Play に入る時の domain reload を無効にしている（`ProjectSettings/EditorSettings.asset` の `m_EnterPlayModeOptions`）。Editor で PlayMode テストを回すと、script の reload の後の最初の 1 回だけ実行され、以降は Test Runner window からも `TestRunnerApi.Execute` からも `testcasecount=0`・`result=Passed` になった（6000.6.0f1 で観測。原因は調べていない）。実行の前に `EditorUtility.RequestScriptReload()` を呼び、緑かどうかではなく XML の `total` を見る。
