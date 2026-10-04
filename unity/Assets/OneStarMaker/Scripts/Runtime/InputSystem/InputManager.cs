#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using R3;
using UnityEngine.InputSystem;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 入力マネージャ。アクションの現在値を Update の早い Layer で公開する。
    /// ゲーム固有のアクション enum は知らない。ゲーム側のコントローラへは接続しない。
    /// アセットは呼び出し側が所有し、このオブジェクトは Destroy しない。
    /// Dispose では自分が有効にした Player / UI マップを両方無効にする。
    /// </summary>
    public sealed class InputManager : IDisposable
    {
        private readonly IInputDeviceReader _reader;
        private readonly IInputMapControl _maps;
        private readonly InputFrame _frame;
        private readonly Subject<InputControlMap> _mapChanged;
        private readonly InputUpdateElement _element;
        private UpdateCoordinator? _coordinator;
        private bool _registered;
        private bool _disposed;

        public InputManager(InputActionAsset asset)
        {
            var reader = new InputActionAssetReader(asset);
            _reader = reader;
            _maps = reader;
            _frame = new InputFrame(reader.Slots);
            UnsupportedActionCount = reader.UnsupportedActionCount;
            _mapChanged = new Subject<InputControlMap>();
            _element = new InputUpdateElement(this);
        }

        internal InputManager(IInputDeviceReader reader, InputActionSlot[] slots)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _maps = NoMapControl.Instance;
            _frame = new InputFrame(slots);
            UnsupportedActionCount = 0;
            _mapChanged = new Subject<InputControlMap>();
            _element = new InputUpdateElement(this);
        }

        /// <summary>
        /// Button / Vector2 以外として席にしなかったアクション数。束ね時に確定し、毎フレームは変えない。
        /// </summary>
        public int UnsupportedActionCount { get; }

        public int ActionCount => _frame.ActionCount;

        public InputControlMap ActiveMap => _frame.ActiveMap;

        public InputProfileId ActiveProfile => _frame.ActiveProfile;

        public SceneState InteractionState => _frame.InteractionState;

        /// <summary>
        /// マップが実際に変わったときだけ流す。現在値の毎フレーム配信ではない。
        /// 購読者は現在のマップを <see cref="ActiveMap"/> で読む。
        /// </summary>
        public Observable<InputControlMap> MapChanged => _mapChanged;

        /// <summary>
        /// 直近の公開バッファ。次の <see cref="Sample"/> で上書きされる。
        /// インデックスは <see cref="TryGetActionIndex"/> で一度解決し、毎フレームは数値で読む。
        /// </summary>
        public ReadOnlySpan<InputActionValue> Published => _frame.Published;

        public bool TryGetActionIndex(string? actionName, InputControlMap map, out int index)
        {
            return _frame.TryGetActionIndex(actionName, map, out index);
        }

        public bool TryRead(int index, out InputActionValue value)
        {
            return _frame.TryRead(index, out value);
        }

        /// <summary>
        /// 操作を受け付けるシーンの状態を、所有者から毎フレームまたは遷移のたびに渡す。
        /// どのシーンを渡すかはゲーム側の判断なので、このスライスは SceneDirector を購読しない。
        /// 未設定の初期値は None であり、公開値は中立のままである。
        /// </summary>
        public void SetInteractionState(SceneState state)
        {
            ThrowIfDisposed();
            _frame.SetInteractionState(state);
        }

        /// <summary>
        /// 既定プロファイルだけを選ぶ。それ以外は false で、バインドも ID も変えない。
        /// </summary>
        public bool TrySelectProfile(InputProfileId profileId)
        {
            ThrowIfDisposed();
            return _frame.TrySelectProfile(profileId);
        }

        /// <summary>
        /// Player と UI を切り替える。デバイス側の有効マップを先に変え、成功したあと公開状態を確定する。
        /// 同じマップの再設定はイベントを出さない。
        /// </summary>
        public bool TrySetMap(InputControlMap map)
        {
            ThrowIfDisposed();
            if (map != InputControlMap.Player && map != InputControlMap.UI)
            {
                return false;
            }

            // 同じマップの再指定では Enable をやり直さない。毎フレーム呼んでも割り当てない。
            if (map == _frame.ActiveMap)
            {
                return true;
            }

            // デバイス側が失敗したら公開側のマップは変えない。
            _maps.Apply(map);
            if (!_frame.TrySetMap(map, out var changed))
            {
                return false;
            }

            if (changed)
            {
                _mapChanged.OnNext(map);
            }

            return true;
        }

        /// <summary>
        /// Input Layer へ登録する。active 化は呼び出し側が行う。
        /// シーン遷移中に Update が止まると最後の公開値が残るため、
        /// 登録後は非 Stable のフレームでもこの Element の Update を止めてはならない。
        /// </summary>
        public bool TryRegister(UpdateCoordinator coordinator)
        {
            ThrowIfDisposed();
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }

            if (_registered)
            {
                return false;
            }

            var registered = coordinator.RegisterElement(
                UpdateLayerIds.Input,
                _element,
                layerOrder: UpdateLayerIds.InputLayerOrder,
                executionOrder: 0);
            if (!registered)
            {
                return false;
            }

            _coordinator = coordinator;
            _registered = true;
            return true;
        }

        public void Sample()
        {
            ThrowIfDisposed();
            _frame.Sample(_reader);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_registered && _coordinator != null)
            {
                _coordinator.UnregisterElement(_element);
                _registered = false;
                _coordinator = null;
            }

            _maps.Disable();
            _mapChanged.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InputManager));
            }
        }

        /// <summary>
        /// テスト用リーダーはアセットを持たない。マップの有効化先が無いので何もしない。
        /// </summary>
        private sealed class NoMapControl : IInputMapControl
        {
            public static readonly NoMapControl Instance = new NoMapControl();

            public void Apply(InputControlMap map)
            {
            }

            public void Disable()
            {
            }
        }

        /// <summary>
        /// Update でだけサンプルする。LateUpdate ではカメラ消費前の値を動かさない。
        /// </summary>
        private sealed class InputUpdateElement : IUpdateElement
        {
            private readonly InputManager _manager;

            public InputUpdateElement(InputManager manager)
            {
                _manager = manager;
            }

            public void OnElementStart()
            {
            }

            public void OnElementUpdate(in UpdateFrameContext context)
            {
                _manager.Sample();
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }
        }
    }
}
