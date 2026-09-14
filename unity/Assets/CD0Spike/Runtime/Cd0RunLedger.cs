#nullable enable

using System;

namespace CD0Spike
{
    internal enum Cd0OperationKind
    {
        Asset,
        Scene,
    }

    internal sealed class Cd0RunLedger
    {
        private readonly int _generation;
        private bool _issued;
        private bool _completed;
        private bool _accepted;
        private bool _cleanupDone;
        private bool _cleanupFailed;
        private bool _abandoned;

        internal Cd0RunLedger(int generation, Cd0OperationKind operationKind)
        {
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            _generation = generation;
            OperationKind = operationKind;
        }

        internal Cd0OperationKind OperationKind { get; }
        internal bool Issued => _issued;
        internal bool Completed => _completed;
        internal bool Accepted => _accepted;
        internal bool CleanupDone => _cleanupDone;
        internal bool CleanupFailed => _cleanupFailed;
        internal bool Abandoned => _abandoned;
        internal bool CanStartRetry => _completed && _cleanupDone && !_cleanupFailed;

        internal void MarkIssued(int generation)
        {
            RequireGeneration(generation);
            if (_issued) throw new InvalidOperationException("Operation was already issued.");
            _issued = true;
        }

        internal void Abandon(int generation)
        {
            RequireGeneration(generation);
            _abandoned = true;
        }

        internal bool MarkCompleted(int generation, bool succeeded)
        {
            RequireGeneration(generation);
            if (!_issued) throw new InvalidOperationException("An unissued operation cannot complete.");
            if (_completed) throw new InvalidOperationException("Operation already completed.");
            _completed = true;
            _accepted = succeeded && !_abandoned;
            return _accepted;
        }

        internal void MarkCleanupDone(int generation)
        {
            RequireGeneration(generation);
            if (!_completed) throw new InvalidOperationException("Cleanup cannot finish before the native terminal event.");
            if (_cleanupDone || _cleanupFailed) throw new InvalidOperationException("Cleanup already reached a terminal state.");
            _cleanupDone = true;
        }

        internal void MarkCleanupFailed(int generation)
        {
            RequireGeneration(generation);
            if (!_completed) throw new InvalidOperationException("Cleanup cannot fail before the native terminal event.");
            if (_cleanupDone || _cleanupFailed) throw new InvalidOperationException("Cleanup already reached a terminal state.");
            _cleanupFailed = true;
        }

        private void RequireGeneration(int generation)
        {
            if (generation != _generation)
            {
                throw new InvalidOperationException($"Stale generation {generation}; active generation is {_generation}.");
            }
        }
    }
}
