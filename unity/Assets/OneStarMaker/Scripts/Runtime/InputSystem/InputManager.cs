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
    /// 入力マネージャ。アクションの現在値を Update の早い Layer で公開する。全 API は main thread 専用。
    /// ゲーム固有のアクション enum は知らない。ゲーム側のコントローラへは接続しない。
    /// アセットは呼び出し側が所有し、Dispose まで生存させる。この型は Destroy しない。
    /// Player/UI は専用 asset の排他的 lease で、PlayerInput や UI module と共有しない。
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
        private bool _mapFaulted;

        public InputManager(InputActionAsset asset)
        {
            var reader = new InputActionAssetReader(asset);
            _reader = reader;
            _maps = reader;
            _frame = new InputFrame(reader.Slots);
            UnsupportedActionCount = reader.UnsupportedActionCount;
            _mapChanged = new Subject<InputControlMap>();
            _element = new InputUpdateElement(this);
            try { _maps.Apply(InputControlMap.Player); }
            catch
            {
                // 検証完了後の lease 取得失敗は閉じ、元の例外を維持する。
                BestEffortDisable();
                _mapChanged.Dispose();
                throw;
            }
        }

        internal InputManager(IInputDeviceReader reader, InputActionSlot[] slots, IInputMapControl? maps = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _maps = maps ?? NoMapControl.Instance;
            _frame = new InputFrame(slots);
            UnsupportedActionCount = 0;
            _mapChanged = new Subject<InputControlMap>();
            _element = new InputUpdateElement(this);
            try { _maps.Apply(InputControlMap.Player); }
            catch
            {
                // 検証完了後の lease 取得失敗は閉じ、元の例外を維持する。
                BestEffortDisable();
                _mapChanged.Dispose();
                throw;
            }
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
        /// 直近の公開バッファ。Sample、停止、マップ変更、Dispose で上書きされる。
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
            if (map == _frame.ActiveMap && !_mapFaulted)
            {
                return true;
            }

            // native 操作より前に停止し、失敗後は選択の成功まで読み取りを再開しない。
            _frame.Neutralize();
            try { _maps.Apply(map); }
            catch
            {
                _mapFaulted = true;
                BestEffortDisable();
                throw;
            }
            _mapFaulted = false;
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
        /// main thread で標準 runtime に登録する。host の scene 安定 gate が初回 activation を所有する。
        /// host 未導入または登録失敗なら再試行でき、成功後の重複登録は false。
        /// 所有者は host 終了前に Dispose する。
        /// </summary>
        public bool TryRegister()
        {
            ThrowIfDisposed();
            if (_registered) return false;
            var coordinator = UpdateSystemRuntime.Coordinator;
            if (coordinator == null) return false;
            if (!UpdateSystemRuntime.RegisterElement(UpdateLayerIds.Input, _element,
                layerOrder: UpdateLayerIds.InputLayerOrder, executionOrder: 0)) return false;
            _coordinator = coordinator;
            _registered = true;
            return true;
        }

        public void Sample()
        {
            ThrowIfDisposed();
            if (_mapFaulted) return;
            _frame.Sample(_reader);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _frame.Neutralize();
            _disposed = true;
            if (_registered && _coordinator != null)
            {
                _coordinator.UnregisterElement(_element);
                _registered = false;
                _coordinator = null;
            }

            try { _maps.Disable(); }
            finally { _mapChanged.Dispose(); }
        }

        private void BestEffortDisable()
        {
            try { _maps.Disable(); }
            catch { /* 元の native 失敗が所有者の判断に必要なので置き換えない。 */ }
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
                // structural removal は遅延されるため Dispose 後の同フレーム callback を無害化する。
                if (!_manager._disposed) _manager.Sample();
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }
        }
    }
}
