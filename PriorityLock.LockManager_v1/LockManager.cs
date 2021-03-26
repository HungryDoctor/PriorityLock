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
        private readonly object _locker;
        private readonly ReaderWriterLockSlim _pendingLocksReaderWriterLock;
        private readonly SortedSet<PendingPriority> _pendingPriorities;
        private readonly int[] _threadIds;
        private readonly int[] _priorities;
        private long _capacity;

        public int Capacity => (int)Interlocked.Read(ref _capacity);


        public LockManager(int maxConcurrentOperations, TimeSpan tryStartOperationTimeSpan, ILogger logger)
        {
            _logger = logger;
            _maxConcurrentOperations = maxConcurrentOperations;
            _capacity = _maxConcurrentOperations;
            _tryStartOperationTimeSpan = tryStartOperationTimeSpan;

            _locker = new object();
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
            _logger.WriteLine("Release. " + GetCurrentState());

            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
            var index = Array.FindIndex(_threadIds, x => x == currentThreadId);
            if (index == -1)
            {
                throw new InvalidOperationException("Currrent therad is not locked");
            }

            Interlocked.Exchange(ref _threadIds[index], 0);
            Interlocked.Increment(ref _capacity);
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

                long currentCapacity = _capacity;
                if (currentCapacity > 0 && IsHighestPriorityFromPending(priority))
                {
                    long lowerCapacity = currentCapacity - 1;
                    if (Interlocked.CompareExchange(ref _capacity, currentCapacity - 1, currentCapacity) == currentCapacity)
                    {
                        Monitor.Enter(_locker);
                        stopWatch.Stop();

                        _logger.WriteLine("Lock Accquired. " + GetCurrentState());

                        break;
                    }
                }


                bool willYield = spinWait.NextSpinWillYield;

                spinWait.SpinOnce();
                if (willYield)
                {
                    spinWait.Reset();
                }
            }

            DecrementPriorityWaitingCounter(priority);

            try
            {
                var freeIndex = Array.FindIndex(_threadIds, x => x == 0);
                if (freeIndex == -1)
                {
                    throw new InvalidOperationException("No free index for thread");
                }

                _threadIds[freeIndex] = Thread.CurrentThread.ManagedThreadId;
                _priorities[freeIndex] = priority;
            }
            finally
            {
                Monitor.Exit(_locker);
            }
        }

        private bool IsHighestPriorityFromPending(in int thisThreadPriority)
        {
            if (thisThreadPriority == Int32.MaxValue)
            {
                return true;
            }

            _pendingLocksReaderWriterLock.EnterReadLock();
            try
            {
                if (_pendingPriorities.Count == 0)
                {
                    return true;
                }

                return _pendingPriorities.Max.Priority <= thisThreadPriority;
            }
            finally
            {
                _pendingLocksReaderWriterLock.ExitReadLock();
            }
        }

        private void IncrementPriorityWaitingCounter(in int thisThreadPriority)
        {
            var dummyPriority = new PendingPriority(thisThreadPriority);

            _pendingLocksReaderWriterLock.EnterWriteLock();
            try
            {
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
            var dummyPriority = new PendingPriority(thisThreadPriority);

            _pendingLocksReaderWriterLock.EnterWriteLock();
            try
            {
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
                    throw new InvalidOperationException("No such item in pending priority.");
                }
            }
            finally
            {
                _pendingLocksReaderWriterLock.ExitWriteLock();
            }
        }

        private string GetCurrentState()
        {
            return $"ThreadId: '{Thread.CurrentThread.ManagedThreadId}'. Current threads state: {string.Join(", ", _threadIds)}. Current priorities state: {string.Join(", ", _priorities)}\n";
        }



        public sealed class Locker : ILocker
        {
            private readonly LockManager _lockManager;
            private bool _isDisposed;


            public Locker(LockManager lockManager, in int priority = 0)
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
