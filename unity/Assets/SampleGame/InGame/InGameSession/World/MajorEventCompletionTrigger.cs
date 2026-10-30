#nullable enable

using System;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// major Event Scene に置く完了の trigger。同じ GameObject の <c>isTrigger</c> の Collider への進入の出来事を、
    /// <see cref="SeasonMajorEventScene"/> が付けた受け口へ流すだけで、判断も状態も持たない。
    /// 受け口が付く前（Scene の初期化より前）の進入は捨てる。進入者は Rigidbody を持つ Collider。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MajorEventCompletionTrigger : MonoBehaviour
    {
        private Action? _entered;

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
