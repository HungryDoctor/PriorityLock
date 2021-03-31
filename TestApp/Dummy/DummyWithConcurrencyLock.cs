using PriorityLock.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using TestApp.Dummy.Interfaces;

namespace TestApp.Dummy
{
    class DummyWithConcurrencyLock : IDummy
    {
        private readonly ILogger _logger;
        private readonly DummyClass _dummyClass;
        private readonly ILockManager _lockManager;

        public int Capacity => _lockManager.Capacity;
        public int OperationsCounter => _dummyClass.OperationsCounter;


        public DummyWithConcurrencyLock(ILogger logger, ILockManager lockManager)
        {
            _logger = logger;
            _dummyClass = new DummyClass();
            _lockManager = lockManager;
        }


        public void DummyOperation1()
        {
            using (new TimeLogger(nameof(DummyOperation1), DummyClass.DummyOperation1SleepMS, _logger))
            using (_lockManager.Lock())
            {
                _dummyClass.DummyOperation1();
            }
        }

        public string DummyOperation2()
        {
            string res;
            using (new TimeLogger(nameof(DummyOperation2), DummyClass.DummyOperation2SleepMS, _logger, 1))
            using (_lockManager.Lock(1))
            {
                res = _dummyClass.DummyOperation2();
            }

            return res;
        }

        public void DummyOperation3()
        {
            using (new TimeLogger(nameof(DummyOperation3), DummyClass.DummyOperation3SleepMS, _logger, 3))
            using (_lockManager.Lock(3))
            {
                _dummyClass.DummyOperation3();
            }
        }

        public void DummyOperation4()
        {
            using (new TimeLogger(nameof(DummyOperation4), DummyClass.DummyOperation4SleepMS, _logger, Int32.MaxValue))
            using (_lockManager.Lock(Int32.MaxValue))
            {
                _dummyClass.DummyOperation4();
            }
        }


        private class TimeLogger : IDisposable
        {
            private readonly ILogger _logger;
            private readonly Stopwatch _stopwatch;
            private readonly string _operationName;
            private readonly int _priority;
            private readonly int _operationTimeMs;

            public TimeLogger(string operationName, int operationTimeMs, ILogger logger, int priority = 0)
            {
                _logger = logger;
                _stopwatch = Stopwatch.StartNew();
                _operationName = operationName;
                _priority = priority;
                _operationTimeMs = operationTimeMs;

                _logger.WriteLine($"Lock started for '{_operationName}'; ThreadId: '{Thread.CurrentThread.ManagedThreadId}'; Priority: '{priority}'");
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _logger.WriteLine($"Lock finished for '{_operationName}'; ThreadId: '{Thread.CurrentThread.ManagedThreadId}'; Priority: '{_priority}'; Elapsed seconds: '{(_stopwatch.ElapsedMilliseconds - _operationTimeMs) / 1000.0}'\n");
            }
        }
    }
}
