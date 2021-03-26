using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Dummy;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            LogWriter logger = new LogWriter();
            DummyWithConcurrencyLock dummyWithLock = new DummyWithConcurrencyLock(logger);

            Parallel.For(0, 5, (x) =>
            {
                Parallel.For(0, 7, (y) =>
                {
                    Parallel.For(0, 9, (z) =>
                    {
                        var someChoise = z % 4;
                        if (someChoise == 0)
                        {
                            OperationSet3(dummyWithLock);
                        }
                        else if (someChoise == 1)
                        {
                            OperationSet2(dummyWithLock);
                        }
                        else if (someChoise == 2)
                        {
                            OperationSet1(dummyWithLock);
                        }
                        else if (someChoise == 3)
                        {
                            OperationSet4(dummyWithLock);
                        }
                    });
                });
            });

            logger.WriteLine();
            logger.WriteLine();
            logger.WriteLine($"Left capacity (should be 10): {dummyWithLock.Capacity}");
            logger.WriteLine($"OperationsCounter: {dummyWithLock.OperationsCounter}");
            logger.SaveToFile();
        }

        private static void OperationSet1(DummyWithConcurrencyLock dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation1();
            dummyWithConcurrency.DummyOperation2();
            dummyWithConcurrency.DummyOperation3();
            dummyWithConcurrency.DummyOperation4();
        }

        private static void OperationSet2(DummyWithConcurrencyLock dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation1();
            dummyWithConcurrency.DummyOperation4();
            dummyWithConcurrency.DummyOperation2();
        }

        private static void OperationSet3(DummyWithConcurrencyLock dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation4();
            dummyWithConcurrency.DummyOperation1();
        }

        private static void OperationSet4(DummyWithConcurrencyLock dummyWithConcurrency)
        {
            dummyWithConcurrency.DummyOperation3();
            dummyWithConcurrency.DummyOperation2();
            dummyWithConcurrency.DummyOperation1();
        }
    }
}
