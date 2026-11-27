#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    // ─── DebugSnapshot ───
    // CaptureDebugSnapshot（観測用の写し）

    public partial class SceneDirector
    {
        /// <summary>
        /// 今の管理集合・各 Scene の <see cref="SceneState"/>・unload の待ち・遷移の履歴を、一つの時点の不変の値に写す。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 観測は SceneDirector の挙動を変えてはならない（05-scene.md §5.12）。そのためここでは field を読むだけにする:
        /// field に書かない、event を出さない、SceneBase の method・<see cref="GetLoadedScene"/>・SceneResource を呼ばない、
        /// 履歴の stack は列挙するだけにする（stack を変える method を呼ぶ箇所を作らない）。
        /// 読む getter（<c>SceneBase.Lifecycle</c>、<c>SceneLifecycleManager.State</c>）は field を返すだけである。
        /// </para>
        /// <para>
        /// main thread から呼ぶ。await を挟まずに一続きで読むので、返す値は一つの時点の値になる。状態の変更は全て main thread の
        /// async method の中で起きるので、操作の途中（PreLoading、LoadCanceled など）の状態はそのまま写る。
        /// Dispose 済みの SceneDirector は扱わない（initializer の ReleaseAll が参照を null にすることに依存する）。
        /// </para>
        /// </remarks>
        internal SceneDirectorDebugSnapshot CaptureDebugSnapshot()
        {
            var scenes = new SceneDirectorDebugSnapshot.SceneEntry[_currentScenes.Count];
            var i = 0;
            foreach (var kvp in _currentScenes)
            {
                scenes[i++] = new SceneDirectorDebugSnapshot.SceneEntry(kvp.Key, kvp.Value.SceneBase.Lifecycle.State);
            }

            var pendingUnloads = new string[_pendingUnloads.Count];
            _pendingUnloads.CopyTo(pendingUnloads);

            // Stack の列挙は新しい順（top から）。
            var history = new SceneDirectorDebugSnapshot.HistoryEntry[_sceneHistory.Count];
            i = 0;
            foreach (var entry in _sceneHistory)
            {
                history[i++] = new SceneDirectorDebugSnapshot.HistoryEntry(entry.FromSceneId, entry.ToSceneId);
            }

            return new SceneDirectorDebugSnapshot(scenes, pendingUnloads, history, CanGoBack);
        }
    }
}
