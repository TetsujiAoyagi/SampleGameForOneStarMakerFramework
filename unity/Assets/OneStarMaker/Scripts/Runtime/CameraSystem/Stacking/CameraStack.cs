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

namespace OneStarMaker.Runtime.CameraSystem.Stacking
{
    /// <summary>
    /// レイヤーごとの LIFO スタックとしてカメラを積み、アクティブカメラを決定するポリシー本体。
    /// 「どのカメラが勝つか」だけを純 C# で決め、実描画は関与しない。アクティブ変化は
    /// <see cref="ActiveCameraChanged"/> で通知し、Backend への反映は購読側（CameraView）に任せる。
    /// </summary>
    public sealed class CameraStack
    {
        // アクティブ解決の優先順。上位レイヤーから探し、最初に非空だったレイヤーの最上段が勝つ。
        private static readonly CameraLayer[] LayerOrder =
        {
            CameraLayer.Debug,
            CameraLayer.Cutscene,
            CameraLayer.Gameplay,
        };

        private readonly LogicalCamera _fallbackCamera;
        private readonly List<StackEntry>[] _layers;
        private int _nextEntryId;
        private LogicalCamera _activeCamera;
        private bool _isReleased;

        public CameraStack(LogicalCamera fallbackCamera)
        {
            _fallbackCamera = fallbackCamera ?? throw new ArgumentNullException(nameof(fallbackCamera));
            _activeCamera = _fallbackCamera;
            _layers = new List<StackEntry>[Enum.GetValues(typeof(CameraLayer)).Length];
            for (var i = 0; i < _layers.Length; i++)
            {
                _layers[i] = new List<StackEntry>();
            }
        }

        public LogicalCamera ActiveCamera => _activeCamera;

        public bool IsUsingFallback => IsStackEmpty();

        public int GetLayerDepth(CameraLayer layer) => _layers[(int)layer].Count;

        public int StackDepthTotal
        {
            get
            {
                var total = 0;
                for (var i = 0; i < _layers.Length; i++)
                {
                    total += _layers[i].Count;
                }

                return total;
            }
        }

        public event Action<ActiveCameraChangeInfo>? ActiveCameraChanged;

        /// <summary>
        /// 指定レイヤーへカメラを積む。積んだ結果アクティブが変わった場合のみ入場側の blend で変更通知する。
        /// 返すハンドルの Dispose がこのエントリの Pop に対応する（呼び出し側が所有権を握る）。
        /// </summary>
        public CameraStackHandle Push(LogicalCamera camera, CameraLayer layer, in CameraBlendSpec blend)
        {
            ThrowIfReleased();
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            var entry = new StackEntry
            {
                Id = _nextEntryId++,
                Camera = camera,
                Layer = layer,
                BlendSpec = blend,
            };

            _layers[(int)layer].Add(entry);

            var previous = _activeCamera;
            ResolveActiveCamera();

            if (!ReferenceEquals(previous, _activeCamera))
            {
                ActiveCameraChanged?.Invoke(new ActiveCameraChangeInfo
                {
                    PreviousCamera = previous,
                    NewCamera = _activeCamera,
                    BlendSpec = blend,
                });
            }

            return new CameraStackHandle(this, entry.Id);
        }

        internal void RemoveEntry(int entryId)
        {
            if (_isReleased)
            {
                return;
            }

            StackEntry? removed = null;
            var removedIndex = -1;
            List<StackEntry>? removedLayer = null;

            for (var layerIndex = 0; layerIndex < _layers.Length; layerIndex++)
            {
                var layer = _layers[layerIndex];
                for (var i = 0; i < layer.Count; i++)
                {
                    if (layer[i].Id == entryId)
                    {
                        removed = layer[i];
                        removedIndex = i;
                        removedLayer = layer;
                        break;
                    }
                }

                if (removed.HasValue)
                {
                    break;
                }
            }

            if (!removed.HasValue || removedLayer == null)
            {
                return;
            }

            // 退場するエントリが自レイヤーの最上段でなければアクティブには影響しないので通知しない。
            // 【意図的仕様】Pop 後の復帰遷移には「退場カメラが Push 時に指定した blend」を使う。
            // カットシーン等は自分の入退場カーブを自分で規定するのが自然なため。復帰先カメラの blend では
            // ない点は Dispose_ActiveChange_UsesDepartingCameraBlendSpec テストで固定されている。
            var wasTopOfItsLayer = removedIndex == removedLayer.Count - 1;
            var departingBlendSpec = removed.Value.BlendSpec;
            var previousActive = _activeCamera;

            removedLayer.RemoveAt(removedIndex);
            ResolveActiveCamera();

            if (wasTopOfItsLayer && !ReferenceEquals(previousActive, _activeCamera))
            {
                ActiveCameraChanged?.Invoke(new ActiveCameraChangeInfo
                {
                    PreviousCamera = previousActive,
                    NewCamera = _activeCamera,
                    BlendSpec = departingBlendSpec,
                });
            }
        }

        internal void Release()
        {
            _isReleased = true;
            for (var i = 0; i < _layers.Length; i++)
            {
                _layers[i].Clear();
            }

            _activeCamera = _fallbackCamera;
        }

        private void ResolveActiveCamera()
        {
            foreach (var layer in LayerOrder)
            {
                var stack = _layers[(int)layer];
                if (stack.Count > 0)
                {
                    _activeCamera = stack[stack.Count - 1].Camera;
                    return;
                }
            }

            _activeCamera = _fallbackCamera;
        }

        private bool IsStackEmpty()
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].Count > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ThrowIfReleased()
        {
            if (_isReleased)
            {
                throw new ObjectDisposedException(nameof(CameraStack));
            }
        }

        private struct StackEntry
        {
            public int Id;
            public LogicalCamera Camera;
            public CameraLayer Layer;
            public CameraBlendSpec BlendSpec;
        }
    }
}
