#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.DebugCommands
{
    /// <summary>
    /// DebugStudio の <c>debug.set-active</c> が書き換えてよい唯一の GameObject（検証用 GameObject）を作り、所有し、破棄する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 所有者は <see cref="AppInitializer"/> で、GameObject の寿命は Editor の Play 1 回。
    /// 置き場所は呼び手が渡す scene の root（SampleGame では UICommon の scene）。hierarchy の capture は DontDestroyOnLoad を
    /// 出さず、ゲームの scene は SceneDirector が入れ替えるため、App 常駐で SceneDirector の管理外の UICommon の scene に置く。
    /// UICommon の root の子にはしない（Framework が所有する GameObject に SampleGame の GameObject を混ぜない）。
    /// </para>
    /// <para>
    /// 選択数は 1。生きた GameObject を持っている間は作らない。Play の終わりに Unity が破棄した GameObject は偽 null になるので
    /// <c>== null</c> で判定し、次の Play では新しく作る。component は Transform だけで、印や登録 API は持たない
    /// （対象の照合は <see cref="SetActiveDebugCommand"/> が NodeId で行い、名前は照合に使わない）。
    /// </para>
    /// </remarks>
    internal sealed class DebugVerificationTargetHost
    {
        /// <summary>hierarchy で検証用 GameObject を見つけるための名前。CLI README に同じ値を固定している。</summary>
        internal const string TargetName = "[DebugStudioVerificationTarget]";

        private GameObject? _target;

        /// <summary>生きている検証用 GameObject。未作成・解放後・破棄済みは null（偽 null を外へ出さない）。</summary>
        internal GameObject? Target => _target != null ? _target : null;

        /// <summary>
        /// Editor のときだけ、渡された scene の root に検証用 GameObject を 1 つ作る。
        /// </summary>
        /// <param name="isEditor">
        /// <see cref="Application.isEditor"/> を渡す。false なら作らない（Player の hierarchy を変えない）。
        /// </param>
        /// <param name="scene">置き場所。無効・未ロードなら作らない（active scene に取り残さない）。</param>
        /// <returns>今回作ったときだけ true。既に生きた GameObject がある場合は false。</returns>
        internal bool TryCreate(bool isEditor, Scene scene)
        {
            if (!isEditor || _target != null || !scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            var target = new GameObject(TargetName);
            SceneManager.MoveGameObjectToScene(target, scene);
            _target = target;
            return true;
        }

        /// <summary>
        /// 検証用 GameObject を破棄して所有を手放す。冪等で、Unity が先に破棄していても失敗しない。
        /// SubsystemRegistration（Domain Reload 無効時の前回 Play の残り）と AfterSceneLoad の途中失敗から呼ばれる。
        /// </summary>
        internal void Release()
        {
            var target = _target;
            _target = null;
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
