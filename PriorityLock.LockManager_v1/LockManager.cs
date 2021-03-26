using PriorityLock.Common;
using PriorityLock.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PriorityLock.LockManager_v1
{
    public class LockManager : ILockManager
    {
        private readonly ILogger _logger;
        private readonly int _maxConcurrentOperations;
        private readonly TimeSpan _tryStartOperationTimeSpan;
        private readonly ReaderWriterLockSlim _locksReaderWriterLock;
        private readonly ReaderWriterLockSlim _pendingLocksReaderWriterLock;
        private readonly SortedSet<PendingPriority> _pendingPriorities;
        private readonly int[] _threadIds;
        private readonly int[] _priorities;
        private long _capacity;

        public int Capacity => (int)_capacity;


        public LockManager(int maxConcurrentOperations, TimeSpan tryStartOperationTimeSpan, ILogger logger)
        {
            _logger = logger;
            _maxConcurrentOperations = maxConcurrentOperations;
            _capacity = _maxConcurrentOperations;
            _tryStartOperationTimeSpan = tryStartOperationTimeSpan;

            _locksReaderWriterLock = new ReaderWriterLockSlim();
            _pendingLocksReaderWriterLock = new ReaderWriterLockSlim();
            _pendingPriorities = new SortedSet<PendingPriority>();

            _threadIds = new int[_maxConcurrentOperations];
            _priorities = new int[_maxConcurrentOperations];
        }

        public LockManager(int maxConcurrentOperations, ILogger logger)
            : this(maxConcurrentOperations, Timeout.InfiniteTimeSpan, logger)
        {
        }


        public ILocker Lock(int priority = 0)
        {
            return new Locker(this, priority);
        }

        private void Release()
        {
            _logger.WriteLine($"Release Stated. ThreadId: '{Thread.CurrentThread.ManagedThreadId}'");
            _locksReaderWriterLock.EnterWriteLock();

            _logger.WriteLine($"Release. ThreadId: '{Thread.CurrentThread.ManagedThreadId}'. Current priorities state: {string.Join(", ", _priorities)}\n");

            try
            {
                var currentThreadId = Thread.CurrentThread.ManagedThreadId;
                var index = Array.FindIndex(_threadIds, x => x == currentThreadId);
                _threadIds[index] = 0;
                _priorities[index] = 0;

                _capacity++;
            }
            finally
            {
                _locksReaderWriterLock.ExitWriteLock();
            }
        }

        private void StartLock(in int priority)
        {
            IncrementPriorityWaitingCounter(priority);

            SpinWait spinWait = new SpinWait();
            Stopwatch stopWatch = Stopwatch.StartNew();

            while (true)
            {
                if (_tryStartOperationTimeSpan != Timeout.InfiniteTimeSpan && stopWatch.Elapsed > _tryStartOperationTimeSpan)
                {
                    throw new TimeoutException("Can't accquire lock");
                }

                _locksReaderWriterLock.EnterUpgradeableReadLock();
                try
                {
                    if (_capacity > 0 && IsHighestPriorityFromPending(priority))
                    {
                        _locksReaderWriterLock.EnterWriteLock();

                        _capacity--;
                        DecrementPriorityWaitingCounter(priority);
                        stopWatch.Stop();

                        _logger.WriteLine($"\nLock Accquired. ThreadId: '{Thread.CurrentThread.ManagedThreadId}'. Current priorities state: {string.Join(", ", _priorities)}\n");

                        break;
                    }
                }
                finally
                {
                    _locksReaderWriterLock.ExitUpgradeableReadLock();
                }

                bool willYield = spinWait.NextSpinWillYield;

                spinWait.SpinOnce();
                if (willYield)
                {
                    spinWait.Reset();
                }
            }

            try
            {
                var freeIndex = Array.FindIndex(_threadIds, x => x == 0);
                _threadIds[freeIndex] = Thread.CurrentThread.ManagedThreadId;
                _priorities[freeIndex] = priority;
            }
            finally
            {
                _locksReaderWriterLock.ExitWriteLock();
            }
        }

        private bool IsHighestPriorityFromPending(in int thisThreadPriority)
        {
            if (thisThreadPriority == Int32.MaxValue)
            {
                return true;
            }

            if (_pendingPriorities.Count == 0)
            {
                return true;
            }

            return _pendingPriorities.Max.Priority <= thisThreadPriority;
        }

        private void IncrementPriorityWaitingCounter(in int thisThreadPriority)
        {
            _pendingLocksReaderWriterLock.EnterWriteLock();

            try
            {
                var dummyPriority = new PendingPriority(thisThreadPriority);
                if (_pendingPriorities.TryGetValue(dummyPriority, out var pendingPriority))
                {
                    pendingPriority.PendingThreadsCounter++;
                }
                else
                {
                    dummyPriority.PendingThreadsCounter++;
                    _pendingPriorities.Add(dummyPriority);
                }
            }
            finally
            {
                _pendingLocksReaderWriterLock.ExitWriteLock();
            }
        }

        private void DecrementPriorityWaitingCounter(in int thisThreadPriority)
        {
            _pendingLocksReaderWriterLock.EnterWriteLock();

            try
            {
                var dummyPriority = new PendingPriority(thisThreadPriority);
                if (_pendingPriorities.TryGetValue(dummyPriority, out var pendingPriority))
                {
                    if (pendingPriority.PendingThreadsCounter == 1)
                    {
                        _pendingPriorities.Remove(pendingPriority);
                    }
                    else
                    {
                        pendingPriority.PendingThreadsCounter--;
                    }
                }
                else
                {
                    throw new InvalidOperationException("How did we get here");
                }
            }
            finally
            {
                _pendingLocksReaderWriterLock.ExitWriteLock();
            }
        }



        public sealed class Locker : ILocker
        {
            private readonly LockManager _lockManager;
            private bool _isDisposed;


            public Locker(LockManager lockManager, int priority = 0)
            {
                _lockManager = lockManager;
                _lockManager.StartLock(priority);
            }


            public void Dispose()
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(Locker));
                }

                _lockManager.Release();
                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
