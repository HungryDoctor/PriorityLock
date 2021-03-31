using System;

namespace PriorityLock.Common.Interfaces
{
    public interface ILockManager : IDisposable
    {
        int Capacity { get; }

        ILocker Lock(int priority = 0);
    }
}
