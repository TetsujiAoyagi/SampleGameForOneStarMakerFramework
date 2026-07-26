#nullable enable

using UnityEngine;

namespace SampleGame.InGame.Player
{
    /// <summary>
    /// PlayerScene.unity に配置する配線用コンポーネント。
    /// SceneBase はシーン内参照をここから解決し、ランタイムで GameObject を組み立てない。
    /// Camera / AudioListener は置かない（CameraSystemHost の View_Main が唯一の描画オーナー）。
    /// </summary>
    public sealed class PlayerRigBindings : MonoBehaviour
    {
        [SerializeField] private FlyController _flyer = null!;
        [SerializeField] private Transform _followTarget = null!;
        [SerializeField] private Transform _lookAtTarget = null!;

        /// <summary>飛行コントローラ（移動・入力の所有者）。</summary>
        public FlyController Flyer => _flyer;

        /// <summary>
        /// CameraSystem Follow に渡す Transform。
        /// 機体ルート直結だと一人称相当になるため、シーン側で後方オフセット子を置く。
        /// </summary>
        public Transform FollowTarget => _followTarget;

        /// <summary>
        /// CameraSystem LookAt に渡す Transform。
        /// マウスピッチはカメラを回さず、この注視点を機体ローカルで上下させる。
        /// </summary>
        public Transform LookAtTarget => _lookAtTarget;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_flyer == null)
            {
                _flyer = GetComponentInChildren<FlyController>();
            }

            if (_followTarget == null)
            {
                _followTarget = transform;
            }
        }
#endif
    }
}
