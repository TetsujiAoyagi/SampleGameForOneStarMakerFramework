#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Modifiers
{
    /// <summary>
    /// View 1 つ分の Pose 後補正 Modifier を登録順に保持して適用するスタック。
    /// Apply 中に false を返した Modifier はその場で除去する（自己終了する演出の後始末）。
    /// </summary>
    public sealed class CameraModifierStack
    {
        private readonly List<ICameraPoseModifier> _modifiers = new();
        private bool _isReleased;

        public CameraModifierHandle AddModifier(ICameraPoseModifier modifier)
        {
            ThrowIfReleased();
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            _modifiers.Add(modifier);
            return new CameraModifierHandle(this, modifier);
        }

        /// <summary>
        /// 全 Modifier を登録順に適用する。false を返した Modifier は除去し、
        /// インデックスを 1 つ戻して後続 Modifier を同一フレームで飛ばさないようにする。
        /// </summary>
        public void Apply(ref CameraPose pose, float deltaTime)
        {
            if (_isReleased || _modifiers.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _modifiers.Count; i++)
            {
                if (!_modifiers[i].Apply(ref pose, deltaTime))
                {
                    // 除去したぶん詰まるので i を戻し、次要素の適用漏れを防ぐ。
                    _modifiers.RemoveAt(i);
                    i--;
                }
            }
        }

        internal void RemoveModifier(ICameraPoseModifier modifier)
        {
            if (_isReleased)
            {
                return;
            }

            _modifiers.Remove(modifier);
        }

        internal void Release()
        {
            _isReleased = true;
            _modifiers.Clear();
        }

        private void ThrowIfReleased()
        {
            if (_isReleased)
            {
                throw new ObjectDisposedException(nameof(CameraModifierStack));
            }
        }
    }
}
