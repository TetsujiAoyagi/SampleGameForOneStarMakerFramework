#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Events Scene に置く局所 VFX の trigger。同じ GameObject の <c>isTrigger</c> の Collider への進入で、
    /// <see cref="VfxAddress"/> の VFX を trigger の位置・回転で起動する。
    /// <para>
    /// この component は authoring の面（address）と physics の callback の受け口だけを持ち、判断はしない。
    /// 判断は Scene class（<see cref="CellCompanionScene"/>）が付ける <see cref="LocalVfxEventSession"/> が持つ。
    /// session が付く前（Unity Scene のロード直後から Scene の初期化まで）に届いた進入・退出は、出来事の列として
    /// 順に溜め、付いた時点で同じ順に流す（在室の集合は session だけが持つ）。
    /// </para>
    /// <para>
    /// VFX asset は直接参照しない。Scene の依存にせず、Events Scene が所有者を宣言して <c>IAssetManagement</c> でロードする。
    /// 進入者は Rigidbody を持つ Collider で、Rigidbody ごとに数える（静的な Collider は数えない）。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalVfxEventTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("起動する .vfx の Addressables address（asset path）。")]
        private string _vfxAddress = string.Empty;

        private readonly List<(bool Enter, ulong BodyId)> _pending = new();
        private LocalVfxEventSession? _session;
        private bool _destroyed;

        public string VfxAddress => _vfxAddress;

        /// <summary>
        /// session を付け、それまでに溜めた出来事を順に流す。既に破棄されていれば、session を終わらせるだけにする。
        /// </summary>
        internal void Attach(LocalVfxEventSession session)
        {
            if (_destroyed)
            {
                session.End();
                return;
            }

            _session = session;
            foreach (var (enter, bodyId) in _pending)
            {
                if (enter)
                {
                    session.Enter(bodyId);
                }
                else
                {
                    session.Exit(bodyId);
                }
            }

            _pending.Clear();
        }

        private void OnTriggerEnter(Collider other) => Forward(other, enter: true);

        private void OnTriggerExit(Collider other) => Forward(other, enter: false);

        // Scene の unload より前に GameObject だけが破棄された場合も、以後この trigger から起動させず、生存中の instance を回収する。
        private void OnDestroy()
        {
            _destroyed = true;
            _pending.Clear();
            _session?.End();
        }

        private void Forward(Collider other, bool enter)
        {
            var body = other.attachedRigidbody;
            if (body == null)
            {
                return;
            }

            // GetInstanceID は obsolete-as-error。EntityId を数値の id にして数える。
            var bodyId = EntityId.ToULong(body.GetEntityId());
            if (_session == null)
            {
                _pending.Add((enter, bodyId));
            }
            else if (enter)
            {
                _session.Enter(bodyId);
            }
            else
            {
                _session.Exit(bodyId);
            }
        }
    }
}
