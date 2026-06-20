using System;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.UpdateSystem.Layers
{
    /// <summary>
    /// 更新ジャンル単位の Layer。
    /// pause、timeScale、実行順、構成変更反映の境界をここで束ねる。
    /// </summary>
    public class UpdateLayer
    {
        private readonly IUpdateExecutionBackend _backend;
        private readonly UpdateElementRegistry _elementRegistry;
        private readonly List<PendingElementRegistration> _pendingRegistrations = new();
        private readonly List<ActiveElementRegistration> _activeRegistrations = new();
        private readonly HashSet<UpdateHandle> _pendingRemovals = new();
        private readonly List<UpdateHandle> _executionHandleBuffer = new();
        private readonly List<IUpdateElement> _executionElementBuffer = new();
        private long _nextSequenceNumber;

        public UpdateLayer(string layerId, int layerOrder, IUpdateExecutionBackend backend, UpdateElementRegistry elementRegistry)
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(layerId));
            }

            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _elementRegistry = elementRegistry ?? throw new ArgumentNullException(nameof(elementRegistry));
            LayerId = layerId.Trim();
            LayerOrder = layerOrder;
            TimeScale = 1f;
        }

        public string LayerId { get; }

        public int LayerOrder { get; }

        public bool IsPaused { get; set; }

        public float TimeScale { get; private set; }

        public int ActiveCount => _activeRegistrations.Count;

        public int PendingRegistrationCount => _pendingRegistrations.Count;

        public bool Register(UpdateHandle handle, int executionOrder = 0)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            if (_pendingRemovals.Remove(handle))
            {
                UpdateActiveRegistrationOrder(handle, executionOrder);
                return true;
            }

            if (Contains(handle))
            {
                return false;
            }

            _pendingRegistrations.Add(new PendingElementRegistration(handle, executionOrder, ++_nextSequenceNumber));
            return true;
        }

        public bool Unregister(UpdateHandle handle)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            for (var i = 0; i < _pendingRegistrations.Count; i++)
            {
                if (_pendingRegistrations[i].Handle == handle)
                {
                    _pendingRegistrations.RemoveAt(i);
                    return true;
                }
            }

            for (var i = 0; i < _activeRegistrations.Count; i++)
            {
                if (_activeRegistrations[i].Handle == handle)
                {
                    _pendingRemovals.Add(handle);
                    return true;
                }
            }

            return false;
        }

        public int ActivatePendingRegistrations()
        {
            if (_pendingRegistrations.Count == 0)
            {
                return 0;
            }

            _pendingRegistrations.Sort(PendingElementRegistrationComparer.Instance);
            var activationBatch = _pendingRegistrations.ToArray();
            _pendingRegistrations.Clear();
            var activatedCount = 0;

            for (var index = 0; index < activationBatch.Length; index++)
            {
                var pending = activationBatch[index];
                if (_pendingRemovals.Remove(pending.Handle))
                {
                    continue;
                }

                if (!_elementRegistry.TryGetElement(pending.Handle, out var element))
                {
                    continue;
                }

                var activeRegistration = new ActiveElementRegistration(pending.Handle, pending.ExecutionOrder, pending.SequenceNumber);
                _activeRegistrations.Add(activeRegistration);

                try
                {
                    element.OnElementStart();
                }
                catch
                {
                    _activeRegistrations.RemoveAt(_activeRegistrations.Count - 1);
                    RestoreActivationRemainder(activationBatch, index + 1);
                    if (_activeRegistrations.Count > 1)
                    {
                        _activeRegistrations.Sort(ActiveElementRegistrationComparer.Instance);
                    }

                    throw;
                }

                activatedCount++;
            }

            if (_activeRegistrations.Count > 1)
            {
                _activeRegistrations.Sort(ActiveElementRegistrationComparer.Instance);
            }

            return activatedCount;
        }

        public void SetTimeScale(float timeScale)
        {
            if (float.IsNaN(timeScale) || float.IsInfinity(timeScale) || timeScale < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale), "timeScale must be a finite value greater than or equal to zero.");
            }

            TimeScale = timeScale;
        }

        public void RunUpdate(uint frameIndex, float deltaTime, float unscaledDeltaTime)
        {
            if (!TryCreateExecutionContext(frameIndex, deltaTime, unscaledDeltaTime, out var context))
            {
                return;
            }

            RunManagedPhase(UpdateExecutionPhase.Update, in context);
        }

        public void RunLateUpdate(uint frameIndex, float deltaTime, float unscaledDeltaTime)
        {
            if (!TryCreateExecutionContext(frameIndex, deltaTime, unscaledDeltaTime, out var context))
            {
                return;
            }

            RunManagedPhase(UpdateExecutionPhase.LateUpdate, in context);
        }

        internal bool TryCreateExecutionContext(uint frameIndex, float deltaTime, float unscaledDeltaTime, out UpdateFrameContext context)
        {
            if (IsPaused)
            {
                context = default;
                return false;
            }

            context = new UpdateFrameContext(frameIndex, deltaTime * TimeScale, unscaledDeltaTime, TimeScale, isPaused: false);
            return true;
        }

        public int ApplyStructuralChanges(List<UpdateHandle> removedHandles)
        {
            if (removedHandles == null)
            {
                throw new ArgumentNullException(nameof(removedHandles));
            }

            if (_pendingRemovals.Count == 0)
            {
                return 0;
            }

            var removedCount = 0;
            for (var i = _activeRegistrations.Count - 1; i >= 0; i--)
            {
                if (_pendingRemovals.Contains(_activeRegistrations[i].Handle))
                {
                    removedHandles.Add(_activeRegistrations[i].Handle);
                    _activeRegistrations.RemoveAt(i);
                    removedCount++;
                }
            }

            _pendingRemovals.Clear();
            return removedCount;
        }

        public bool Contains(UpdateHandle handle)
        {
            for (var i = 0; i < _pendingRegistrations.Count; i++)
            {
                if (_pendingRegistrations[i].Handle == handle)
                {
                    return true;
                }
            }

            for (var i = 0; i < _activeRegistrations.Count; i++)
            {
                if (_activeRegistrations[i].Handle == handle)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryReorder(UpdateHandle handle, int executionOrder)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            for (var i = 0; i < _pendingRegistrations.Count; i++)
            {
                if (_pendingRegistrations[i].Handle != handle)
                {
                    continue;
                }

                _pendingRegistrations[i] = new PendingElementRegistration(handle, executionOrder, _pendingRegistrations[i].SequenceNumber);
                _pendingRegistrations.Sort(PendingElementRegistrationComparer.Instance);
                return true;
            }

            return UpdateActiveRegistrationOrder(handle, executionOrder);
        }

        internal void RunManagedPhase(UpdateExecutionPhase phase, in UpdateFrameContext context)
        {
            BuildExecutionBuffer();
            var batch = new ManagedExecutionBatch(phase, _executionHandleBuffer, _executionElementBuffer, in context);
            _backend.ExecuteManaged(in batch);
            _executionHandleBuffer.Clear();
            _executionElementBuffer.Clear();
        }

        private void BuildExecutionBuffer()
        {
            _executionHandleBuffer.Clear();
            _executionElementBuffer.Clear();
            for (var i = 0; i < _activeRegistrations.Count; i++)
            {
                var handle = _activeRegistrations[i].Handle;
                if (!_elementRegistry.TryGetElement(handle, out var element))
                {
                    continue;
                }

                _executionHandleBuffer.Add(handle);
                _executionElementBuffer.Add(element);
            }
        }

        private bool UpdateActiveRegistrationOrder(UpdateHandle handle, int executionOrder)
        {
            for (var i = 0; i < _activeRegistrations.Count; i++)
            {
                if (_activeRegistrations[i].Handle != handle)
                {
                    continue;
                }

                _activeRegistrations[i] = new ActiveElementRegistration(handle, executionOrder, ++_nextSequenceNumber);
                if (_activeRegistrations.Count > 1)
                {
                    _activeRegistrations.Sort(ActiveElementRegistrationComparer.Instance);
                }

                return true;
            }

            return false;
        }

        private void RestoreActivationRemainder(PendingElementRegistration[] activationBatch, int startIndex)
        {
            for (var i = startIndex; i < activationBatch.Length; i++)
            {
                _pendingRegistrations.Add(activationBatch[i]);
            }
        }

        private readonly struct PendingElementRegistration
        {
            public PendingElementRegistration(UpdateHandle handle, int executionOrder, long sequenceNumber)
            {
                Handle = handle;
                ExecutionOrder = executionOrder;
                SequenceNumber = sequenceNumber;
            }

            public UpdateHandle Handle { get; }
            public int ExecutionOrder { get; }
            public long SequenceNumber { get; }
        }

        private struct ActiveElementRegistration
        {
            public ActiveElementRegistration(UpdateHandle handle, int executionOrder, long sequenceNumber)
            {
                Handle = handle;
                ExecutionOrder = executionOrder;
                SequenceNumber = sequenceNumber;
            }

            public UpdateHandle Handle { get; }
            public int ExecutionOrder { get; }
            public long SequenceNumber { get; }
        }

        private sealed class PendingElementRegistrationComparer : IComparer<PendingElementRegistration>
        {
            public static readonly PendingElementRegistrationComparer Instance = new();

            public int Compare(PendingElementRegistration x, PendingElementRegistration y)
            {
                var order = x.ExecutionOrder.CompareTo(y.ExecutionOrder);
                return order != 0 ? order : x.SequenceNumber.CompareTo(y.SequenceNumber);
            }
        }

        private sealed class ActiveElementRegistrationComparer : IComparer<ActiveElementRegistration>
        {
            public static readonly ActiveElementRegistrationComparer Instance = new();

            public int Compare(ActiveElementRegistration x, ActiveElementRegistration y)
            {
                var order = x.ExecutionOrder.CompareTo(y.ExecutionOrder);
                return order != 0 ? order : x.SequenceNumber.CompareTo(y.SequenceNumber);
            }
        }
    }
}
