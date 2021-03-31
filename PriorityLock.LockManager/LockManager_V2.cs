using PriorityLock.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PriorityLock.LockManager
{
    public class LockManager_V2 : ILockManager, IDisposable
    {
        private static readonly TimeSpan resetEventWaitTimeSpan = TimeSpan.FromSeconds(10);
        private readonly ILogger _logger;
        private readonly int _maxConcurrentOperations;
        private readonly TimeSpan _tryStartOperationTimeSpan;
        private readonly object _locker;
        private readonly ManualResetEvent _resetEvent;
        private readonly ReaderWriterLockSlim _pendingLocksReaderWriterLock;
        private readonly SortedSet<PendingPriority> _pendingPriorities;
        private readonly int[] _operationsTokens;
        private readonly int[] _priorities;
        private long _capacity;
        private bool _disposed;

        public int Capacity => (int)Interlocked.Read(ref _capacity);


        public LockManager_V2(int maxConcurrentOperations, TimeSpan tryStartOperationTimeSpan, ILogger logger)
        {
            _logger = logger;
            _maxConcurrentOperations = maxConcurrentOperations;
            _capacity = _maxConcurrentOperations;
            _tryStartOperationTimeSpan = tryStartOperationTimeSpan;

            _locker = new object();
            _resetEvent = new ManualResetEvent(false);
            _pendingLocksReaderWriterLock = new ReaderWriterLockSlim();
            _pendingPriorities = new SortedSet<PendingPriority>();

            _operationsTokens = new int[_maxConcurrentOperations];
            _priorities = new int[_maxConcurrentOperations];
            _disposed = false;

            ClearTokens();
        }

        public LockManager_V2(int maxConcurrentOperations, ILogger logger)
            : this(maxConcurrentOperations, Timeout.InfiniteTimeSpan, logger)
        {
        }


        ~LockManager_V2()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _resetEvent.Dispose();
                _pendingLocksReaderWriterLock.Dispose();

                _disposed = true;
            }
        }


        public ILocker Lock(int priority = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LockManager_V2));
            }

            return new Locker(this, in priority);
        }

        private void Release(in int operationIndex)
        {
            _logger.WriteLine("Release. " + GetCurrentState(in operationIndex));

            Interlocked.Exchange(ref _operationsTokens[operationIndex], -1);
            Interlocked.Increment(ref _capacity);

            _resetEvent.Set();
        }

        private int StartLock(in int priority)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LockManager_V2));
            }

            IncrementPriorityWaitingCounter(priority);

            SpinWait spinWait = new SpinWait();
            Stopwatch stopWatch = Stopwatch.StartNew();

            while (true)
            {
                if (_tryStartOperationTimeSpan != Timeout.InfiniteTimeSpan && stopWatch.Elapsed > _tryStartOperationTimeSpan)
                {
                    throw new TimeoutException("Can't accquire lock");
                }

                long currentCapacity = Interlocked.Read(ref _capacity);
                if (currentCapacity > 0 &&
                    IsHighestPriorityFromPending(in priority) &&
                    Interlocked.CompareExchange(ref _capacity, currentCapacity - 1, currentCapacity) == currentCapacity)
                {
                    Monitor.Enter(_locker);
                    stopWatch.Stop();

                    break;
                }

                if (spinWait.NextSpinWillYield)
                {
                    _resetEvent.Reset();
                    if(_resetEvent.WaitOne(resetEventWaitTimeSpan))
                    {
                        spinWait.Reset();
                    }
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }

            DecrementPriorityWaitingCounter(priority);

            try
            {
                var freeIndex = Array.IndexOf(_operationsTokens, -1);
                if (freeIndex == -1)
                {
                    throw new InvalidOperationException("No free index for thread");
                }

                _logger.WriteLine("Lock Accquired. " + GetCurrentState(in freeIndex));

                _operationsTokens[freeIndex] = freeIndex;
                _priorities[freeIndex] = priority;

                return freeIndex;
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
            var dummyPriority = new PendingPriority(in thisThreadPriority);

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
            var dummyPriority = new PendingPriority(in thisThreadPriority);

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

        private string GetCurrentState(in int operationToken)
        {
            return $"OpeartionToken: '{operationToken}'. ThreadId: '{Thread.CurrentThread.ManagedThreadId}'. Current threads state: {string.Join(", ", _operationsTokens)}. Current priorities state: {string.Join(", ", _priorities)}\n";
        }

        private void ClearTokens()
        {
            for (int x = 0; x < _operationsTokens.Length; x++)
            {
                _operationsTokens[x] = -1;
            }
        }


        public sealed class Locker : ILocker
        {
            private readonly LockManager_V2 _lockManager;
            private bool _isDisposed;
            private readonly int _operationIndex;


            public Locker(LockManager_V2 lockManager, in int priority = 0)
            {
                _lockManager = lockManager;
                _operationIndex = _lockManager.StartLock(priority);
            }


            public void Dispose()
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(Locker));
                }

                _lockManager.Release(in _operationIndex);
                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        private sealed class PendingPriority : IEquatable<PendingPriority>, IComparable<PendingPriority>
        {
            public readonly int Priority;
            public int PendingThreadsCounter;


            public PendingPriority(in int priority)
            {
                Priority = priority;
            }


            public int CompareTo(PendingPriority other)
            {
                if (other == null)
                {
                    return -1;
                }

                return Priority.CompareTo(other.Priority);
            }

            public override bool Equals(object obj)
            {
                if (obj is PendingPriority pendingPriority)
                {
                    return Equals(pendingPriority);
                }

                return base.Equals(obj);
            }

            public bool Equals(PendingPriority other)
            {
                return other != null && Priority == other.Priority;
            }

            public override int GetHashCode()
            {
                return Priority.GetHashCode();
            }

            public override string ToString()
            {
                return $"Priority: '{Priority}', PendingThreadsCounter: '{PendingThreadsCounter}'";
            }
        }
    }
}
