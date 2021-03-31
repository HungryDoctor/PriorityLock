using PriorityLock.Common.Interfaces;
using PriorityLock.LockManager;
using System;
using System.Threading.Tasks;
using TestApp.Dummy;
using TestApp.Dummy.Interfaces;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            LogWriter logger = new LogWriter();

            PerformActions(logger, new LockManager(10, logger));
            logger.SaveToFile("./logFile_v1.log");

            Console.ReadKey();

            PerformActions(logger, new LockManager_V2(10, logger));
            logger.SaveToFile("./logFile_v2.log");
        }

        private static void PerformActions(ILogger logger, ILockManager lockManager)
        {
            DummyWithConcurrencyLock dummy = new DummyWithConcurrencyLock(logger, lockManager);

            Parallel.For(0, 5, (x) =>
            {
                Parallel.For(0, 7, (y) =>
                {
                    Parallel.For(0, 9, (z) =>
                    {
                        var someChoise = z % 4;
                        if (someChoise == 0)
                        {
                            OperationSet3(dummy);
                        }
                        else if (someChoise == 1)
                        {
                            OperationSet2(dummy);
                        }
                        else if (someChoise == 2)
                        {
                            OperationSet1(dummy);
                        }
                        else if (someChoise == 3)
                        {
                            OperationSet4(dummy);
                        }
                    });
                });
            });

            logger.WriteLine();
            logger.WriteLine();
            logger.WriteLine($"Left capacity (should be 10): {dummy.Capacity}");
            logger.WriteLine($"OperationsCounter: {dummy.OperationsCounter}");
        }

        private static void OperationSet1(IDummy dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation1();
            dummyWithConcurrency.DummyOperation2();
            dummyWithConcurrency.DummyOperation3();
            dummyWithConcurrency.DummyOperation4();
        }

        private static void OperationSet2(IDummy dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation1();
            dummyWithConcurrency.DummyOperation4();
            dummyWithConcurrency.DummyOperation2();
        }

        private static void OperationSet3(IDummy dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation4();
            dummyWithConcurrency.DummyOperation1();
        }

        private static void OperationSet4(IDummy dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation3();
            dummyWithConcurrency.DummyOperation2();
            dummyWithConcurrency.DummyOperation1();
        }
    }
}
