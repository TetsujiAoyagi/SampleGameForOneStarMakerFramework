#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using SampleGame.InGame.LevelStreaming;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SampleGame.InGame.Player
{
    /// <summary>
    /// 四季マップ巡回用の飛行コントローラ（New Input System）。
    /// カメラ本体は持たない。視点は CameraSystem の Follow/LookAt に任せ、
    /// ここでは機体 yaw と LookAt ターゲットのローカルピッチだけを更新する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlyController : MonoBehaviour, IFlightReadModel
    {
        [SerializeField] private float _moveSpeed = 42f;
        [SerializeField] private float _boostMultiplier = 2.4f;
        [SerializeField] private float _lookSensitivity = 0.12f;
        [SerializeField] private float _verticalSpeed = 32f;
        [SerializeField] private float _lookAtDistance = 8f;
        [SerializeField] private Transform? _lookAtTarget;

        private Rigidbody _body = null!;
        private float _yaw;
        private float _pitch;
        private bool _inputEnabled = true;
        private bool _cursorLocked = true;
        private CancellationTokenSource? _teleportCts;

        /// <inheritdoc />
        public Vector3 Position => transform.position;

        /// <inheritdoc />
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                _inputEnabled = value;
                // ブートストラップ待ち（入力オフ）中はカーソルをロックしない。
                // 有効化時は飛行操作のためロック、無効化時は UI / 待機用に解除。
                if (isActiveAndEnabled)
                {
                    SetCursorLocked(value);
                }
            }
        }

        /// <summary>
        /// シーン配置の LookAt ターゲットを結びつける。
        /// CameraSystem.SetLookAt には同じ Transform を渡す。
        /// </summary>
        public void Configure(Transform lookAtTarget)
        {
            _lookAtTarget = lookAtTarget ?? throw new System.ArgumentNullException(nameof(lookAtTarget));
            ApplyLook();
        }

        public void Teleport(Vector3 worldPosition, Vector3 lookForward)
        {
            transform.position = worldPosition;
            _yaw = Mathf.Atan2(lookForward.x, lookForward.z) * Mathf.Rad2Deg;
            _pitch = 0f;
            ApplyLook();
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            // InputEnabled が false の間（Player bootstrap 待ち）はロックしない。
            SetCursorLocked(_inputEnabled);
        }

        private void OnDisable()
        {
            _teleportCts?.Cancel();
            SetCursorLocked(false);
        }

        private void OnDestroy()
        {
            _teleportCts?.Cancel();
            _teleportCts?.Dispose();
            _teleportCts = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(!_cursorLocked);
            }

            HandleDebugTeleport(keyboard);

            if (!_inputEnabled || !_cursorLocked)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * _lookSensitivity;
                _pitch -= delta.y * _lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);
                ApplyLook();
            }
        }

        private void FixedUpdate()
        {
            // 暫定: LevelStreamCoordinator.Current を直接参照。
            // Session サービス面へ寄せるのは Level 書き直し時にまとめて行う。
            if (!_inputEnabled || LevelStreamCoordinator<InGameSession>.Current is { IsTransitionBusy: true })
            {
                _body.linearVelocity = Vector3.zero;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var input = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

            var vertical = 0f;
            if (keyboard.spaceKey.isPressed) vertical += 1f;
            if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed) vertical -= 1f;

            var boost = keyboard.leftShiftKey.isPressed ? _boostMultiplier : 1f;
            var planar = transform.TransformDirection(new Vector3(input.x, 0f, input.z));
            if (planar.sqrMagnitude > 1f)
            {
                planar.Normalize();
            }

            var velocity = planar * (_moveSpeed * boost) + Vector3.up * (vertical * _verticalSpeed * boost);
            _body.linearVelocity = velocity;
        }

        /// <summary>
        /// 機体は yaw のみ。ピッチは LookAt ターゲットを機体前方へ飛ばして表現する。
        /// （旧実装のように CameraPivot をプレイヤー配下で回さない。）
        /// </summary>
        private void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_lookAtTarget == null)
            {
                return;
            }

            var localForward = Quaternion.Euler(_pitch, 0f, 0f) * Vector3.forward;
            _lookAtTarget.localPosition = localForward * _lookAtDistance;
        }

        private void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleDebugTeleport(Keyboard? keyboard)
        {
            if (keyboard == null || !_inputEnabled)
            {
                return;
            }

            string? target = null;
            if (keyboard.f1Key.wasPressedThisFrame) target = "SpringLevel";
            else if (keyboard.f2Key.wasPressedThisFrame) target = "SummerLevel";
            else if (keyboard.f3Key.wasPressedThisFrame) target = "AutumnLevel";
            else if (keyboard.f4Key.wasPressedThisFrame) target = "WinterLevel";

            if (target == null)
            {
                return;
            }

            _teleportCts?.Cancel();
            _teleportCts?.Dispose();
            _teleportCts = new CancellationTokenSource();
            DebugTeleportAsync(target, _teleportCts.Token).Forget();
        }

        private async UniTaskVoid DebugTeleportAsync(string target, CancellationToken ct)
        {
            var coordinator = LevelStreamCoordinator<InGameSession>.Current;
            InputEnabled = false;
            try
            {
                if (coordinator != null)
                {
                    await coordinator.EnsureLevelLoadedAsync(target, ct);
                    coordinator.DebugForceArrive(target);
                }

                Teleport(SeasonWorldCatalog.SpawnPosition(target), Vector3.forward);
            }
            catch (System.OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                InputEnabled = true;
            }
        }
    }
}
