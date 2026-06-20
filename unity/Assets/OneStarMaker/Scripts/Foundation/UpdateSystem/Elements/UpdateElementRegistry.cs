using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// `IUpdateElement` と `UpdateHandle` の対応表。
    /// element 解決、main-thread apply 対象判定、再利用 slot 管理をここで担う。
    /// </summary>
    public class UpdateElementRegistry
    {
        private readonly Dictionary<IUpdateElement, UpdateHandle> _handlesByElement =
            new(ReferenceEqualityComparer<IUpdateElement>.Instance);
        private readonly List<ElementEntry> _entries = new();
        private readonly Stack<int> _freeSlots = new();

        public bool Register(IUpdateElement element, UpdateElementSyncPolicy policy, out UpdateHandle handle)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (_handlesByElement.TryGetValue(element, out handle))
            {
                return false;
            }

            int slot;
            uint generation;
            if (_freeSlots.Count > 0)
            {
                slot = _freeSlots.Pop();
                generation = _entries[slot].Generation;
                _entries[slot] = new ElementEntry(element, generation, policy, isAlive: true);
            }
            else
            {
                slot = _entries.Count;
                generation = 1;
                _entries.Add(new ElementEntry(element, generation, policy, isAlive: true));
            }

            handle = new UpdateHandle(slot, generation);
            _handlesByElement.Add(element, handle);
            return true;
        }

        public bool TryGetHandle(IUpdateElement element, out UpdateHandle handle)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return _handlesByElement.TryGetValue(element, out handle);
        }

        public bool TryGetElement(UpdateHandle handle, out IUpdateElement element)
        {
            element = null!;
            if (!TryGetEntry(handle, out var entry) || entry.Element == null)
            {
                return false;
            }

            element = entry.Element;
            return true;
        }

        public bool TryGetMainThreadApplyElement(UpdateHandle handle, out IMainThreadApplyElement element)
        {
            element = null!;
            if (!TryGetEntry(handle, out var entry) || entry.Element == null)
            {
                return false;
            }

            if ((entry.Policy & UpdateElementSyncPolicy.AllowMainThreadApply) == 0)
            {
                return false;
            }

            if (entry.Element is not IMainThreadApplyElement applyElement)
            {
                return false;
            }

            element = applyElement;
            return true;
        }

        public bool Contains(UpdateHandle handle)
        {
            return TryGetEntry(handle, out _);
        }

        public bool Remove(UpdateHandle handle)
        {
            if (!TryGetEntry(handle, out var entry) || entry.Element == null)
            {
                return false;
            }

            _handlesByElement.Remove(entry.Element);
            _entries[handle.Slot] = new ElementEntry(
                element: null,
                generation: NextGeneration(entry.Generation),
                policy: entry.Policy,
                isAlive: false);
            _freeSlots.Push(handle.Slot);
            return true;
        }

        public bool TryGetPolicy(UpdateHandle handle, out UpdateElementSyncPolicy policy)
        {
            policy = UpdateElementSyncPolicy.None;
            if (!TryGetEntry(handle, out var entry))
            {
                return false;
            }

            policy = entry.Policy;
            return true;
        }

        private bool TryGetEntry(UpdateHandle handle, out ElementEntry entry)
        {
            entry = default;
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _entries.Count)
            {
                return false;
            }

            entry = _entries[handle.Slot];
            return entry.IsAlive && entry.Generation == handle.Generation;
        }

        private static uint NextGeneration(uint currentGeneration)
        {
            var nextGeneration = unchecked(currentGeneration + 1);
            return nextGeneration == 0 ? 1u : nextGeneration;
        }

        private readonly struct ElementEntry
        {
            public ElementEntry(IUpdateElement? element, uint generation, UpdateElementSyncPolicy policy, bool isAlive)
            {
                Element = element;
                Generation = generation;
                Policy = policy;
                IsAlive = isAlive;
            }

            public IUpdateElement? Element { get; }
            public uint Generation { get; }
            public UpdateElementSyncPolicy Policy { get; }
            public bool IsAlive { get; }
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new();

            public bool Equals(T? x, T? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
