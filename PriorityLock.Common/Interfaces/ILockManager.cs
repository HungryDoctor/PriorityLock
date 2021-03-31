namespace PriorityLock.Common.Interfaces
{
    public interface ILockManager
    {
        int Capacity { get; }

        ILocker Lock(int priority = 0);
    }
}
