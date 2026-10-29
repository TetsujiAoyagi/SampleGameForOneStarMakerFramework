#nullable enable

using System;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Cell の Events Scene に置く major Event の開始の trigger。同じ GameObject の <c>isTrigger</c> の Collider への進入ごとに、
    /// <see cref="MajorEventStartBinder"/> が付けた受け口へ出来事を流すだけで、判断も状態も持たない
    /// （重複の要求は受理の側が吸収する）。受け口が付く前の進入は捨てる。進入者は Rigidbody を持つ Collider。
    /// <para>
    /// 別 Scene の Event とのつながりは event の identity の文字列だけ（Scene を跨ぐ GameObject の参照は持たない）。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MajorEventStartTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("開始する major Event Scene の identity（例: Spring_Event_MultiCellProof）。")]
        private string _eventIdentity = string.Empty;

        private Action? _entered;

        public string EventIdentity => _eventIdentity;

        internal void Attach(Action entered)
        {
            _entered = entered ?? throw new ArgumentNullException(nameof(entered));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == null)
            {
                return;
            }

            _entered?.Invoke();
        }
    }
}
