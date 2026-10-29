#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using UnityEngine;
using ZLogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Events Scene に置いた trigger 1 つ分の、局所 VFX の起動・停止の判断。Unity の callback を持たない純 C# で、
    /// <see cref="LocalVfxEventTrigger"/> から在室の出来事を、<see cref="LocalVfxEventBinder"/> から準備と終了を受け取る。
    /// <para>
    /// 状態は 準備中 → 準備済み / 準備失敗 → 終了 の一方向で、終了は終端。終了の後は進入も準備の完了も何もしない
    /// （Scene の unload が始まった trigger や、破棄された trigger から起動しない）。
    /// </para>
    /// <para>
    /// 寿命: instance の所有者は trigger の GameObject（<c>Bind</c>）、source asset の所有者は Events Scene（<c>Scene(id)</c>）。
    /// この session は source の handle を持たず、解放しない（Scene の <c>ReleaseScene</c> が解放する）。
    /// 終了で生存中の instance を <see cref="IVfxHandle.Stop"/> で回収するので、Cell の unload では
    /// Unity Scene の unload と source の解放より前に instance が消える。<c>Bind</c> の破棄通知はこの回収に頼らない保険。
    /// </para>
    /// <para>
    /// 在室は進入者の Rigidbody の id で数え、生存 instance は高々 1 つ。1 つの Rigidbody が trigger に重なる
    /// Collider を複数持つ場合と、退出の通知が来ない破棄は扱わない（片方の退出で在室が消える・在室が残る）。
    /// </para>
    /// </summary>
    internal sealed class LocalVfxEventSession
    {
        private enum State
        {
            Preparing,
            Ready,
            Failed,
            Ended,
        }

        private readonly IVfxSystem _vfx;
        private readonly AssetOwner _owner;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly string _label;
        private readonly ILogger _logger;
        private readonly HashSet<ulong> _occupants = new();

        private State _state = State.Preparing;
        private Object? _source;
        private IVfxHandle? _handle;

        /// <param name="owner">起動する instance の所有者（trigger の GameObject の <c>Bind</c>）。</param>
        /// <param name="label">ログで trigger を見分ける名前（GameObject 名と address 等）。</param>
        internal LocalVfxEventSession(
            IVfxSystem vfx,
            AssetOwner owner,
            Vector3 position,
            Quaternion rotation,
            string label,
            ILogger logger)
        {
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _owner = owner;
            _position = position;
            _rotation = rotation;
            _label = label ?? string.Empty;
        }

        internal bool IsEnded => _state == State.Ended;

        /// <summary>進入者（Rigidbody の id）が入った。在室が 0→1 で、準備済みなら起動する。</summary>
        internal void Enter(ulong bodyId)
        {
            if (_state == State.Ended || !_occupants.Add(bodyId) || _occupants.Count != 1)
            {
                return;
            }

            if (_state == State.Ready)
            {
                Launch();
            }
        }

        /// <summary>進入者が出た。在室が 1→0 なら、生存中の instance を消え切りで止める（handle は次の進入まで持つ）。</summary>
        internal void Exit(ulong bodyId)
        {
            if (_state == State.Ended || !_occupants.Remove(bodyId) || _occupants.Count != 0)
            {
                return;
            }

            _handle?.StopAllowingParticlesToFinish();
        }

        /// <summary>
        /// source asset をロードして準備済みにする。準備中のときだけ loader を呼ぶ。
        /// ロードの失敗（例外か null）は trigger ごとに Error ログ 1 件にとどめ、外へ出さない: trigger の VFX は表示の演出で、
        /// Events Scene の他の進行制御まで止めない（Content Directory を使う実行では、この asset が directory に無い）。
        /// キャンセルだけは呼び出し側の中断なので外へ出す。
        /// </summary>
        /// <param name="loader">source をロードして返す（null は失敗）。所有者は呼び出し側が決める。</param>
        internal async UniTask PrepareAsync(Func<UniTask<Object?>> loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            if (_state != State.Preparing)
            {
                return;
            }

            Object? source;
            try
            {
                source = await loader();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // 終了の後に届いた失敗は、もう誰も起動しないので記録しない。
                if (_state == State.Preparing)
                {
                    _state = State.Failed;
                    _logger.ZLogError(exception, $"Local VFX event trigger failed to load its source and stays inactive: {_label}");
                }

                return;
            }

            // 終了の後に届いたロードの完了では起動しない。source の解放は所有者（Events Scene）に任せる。
            if (_state != State.Preparing)
            {
                return;
            }

            if (source == null)
            {
                _state = State.Failed;
                _logger.ZLogError($"Local VFX event trigger loaded no source and stays inactive: {_label}");
                return;
            }

            _source = source;
            _state = State.Ready;

            // 準備の前に入って、まだ居る進入者がいれば、ここで起動する。
            if (_occupants.Count > 0)
            {
                Launch();
            }
        }

        /// <summary>
        /// 終了する。生存中の instance を即時に回収し、以後は何もしない。2 回目以降は何もしない。
        /// Scene の unload の開始（<c>OnPreUnLoadedImpl</c>）と trigger の破棄（<c>OnDestroy</c>）の早いほうで呼ばれる。
        /// </summary>
        internal void End()
        {
            if (_state == State.Ended)
            {
                return;
            }

            _state = State.Ended;
            _occupants.Clear();
            _source = null;
            var handle = _handle;
            _handle = null;
            if (handle != null && handle.IsActive)
            {
                handle.Stop();
            }
        }

        private void Launch()
        {
            // trigger ごとに生存 instance は高々 1 つ。消え切り待ちの残りがあれば打ち切ってから起動する。
            // 消え切って回収済みの handle は参照を捨てるだけ。
            var previous = _handle;
            _handle = null;
            if (previous != null && previous.IsActive)
            {
                previous.Stop();
            }

            var handle = _vfx.Spawn(new VfxSpawnRequest(_source!, _position, _rotation), _owner);
            _handle = handle;
            handle.Play();
        }
    }
}
