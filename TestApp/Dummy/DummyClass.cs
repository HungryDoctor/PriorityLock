using System.Threading;
using TestApp.Dummy.Interfaces;

namespace TestApp
{
    class DummyClass : IDummy
    {
        private int operationsCounter;
        public int OperationsCounter => operationsCounter;

        public const int DummyOperation1SleepMS = 2000;
        public void DummyOperation1()
        {
            Interlocked.Increment(ref operationsCounter);

            Thread.Sleep(DummyOperation1SleepMS);
        }

        public const int DummyOperation2SleepMS = 500;
        public string DummyOperation2()
        {
            Interlocked.Increment(ref operationsCounter);

            Thread.Sleep(1000);

            return "";
        }

        public const int DummyOperation3SleepMS = 800;
        public void DummyOperation3()
        {
            Interlocked.Increment(ref operationsCounter);

            Thread.Sleep(DummyOperation3SleepMS);
        }

        public const int DummyOperation4SleepMS = 2000;
        public void DummyOperation4()
        {
            Interlocked.Increment(ref operationsCounter);

            Thread.Sleep(DummyOperation4SleepMS);
        }
    }
}
