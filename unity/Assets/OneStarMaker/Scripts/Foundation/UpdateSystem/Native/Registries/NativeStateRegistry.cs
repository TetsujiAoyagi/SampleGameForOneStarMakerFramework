using System;
using Unity.Collections;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// native/job path における `TState` 正本レジストリ。
    /// state / handle / order / dirty flag を dense 配列で持ち、
    /// 外部へは stable な `UpdateHandle` のみを公開する。
    /// 
    /// 重要なのは「slot と dense index を分離する」こと。
    /// job 実行効率のため実データは dense に詰める一方、
    /// 外部参照は slot + generation で安定化し、swap-back compaction と両立させる。
    /// </summary>
    public sealed class NativeStateRegistry<TState> : IDisposable
        where TState : unmanaged
    {
        private NativeList<TState> _states;
        private NativeList<UpdateHandle> _handles;
        private NativeList<int> _executionOrders;
        private NativeList<byte> _dirtyFlags;
        private NativeList<SlotEntry> _slotEntries;
        private NativeList<int> _freeSlots;
        private bool _hasActiveExecutionLease;
        private uint _activeExecutionLeaseEpoch;

        public NativeStateRegistry(int initialCapacity = 0, Allocator allocator = Allocator.Persistent)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            _states = new NativeList<TState>(initialCapacity, allocator);
            _handles = new NativeList<UpdateHandle>(initialCapacity, allocator);
            _executionOrders = new NativeList<int>(initialCapacity, allocator);
            _dirtyFlags = new NativeList<byte>(initialCapacity, allocator);
            _slotEntries = new NativeList<SlotEntry>(initialCapacity, allocator);
            _freeSlots = new NativeList<int>(initialCapacity, allocator);
        }

        public bool IsCreated => _states.IsCreated;

        public int Count
        {
            get
            {
                ThrowIfDisposed();
                return _states.Length;
            }
        }

        /// <summary>
        /// state を新規登録し、安定ハンドルを返す。
        /// swap-back compaction が起きても handle 自体は不変であり、
        /// dense index の追跡は slot table 側で吸収する。
        /// </summary>
        public UpdateHandle Register(in TState state, int executionOrder = 0, bool isDirty = false)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(Register));

            EnsureRegisterCapacity();

            var previousStatesLength = _states.Length;
            var previousHandlesLength = _handles.Length;
            var previousOrdersLength = _executionOrders.Length;
            var previousDirtyLength = _dirtyFlags.Length;
            var slot = -1;
            var reusedSlot = false;
            var createdNewSlot = false;
            var previousSlotEntry = default(SlotEntry);

            try
            {
                slot = AllocateSlot(out reusedSlot, out createdNewSlot, out previousSlotEntry);
                var generation = _slotEntries[slot].Generation;
                var handle = new UpdateHandle(slot, generation);
                var denseIndex = _states.Length;

                _states.Add(state);
                _handles.Add(handle);
                _executionOrders.Add(executionOrder);
                _dirtyFlags.Add(isDirty ? (byte)1 : (byte)0);
                _slotEntries[slot] = new SlotEntry(denseIndex, generation, isAllocated: true);

                return handle;
            }
            catch
            {
                RollbackToLength(ref _states, previousStatesLength);
                RollbackToLength(ref _handles, previousHandlesLength);
                RollbackToLength(ref _executionOrders, previousOrdersLength);
                RollbackToLength(ref _dirtyFlags, previousDirtyLength);

                if (slot >= 0)
                {
                    if (createdNewSlot)
                    {
                        RollbackToLength(ref _slotEntries, slot);
                    }
                    else
                    {
                        _slotEntries[slot] = previousSlotEntry;
                    }

                    if (reusedSlot)
                    {
                        _freeSlots.Add(slot);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// handle に対応する state を削除する。
        /// 実データ列は swap-back で詰め直し、移動した要素の slot table だけ更新する。
        /// </summary>
        public bool Unregister(UpdateHandle handle)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(Unregister));

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                return false;
            }

            var lastIndex = _states.Length - 1;
            var removedSlot = handle.Slot;

            _states.RemoveAtSwapBack(denseIndex);
            _handles.RemoveAtSwapBack(denseIndex);
            _executionOrders.RemoveAtSwapBack(denseIndex);
            _dirtyFlags.RemoveAtSwapBack(denseIndex);

            if (denseIndex != lastIndex)
            {
                var movedHandle = _handles[denseIndex];
                var movedEntry = _slotEntries[movedHandle.Slot];
                _slotEntries[movedHandle.Slot] = new SlotEntry(
                    denseIndex,
                    movedEntry.Generation,
                    isAllocated: true);
            }

            var removedEntry = _slotEntries[removedSlot];
            _slotEntries[removedSlot] = new SlotEntry(
                denseIndex: -1,
                generation: NextGeneration(removedEntry.Generation),
                isAllocated: false);
            _freeSlots.Add(removedSlot);
            return true;
        }

        public bool Contains(UpdateHandle handle)
        {
            ThrowIfDisposed();
            return TryResolveDenseIndex(handle, out _);
        }

        public bool TryGetDenseIndex(UpdateHandle handle, out int denseIndex)
        {
            ThrowIfDisposed();
            return TryResolveDenseIndex(handle, out denseIndex);
        }

        public bool TryGetState(UpdateHandle handle, out TState state)
        {
            ThrowIfDisposed();

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                state = default;
                return false;
            }

            state = _states[denseIndex];
            return true;
        }

        public void SetState(UpdateHandle handle, in TState state, bool markDirty = true)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(SetState));

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                throw new InvalidOperationException($"Handle '{handle}' is not registered.");
            }

            _states[denseIndex] = state;
            if (markDirty)
            {
                _dirtyFlags[denseIndex] = 1;
            }
        }

        public bool TryGetExecutionOrder(UpdateHandle handle, out int executionOrder)
        {
            ThrowIfDisposed();

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                executionOrder = default;
                return false;
            }

            executionOrder = _executionOrders[denseIndex];
            return true;
        }

        public void SetExecutionOrder(UpdateHandle handle, int executionOrder)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(SetExecutionOrder));

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                throw new InvalidOperationException($"Handle '{handle}' is not registered.");
            }

            _executionOrders[denseIndex] = executionOrder;
        }

        public bool IsDirty(UpdateHandle handle)
        {
            ThrowIfDisposed();

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                return false;
            }

            return _dirtyFlags[denseIndex] != 0;
        }

        public void MarkDirty(UpdateHandle handle)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(MarkDirty));

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                throw new InvalidOperationException($"Handle '{handle}' is not registered.");
            }

            _dirtyFlags[denseIndex] = 1;
        }

        public void ClearDirty(UpdateHandle handle)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(ClearDirty));

            if (!TryResolveDenseIndex(handle, out var denseIndex))
            {
                throw new InvalidOperationException($"Handle '{handle}' is not registered.");
            }

            _dirtyFlags[denseIndex] = 0;
        }

        public void ClearAllDirty()
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(ClearAllDirty));
            ClearDirtyFlagsCore();
        }

        /// <summary>
        /// registry 正本を direct view のまま実行する lease を開始する。
        /// 
        /// ここでは snapshot copy を作らず、`NativeList<T>` の `AsArray()` をそのまま batch 化する。
        /// その代わり lease が有効な間は structural mutation を禁止し、
        /// 「正本配列を読んでいる job」と「長さや配置を変える mutation」が同居しないようにする。
        /// 
        /// direct view 化の第一段階として、
        /// まずは同期実行 + 明示 lease で ownership を固定する。
        /// 非同期 job や複数同時 lease は後段で扱う。
        /// </summary>
        public NativeExecutionLease<TState> BeginExecutionLease(
            UpdateExecutionPhase phase,
            in UpdateFrameContext context)
        {
            ThrowIfDisposed();

            if (_hasActiveExecutionLease)
            {
                throw new InvalidOperationException(
                    "Cannot begin a new native execution lease while another lease is active.");
            }

            _hasActiveExecutionLease = true;
            _activeExecutionLeaseEpoch = NextGeneration(_activeExecutionLeaseEpoch);

            var batch = new NativeExecutionBatch<TState>(
                phase,
                _states.AsArray(),
                _handles.AsArray(),
                _executionOrders.AsArray(),
                _dirtyFlags.AsArray(),
                in context,
                ownsBuffers: false);

            return new NativeExecutionLease<TState>(batch, _activeExecutionLeaseEpoch, CompleteExecutionLease);
        }

        /// <summary>
        /// 指定 epoch の lease を完了する。
        /// 
        /// stale lease の完了を黙って受けると、
        /// いま有効な ownership がどれか分からなくなる。
        /// そのため epoch 不一致は例外にして、
        /// 「lease の開始と終了は必ず 1 対 1 で対応する」契約を維持する。
        /// </summary>
        public void CompleteExecutionLease(uint leaseEpoch)
        {
            ThrowIfDisposed();
            ValidateActiveLease(leaseEpoch, nameof(CompleteExecutionLease));
            _hasActiveExecutionLease = false;
        }

        /// <summary>
        /// 現在の lease が直接更新した dirty flag を一括で消す。
        /// 
        /// direct view では dirty flag 自体も正本配列の一部なので、
        /// snapshot 時代のような write-back 時 clear ではなく、
        /// export 完了後に lease 所有者が直接 clear する責務へ寄せる。
        /// これにより dirty の責任点を
        /// 「job が立てる -> world/native pipeline が export する -> lease 所有者が clear する」
        /// の 1 本へ再定義する。
        /// </summary>
        public void ClearAllDirtyForLease(uint leaseEpoch)
        {
            ThrowIfDisposed();
            ValidateActiveLease(leaseEpoch, nameof(ClearAllDirtyForLease));
            
            // lease 所有者だけは、実行中に自分で立てた dirty flag を
            // export 完了直後に畳む責務を持つ。
            // 一般公開の ClearAllDirty() は structural mutation と同じく
            // lease 中の外部呼び出しを禁止したままにし、
            // 「誰でも lease 中に dirty を消せる」状態を避ける。
            ClearDirtyFlagsCore();
        }

        /// <summary>
        /// 現在の dense state をそのまま native execution batch として切り出す。
        /// 本来の本命は registry 正本を直接 job payload にする形だが、
        /// 現段階では structural mutation / dispose と batch 寿命の lease 管理をまだ持っていない。
        /// そのため一旦は snapshot batch を返し、正本の compaction と安全に分離する。
        /// </summary>
        public NativeExecutionBatch<TState> BuildExecutionBatch(
            UpdateExecutionPhase phase,
            in UpdateFrameContext context,
            Allocator allocator = Allocator.TempJob)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(BuildExecutionBatch));

            var states = default(NativeArray<TState>);
            var handles = default(NativeArray<UpdateHandle>);
            var executionOrders = default(NativeArray<int>);
            var dirtyFlags = default(NativeArray<byte>);

            try
            {
                states = CopyToArray(_states, allocator);
                handles = CopyToArray(_handles, allocator);
                executionOrders = CopyToArray(_executionOrders, allocator);
                dirtyFlags = CopyToArray(_dirtyFlags, allocator);
            }
            catch
            {
                DisposeIfCreated(states);
                DisposeIfCreated(handles);
                DisposeIfCreated(executionOrders);
                DisposeIfCreated(dirtyFlags);
                throw;
            }

            return new NativeExecutionBatch<TState>(
                phase,
                states,
                handles,
                executionOrders,
                dirtyFlags,
                in context,
                ownsBuffers: true);
        }

        /// <summary>
        /// snapshot batch 上で更新された state / dirty flag を registry 正本へ書き戻す。
        /// 
        /// これは lease 導入前の snapshot 実行系を支える互換 API であり、
        /// 現在の本線である direct view + lease path では通常使わない。
        /// それでも snapshot batch を使う backend / テスト / 段階移行コードのために残し、
        /// 「どこで dirty を clear するか」をここへ集約して責務を固定している。
        /// 
        /// `clearDirtyAfterWriteBack` を true にした場合、
        /// dirty handle は export 済みであることを前提に registry 側では 0 へ戻す。
        /// これにより dirty の責任主体を
        /// 「job が立てる -> world/native pipeline が export する -> write-back で clear する」
        /// の 1 本に固定する。
        /// 
        /// また、この batch 自体は snapshot なので、
        /// 生成後に該当 handle が structural change で消えていても write-back 側では例外にしない。
        /// ここで stale handle を best-effort に捨てることで、
        /// 「snapshot は後続の register/unregister から独立して評価できる」という契約を守る。
        /// </summary>
        public void ApplyExecutionResult(
            NativeExecutionBatch<TState> batch,
            bool clearDirtyAfterWriteBack)
        {
            ThrowIfDisposed();
            ThrowIfMutationBlocked(nameof(ApplyExecutionResult));

            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            for (var i = 0; i < batch.ElementCount; i++)
            {
                var handle = batch.Handles[i];
                if (!TryResolveDenseIndex(handle, out var denseIndex))
                {
                    // snapshot 作成後に handle が unregister されていた場合は、
                    // その要素への書き戻しだけを無視する。
                    // job 実行結果そのものは過去時点の snapshot に対する正当な結果なので、
                    // ここで失敗扱いにすると structural mutation と native 実行の分離が崩れる。
                    continue;
                }

                _states[denseIndex] = batch.States[i];
                _dirtyFlags[denseIndex] = clearDirtyAfterWriteBack
                    ? (byte)0
                    : batch.DirtyFlags[i];
            }
        }

        public void Dispose()
        {
            ThrowIfMutationBlocked(nameof(Dispose));

            if (_states.IsCreated)
            {
                _states.Dispose();
            }

            if (_handles.IsCreated)
            {
                _handles.Dispose();
            }

            if (_executionOrders.IsCreated)
            {
                _executionOrders.Dispose();
            }

            if (_dirtyFlags.IsCreated)
            {
                _dirtyFlags.Dispose();
            }

            if (_slotEntries.IsCreated)
            {
                _slotEntries.Dispose();
            }

            if (_freeSlots.IsCreated)
            {
                _freeSlots.Dispose();
            }
        }

        private int AllocateSlot(out bool reusedSlot, out bool createdNewSlot, out SlotEntry previousSlotEntry)
        {
            reusedSlot = false;
            createdNewSlot = false;
            previousSlotEntry = default;

            if (_freeSlots.Length > 0)
            {
                var freeIndex = _freeSlots.Length - 1;
                var slot = _freeSlots[freeIndex];
                _freeSlots.RemoveAtSwapBack(freeIndex);
                previousSlotEntry = _slotEntries[slot];
                _slotEntries[slot] = new SlotEntry(-1, previousSlotEntry.Generation, isAllocated: false);
                reusedSlot = true;
                return slot;
            }

            var newSlot = _slotEntries.Length;
            _slotEntries.Add(new SlotEntry(-1, generation: 1, isAllocated: false));
            createdNewSlot = true;
            return newSlot;
        }

        private bool TryResolveDenseIndex(UpdateHandle handle, out int denseIndex)
        {
            denseIndex = -1;
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _slotEntries.Length)
            {
                return false;
            }

            var entry = _slotEntries[handle.Slot];
            if (!entry.IsAllocated || entry.Generation != handle.Generation)
            {
                return false;
            }

            if (entry.DenseIndex < 0 || entry.DenseIndex >= _states.Length)
            {
                return false;
            }

            if (_handles[entry.DenseIndex] != handle)
            {
                return false;
            }

            denseIndex = entry.DenseIndex;
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (!_states.IsCreated)
            {
                throw new ObjectDisposedException(nameof(NativeStateRegistry<TState>));
            }
        }

        private void ThrowIfMutationBlocked(string operationName)
        {
            if (_hasActiveExecutionLease)
            {
                throw new InvalidOperationException(
                    $"Cannot perform '{operationName}' while a native execution lease is active.");
            }
        }

        private void ValidateActiveLease(uint leaseEpoch, string operationName)
        {
            if (!_hasActiveExecutionLease)
            {
                throw new InvalidOperationException(
                    $"Cannot perform '{operationName}' because no native execution lease is active.");
            }

            if (_activeExecutionLeaseEpoch != leaseEpoch)
            {
                throw new InvalidOperationException(
                    $"Cannot perform '{operationName}' for stale lease epoch {leaseEpoch}. Active epoch is {_activeExecutionLeaseEpoch}.");
            }
        }

        private void ClearDirtyFlagsCore()
        {
            for (var i = 0; i < _dirtyFlags.Length; i++)
            {
                _dirtyFlags[i] = 0;
            }
        }

        private static uint NextGeneration(uint generation)
        {
            return generation == uint.MaxValue ? 1u : generation + 1u;
        }

        private void EnsureRegisterCapacity()
        {
            EnsureNextCapacity(ref _states);
            EnsureNextCapacity(ref _handles);
            EnsureNextCapacity(ref _executionOrders);
            EnsureNextCapacity(ref _dirtyFlags);

            if (_freeSlots.Length == 0)
            {
                EnsureNextCapacity(ref _slotEntries);
            }
        }

        private static NativeArray<T> CopyToArray<T>(NativeList<T> source, Allocator allocator)
            where T : unmanaged
        {
            var copied = new NativeArray<T>(source.Length, allocator);
            NativeArray<T>.Copy(source.AsArray(), copied, source.Length);
            return copied;
        }

        private static void DisposeIfCreated<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        private static void EnsureNextCapacity<T>(ref NativeList<T> list)
            where T : unmanaged
        {
            if (list.Length < list.Capacity)
            {
                return;
            }

            list.Capacity = list.Capacity == 0
                ? 4
                : list.Capacity * 2;
        }

        private static void RollbackToLength<T>(ref NativeList<T> list, int targetLength)
            where T : unmanaged
        {
            while (list.Length > targetLength)
            {
                list.RemoveAtSwapBack(list.Length - 1);
            }
        }

        private readonly struct SlotEntry
        {
            public SlotEntry(int denseIndex, uint generation, bool isAllocated)
            {
                DenseIndex = denseIndex;
                Generation = generation;
                IsAllocated = isAllocated;
            }

            public int DenseIndex { get; }

            public uint Generation { get; }

            public bool IsAllocated { get; }
        }
    }
}
